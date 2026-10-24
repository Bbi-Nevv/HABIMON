using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SignUp : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private Button signUpButton;

    public TextMeshProUGUI notifi;

    private const int MinPasswordLength = 6;
    private bool isSubmitting;

    private void Start()
    {
        if (notifi != null)
            notifi.text = string.Empty;

        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);
    }

    private void OnDestroy()
    {
        if (signUpButton != null)
            signUpButton.onClick.RemoveListener(OnSignUpClicked);
    }

    private async void OnSignUpClicked()
    {
        if (isSubmitting)
            return;

        isSubmitting = true;
        if (signUpButton != null)
            signUpButton.interactable = false;

        try
        {
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        string confirmPassword = confirmPasswordInput != null ? confirmPasswordInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Email hoặc mật khẩu không được để trống.", Color.red);
            Debug.LogWarning("[SignUp] Email hoặc mật khẩu trống.");
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowMessage("Email không hợp lệ.", Color.red);
            Debug.LogWarning("[SignUp] Email không hợp lệ.");
            return;
        }

        if (password.Length < MinPasswordLength)
        {
            ShowMessage($"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.", Color.red);
            Debug.LogWarning("[SignUp] Mật khẩu quá ngắn.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowMessage("Mật khẩu xác nhận không khớp.", Color.red);
            Debug.LogWarning("[SignUp] Mật khẩu xác nhận không khớp.");
            return;
        }

        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            ShowMessage("Không tìm thấy FirebaseManager.", Color.red);
            return;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            ShowMessage($"Firebase chưa sẵn sàng: {manager.InitializationError}", Color.red);
            return;
        }

        Debug.Log("[SignUp] Đang tạo tài khoản...");

        var (success, message) = await manager.RegisterParentAsync(email, password);
        if (this == null)
            return;

        if (success)
        {
            Debug.Log("[SignUp] Tạo Parent thành công.");
            ShowMessage("Tạo tài khoản thành công.", Color.green);

            if (passwordInput != null)
                passwordInput.text = string.Empty;

            if (confirmPasswordInput != null)
                confirmPasswordInput.text = string.Empty;

            if (emailInput != null)
                emailInput.text = string.Empty;
        }
        else
        {
            ShowMessage(message, Color.red);
            Debug.LogWarning($"[SignUp] {message}");
        }
        }
        catch (Exception ex)
        {
            if (this != null)
            {
                Debug.LogError($"[SignUp] Đăng ký thất bại: {ex.Message}");
                ShowMessage("Không thể hoàn tất đăng ký. Vui lòng thử lại.", Color.red);
            }
        }
        finally
        {
            if (this != null)
            {
                isSubmitting = false;
                if (signUpButton != null)
                    signUpButton.interactable = true;
            }
        }
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void ShowMessage(string message, Color color)
    {
        if (notifi == null)
            return;

        notifi.text = message;
        notifi.color = color;
    }
}
