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
	private bool isVerifying;
	private bool isChanging;

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
		if (!isVerifying)
			failedAttempts = 0;

		if (verifyPinButton != null)
			verifyPinButton.interactable = !isVerifying;

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
		if (isVerifying)
			return;

		FirebaseManager manager = FirebaseManager.Instance;
		if (manager == null)
		{
			SetStatus(verifyStatusText, "Không tìm thấy FirebaseManager.", Color.red);
			return;
		}

		string pin = verifyPinInput != null ? verifyPinInput.text.Trim() : string.Empty;

		if (!IsValidPin(pin))
		{
			SetStatus(verifyStatusText, "PIN phải gồm đúng 4 chữ số.", Color.red);
			return;
		}

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

			if (!await manager.HasParentPinAsync())
			{
				SetStatus(verifyStatusText, "Mã phụ huynh chưa được thiết lập. Vui lòng mở chế độ phụ huynh để tạo mã.", Color.red);
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
		if (isChanging)
			return;

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

			bool hasPin = await manager.HasParentPinAsync();
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
				SetStatus(changeStatusText, "Lưu mã phụ huynh thành công.", Color.green);
				ClearChangeInputs();
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

	private void ClearChangeInputs()
	{
		if (currentPinInput != null) currentPinInput.text = string.Empty;
		if (newPinInput != null) newPinInput.text = string.Empty;
		if (confirmPinInput != null) confirmPinInput.text = string.Empty;
	}
}
