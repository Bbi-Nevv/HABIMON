using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    private int childrenLoadVersion;
    private FirebaseManager subscribedManager;
    private readonly Dictionary<string, float> pendingDeleteConfirmations = new Dictionary<string, float>();

    private void OnEnable()
    {
        SubscribeToSession();
    }

    private async void Start()
    {
        if (addHabitPopup == null)
            addHabitPopup = FindFirstObjectByType<AddHabitPopup>();

        if (updateChildPopup == null)
            updateChildPopup = FindFirstObjectByType<UpdateChild>(FindObjectsInactive.Include);

        SubscribeToSession();
        await LoadChildrenAsync();
    }

    private void OnDisable()
    {
        childrenLoadVersion++;
        if (subscribedManager != null)
        {
            subscribedManager.SessionChanged -= OnSessionChanged;
            subscribedManager = null;
        }
    }

    private void SubscribeToSession()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || subscribedManager == manager)
            return;

        if (subscribedManager != null)
            subscribedManager.SessionChanged -= OnSessionChanged;
        subscribedManager = manager;
        subscribedManager.SessionChanged += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        ClearChildren();
        if (Mission != null)
            Mission.SetActive(false);

        if (subscribedManager != null && subscribedManager.CurrentUser != null)
            _ = LoadChildrenAsync();
    }

    public async Task<bool> LoadChildrenAsync()
    {
        int requestVersion = ++childrenLoadVersion;
        FirebaseManager firebaseManager = FirebaseManager.Instance;
        if (firebaseManager == null)
        {
            Debug.LogWarning("[Childrent] Firebase chưa sẵn sàng.");
            return false;
        }

        if (!await firebaseManager.EnsureInitializedAsync())
        {
            Debug.LogError($"[Childrent] Firebase initialization failed: {firebaseManager.InitializationError}");
            return false;
        }

        if (childPrefab == null || contentTransform == null)
        {
            Debug.LogWarning("[Childrent] Chưa gán childPrefab hoặc contentTransform.");
            return false;
        }

        string parentId = firebaseManager.GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId))
        {
            Debug.LogWarning("[Childrent] Không xác định được phụ huynh đang đăng nhập.");
            return false;
        }

        List<ChildModel> children;
        try
        {
            children = await firebaseManager.GetChildrenAsync(parentId);
        }
        catch (Exception ex)
        {
            if (this != null && requestVersion == childrenLoadVersion)
                Debug.LogError($"[Childrent] Không thể tải danh sách trẻ: {ex.Message}");
            return false;
        }

        if (this == null || requestVersion != childrenLoadVersion ||
            FirebaseManager.Instance != firebaseManager ||
            parentId != firebaseManager.GetCurrentParentId())
            return false;

        try
        {
            ClearChildren();
            foreach (ChildModel child in children)
                SpawnChildItem(child, firebaseManager);
            return true;
        }
        catch (Exception ex)
        {
            if (this != null && requestVersion == childrenLoadVersion)
                Debug.LogError($"[Childrent] Không thể hiển thị danh sách trẻ: {ex.Message}");
            return false;
        }
    }

    private void ClearChildren()
    {
        if (contentTransform == null)
            return;

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
            // The update date remains in the child model; only its redundant qualifier is
            // omitted from this compact parent task-list label.
            string age = child.age > 0 ? $"{child.age} tuổi" : "Chưa cập nhật tuổi";

            string gender = child.gender == 0 ? "Nữ" : child.gender == 1 ? "Nam" : "Chưa cập nhật";
            texts[1].text = $"{age}, {gender}";
        }

        Image childImage = item.transform.Find("Image")?.GetComponentInChildren<Image>();
        if (childImage != null && genderSprites != null && child.gender >= 0 && child.gender < genderSprites.Length && genderSprites[child.gender] != null)
        {
            childImage.sprite = genderSprites[child.gender];
        }

        bool isParentMode = SceneManager.GetActiveScene().name == "Parent";
        if (isParentMode && updateChildPopup != null && !string.IsNullOrEmpty(child.childId))
        {
            Button editButton = item.transform.Find("EditButton")?.GetComponent<Button>();
            if (editButton == null)
                editButton = CreateActionButton(item.transform, texts.Length > 0 ? texts[0] : null,
                    "EditButton", "Sửa", -84f, new Color(0.85f, 0.9f, 1f));

            // Use a fresh event so an Inspector callback cannot open a different child.
            editButton.onClick = new Button.ButtonClickedEvent();
            editButton.onClick.AddListener(() => updateChildPopup.OpenPopup(child));
        }

        if (!string.IsNullOrEmpty(child.childId))
        {
            Button deleteButton = item.transform.Find("DeleteButton")?.GetComponent<Button>();
            if (!isParentMode)
            {
                if (deleteButton != null)
                    deleteButton.gameObject.SetActive(false);

                Button editButton = item.transform.Find("EditButton")?.GetComponent<Button>();
                if (editButton != null)
                    editButton.gameObject.SetActive(false);
            }
            else if (deleteButton == null)
            {
                deleteButton = CreateActionButton(item.transform, texts.Length > 0 ? texts[0] : null,
                    "DeleteButton", "Xóa", -12f, new Color(1f, 0.85f, 0.85f));

                deleteButton.onClick = new Button.ButtonClickedEvent();
                deleteButton.onClick.AddListener(() => OnDeleteChildClicked(child, item, firebaseManager));
            }
            else if (isParentMode)
            {
                deleteButton.gameObject.SetActive(true);
                deleteButton.onClick = new Button.ButtonClickedEvent();
                deleteButton.onClick.AddListener(() => OnDeleteChildClicked(child, item, firebaseManager));
            }
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

                    DisplayMission displayMission = Mission.GetComponent<DisplayMission>();
                    if (displayMission != null)
                        _ = displayMission.LoadHabitsForChildAsync(child.childId);
                    TMP_Text nameChildText = Mission.transform.Find("NameChild")?.GetComponent<TMP_Text>();
                    if (nameChildText != null)
                        nameChildText.text = $"Thói quen của {child.name}";
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
        if (SceneManager.GetActiveScene().name != "Parent" || child == null || string.IsNullOrEmpty(child.childId))
            return;

        float now = Time.unscaledTime;
        if (!pendingDeleteConfirmations.TryGetValue(child.childId, out float expiresAt) || now > expiresAt)
        {
            pendingDeleteConfirmations[child.childId] = now + 5f;
            SetDeleteButtonCaption(item, "Xác nhận xóa");
            return;
        }

        pendingDeleteConfirmations.Remove(child.childId);
        Button[] buttons = item.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            button.interactable = false;

        try
        {
            string parentId = firebaseManager.GetCurrentParentId();
            bool wasCurrentChild = firebaseManager.GetCurrentChildId() == child.childId;
            var (success, message) = await firebaseManager.DeleteChildAsync(child.childId);
            if (!success)
            {
                Debug.LogWarning($"[Childrent] {message}");
                return;
            }

            if (firebaseManager.GetCurrentParentId() == parentId && wasCurrentChild &&
                string.IsNullOrEmpty(firebaseManager.GetCurrentChildId()) && Mission != null)
                Mission.SetActive(false);

            if (this != null && firebaseManager.GetCurrentParentId() == parentId)
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
            SetDeleteButtonCaption(item, "Xóa");
        }
    }

    private static void SetDeleteButtonCaption(GameObject item, string caption)
    {
        if (item == null)
            return;

        TMP_Text label = item.transform.Find("DeleteButton")?.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = caption;
    }

    private Button CreateActionButton(Transform parent, TMP_Text sourceText,
        string objectName, string caption, float positionX, Color color)
    {
        GameObject editObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        editObject.layer = parent.gameObject.layer;
        editObject.transform.SetParent(parent, false);

        RectTransform rect = editObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(positionX, 0f);
        rect.sizeDelta = new Vector2(64f, 36f);

        Image image = editObject.GetComponent<Image>();
        image.color = color;
        Button button = editObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = editObject.layer;
        labelObject.transform.SetParent(editObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (sourceText != null)
            label.font = sourceText.font;
        label.text = caption;
        label.fontSize = 18f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return button;
    }
}
