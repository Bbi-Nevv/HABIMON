using System.Collections.Generic;
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

        if (popupPanel != null)
            popupPanel.SetActive(false);
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
            popupStatusText.text = string.Empty;

        popupPanel.SetActive(true);
    }

    public void ClosePopup()
    {
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

    private async void OnAddChildClicked()
    {
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

        if (addChildButton != null)
            addChildButton.interactable = false;

        try
        {
            FirebaseManager firebaseManager = FirebaseManager.Instance;
            if (firebaseManager == null)
            {
                SetPopupStatus("Firebase chưa sẵn sàng.");
                return;
            }

            var (success, message) = await firebaseManager.AddChildAsync(
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

            ClosePopup();
        }
        finally
        {
            if (addChildButton != null)
                addChildButton.interactable = true;
        }
    }

    private void SetPopupStatus(string message)
    {
        if (popupStatusText != null)
            popupStatusText.text = message;
        else
            Debug.LogWarning($"[AddChild] {message}");
    }
}
