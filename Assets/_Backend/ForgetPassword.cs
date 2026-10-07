using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForgetPassword : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        if (statusText != null)
            statusText.text = string.Empty;

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnSendClicked);
    }

    private async void OnSendClicked()
    {
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            ShowStatus("Vui lòng nhập email hợp lệ.", Color.red);
            return;
        }

        if (sendButton != null)
            sendButton.interactable = false;

        ShowStatus("Đang gửi email đặt lại mật khẩu...", Color.gray);

        var (success, message) = await FirebaseManager.Instance.SendPasswordResetEmailAsync(email);

        if (success)
        {
            ShowStatus("Email đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra hộp thư.", Color.green);
            if (emailInput != null)
                emailInput.text = string.Empty;
        }
        else
        {
            ShowStatus(message, Color.red);
        }

        if (sendButton != null)
            sendButton.interactable = true;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = color;
    }
}
