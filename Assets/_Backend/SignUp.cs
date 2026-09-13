using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;
using Firebase.Firestore;

public class SignUp : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private Button signUpButton;

    public TextMeshProUGUI notifi;
    private void Start()
    {
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
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("[SignUp] Email hoặc mật khẩu trống.");
            return;
        }

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[SignUp] Firebase chưa sẵn sàng.");
            return;
        }

        if (passwordInput.text != confirmPasswordInput.text)
        {
            
            Debug.LogWarning("[SignUp] Mật khẩu xác nhận không khớp.");
            notifi.text = "Mật khẩu xác nhận không khớp.";
            notifi.color = Color.red;
            return;
        }

        Debug.Log("[SignUp] Đang tạo tài khoản...");

        ParentModel parent = new ParentModel
        {
            email = email,
            password = password
        };

        var (success, message) = await FirebaseManager.Instance.RegisterParentAsync(parent);

        if (success)
        {
            Debug.Log("[SignUp] Tạo Parent thành công.");
            notifi.text = "Tạo tài khoản thành công.";
            notifi.color = Color.green;
            // Optionally clear inputs or proceed to next scene
        }
        else
        {
            Debug.LogWarning($"[SignUp] {message}");
        }
    }
}