using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HABIMON.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayMission : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Button allButton;
    [SerializeField] private Button morningButton;
    [SerializeField] private Button afternoonButton;
    [SerializeField] private Button eveningButton;
    [SerializeField] private List<Sprite> iconSprites;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Sprite bookIcon;

    private readonly List<HabitModel> allHabits = new List<HabitModel>();
    private readonly Dictionary<string, HabitLogModel> todayLogsByHabit = new Dictionary<string, HabitLogModel>();
    private string currentPeriod = "Tất cả";
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
        if (allButton != null)
            allButton.onClick.RemoveAllListeners();
        if (morningButton != null)
            morningButton.onClick.RemoveAllListeners();
        if (afternoonButton != null)
            afternoonButton.onClick.RemoveAllListeners();
        if (eveningButton != null)
            eveningButton.onClick.RemoveAllListeners();

        if (allButton != null)
            allButton.onClick.AddListener(() => ShowByPeriod("Tất cả"));
        if (morningButton != null)
            morningButton.onClick.AddListener(() => ShowByPeriod("Buổi Sáng"));
        if (afternoonButton != null)
            afternoonButton.onClick.AddListener(() => ShowByPeriod("Buổi Chiều"));
        if (eveningButton != null)
            eveningButton.onClick.AddListener(() => ShowByPeriod("Buổi Tối"));
    }

    private async Task LoadHabitsAsync()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DisplayMission] Firebase chưa sẵn sàng.");
            return;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            Debug.LogError($"[DisplayMission] Firebase initialization failed: {manager.InitializationError}");
            return;
        }

        string parentId = manager.GetCurrentParentId();
        string childId = manager.GetCurrentChildId();
        if (string.IsNullOrEmpty(childId))
        {
            try
            {
                childId = await manager.ResolveCurrentChildIdAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DisplayMission] Không thể xác định trẻ đang chọn: {ex.Message}");
                return;
            }
        }

        if (string.IsNullOrEmpty(parentId) || parentId != manager.GetCurrentParentId() || string.IsNullOrEmpty(childId))
        {
            Debug.LogWarning("[DisplayMission] Thiếu ParentId hoặc ChildId để tải nhiệm vụ.");
            return;
        }

        await LoadHabitsForChildAsync(childId);
    }

    public async Task LoadHabitsForChildAsync(string childId)
    {
        int requestVersion = ++habitLoadVersion;

        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DisplayMission] Firebase chưa sẵn sàng.");
            return;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            Debug.LogError($"[DisplayMission] Firebase initialization failed: {manager.InitializationError}");
            return;
        }

        string parentId = manager.GetCurrentParentId();
        int sessionGeneration = manager.SessionGeneration;
        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
        {
            Debug.LogWarning("[DisplayMission] Thiếu ParentId hoặc ChildId để tải nhiệm vụ.");
            return;
        }

        if (loadedParentId != parentId || loadedChildId != childId)
        {
            loadedParentId = parentId;
            loadedChildId = childId;
            allHabits.Clear();
            todayLogsByHabit.Clear();
            ClearItems();
        }

        try
        {
            Task<List<HabitModel>> habitsTask = manager.GetHabitsAsync(parentId, childId);
            Task<List<HabitLogModel>> logsTask = manager.GetHabitLogsForDateAsync(
                parentId, childId, HabitLog.GetTodayDateKey());
            await Task.WhenAll(habitsTask, logsTask);
            if (this == null || requestVersion != habitLoadVersion ||
                FirebaseManager.Instance != manager ||
                manager.SessionGeneration != sessionGeneration ||
                manager.GetCurrentParentId() != parentId || manager.GetCurrentChildId() != childId)
                return;

            allHabits.Clear();
            allHabits.AddRange(habitsTask.Result.Where(h => h != null));
            todayLogsByHabit.Clear();
            string todayKey = HabitLog.GetTodayDateKey();
            foreach (HabitLogModel log in logsTask.Result)
            {
                if (log != null && log.dateKey == todayKey && !string.IsNullOrEmpty(log.habitId))
                    todayLogsByHabit[log.habitId] = log;
            }
            loadedParentId = parentId;
            loadedChildId = childId;

            ShowByPeriod(currentPeriod);
        }
        catch (System.Exception ex)
        {
            if (this != null && requestVersion == habitLoadVersion)
                Debug.LogError($"[DisplayMission] Không thể tải nhiệm vụ: {ex.Message}");
        }
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

        DateTime businessDate = HabitLog.GetBusinessDate(DateTimeOffset.UtcNow);
        foreach (var habit in allHabits)
        {
            if (habit == null)
                continue;

            if (!HabitLog.IsScheduledForDate(habit, businessDate))
                continue;

            if (!IsAllPeriod(period) &&
                !HabitSchedule.FromStorage(habit.scheduleKind, habit.scheduleValue, habit.timeTarget).MatchesPeriod(period))
                continue;

            SpawnItem(habit, loadedParentId, loadedChildId);
        }
    }

    private bool IsAllPeriod(string period)
    {
        return string.Equals(period?.Trim(), "Tất cả", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(period?.Trim(), "All", System.StringComparison.OrdinalIgnoreCase);
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
            else if (fallbackIcon != null)
                iconImage.sprite = fallbackIcon;
            else if (iconSprites != null && iconSprites.Count > 0)
                iconImage.sprite = iconSprites[0];
        }

        if (toggle != null)
        {
            if (!string.IsNullOrEmpty(parentId) && !string.IsNullOrEmpty(childId) && !string.IsNullOrEmpty(habit.id))
            {
                todayLogsByHabit.TryGetValue(habit.id, out HabitLogModel todayLog);
                ConfigureToggle(toggle, statusText, parentId, childId, habit, todayLog);
            }
            else
            {
                toggle.interactable = false;
                if (statusText != null)
                    statusText.text = "Không thể cập nhật";
            }
        }
    }

    private void ConfigureToggle(
        Toggle toggle,
        TMP_Text statusText,
        string parentId,
        string childId,
        HabitModel habit,
        HabitLogModel todayLog)
    {
        if (toggle == null || habit == null)
            return;

        bool isCompleted = todayLog != null && todayLog.isCompleted;
        bool canEditToday = todayLog == null || HabitLog.CanEditToday(todayLog.dateKey);
        bool requiresApproval = todayLog?.requiresApprovalAtCompletion ?? habit.requireApproval;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(isCompleted);
        toggle.interactable = canEditToday;

        if (statusText != null)
        {
            if (!canEditToday)
                statusText.text = "Đã khóa lịch sử ngày cũ";
            else if (todayLog == null)
                statusText.text = "Chưa hoàn thành";
            else if (isCompleted && requiresApproval && !todayLog.isApproved)
                statusText.text = "Đã gửi chờ phụ huynh duyệt";
            else if (isCompleted)
                statusText.text = "Đã hoàn thành hôm nay";
            else
                statusText.text = "Chưa hoàn thành hôm nay";
        }

        toggle.onValueChanged.AddListener(async isOn =>
        {
            FirebaseManager manager = FirebaseManager.Instance;
            if (manager == null || manager.GetCurrentParentId() != parentId || manager.GetCurrentChildId() != childId)
                return;

            int sessionGeneration = manager.SessionGeneration;
            bool previousValue = !isOn;
            toggle.interactable = false;
            bool success = false;
            try
            {
                success = await HabitLog.ToggleHabitForTodayAsync(parentId, childId, habit.id, isOn);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DisplayMission] Không thể lưu trạng thái thói quen: {ex.Message}");
            }

            if (this == null || toggle == null || manager != FirebaseManager.Instance ||
                manager.SessionGeneration != sessionGeneration || manager.GetCurrentChildId() != childId)
                return;

            toggle.interactable = canEditToday;
            if (!success)
            {
                toggle.SetIsOnWithoutNotify(previousValue);
                if (statusText != null)
                    statusText.text = "Không thể lưu. Vui lòng thử lại.";
                return;
            }

            if (isOn)
            {
                todayLog = new HabitLogModel
                {
                    habitId = habit.id,
                    dateKey = HabitLog.GetTodayDateKey(),
                    isCompleted = true,
                    completedAt = Firebase.Firestore.Timestamp.GetCurrentTimestamp(),
                    isApproved = !habit.requireApproval,
                    requiresApprovalAtCompletion = habit.requireApproval,
                    rewardGoldAtCompletion = habit.rewardGold,
                    rewardExpAtCompletion = habit.rewardExp,
                    rewardGranted = !habit.requireApproval,
                    rewardGoldGranted = habit.requireApproval ? 0 : habit.rewardGold,
                    rewardExpGranted = habit.requireApproval ? 0 : habit.rewardExp
                };
                todayLogsByHabit[habit.id] = todayLog;
            }
            else
            {
                todayLogsByHabit.Remove(habit.id);
            }

            if (statusText != null)
                statusText.text = isOn
                    ? (habit.requireApproval ? "Đã gửi chờ phụ huynh duyệt" : "Đã hoàn thành hôm nay")
                    : "Chưa hoàn thành hôm nay";
        });
    }

    private Sprite GetSpriteByKey(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey))
            return null;

        if (string.Equals(iconKey, "icon_book", System.StringComparison.OrdinalIgnoreCase))
            return bookIcon != null ? bookIcon : fallbackIcon;
        if (string.Equals(iconKey, "icon_default", System.StringComparison.OrdinalIgnoreCase))
            return fallbackIcon;
        if (iconSprites == null)
            return null;

        foreach (var sprite in iconSprites)
        {
            if (sprite != null && sprite.name.Equals(iconKey, System.StringComparison.OrdinalIgnoreCase))
                return sprite;
        }

        return null;
    }
}

