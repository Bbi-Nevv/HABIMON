using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForgetPassword : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private TMP_Text statusText;
    private bool isSending;

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
        if (isSending)
            return;

        isSending = true;
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            ShowStatus("Vui lòng nhập email hợp lệ.", Color.red);
            isSending = false;
            return;
        }

        if (sendButton != null)
            sendButton.interactable = false;

        ShowStatus("Đang gửi email đặt lại mật khẩu...", Color.gray);
        try
        {
            FirebaseManager manager = FirebaseManager.Instance;
            if (manager == null || !await manager.EnsureInitializedAsync())
            {
                ShowStatus("Firebase chưa sẵn sàng. Vui lòng thử lại.", Color.red);
                return;
            }

            var (success, message) = await manager.SendPasswordResetEmailAsync(email);
            if (this == null)
                return;

            if (success)
            {
                ShowStatus(message, Color.green);
                if (emailInput != null)
                    emailInput.text = string.Empty;
            }
            else
            {
                ShowStatus(message, Color.red);
            }
        }
        catch (System.Exception ex)
        {
            if (this != null)
            {
                Debug.LogWarning($"[ForgetPassword] {ex.Message}");
                ShowStatus("Không thể gửi email. Vui lòng kiểm tra kết nối rồi thử lại.", Color.red);
            }
        }
        finally
        {
            if (this != null)
            {
                isSending = false;
                if (sendButton != null)
                    sendButton.interactable = true;
            }
        }
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
