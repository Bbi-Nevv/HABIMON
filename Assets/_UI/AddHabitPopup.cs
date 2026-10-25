using System.Collections.Generic;
using System;
using HABIMON.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddHabitPopup : MonoBehaviour
{
    [Header("Popup UI")]
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputDescription;
    [SerializeField] private TMP_Dropdown dropdownTimeTarget;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private List<Sprite> habitIcons;
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TMP_Text popupStatusText;

    [Header("Target")]
    [SerializeField] private Mission missionTarget;

    private int selectedIconIndex = 0;
    private bool isSubmitting;
    private string pendingOperationId;
    private PendingHabitCreate pendingCreate;

    public void SelectIcon(int index)
    {
        if (pendingCreate != null)
            return;

        if (habitIcons == null || habitIcons.Count == 0)
            return;

        selectedIconIndex = Mathf.Clamp(index, 0, habitIcons.Count - 1);

        if (selectedIconImage != null)
            selectedIconImage.sprite = habitIcons[selectedIconIndex];
    }

    public string GetSelectedIconKey()
    {
        if (habitIcons == null || habitIcons.Count == 0 || selectedIconIndex < 0 || selectedIconIndex >= habitIcons.Count)
            return "icon_default";

        return habitIcons[selectedIconIndex].name;
    }

    private void Start()
    {
        EnsurePopupStatusText();

        if (dropdownTimeTarget != null)
        {
            dropdownTimeTarget.ClearOptions();
            dropdownTimeTarget.AddOptions(new System.Collections.Generic.List<string>
            {
                "Buổi Sáng",
                "Buổi Chiều",
                "Buổi Tối"
            });
        }

        if (selectedIconImage != null && habitIcons != null && habitIcons.Count > 0)
            selectedIconImage.sprite = habitIcons[selectedIconIndex];

        if (btnConfirm != null)
            btnConfirm.onClick.AddListener(OnConfirmClicked);

        if (btnCancel != null)
            btnCancel.onClick.AddListener(ClosePopup);

    }

    private void OnDestroy()
    {
        if (btnConfirm != null)
            btnConfirm.onClick.RemoveListener(OnConfirmClicked);

        if (btnCancel != null)
            btnCancel.onClick.RemoveListener(ClosePopup);
    }

    public void OpenPopup()
    {
        EnsurePopupStatusText();
        if (popupPanel != null)
            popupPanel.SetActive(true);
        SetStatus(string.IsNullOrEmpty(pendingOperationId)
            ? "Hoàn thành sẽ chờ phụ huynh duyệt."
            : "Thao tác chưa hoàn tất. Nhấn Thử lại để tiếp tục với cùng thói quen.");
        SetFormInteractable(string.IsNullOrEmpty(pendingOperationId));
        SetConfirmCaption(string.IsNullOrEmpty(pendingOperationId) ? "Thêm" : "Thử lại");
    }

    public void ClosePopup()
    {
        if (isSubmitting)
            return;

        if (popupPanel != null)
            popupPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingOperationId))
        {
            pendingCreate = null;
            if (inputTitle != null) inputTitle.text = string.Empty;
            if (inputDescription != null) inputDescription.text = string.Empty;
            if (dropdownTimeTarget != null)
                dropdownTimeTarget.value = 0;
            SetFormInteractable(true);
            SetConfirmCaption("Thêm");
            SetStatus(string.Empty);
        }
        else
        {
            SetFormInteractable(false);
            SetConfirmCaption("Thử lại");
        }
    }

    private async void OnConfirmClicked()
    {
        if (isSubmitting)
            return;

        if (missionTarget == null)
        {
            SetStatus("Chưa kết nối màn hình nhiệm vụ.");
            return;
        }

        string title = pendingCreate != null ? pendingCreate.Title :
            inputTitle != null ? inputTitle.text.Trim() : string.Empty;
        string description = pendingCreate != null ? pendingCreate.Description :
            inputDescription != null ? inputDescription.text.Trim() : string.Empty;
        string timeTarget = pendingCreate != null ? pendingCreate.TimeTarget :
            dropdownTimeTarget != null && dropdownTimeTarget.options.Count > 0
                ? dropdownTimeTarget.options[dropdownTimeTarget.value].text
                : string.Empty;

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(timeTarget))
        {
            SetStatus("Vui lòng nhập đầy đủ thông tin thói quen.");
            return;
        }

        isSubmitting = true;
        if (btnConfirm != null)
            btnConfirm.interactable = false;
        if (btnCancel != null)
            btnCancel.interactable = false;

        bool closeOnSuccess = false;
        try
        {
            if (pendingCreate == null)
            {
                string operationId = string.IsNullOrEmpty(pendingOperationId)
                    ? Guid.NewGuid().ToString("N")
                    : pendingOperationId;
                pendingCreate = await missionTarget.PrepareHabitCreateFromPopupAsync(
                    title, description, timeTarget, GetSelectedIconKey(), operationId);
                if (pendingCreate == null)
                {
                    SetStatus("Không thể xác định hồ sơ hiện tại. Vui lòng thử lại.");
                    return;
                }
                pendingOperationId = pendingCreate.OperationId;
                SetFormInteractable(false);
                SetConfirmCaption("Thử lại");
            }

            FirebaseManager firebaseManager = FirebaseManager.Instance;
            if (firebaseManager == null)
            {
                HandleSubmitResult(false, "Firebase chưa sẵn sàng.");
                return;
            }

            if (!pendingCreate.IsCurrent(firebaseManager.GetCurrentParentId(),
                    firebaseManager.GetCurrentChildId(), firebaseManager.SessionGeneration))
            {
                try
                {
                    var currentScope = new FirebaseWriteScope(
                        firebaseManager.GetCurrentParentId(),
                        firebaseManager.GetCurrentChildId(),
                        firebaseManager.SessionGeneration);
                    pendingCreate = pendingCreate.ResumeInSameTarget(currentScope);
                }
                catch (ArgumentException)
                {
                    HandleSubmitResult(false, "Đã đổi hồ sơ. Quay lại đúng trẻ ban đầu để tiếp tục thao tác này.");
                    return;
                }
                catch (InvalidOperationException)
                {
                    HandleSubmitResult(false, "Đã đổi phụ huynh hoặc trẻ. Thao tác này không thể chuyển sang hồ sơ khác.");
                    return;
                }
            }

            var result = await missionTarget.SubmitHabitFromPopup(pendingCreate);
            if (this == null)
                return;

            closeOnSuccess = HandleSubmitResult(result.success, result.message);
        }
        catch (Exception ex)
        {
            if (this != null)
            {
                Debug.LogError($"[AddHabitPopup] Không thể lưu thói quen: {ex.Message}");
                SetStatus("Không thể lưu thói quen. Vui lòng thử lại.");
                SetFormInteractable(string.IsNullOrEmpty(pendingOperationId));
                SetConfirmCaption(string.IsNullOrEmpty(pendingOperationId) ? "Thêm" : "Thử lại");
            }
        }
        finally
        {
            if (this != null)
            {
                isSubmitting = false;
                if (btnConfirm != null)
                    btnConfirm.interactable = true;
                if (btnCancel != null)
                    btnCancel.interactable = true;
            }
        }

        if (closeOnSuccess && this != null)
            ClosePopup();
    }

    private bool HandleSubmitResult(bool success, string message)
    {
        if (!success)
        {
            SetStatus(message);
            SetFormInteractable(string.IsNullOrEmpty(pendingOperationId));
            SetConfirmCaption(string.IsNullOrEmpty(pendingOperationId) ? "Thêm" : "Thử lại");
            return false;
        }

        SetStatus(string.Empty);
        pendingOperationId = null;
        pendingCreate = null;
        SetFormInteractable(true);
        SetConfirmCaption("Thêm");
        return true;
    }

    private void SetStatus(string message)
    {
        EnsurePopupStatusText();
        if (popupStatusText != null)
            popupStatusText.text = message;
        else if (!string.IsNullOrEmpty(message))
            Debug.LogWarning($"[AddHabitPopup] {message}");
    }

    private void EnsurePopupStatusText()
    {
        if (popupStatusText != null || popupPanel == null)
            return;

        GameObject statusObject = new GameObject("SaveStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObject.layer = popupPanel.layer;
        statusObject.transform.SetParent(popupPanel.transform, false);
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(700f, 72f);
        statusRect.anchoredPosition = new Vector2(0f, -330f);

        popupStatusText = statusObject.GetComponent<TextMeshProUGUI>();
        if (inputTitle != null && inputTitle.textComponent != null)
            popupStatusText.font = inputTitle.textComponent.font;
        popupStatusText.fontSize = 24f;
        popupStatusText.color = new Color(0.85f, 0.15f, 0.15f, 1f);
        popupStatusText.alignment = TextAlignmentOptions.Center;
        popupStatusText.textWrappingMode = TextWrappingModes.Normal;
        popupStatusText.raycastTarget = false;
    }

    private void SetFormInteractable(bool value)
    {
        if (inputTitle != null) inputTitle.interactable = value;
        if (inputDescription != null) inputDescription.interactable = value;
        if (dropdownTimeTarget != null) dropdownTimeTarget.interactable = value;
    }

    private void SetConfirmCaption(string caption)
    {
        TMP_Text label = btnConfirm != null ? btnConfirm.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
            label.text = caption;
    }
}
