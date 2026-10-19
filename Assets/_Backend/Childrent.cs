using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Childrent : MonoBehaviour
{
    [Header("Child List")]
    [SerializeField] private GameObject childPrefab;
    [SerializeField] private Transform contentTransform;
    [SerializeField] private Sprite[] genderSprites = new Sprite[2];
    [SerializeField] private AddHabitPopup addHabitPopup;
    [SerializeField] private UpdateChild updateChildPopup;
    [SerializeField] private GameObject Mission;

    private async void Start()
    {
        if (addHabitPopup == null)
            addHabitPopup = FindFirstObjectByType<AddHabitPopup>();

        if (updateChildPopup == null)
            updateChildPopup = FindFirstObjectByType<UpdateChild>(FindObjectsInactive.Include);

        await LoadChildrenAsync();
    }

    public async Task LoadChildrenAsync()
    {
        FirebaseManager firebaseManager = FirebaseManager.Instance;
        if (firebaseManager == null || !firebaseManager.IsInitialized)
        {
            Debug.LogWarning("[Childrent] Firebase chưa sẵn sàng.");
            return;
        }

        if (childPrefab == null || contentTransform == null)
        {
            Debug.LogWarning("[Childrent] Chưa gán childPrefab hoặc contentTransform.");
            return;
        }

        string parentId = firebaseManager.GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId))
        {
            Debug.LogWarning("[Childrent] Không xác định được phụ huynh đang đăng nhập.");
            return;
        }

        ClearChildren();
        List<ChildModel> children = await firebaseManager.GetChildrenAsync(parentId);
        foreach (ChildModel child in children)
        {
            SpawnChildItem(child, firebaseManager);
        }
    }

    private void ClearChildren()
    {
        for (int index = contentTransform.childCount - 1; index >= 0; index--)
        {
            Destroy(contentTransform.GetChild(index).gameObject);
        }
    }

    private void SpawnChildItem(ChildModel child, FirebaseManager firebaseManager)
    {
        if (child == null)
            return;

        GameObject item = Instantiate(childPrefab, contentTransform);
        TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
            texts[0].text = string.IsNullOrWhiteSpace(child.name) ? "Chưa đặt tên" : child.name;

        if (texts.Length > 1)
        {
            string age = child.age > 0 ? $"{child.age} tuổi" : "Chưa cập nhật tuổi";
            string gender = child.gender == 0 ? "Nữ" : child.gender == 1 ? "Nam" : "Chưa cập nhật";
            texts[1].text = $"{age}, {gender}";
        }

        Image childImage = item.transform.Find("Image")?.GetComponentInChildren<Image>();
        if (childImage != null && genderSprites != null && child.gender >= 0 && child.gender < genderSprites.Length && genderSprites[child.gender] != null)
        {
            childImage.sprite = genderSprites[child.gender];
        }

        if (updateChildPopup != null && !string.IsNullOrEmpty(child.childId))
        {
            Button editButton = item.transform.Find("EditButton")?.GetComponent<Button>();
            if (editButton == null) Debug.LogWarning("[Childrent] Không tìm thấy nút chỉnh sửa trẻ trong prefab.");
            else
            {
                // Use a fresh event so an Inspector callback cannot open a different child.
                editButton.onClick = new Button.ButtonClickedEvent();
                editButton.onClick.AddListener(() => updateChildPopup.OpenPopup(child));
            }

            // Use a fresh event so an Inspector callback cannot open a different child.
            // editButton.onClick = new Button.ButtonClickedEvent();
            // editButton.onClick.AddListener(() => updateChildPopup.OpenPopup(child));
        }

        if (!string.IsNullOrEmpty(child.childId))
        {
            Button deleteButton = item.transform.Find("DeleteButton")?.GetComponent<Button>();
            if (deleteButton == null) Debug.LogWarning("[Childrent] Không tìm thấy nút xóa trẻ trong prefab.");
            else
            {
                // Use a fresh event so an Inspector callback cannot delete a different child.
                deleteButton.onClick = new Button.ButtonClickedEvent();
                deleteButton.onClick.AddListener(() => OnDeleteChildClicked(child, item, firebaseManager));
            }

            // deleteButton.onClick = new Button.ButtonClickedEvent();
            // deleteButton.onClick.AddListener(() => OnDeleteChildClicked(child, item, firebaseManager));
        }

        Button button = item.GetComponent<Button>();
        if (button != null && !string.IsNullOrEmpty(child.childId))
        {
            button.onClick.AddListener(() =>
            {
                firebaseManager.SetCurrentChildId(child.childId);

                if (Mission != null)
                {
                    Mission.SetActive(true);

                    var missionController = Mission.GetComponent<Mission>();
                    if (missionController != null && missionController.HasStarted)
                    {
                        _ = missionController.LoadHabitsForCurrentUser();
                    }

                    var displayMission = Mission.GetComponent<DisplayMission>();
                    var nameChildText = Mission.transform.Find("NameChild")?.GetComponent<TMP_Text>();
                    if (displayMission != null && displayMission.HasStarted)
                    {
                        _ = displayMission.LoadHabitsForChildAsync(child.childId);
                        if (nameChildText != null)
                            nameChildText.text = $"Thói quen của {child.name}";
                    }
                }

                // if (addHabitPopup != null)
                //     addHabitPopup.OpenPopup();
                // else
                //     Debug.LogWarning("[Childrent] Chưa tìm thấy AddHabitPopup.");
            });
        }
    }

    private async void OnDeleteChildClicked(ChildModel child, GameObject item, FirebaseManager firebaseManager)
    {
        Button[] buttons = item.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            button.interactable = false;

        try
        {
            bool wasCurrentChild = firebaseManager.GetCurrentChildId() == child.childId;
            var (success, message) = await firebaseManager.DeleteChildAsync(child.childId);
            if (!success)
            {
                Debug.LogWarning($"[Childrent] {message}");
                return;
            }

            if (wasCurrentChild && string.IsNullOrEmpty(firebaseManager.GetCurrentChildId()) && Mission != null)
                Mission.SetActive(false);

            if (this != null)
                await LoadChildrenAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Childrent] Không thể hoàn tất xóa trẻ: {ex.Message}");
        }
        finally
        {
            foreach (Button button in buttons)
            {
                if (button != null)
                    button.interactable = true;
            }
        }
    }
}
