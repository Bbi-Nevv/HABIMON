using System.Collections.Generic;
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

    [Header("Target")]
    [SerializeField] private Mission missionTarget;

    private int selectedIconIndex = 0;

    public void SelectIcon(int index)
    {
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

        if (popupPanel != null)
            popupPanel.SetActive(false);
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
        if (popupPanel != null)
            popupPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        if (inputTitle != null) inputTitle.text = string.Empty;
        if (inputDescription != null) inputDescription.text = string.Empty;
        if (dropdownTimeTarget != null)
            dropdownTimeTarget.value = 0;
    }

    private async void OnConfirmClicked()
    {
        if (missionTarget == null)
        {
            Debug.LogWarning("[AddHabitPopup] Chưa gán Mission target.");
            return;
        }

        string title = inputTitle != null ? inputTitle.text.Trim() : string.Empty;
        string description = inputDescription != null ? inputDescription.text.Trim() : string.Empty;
        string timeTarget = dropdownTimeTarget != null && dropdownTimeTarget.options.Count > 0
            ? dropdownTimeTarget.options[dropdownTimeTarget.value].text
            : string.Empty;

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(timeTarget))
        {
            Debug.LogWarning("[AddHabitPopup] Vui lòng nhập đầy đủ thông tin thói quen.");
            return;
        }

        string selectedIconKey = GetSelectedIconKey();

        await missionTarget.SubmitHabitFromPopup(title, description, timeTarget, selectedIconKey);
        ClosePopup();
    }
}
