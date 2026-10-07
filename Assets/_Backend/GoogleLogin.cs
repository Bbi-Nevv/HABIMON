using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoogleLogin : MonoBehaviour
{
    [SerializeField] private Button googleButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject mode;
    [SerializeField] private GameObject login;

    private bool isSigningIn;

    private void Start()
    {
        if (googleButton != null)
            googleButton.onClick.AddListener(OnClickGoogleLogin);
    }

    private void OnDestroy()
    {
        if (googleButton != null)
            googleButton.onClick.RemoveListener(OnClickGoogleLogin);
    }

    public async void OnClickGoogleLogin()
    {
        if (isSigningIn)
            return;

        isSigningIn = true;
        if (googleButton != null)
            googleButton.interactable = false;

        SetStatus("Đang mở đăng nhập Google...");
        try
        {
            if (FirebaseManager.Instance == null)
            {
                SetStatus("Không tìm thấy FirebaseManager trong scene.");
                return;
            }

            var (success, message) = await FirebaseManager.Instance.LoginWithGoogleAsync();

            SetStatus(message);
            if (success)
            {
                if (mode != null) mode.SetActive(true);
                if (login != null) login.SetActive(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Google Sign-In] {ex.Message}");
            SetStatus("Đăng nhập Google thất bại. Vui lòng thử lại.");
        }
        finally
        {
            isSigningIn = false;
            if (googleButton != null)
                googleButton.interactable = true;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
