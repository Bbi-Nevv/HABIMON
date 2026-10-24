using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class Login : MonoBehaviour
{
    [Header("UI Controls")]
    [SerializeField] private TMP_InputField inputEmail;
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private Button btnLogin;
    [SerializeField] private TMP_Text txtStatus;

    [Header("GameObject")]
    [SerializeField] private GameObject mode;
    [SerializeField] private GameObject login;
    private bool isLoggingIn;

    private void Start()
    {
        if (mode != null)
            mode.SetActive(false);
        if (txtStatus != null)
            txtStatus.text = string.Empty;
        if (btnLogin != null)
            btnLogin.onClick.AddListener(OnClickLogin);
    }

    private void OnDestroy()
    {
        if (btnLogin != null)
            btnLogin.onClick.RemoveListener(OnClickLogin);
    }

    private async void OnClickLogin()
    {
        if (isLoggingIn)
            return;

        string email = inputEmail != null ? inputEmail.text.Trim() : string.Empty;
        string password = inputPassword != null ? inputPassword.text : string.Empty;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.Log("Tài khoản chưa tồn tại");
            if (txtStatus != null) txtStatus.text = "Vui lòng nhập Email và Mật khẩu!";
            return;
        }

        isLoggingIn = true;
        if (btnLogin != null)
            btnLogin.interactable = false;
        if (txtStatus != null) txtStatus.text = "Đang kiểm tra...";

        try
        {
            FirebaseManager manager = FirebaseManager.Instance;
            if (manager == null)
                throw new InvalidOperationException("Không tìm thấy FirebaseManager trong scene.");

            var (success, message) = await manager.LoginParentAsync(email, password);
            if (this == null)
                return;

            if (success)
            {
                if (txtStatus != null)
                {
                    txtStatus.text = "Đăng nhập thành công!";
                    txtStatus.color = Color.green;
                }
                if (mode != null)
                    mode.SetActive(true);
                if (login != null)
                    login.SetActive(false);
            }
            else
            {
                if (txtStatus != null)
                {
                    txtStatus.text = message;
                    txtStatus.color = Color.red;
                }
            }
        }
        catch (Exception ex)
        {
            if (this != null)
            {
                Debug.LogWarning($"[Login] {ex.Message}");
                if (txtStatus != null)
                {
                    txtStatus.text = "Không thể đăng nhập. Kiểm tra kết nối rồi thử lại.";
                    txtStatus.color = Color.red;
                }
            }
        }
        finally
        {
            if (this != null)
            {
                isLoggingIn = false;
                if (btnLogin != null)
                    btnLogin.interactable = true;
            }
        }
    }
}
