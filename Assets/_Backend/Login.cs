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

    private void Start()
    {
        mode.SetActive(false);
        txtStatus.text = string.Empty;
        if (btnLogin != null)
        {
            btnLogin.onClick.AddListener(OnClickLogin);
        }
    }

    private async void OnClickLogin()
    {
        string email = inputEmail.text.Trim();
        string password = inputPassword.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.Log("Tài khoản chưa tồn tại");
            if (txtStatus != null) txtStatus.text = "Vui lòng nhập Email và Mật khẩu!";
            return;
        }

        btnLogin.interactable = false;
        if (txtStatus != null) txtStatus.text = "Đang kiểm tra...";

        var (success, message) = await FirebaseManager.Instance.LoginParentAsync(email, password);

        if (success)
        {
            string childId = await FirebaseManager.Instance.ResolveCurrentChildIdAsync();
            Debug.Log($"<color=green>[Login] Đăng nhập thành công! Child đang dùng: {childId}</color>");
            txtStatus.text = "Đăng nhập thành công!";
            txtStatus.color = Color.green;

            // SceneManager.LoadScene("Parent");
            mode.SetActive(true);
            login.SetActive(false);

        }
        else
        {
            Debug.Log("Tài khoản chưa tồn tại");
            if (txtStatus != null) txtStatus.text = "Tài khoản chưa tồn tại";
            txtStatus.color = Color.red;
            btnLogin.interactable = true;
        }
    }
}