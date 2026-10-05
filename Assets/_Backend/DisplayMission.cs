using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayMission : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Button morningButton;
    [SerializeField] private Button afternoonButton;
    [SerializeField] private Button eveningButton;
    [SerializeField] private List<Sprite> iconSprites;

    private readonly List<HabitModel> allHabits = new List<HabitModel>();
    private string currentPeriod = "Buổi Sáng";
    private string loadedParentId = string.Empty;
    private string loadedChildId = string.Empty;
    private int habitLoadVersion;

    private void Start()
    {
        BindButtons();
        _ = LoadHabitsAsync();
        ShowByPeriod(currentPeriod);
    }

    private void BindButtons()
    {
        if (morningButton != null)
            morningButton.onClick.RemoveAllListeners();
        if (afternoonButton != null)
            afternoonButton.onClick.RemoveAllListeners();
        if (eveningButton != null)
            eveningButton.onClick.RemoveAllListeners();

        if (morningButton != null)
            morningButton.onClick.AddListener(() => ShowByPeriod("Buổi Sáng"));
        if (afternoonButton != null)
            afternoonButton.onClick.AddListener(() => ShowByPeriod("Buổi Chiều"));
        if (eveningButton != null)
            eveningButton.onClick.AddListener(() => ShowByPeriod("Buổi Tối"));
    }

    private async Task LoadHabitsAsync()
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[DisplayMission] Firebase chưa sẵn sàng.");
            return;
        }

        string parentId = FirebaseManager.Instance.GetCurrentParentId();
        string childId = await FirebaseManager.Instance.ResolveCurrentChildIdAsync();

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
        {
            Debug.LogWarning("[DisplayMission] Thiếu ParentId hoặc ChildId để tải nhiệm vụ.");
            return;
        }

        await LoadHabitsForChildAsync(childId);
    }

    public async Task LoadHabitsForChildAsync(string childId)
    {
        int requestVersion = ++habitLoadVersion;

        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[DisplayMission] Firebase chưa sẵn sàng.");
            return;
        }

        string parentId = FirebaseManager.Instance.GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
        {
            Debug.LogWarning("[DisplayMission] Thiếu ParentId hoặc ChildId để tải nhiệm vụ.");
            return;
        }

        var habits = await FirebaseManager.Instance.GetHabitsAsync(parentId, childId);
        if (requestVersion != habitLoadVersion)
            return;

        allHabits.Clear();
        allHabits.AddRange(habits.Where(h => h != null));
        loadedParentId = parentId;
        loadedChildId = childId;

        ShowByPeriod(currentPeriod);
    }

    public void ShowByPeriod(string period)
    {
        currentPeriod = period;
        ClearItems();

        if (itemPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("[DisplayMission] Chưa gán itemPrefab hoặc contentRoot.");
            return;
        }

        foreach (var habit in allHabits)
        {
            if (habit == null)
                continue;

            if (!habit.isActive)
                continue;

            if (!MatchesPeriod(habit.timeTarget, period))
                continue;

            SpawnItem(habit, loadedParentId, loadedChildId);
        }
    }

    private bool MatchesPeriod(string timeTarget, string targetPeriod)
    {
        if (string.IsNullOrWhiteSpace(timeTarget))
            return false;

        string target = targetPeriod?.Trim();
        string input = timeTarget.Trim();

        if (string.IsNullOrEmpty(target))
            return false;

        var normalizedTarget = target.ToLowerInvariant();
        var normalizedInput = input.ToLowerInvariant();

        if (normalizedTarget.Contains("sáng") || normalizedTarget.Contains("morning"))
            return normalizedInput.Contains("sáng") || normalizedInput.Contains("morning");

        if (normalizedTarget.Contains("chiều") || normalizedTarget.Contains("afternoon"))
            return normalizedInput.Contains("chiều") || normalizedInput.Contains("afternoon");

        if (normalizedTarget.Contains("tối") || normalizedTarget.Contains("evening") || normalizedTarget.Contains("night"))
            return normalizedInput.Contains("tối") || normalizedInput.Contains("evening") || normalizedInput.Contains("night");

        return normalizedInput == normalizedTarget;
    }

    private void ClearItems()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private void SpawnItem(HabitModel habit, string parentId, string childId)
    {
        GameObject item = Instantiate(itemPrefab, contentRoot);

        var titleText = item.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (titleText == null)
            titleText = item.GetComponentInChildren<TMP_Text>(true);

        var descriptionText = item.transform.Find("description")?.GetComponent<TMP_Text>();
        if (descriptionText == null)
        {
            TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 1)
                descriptionText = texts[1];
        }

        var iconImage = item.transform.Find("icon")?.GetComponent<Image>();
        if (iconImage == null)
            iconImage = item.GetComponentInChildren<Image>(true);

        var toggle = item.transform.Find("toggle")?.GetComponent<Toggle>();
        if (toggle == null)
            toggle = item.GetComponentInChildren<Toggle>(true);

        var statusText = item.transform.Find("status")?.GetComponent<TMP_Text>();
        if (statusText == null)
            statusText = item.transform.Find("Status")?.GetComponent<TMP_Text>();

        if (titleText != null)
            titleText.text = habit.title;

        if (descriptionText != null)
            descriptionText.text = habit.description;

        if (iconImage != null)
        {
            Sprite foundIcon = GetSpriteByKey(habit.icon);
            if (foundIcon != null)
                iconImage.sprite = foundIcon;
            else if (iconSprites != null && iconSprites.Count > 0)
                iconImage.sprite = iconSprites[0];
        }

        if (toggle != null)
        {
            if (!string.IsNullOrEmpty(parentId) && !string.IsNullOrEmpty(childId) && !string.IsNullOrEmpty(habit.id))
            {
                _ = ApplyToggleStateAsync(toggle, statusText, parentId, childId, habit);
            }
            else
            {
                toggle.interactable = false;
                if (statusText != null)
                    statusText.text = "Không thể cập nhật";
            }
        }
    }

    private async Task ApplyToggleStateAsync(Toggle toggle, TMP_Text statusText, string parentId, string childId, HabitModel habit)
    {
        if (toggle == null || habit == null)
            return;

        HabitLogModel todayLog = await HabitLog.GetTodayHabitLogAsync(parentId, childId, habit.id);
        bool isCompleted = todayLog != null && todayLog.isCompleted;
        bool canEditToday = todayLog == null || HabitLog.CanEditToday(todayLog.dateKey);

        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(isCompleted);
        toggle.interactable = canEditToday;

        if (statusText != null)
        {
            if (!canEditToday)
                statusText.text = "Đã khóa lịch sử ngày cũ";
            else if (todayLog == null)
                statusText.text = "Chưa hoàn thành";
            else if (isCompleted)
                statusText.text = "Đã hoàn thành hôm nay";
            else
                statusText.text = "Chưa hoàn thành hôm nay";
        }

        toggle.onValueChanged.AddListener(async isOn =>
        {
            bool success = await HabitLog.ToggleHabitForTodayAsync(parentId, childId, habit.id, isOn);
            if (!success)
            {
                toggle.SetIsOnWithoutNotify(!isOn);
                if (statusText != null)
                    statusText.text = "Không thể sửa vì quá ngày";
                return;
            }

            if (statusText != null)
                statusText.text = isOn ? "Đã hoàn thành hôm nay" : "Chưa hoàn thành hôm nay";
        });
    }

    private Sprite GetSpriteByKey(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey) || iconSprites == null)
            return null;

        foreach (var sprite in iconSprites)
        {
            if (sprite != null && sprite.name.Equals(iconKey, System.StringComparison.OrdinalIgnoreCase))
                return sprite;
        }

        return null;
    }
}

