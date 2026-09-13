using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pin : MonoBehaviour
{
	private const string ParentSceneName = "Parent";
	private const int MaxAttempts = 3;

	[Header("Change PIN Popup")]
	[SerializeField] private TMP_InputField currentPinInput;
	[SerializeField] private TMP_InputField newPinInput;
	[SerializeField] private TMP_InputField confirmPinInput;
	[SerializeField] private Button changePinButton;
	[SerializeField] private TMP_Text changeStatusText;

	[Header("Verify PIN Popup")]
	[SerializeField] private TMP_InputField verifyPinInput;
	[SerializeField] private Button verifyPinButton;
	[SerializeField] private GameObject verifyPopup;
	[SerializeField] private TMP_Text verifyStatusText;

	private int failedAttempts;

	private void Awake()
	{
		if (changePinButton != null)
			changePinButton.onClick.AddListener(OnChangePinClicked);

		if (verifyPinButton != null)
			verifyPinButton.onClick.AddListener(OnVerifyPinClicked);
	}

	private void OnDestroy()
	{
		if (changePinButton != null)
			changePinButton.onClick.RemoveListener(OnChangePinClicked);

		if (verifyPinButton != null)
			verifyPinButton.onClick.RemoveListener(OnVerifyPinClicked);
	}

	public void OpenVerifyPopup()
	{
		failedAttempts = 0;

		if (verifyPinButton != null)
			verifyPinButton.interactable = true;

		if (verifyPinInput != null)
			verifyPinInput.text = string.Empty;

		SetStatus(verifyStatusText, string.Empty, Color.white);

		if (verifyPopup != null)
			verifyPopup.SetActive(true);
	}

	public void CloseVerifyPopup()
	{
		if (verifyPopup != null)
			verifyPopup.SetActive(false);
	}

	private async void OnVerifyPinClicked()
	{
		if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
		{
			SetStatus(verifyStatusText, "Firebase chưa sẵn sàng.", Color.red);
			return;
		}

		string pin = verifyPinInput != null ? verifyPinInput.text.Trim() : string.Empty;

		if (!IsValidPin(pin))
		{
			SetStatus(verifyStatusText, "PIN phải gồm đúng 4 chữ số.", Color.red);
			return;
		}

		bool verified = await FirebaseManager.Instance.VerifyParentPinAsync(pin);
		if (verified)
		{
			SceneManager.LoadScene(ParentSceneName);
			return;
		}

		failedAttempts++;
		if (failedAttempts >= MaxAttempts)
		{
			SetStatus(verifyStatusText, "Bạn đã nhập sai PIN 3 lần.", Color.red);
			if (verifyPinButton != null)
				verifyPinButton.interactable = false;
			return;
		}

		SetStatus(verifyStatusText, $"PIN không đúng. Còn {MaxAttempts - failedAttempts} lần thử.", Color.red);
	}

	private async void OnChangePinClicked()
	{
		if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
		{
			SetStatus(changeStatusText, "Firebase chưa sẵn sàng.", Color.red);
			return;
		}

		string currentPin = currentPinInput != null ? currentPinInput.text.Trim() : string.Empty;
		string newPin = newPinInput != null ? newPinInput.text.Trim() : string.Empty;
		string confirmPin = confirmPinInput != null ? confirmPinInput.text.Trim() : string.Empty;

		if (!IsValidPin(currentPin) || !IsValidPin(newPin) || !IsValidPin(confirmPin))
		{
			SetStatus(changeStatusText, "Mỗi mã PIN phải gồm đúng 4 chữ số.", Color.red);
			return;
		}

		if (newPin != confirmPin)
		{
			SetStatus(changeStatusText, "Mã PIN mới xác nhận không khớp.", Color.red);
			return;
		}

		if (changePinButton != null)
			changePinButton.interactable = false;

		bool changed = await FirebaseManager.Instance.ChangeParentPinAsync(currentPin, newPin);

		if (changed)
		{
			SetStatus(changeStatusText, "Đổi mã PIN thành công.", Color.green);
			ClearChangeInputs();
		}
		else
		{
			SetStatus(changeStatusText, "Mã PIN hiện tại không đúng hoặc hệ thống chưa sẵn sàng.", Color.red);
		}

		if (changePinButton != null)
			changePinButton.interactable = true;
	}

	private static bool IsValidPin(string pin)
	{
		return Regex.IsMatch(pin ?? string.Empty, "^[0-9]{4}$");
	}

	private static void SetStatus(TMP_Text statusText, string message, Color color)
	{
		if (statusText == null)
			return;

		statusText.text = message;
		statusText.color = color;
	}

	private void ClearChangeInputs()
	{
		if (currentPinInput != null) currentPinInput.text = string.Empty;
		if (newPinInput != null) newPinInput.text = string.Empty;
		if (confirmPinInput != null) confirmPinInput.text = string.Empty;
	}
}
