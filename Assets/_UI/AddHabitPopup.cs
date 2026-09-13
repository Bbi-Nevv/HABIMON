using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddHabitPopup : MonoBehaviour
{
    [Header("Popup UI")]
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputDescription;
    [SerializeField] private TMP_InputField inputTimeTarget;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;
    [SerializeField] private GameObject popupPanel;

    [Header("Target")]
    [SerializeField] private Mission missionTarget;

    private void Start()
    {
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
        if (inputTimeTarget != null) inputTimeTarget.text = string.Empty;
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
        string timeTarget = inputTimeTarget != null ? inputTimeTarget.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(timeTarget))
        {
            Debug.LogWarning("[AddHabitPopup] Vui lòng nhập đầy đủ thông tin thói quen.");
            return;
        }

        await missionTarget.SubmitHabitFromPopup(title, description, timeTarget);
        ClosePopup();
    }
}
