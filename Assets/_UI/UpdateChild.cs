using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpdateChild : MonoBehaviour
{
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField inputChildName;
    [SerializeField] private TMP_InputField inputChildAge;
    [SerializeField] private TMP_Dropdown dropdownChildGender;
    [SerializeField] private Button updateChildButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text popupStatusText;
    [SerializeField] private Childrent childList;

    private string editingChildId = string.Empty;
    private bool isSaving;
    private int popupVersion;

    private void Awake()
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

        if (updateChildButton != null)
        {
            updateChildButton.onClick.RemoveListener(OnUpdateChildClicked);
            updateChildButton.onClick.AddListener(OnUpdateChildClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ClosePopup);
            cancelButton.onClick.AddListener(ClosePopup);
        }

        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (updateChildButton != null)
            updateChildButton.onClick.RemoveListener(OnUpdateChildClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(ClosePopup);
    }

    public async void OpenPopup()
    {
        if (isSaving)
            return;

        FirebaseManager firebaseManager = FirebaseManager.Instance;
        if (firebaseManager == null || !firebaseManager.IsInitialized)
        {
            SetPopupStatus("Firebase chưa sẵn sàng.");
            return;
        }

        int version = ++popupVersion;
        string childId = firebaseManager.GetCurrentChildId();
        if (string.IsNullOrEmpty(childId))
        {
            SetPopupStatus("Vui lòng chọn trẻ cần cập nhật.");
            return;
        }

        try
        {
            List<ChildModel> children = await firebaseManager.GetChildrenAsync(firebaseManager.GetCurrentParentId());
            if (this == null || version != popupVersion)
                return;

            ChildModel child = children.Find(item => item.childId == childId);
            if (child == null)
            {
                SetPopupStatus("Không tìm thấy thông tin trẻ.");
                return;
            }

            OpenPopup(child);
        }
        catch (System.Exception ex)
        {
            if (this == null || version != popupVersion)
                return;

            Debug.LogError($"[UpdateChild] {ex.Message}");
            SetPopupStatus("Không thể tải thông tin trẻ. Vui lòng thử lại.");
        }
    }

    public void OpenPopup(ChildModel child)
    {
        if (isSaving)
            return;

        ++popupVersion;
        if (child == null || string.IsNullOrEmpty(child.childId))
        {
            SetPopupStatus("Không xác định được trẻ cần cập nhật.");
            return;
        }

        if (popupPanel == null)
        {
            Debug.LogWarning("[UpdateChild] Chưa gán popupPanel.");
            return;
        }

        if (popupStatusText != null)
            popupStatusText.text = string.Empty;

        popupPanel.SetActive(true);
        editingChildId = child.childId;
        if (inputChildName != null)
            inputChildName.text = child.name ?? string.Empty;
        if (inputChildAge != null)
            inputChildAge.text = child.age.ToString();
        if (dropdownChildGender != null)
        {
            dropdownChildGender.value = child.gender == 0 || child.gender == 1 ? child.gender : -1;
            dropdownChildGender.RefreshShownValue();
        }
    }

    public void ClosePopup()
    {
        if (isSaving)
            return;

        ++popupVersion;
        editingChildId = string.Empty;
        if (popupPanel != null)
            popupPanel.SetActive(false);

        if (inputChildName != null)
            inputChildName.text = string.Empty;

        if (inputChildAge != null)
            inputChildAge.text = string.Empty;

        if (dropdownChildGender != null)
            dropdownChildGender.value = 0;

        if (popupStatusText != null)
            popupStatusText.text = string.Empty;
    }

    private async void OnUpdateChildClicked()
    {
        if (isSaving)
            return;

        string name = inputChildName != null ? inputChildName.text.Trim() : string.Empty;
        string ageText = inputChildAge != null ? inputChildAge.text.Trim() : string.Empty;

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

        if (dropdownChildGender == null)
        {
            SetPopupStatus("Chưa chọn giới tính.");
            return;
        }

        isSaving = true;
        if (updateChildButton != null)
            updateChildButton.interactable = false;
        if (cancelButton != null)
            cancelButton.interactable = false;

        try
        {
            FirebaseManager firebaseManager = FirebaseManager.Instance;
            if (firebaseManager == null)
            {
                SetPopupStatus("Firebase chưa sẵn sàng.");
                return;
            }

            var (success, message) = await firebaseManager.UpdateChildAsync(
                editingChildId,
                name,
                age,
                dropdownChildGender.value);

            if (!success)
            {
                SetPopupStatus(message);
                return;
            }

            if (childList != null)
                await childList.LoadChildrenAsync();

            isSaving = false;
            ClosePopup();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UpdateChild] {ex.Message}");
            SetPopupStatus("Không thể hoàn tất cập nhật. Vui lòng thử lại.");
        }
        finally
        {
            isSaving = false;
            if (cancelButton != null)
                cancelButton.interactable = true;
            if (updateChildButton != null)
                updateChildButton.interactable = true;
        }
    }

    private void SetPopupStatus(string message)
    {
        if (popupStatusText != null)
            popupStatusText.text = message;
        else
            Debug.LogWarning($"[UpdateChild] {message}");
    }
}
