using System.Collections.Generic;
using System;
using HABIMON.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddChild : MonoBehaviour
{
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField inputChildName;
    [SerializeField] private TMP_InputField inputChildAge;
    [SerializeField] private TMP_Dropdown dropdownChildGender;
    [SerializeField] private Button addChildButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text popupStatusText;
    [SerializeField] private Childrent childList;

    private bool isSubmitting;
    private string pendingOperationId;
    private PendingChildCreate pendingCreate;
    private bool pendingWriteCommitted;

    private void Start()
    {
        if (childList == null)
            childList = FindFirstObjectByType<Childrent>();

        if (inputChildAge != null)
            inputChildAge.contentType = TMP_InputField.ContentType.IntegerNumber;

        if (dropdownChildGender != null)
        {
            dropdownChildGender.ClearOptions();
            dropdownChildGender.AddOptions(new List<string> { "Nữ", "Nam" });
            dropdownChildGender.value = 0;
            dropdownChildGender.RefreshShownValue();
        }

        if (addChildButton != null)
        {
            addChildButton.onClick.RemoveListener(OnAddChildClicked);
            addChildButton.onClick.AddListener(OnAddChildClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ClosePopup);
            cancelButton.onClick.AddListener(ClosePopup);
        }

    }

    private void OnDestroy()
    {
        if (addChildButton != null)
            addChildButton.onClick.RemoveListener(OnAddChildClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(ClosePopup);
    }

    public void OpenPopup()
    {
        if (popupPanel == null)
        {
            Debug.LogWarning("[AddChild] Chưa gán popupPanel.");
            return;
        }

        if (popupStatusText != null)
            popupStatusText.text = string.IsNullOrEmpty(pendingOperationId)
                ? string.Empty
                : "Thao tác chưa hoàn tất. Nhấn Thử lại để tiếp tục với cùng hồ sơ.";
        SetFormInteractable(string.IsNullOrEmpty(pendingOperationId));
        SetSubmitCaption(string.IsNullOrEmpty(pendingOperationId) ? "Thêm" : "Thử lại");

        popupPanel.SetActive(true);
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
            pendingWriteCommitted = false;
            if (inputChildName != null)
                inputChildName.text = string.Empty;
            if (inputChildAge != null)
                inputChildAge.text = string.Empty;
            if (dropdownChildGender != null)
                dropdownChildGender.value = 0;
            if (popupStatusText != null)
                popupStatusText.text = string.Empty;
            SetFormInteractable(true);
            SetSubmitCaption("Thêm");
        }
        else
        {
            SetFormInteractable(false);
            SetSubmitCaption("Thử lại");
        }
    }

    private async void OnAddChildClicked()
    {
        if (isSubmitting)
            return;

        FirebaseManager managerAtClick = FirebaseManager.Instance;
        string parentAtClick = managerAtClick != null ? managerAtClick.GetCurrentParentId() : string.Empty;
        int generationAtClick = managerAtClick != null ? managerAtClick.SessionGeneration : -1;

        string name = pendingCreate != null ? pendingCreate.Name :
            inputChildName != null ? inputChildName.text.Trim() : string.Empty;
        string ageText = inputChildAge != null ? pendingCreate != null
            ? pendingCreate.Age.ToString()
            : inputChildAge.text.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            SetPopupStatus("Vui lòng nhập tên trẻ.");
            return;
        }

        if (!int.TryParse(ageText, out int age) || age <= 0)
        {
            SetPopupStatus("Vui lòng nhập tuổi là số lớn hơn 0.");
            return;
        }

        if (pendingCreate == null && dropdownChildGender == null)
        {
            SetPopupStatus("Chưa chọn giới tính.");
            return;
        }
        int gender = pendingCreate != null ? pendingCreate.Gender : dropdownChildGender.value;

        isSubmitting = true;
        if (addChildButton != null)
            addChildButton.interactable = false;
        if (cancelButton != null)
            cancelButton.interactable = false;

        bool closeOnSuccess = false;
        try
        {
            FirebaseManager firebaseManager = FirebaseManager.Instance;
            if (firebaseManager == null)
            {
                SetPopupStatus("Firebase chưa sẵn sàng.");
                return;
            }

            if (!await firebaseManager.EnsureInitializedAsync())
            {
                SetPopupStatus("Firebase chưa sẵn sàng.");
                return;
            }

            if (pendingWriteCommitted)
            {
                if (pendingCreate == null ||
                    firebaseManager.GetCurrentParentId() != pendingCreate.Scope.ParentId)
                {
                    SetPopupStatus("Đã lưu trẻ trong hồ sơ ban đầu. Quay lại đúng phụ huynh để làm mới danh sách.");
                    return;
                }

                bool refreshed = childList != null && await childList.LoadChildrenAsync();
                closeOnSuccess = HandleChildListRefreshAfterSave(refreshed);
                return;
            }

            if (pendingCreate == null)
            {
                if (managerAtClick == null || parentAtClick != firebaseManager.GetCurrentParentId() ||
                    generationAtClick != firebaseManager.SessionGeneration ||
                    string.IsNullOrEmpty(parentAtClick))
                {
                    SetPopupStatus("Phiên hoặc phụ huynh đã thay đổi. Vui lòng mở lại thao tác.");
                    return;
                }

                pendingOperationId = Guid.NewGuid().ToString("N");
                pendingCreate = new PendingChildCreate(
                    new FirebaseWriteScope(parentAtClick, string.Empty, generationAtClick),
                    pendingOperationId,
                    name,
                    age,
                    gender);
                SetFormInteractable(false);
                SetSubmitCaption("Thử lại");
            }

            if (!pendingCreate.IsCurrent(firebaseManager.GetCurrentParentId(), firebaseManager.SessionGeneration))
            {
                try
                {
                    var currentScope = new FirebaseWriteScope(
                        firebaseManager.GetCurrentParentId(),
                        string.Empty,
                        firebaseManager.SessionGeneration);
                    pendingCreate = pendingCreate.ResumeInSameParent(currentScope);
                }
                catch (ArgumentException)
                {
                    SetPopupStatus("Đã đổi hồ sơ. Quay lại đúng phụ huynh và trẻ ban đầu để tiếp tục thao tác này.");
                    return;
                }
                catch (InvalidOperationException)
                {
                    SetPopupStatus("Đã đổi phụ huynh hoặc trẻ. Thao tác này không thể chuyển sang hồ sơ khác.");
                    return;
                }
            }

            var (success, message) = await firebaseManager.AddChildAsync(pendingCreate);

            if (this == null)
                return;

            if (!success)
            {
                SetPopupStatus(message);
                SetFormInteractable(false);
                SetSubmitCaption("Thử lại");
                return;
            }

            pendingWriteCommitted = true;
            if (firebaseManager.GetCurrentParentId() != pendingCreate.Scope.ParentId)
            {
                SetPopupStatus("Đã lưu trẻ trong hồ sơ ban đầu. Quay lại đúng phụ huynh để làm mới danh sách.");
                SetFormInteractable(false);
                SetSubmitCaption("Thử lại");
                return;
            }

            bool listRefreshed = false;
            if (childList != null)
            {
                try
                {
                    listRefreshed = await childList.LoadChildrenAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AddChild] Đã lưu trẻ nhưng không thể làm mới danh sách: {ex.Message}");
                }
            }

            closeOnSuccess = HandleChildListRefreshAfterSave(listRefreshed);
        }
        catch (Exception ex)
        {
            if (this != null)
            {
                Debug.LogError($"[AddChild] Không thể hoàn tất thêm trẻ: {ex.Message}");
                SetPopupStatus("Không thể hoàn tất thêm trẻ. Vui lòng thử lại.");
                SetFormInteractable(false);
                SetSubmitCaption("Thử lại");
            }
        }
        finally
        {
            if (this != null)
            {
                isSubmitting = false;
                if (addChildButton != null)
                    addChildButton.interactable = true;
                if (cancelButton != null)
                    cancelButton.interactable = true;
            }
        }

        if (closeOnSuccess && this != null)
            ClosePopup();
    }

    private bool HandleChildListRefreshAfterSave(bool listRefreshed)
    {
        if (!listRefreshed)
        {
            SetPopupStatus("Đã lưu trẻ nhưng chưa tải lại được danh sách. Nhấn Thử lại để tiếp tục.");
            SetFormInteractable(false);
            SetSubmitCaption("Thử lại");
            return false;
        }

        pendingOperationId = null;
        pendingCreate = null;
        pendingWriteCommitted = false;
        SetFormInteractable(true);
        SetSubmitCaption("Thêm");
        return true;
    }

    private void SetPopupStatus(string message)
    {
        if (popupStatusText != null)
            popupStatusText.text = message;
        else
            Debug.LogWarning($"[AddChild] {message}");
    }

    private void SetFormInteractable(bool value)
    {
        if (inputChildName != null) inputChildName.interactable = value;
        if (inputChildAge != null) inputChildAge.interactable = value;
        if (dropdownChildGender != null) dropdownChildGender.interactable = value;
    }

    private void SetSubmitCaption(string caption)
    {
        TMP_Text label = addChildButton != null ? addChildButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
            label.text = caption;
    }
}
