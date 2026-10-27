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
	[SerializeField] private GameObject changePinPopup;
	[SerializeField] private Button changePinPopupBackButton;

	[Header("Verify PIN Popup")]
	[SerializeField] private TMP_InputField verifyPinInput;
	[SerializeField] private Button verifyPinButton;
	[SerializeField] private GameObject verifyPopup;
	[SerializeField] private TMP_Text verifyStatusText;

	private int failedAttempts;
	private bool isVerifying;
	private bool isChanging;
	private bool isCheckingPinState;
	private bool pinStateCheckFailed;
	private bool initialPinSetupRequired;
	private string verifyButtonDefaultLabel;
	private string changePinButtonDefaultLabel;

	private void Awake()
	{
		if (changePinButton != null)
		{
			TMP_Text label = changePinButton.GetComponentInChildren<TMP_Text>(true);
			changePinButtonDefaultLabel = label != null ? label.text : string.Empty;
			changePinButton.onClick.AddListener(OnChangePinClicked);
		}

		if (verifyPinButton != null)
		{
			TMP_Text label = verifyPinButton.GetComponentInChildren<TMP_Text>(true);
			verifyButtonDefaultLabel = label != null ? label.text : string.Empty;
			verifyPinButton.onClick.AddListener(OnVerifyPinClicked);
		}
	}

	private void OnDestroy()
	{
		if (changePinButton != null)
			changePinButton.onClick.RemoveListener(OnChangePinClicked);

		if (verifyPinButton != null)
			verifyPinButton.onClick.RemoveListener(OnVerifyPinClicked);
	}

	private async void Start()
	{
		if (currentPinInput == null || changePinPopup == null)
			return;
		await CheckParentPinStateAsync();
	}

	public void OpenVerifyPopup()
	{
		if (verifyPinInput != null)
			verifyPinInput.gameObject.SetActive(true);
		SetVerifyButtonLabel(verifyButtonDefaultLabel);

		if (verifyPinButton != null)
			verifyPinButton.interactable = !isVerifying && failedAttempts < MaxAttempts;

		if (verifyPinInput != null)
			verifyPinInput.text = string.Empty;

		bool locked = failedAttempts >= MaxAttempts;
		SetStatus(verifyStatusText, locked
			? "Bạn đã nhập sai PIN 3 lần. Hãy thử lại sau khi mở lại ứng dụng."
			: string.Empty, locked ? Color.red : Color.white);

		if (verifyPopup != null)
		{
			verifyPopup.SetActive(true);
			verifyPopup.transform.SetAsLastSibling();
		}
	}

	public void CloseVerifyPopup()
	{
		if (verifyPopup != null)
			verifyPopup.SetActive(false);
	}

	private async void OnVerifyPinClicked()
	{
		if (isVerifying || failedAttempts >= MaxAttempts)
			return;

		FirebaseManager manager = FirebaseManager.Instance;
		if (manager == null)
		{
			SetStatus(verifyStatusText, "Không tìm thấy FirebaseManager.", Color.red);
			return;
		}

		string pin = verifyPinInput != null ? verifyPinInput.text.Trim() : string.Empty;

		isVerifying = true;
		if (verifyPinButton != null)
			verifyPinButton.interactable = false;
		try
		{
			if (!await manager.EnsureInitializedAsync())
			{
				SetStatus(verifyStatusText, "Firebase chưa sẵn sàng.", Color.red);
				return;
			}

			ParentPinState pinState = await manager.GetParentPinStateAsync();
			if (pinState == ParentPinState.NotAuthenticated)
			{
				SetStatus(verifyStatusText, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", Color.red);
				return;
			}
			if (pinState == ParentPinState.FirebaseUnavailable || pinState == ParentPinState.ParentProfileMissing)
			{
				SetStatus(verifyStatusText, "Không thể đọc hồ sơ phụ huynh. Hãy kiểm tra kết nối rồi thử lại.", Color.red);
				return;
			}

			if (pinState == ParentPinState.NotConfigured)
			{
				SceneManager.LoadScene(ParentSceneName);
				return;
			}

			if (verifyPinInput != null)
				verifyPinInput.gameObject.SetActive(true);
			SetVerifyButtonLabel(verifyButtonDefaultLabel);
			if (!IsValidPin(pin))
			{
				SetStatus(verifyStatusText, "PIN phải gồm đúng 4 chữ số.", Color.red);
				return;
			}

			bool verified = await manager.VerifyParentPinAsync(pin);
			if (verified)
			{
				SceneManager.LoadScene(ParentSceneName);
				return;
			}

			failedAttempts++;
			if (failedAttempts >= MaxAttempts)
			{
				SetStatus(verifyStatusText, "Bạn đã nhập sai PIN 3 lần.", Color.red);
				return;
			}

			SetStatus(verifyStatusText, $"PIN không đúng. Còn {MaxAttempts - failedAttempts} lần thử.", Color.red);
		}
		catch (System.Exception ex)
		{
			Debug.LogError($"[Pin] Không thể kiểm tra mã phụ huynh: {ex.Message}");
			SetStatus(verifyStatusText, "Không thể kiểm tra mã. Vui lòng thử lại.", Color.red);
		}
		finally
		{
			if (this != null)
			{
				isVerifying = false;
				if (verifyPinButton != null && failedAttempts < MaxAttempts)
					verifyPinButton.interactable = true;
			}
		}
	}

	private async void OnChangePinClicked()
	{
		if (isChanging || isCheckingPinState)
			return;
		if (pinStateCheckFailed)
		{
			await CheckParentPinStateAsync();
			return;
		}

		FirebaseManager manager = FirebaseManager.Instance;
		if (manager == null)
		{
			SetStatus(changeStatusText, "Không tìm thấy FirebaseManager.", Color.red);
			return;
		}

		string currentPin = currentPinInput != null ? currentPinInput.text.Trim() : string.Empty;
		string newPin = newPinInput != null ? newPinInput.text.Trim() : string.Empty;
		string confirmPin = confirmPinInput != null ? confirmPinInput.text.Trim() : string.Empty;

		if (!IsValidPin(newPin) || !IsValidPin(confirmPin))
		{
			SetStatus(changeStatusText, "Mã PIN mới phải gồm đúng 4 chữ số.", Color.red);
			return;
		}

		if (newPin != confirmPin)
		{
			SetStatus(changeStatusText, "Mã PIN mới xác nhận không khớp.", Color.red);
			return;
		}

		isChanging = true;
		if (changePinButton != null)
			changePinButton.interactable = false;
		try
		{
			if (!await manager.EnsureInitializedAsync())
			{
				SetStatus(changeStatusText, "Firebase chưa sẵn sàng.", Color.red);
				return;
			}

			ParentPinState pinState = await manager.GetParentPinStateAsync();
			if (pinState == ParentPinState.NotAuthenticated ||
				pinState == ParentPinState.FirebaseUnavailable ||
				pinState == ParentPinState.ParentProfileMissing)
			{
				SetStatus(changeStatusText, "Không thể xác minh hồ sơ phụ huynh. Hãy đăng nhập lại hoặc kiểm tra kết nối.", Color.red);
				return;
			}
			if (pinState == ParentPinState.NotConfigured && !initialPinSetupRequired)
			{
				OpenInitialPinSetupGate();
				return;
			}

			bool hasPin = pinState == ParentPinState.Configured;
			if ((hasPin && !IsValidPin(currentPin)) || (!hasPin && !string.IsNullOrEmpty(currentPin)))
			{
				SetStatus(changeStatusText, hasPin
					? "Mã PIN hiện tại phải gồm đúng 4 chữ số."
					: "Chưa có mã hiện tại. Để trống ô này để tạo mã phụ huynh.", Color.red);
				return;
			}

			bool changed = await manager.ChangeParentPinAsync(currentPin, newPin);
			if (changed)
			{
				ClearChangeInputs();
				if (initialPinSetupRequired)
				{
					initialPinSetupRequired = false;
					pinStateCheckFailed = false;
					if (changePinPopupBackButton != null)
						changePinPopupBackButton.interactable = true;
					SetInitialPinSetupLayout(false);
					SetChangePinButtonLabel(changePinButtonDefaultLabel);
					changePinPopup.SetActive(false);
				}
				else
					SetStatus(changeStatusText, "Lưu mã phụ huynh thành công.", Color.green);
			}
			else
			{
				SetStatus(changeStatusText, "Mã hiện tại không đúng hoặc không thể lưu.", Color.red);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogError($"[Pin] Không thể lưu mã phụ huynh: {ex.Message}");
			SetStatus(changeStatusText, "Không thể lưu mã phụ huynh. Vui lòng thử lại.", Color.red);
		}
		finally
		{
			if (this != null)
			{
				isChanging = false;
				if (changePinButton != null)
					changePinButton.interactable = true;
			}
		}
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

	private void SetVerifyButtonLabel(string label)
	{
		if (verifyPinButton == null || string.IsNullOrEmpty(label))
			return;

		TMP_Text buttonText = verifyPinButton.GetComponentInChildren<TMP_Text>(true);
		if (buttonText != null)
			buttonText.text = label;
	}

	private void SetChangePinButtonLabel(string label)
	{
		if (changePinButton == null || string.IsNullOrEmpty(label))
			return;

		TMP_Text buttonText = changePinButton.GetComponentInChildren<TMP_Text>(true);
		if (buttonText != null)
			buttonText.text = label;
	}

	private async Task CheckParentPinStateAsync()
	{
		if (isCheckingPinState || changePinPopup == null)
			return;

		isCheckingPinState = true;
		pinStateCheckFailed = false;
		initialPinSetupRequired = false;
		changePinPopup.SetActive(true);
		changePinPopup.transform.SetAsLastSibling();
		if (currentPinInput != null) currentPinInput.gameObject.SetActive(false);
		if (newPinInput != null) newPinInput.gameObject.SetActive(false);
		if (confirmPinInput != null) confirmPinInput.gameObject.SetActive(false);
		if (changePinPopupBackButton != null) changePinPopupBackButton.interactable = false;
		if (changePinButton != null) changePinButton.interactable = false;
		SetChangePinButtonLabel("Đang kiểm tra...");
		SetStatus(changeStatusText, "Đang kiểm tra trạng thái PIN phụ huynh...", Color.white);

		try
		{
			FirebaseManager manager = FirebaseManager.Instance;
			if (manager == null)
				throw new System.InvalidOperationException("Không tìm thấy FirebaseManager.");
			if (!await manager.EnsureInitializedAsync())
				throw new System.InvalidOperationException("Firebase chưa sẵn sàng.");

			int sessionGeneration = manager.SessionGeneration;
			string parentId = manager.GetCurrentParentId();
			ParentPinState pinState = await manager.GetParentPinStateAsync();
			if (this == null)
				return;
			if (manager != FirebaseManager.Instance || sessionGeneration != manager.SessionGeneration ||
				parentId != manager.GetCurrentParentId())
			{
				ShowPinStateCheckFailure("Phiên đăng nhập vừa thay đổi. Hãy kiểm tra lại.");
				return;
			}

			if (pinState == ParentPinState.Configured)
			{
				changePinPopup.SetActive(false);
				if (changePinPopupBackButton != null) changePinPopupBackButton.interactable = true;
				SetInitialPinSetupLayout(false);
				if (newPinInput != null) newPinInput.gameObject.SetActive(true);
				if (confirmPinInput != null) confirmPinInput.gameObject.SetActive(true);
				SetChangePinButtonLabel(changePinButtonDefaultLabel);
				ClearChangeInputs();
				return;
			}

			if (pinState == ParentPinState.NotConfigured)
			{
				OpenInitialPinSetupGate();
				return;
			}

			throw new System.InvalidOperationException(pinState == ParentPinState.NotAuthenticated
				? "Phiên đăng nhập phụ huynh đã hết hạn."
				: "Không thể đọc hồ sơ phụ huynh.");
		}
		catch (System.Exception ex)
		{
			if (this != null)
			{
				Debug.LogWarning($"[Pin] Chưa thể xác minh trạng thái PIN: {ex.GetType().Name}");
				ShowPinStateCheckFailure("Không thể xác minh PIN. Kiểm tra kết nối rồi nhấn Thử lại.");
			}
		}
		finally
		{
			if (this != null)
				isCheckingPinState = false;
		}
	}

	private void ShowPinStateCheckFailure(string message)
	{
		pinStateCheckFailed = true;
		initialPinSetupRequired = false;
		changePinPopup.SetActive(true);
		changePinPopup.transform.SetAsLastSibling();
		if (currentPinInput != null) currentPinInput.gameObject.SetActive(false);
		if (newPinInput != null) newPinInput.gameObject.SetActive(false);
		if (confirmPinInput != null) confirmPinInput.gameObject.SetActive(false);
		if (changePinPopupBackButton != null) changePinPopupBackButton.interactable = false;
		SetStatus(changeStatusText, message, Color.red);
		SetChangePinButtonLabel("Thử lại");
		if (changePinButton != null) changePinButton.interactable = true;
	}

	private void OpenInitialPinSetupGate()
	{
		initialPinSetupRequired = true;
		pinStateCheckFailed = false;
		changePinPopup.SetActive(true);
		changePinPopup.transform.SetAsLastSibling();
		if (currentPinInput != null)
			currentPinInput.gameObject.SetActive(false);
		if (newPinInput != null)
			newPinInput.gameObject.SetActive(true);
		if (confirmPinInput != null)
			confirmPinInput.gameObject.SetActive(true);
		SetChangePinButtonLabel(changePinButtonDefaultLabel);
		if (changePinButton != null)
			changePinButton.interactable = true;
		if (changePinPopupBackButton != null)
			changePinPopupBackButton.interactable = false;
		SetInitialPinSetupLayout(true);
		SetStatus(changeStatusText,
			"Chưa có PIN. Tạo PIN mới 4 số và xác nhận; để trống ô PIN hiện tại.",
			Color.red);
	}

	private void SetInitialPinSetupLayout(bool isFirstSetup)
	{
		if (currentPinInput != null)
			currentPinInput.gameObject.SetActive(!isFirstSetup);

		SetAnchoredY(newPinInput != null ? newPinInput.GetComponent<RectTransform>() : null,
			isFirstSetup ? 518f : 351f);
		SetAnchoredY(confirmPinInput != null ? confirmPinInput.GetComponent<RectTransform>() : null,
			isFirstSetup ? 351f : 184f);
		SetAnchoredY(changeStatusText != null ? changeStatusText.rectTransform : null,
			isFirstSetup ? 184f : 7f);
		SetAnchoredY(changePinButton != null ? changePinButton.transform as RectTransform : null,
			isFirstSetup ? 28f : -139f);
	}

	private static void SetAnchoredY(RectTransform rect, float y)
	{
		if (rect == null)
			return;

		Vector2 position = rect.anchoredPosition;
		position.y = y;
		rect.anchoredPosition = position;
	}

	private void ClearChangeInputs()
	{
		if (currentPinInput != null) currentPinInput.text = string.Empty;
		if (newPinInput != null) newPinInput.text = string.Empty;
		if (confirmPinInput != null) confirmPinInput.text = string.Empty;
	}
}
