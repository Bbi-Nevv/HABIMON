using Firebase.Firestore;
using HABIMON.Core;
using System;
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
    [SerializeField] private Button allButton;
    [SerializeField] private Button morningButton;
    [SerializeField] private Button afternoonButton;
    [SerializeField] private Button eveningButton;
    [SerializeField] private List<Sprite> iconSprites;

    private readonly List<HabitModel> allHabits = new List<HabitModel>();
    private readonly Dictionary<string, MissionItem> visibleItems = new Dictionary<string, MissionItem>();
    private readonly Dictionary<string, HabitLogModel> todayLogs = new Dictionary<string, HabitLogModel>();
    private readonly Dictionary<string, bool> pendingChanges = new Dictionary<string, bool>();
    private readonly Dictionary<string, string> itemErrors = new Dictionary<string, string>();
    private string currentPeriod = "Tất cả";
    private string loadedParentId = string.Empty;
    private string loadedChildId = string.Empty;
    private string loadedDateKey = string.Empty;
    private ListenerRegistration habitLogsListener;
    private int habitLoadVersion;
    private int loadedGeneration;
    private FirebaseManager subscribedManager;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Sprite bookIcon;
    private int logSnapshotRevision;
    private bool hasStarted;
    private bool hasLogSnapshot;
    private bool logListenerFailed;
    private float nextDateCheckTime;

    private sealed class MissionItem
    {
        public Toggle toggle;
        public TMP_Text statusText;
        public TMP_Text descriptionText;
        public string description;
    }

    public bool HasStarted => hasStarted;

    private void Start()
    {
        hasStarted = true;
        BindButtons();
        _ = LoadHabitsAsync();
    }

    private void OnEnable()
    {
        SubscribeToSession();
        if (hasStarted)
            _ = LoadHabitsAsync();
    }

    private void OnDisable()
    {
        ++habitLoadVersion;
        UnsubscribeFromSession();
        StopHabitLogListener();
        foreach (MissionItem item in visibleItems.Values)
        {
            if (item.toggle != null)
                item.toggle.interactable = false;
        }
    }

    private void OnDestroy()
    {
        ++habitLoadVersion;
        UnsubscribeFromSession();
        StopHabitLogListener();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextDateCheckTime)
            return;

        nextDateCheckTime = Time.unscaledTime + 1f;
        if (!string.IsNullOrEmpty(loadedDateKey) && loadedDateKey != HabitLog.GetTodayDateKey())
            _ = LoadHabitsAsync();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && hasStarted && isActiveAndEnabled)
            _ = LoadHabitsAsync();
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

    private async Task LoadHabitsAsync(string requestedChildId = null)
    {
        int requestVersion = ++habitLoadVersion;
        StopHabitLogListener();
        allHabits.Clear();
        todayLogs.Clear();
        itemErrors.Clear();
        hasLogSnapshot = false;
        logListenerFailed = false;
        logSnapshotRevision = 0;
        loadedParentId = string.Empty;
        loadedChildId = string.Empty;
        loadedDateKey = HabitLog.GetTodayDateKey();
        ClearItems();

        try
        {
            FirebaseManager manager = FirebaseManager.Instance;
            if (manager == null || !await manager.EnsureInitializedAsync() || !IsCurrentRequest(requestVersion))
                return;
            SubscribeToSession();
            string parentId = manager.GetCurrentParentId();
            string childId = string.IsNullOrEmpty(requestedChildId) ? manager.GetCurrentChildId() : requestedChildId;
            if (string.IsNullOrEmpty(childId))
                childId = await manager.ResolveCurrentChildIdAsync();

            if (!IsCurrentRequest(requestVersion) || manager != FirebaseManager.Instance ||
                parentId != manager.GetCurrentParentId() || childId != manager.GetCurrentChildId())
                return;

            if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
            {
                Debug.LogWarning("[DisplayMission] Thiếu ParentId hoặc ChildId để tải nhiệm vụ.");
                return;
            }

            loadedParentId = parentId;
            loadedChildId = childId;
            loadedGeneration = manager.SessionGeneration;
            var habits = await manager.GetHabitsAsync(parentId, childId);
            if (!IsCurrentScope(requestVersion))
                return;

            allHabits.AddRange(habits.Where(habit => habit != null));
            StartHabitLogListener(manager, requestVersion);
            ShowByPeriod(currentPeriod);
        }
        catch (Exception ex)
        {
            if (IsCurrentRequest(requestVersion))
                Debug.LogError($"[DisplayMission] Không thể tải nhiệm vụ: {ex.Message}");
        }
    }

    public Task LoadHabitsForChildAsync(string childId)
    {
        return LoadHabitsAsync(childId);
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

            if (!HabitLog.IsScheduledForDate(habit, HabitLog.GetBusinessDate(DateTimeOffset.UtcNow)))
                continue;

            if (!IsAllPeriod(period) && !HabitSchedule.FromStorage(habit.scheduleKind, habit.scheduleValue, habit.timeTarget).MatchesPeriod(period))
                continue;

            SpawnItem(habit);
        }
    }

    private bool IsAllPeriod(string period)
    {
        return string.Equals(period?.Trim(), "Tất cả", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(period?.Trim(), "All", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ClearItems()
    {
        foreach (MissionItem item in visibleItems.Values)
        {
            if (item.toggle != null)
            {
                item.toggle.interactable = false;
                item.toggle.onValueChanged.RemoveAllListeners();
            }
        }
        visibleItems.Clear();

        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject item = contentRoot.GetChild(i).gameObject;
            item.SetActive(false);
            Destroy(item);
        }
    }

    private void SpawnItem(HabitModel habit)
    {
        GameObject item = Instantiate(itemPrefab, contentRoot);

        var titleText = item.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (titleText == null)
            titleText = item.GetComponentInChildren<TMP_Text>(true);

        var descriptionText = item.transform.Find("description")?.GetComponent<TMP_Text>();
        if (descriptionText == null)
            descriptionText = item.transform.Find("Desription")?.GetComponent<TMP_Text>();
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

        if (toggle == null)
            return;

        toggle.interactable = false;
        toggle.group = null;
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.SetIsOnWithoutNotify(false);

        MissionItem view = new MissionItem
        {
            toggle = toggle,
            statusText = statusText,
            descriptionText = descriptionText,
            description = habit.description
        };

        if (string.IsNullOrEmpty(habit.id))
        {
            SetItemStatus(view, "Không thể cập nhật");
            return;
        }

        visibleItems[habit.id] = view;
        int requestVersion = habitLoadVersion;
        toggle.onValueChanged.AddListener(isOn => _ = SaveCompletionAsync(habit.id, isOn, requestVersion));
        RenderItem(habit.id);
    }

    private bool IsCurrentRequest(int requestVersion)
    {
        return this != null && isActiveAndEnabled && requestVersion == habitLoadVersion;
    }

    private bool IsCurrentScope(int requestVersion)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        return IsCurrentRequest(requestVersion) && manager != null && manager == subscribedManager &&
            loadedGeneration == manager.SessionGeneration && loadedParentId == manager.GetCurrentParentId() &&
            loadedChildId == manager.GetCurrentChildId();
    }

    private void SubscribeToSession()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || manager == subscribedManager) return;
        UnsubscribeFromSession();
        subscribedManager = manager;
        manager.SessionChanged += ReloadForSession;
        manager.CurrentChildChanged += ReloadForSession;
    }

    private void UnsubscribeFromSession()
    {
        if (subscribedManager == null) return;
        subscribedManager.SessionChanged -= ReloadForSession;
        subscribedManager.CurrentChildChanged -= ReloadForSession;
        subscribedManager = null;
    }

    private void ReloadForSession()
    {
        if (hasStarted && isActiveAndEnabled) _ = LoadHabitsAsync();
    }

    private void StartHabitLogListener(FirebaseManager manager, int requestVersion)
    {
        try
        {
            habitLogsListener = manager.ListenHabitLogsForDate(loadedParentId, loadedChildId, loadedDateKey, logs =>
            {
                if (!IsCurrentScope(requestVersion))
                    return;

                ++logSnapshotRevision;
                // Approval cannot be revoked within this child/day, including by a stale refresh.
                var approvedLogs = todayLogs.Values.Where(log => log.isApproved).ToList();
                todayLogs.Clear();
                foreach (HabitLogModel log in logs)
                {
                    if (log != null && !string.IsNullOrEmpty(log.habitId) && log.dateKey == loadedDateKey)
                        todayLogs[log.habitId] = log;
                }
                foreach (HabitLogModel log in approvedLogs)
                    todayLogs[log.habitId] = log;

                hasLogSnapshot = true;
                logListenerFailed = false;
                itemErrors.Clear();
                RenderItems();
            });

            if (habitLogsListener == null)
                throw new InvalidOperationException("Không thể bắt đầu theo dõi trạng thái nhiệm vụ.");

            _ = ObserveHabitLogListenerAsync(habitLogsListener, requestVersion);
        }
        catch (Exception ex)
        {
            logListenerFailed = true;
            Debug.LogError($"[DisplayMission] Không thể theo dõi nhiệm vụ: {ex.Message}");
            RenderItems();
        }
    }

    private async Task ObserveHabitLogListenerAsync(ListenerRegistration listener, int requestVersion)
    {
        try
        {
            await listener.ListenerTask;
        }
        catch (Exception ex)
        {
            if (IsCurrentScope(requestVersion))
                Debug.LogError($"[DisplayMission] Theo dõi nhiệm vụ bị gián đoạn: {ex.Message}");
        }

        if (IsCurrentScope(requestVersion))
        {
            logListenerFailed = true;
            RenderItems();
        }
    }

    private void StopHabitLogListener()
    {
        if (habitLogsListener == null)
            return;

        habitLogsListener.Stop();
        habitLogsListener = null;
    }

    private async Task SaveCompletionAsync(string habitId, bool isOn, int requestVersion)
    {
        if (!IsCurrentScope(requestVersion))
            return;

        string changeKey = GetPendingChangeKey(habitId);
        todayLogs.TryGetValue(habitId, out HabitLogModel existingLog);
        if (!hasLogSnapshot || logListenerFailed || !HabitLog.CanEditToday(loadedDateKey)
            || pendingChanges.ContainsKey(changeKey) || (existingLog != null && existingLog.isApproved))
        {
            RenderItem(habitId);
            return;
        }

        string parentId = loadedParentId;
        string childId = loadedChildId;
        string dateKey = loadedDateKey;
        pendingChanges[changeKey] = isOn;
        itemErrors.Remove(habitId);
        RenderItem(habitId);

        try
        {
            FirebaseManager manager = FirebaseManager.Instance;
            bool success = await HabitRewardService.SetTodayCompletionAsync(parentId, childId, habitId, dateKey, isOn);
            if (!IsCurrentScope(requestVersion))
                return;

            int refreshRevision = logSnapshotRevision;
            HabitLogModel freshLog = await manager.GetHabitLogAsync(parentId, childId, habitId, dateKey);
            if (!IsCurrentScope(requestVersion))
                return;

            todayLogs.TryGetValue(habitId, out HabitLogModel cachedLog);
            if (refreshRevision == logSnapshotRevision && (cachedLog == null || !cachedLog.isApproved))
            {
                if (freshLog == null)
                    todayLogs.Remove(habitId);
                else
                    todayLogs[habitId] = freshLog;
            }

            if (!success)
                itemErrors[habitId] = "Không thể cập nhật. Vui lòng thử lại.";
        }
        catch (Exception ex)
        {
            if (IsCurrentScope(requestVersion))
            {
                hasLogSnapshot = false;
                itemErrors[habitId] = "Không thể đồng bộ trạng thái. Vui lòng mở lại danh sách.";
                Debug.LogError($"[DisplayMission] Không thể cập nhật nhiệm vụ: {ex.Message}");
            }
        }
        finally
        {
            FinishPendingChange(changeKey);
        }
    }

    private void FinishPendingChange(string changeKey)
    {
        pendingChanges.Remove(changeKey);
        if (IsCurrentScope(habitLoadVersion))
            RenderItems();
    }

    private string GetPendingChangeKey(string habitId)
    {
        return $"{loadedParentId}/{loadedChildId}/{loadedDateKey}/{habitId}";
    }

    private void RenderItems()
    {
        foreach (string habitId in visibleItems.Keys)
            RenderItem(habitId);
    }

    private void RenderItem(string habitId)
    {
        if (!visibleItems.TryGetValue(habitId, out MissionItem item) || item.toggle == null)
            return;

        todayLogs.TryGetValue(habitId, out HabitLogModel log);
        bool isApproved = log != null && log.isApproved;
        bool isSaving = pendingChanges.TryGetValue(GetPendingChangeKey(habitId), out bool requestedState);
        bool isCompleted = isApproved || (isSaving ? requestedState : log != null && log.isCompleted);
        bool canEditToday = HabitLog.CanEditToday(loadedDateKey);
        item.toggle.SetIsOnWithoutNotify(isCompleted);
        item.toggle.interactable = isActiveAndEnabled && hasLogSnapshot && !logListenerFailed
            && canEditToday && !isApproved && !isSaving;

        string status;
        if (isApproved)
            status = log.requiresApprovalAtCompletion == false ? "Đã hoàn thành hôm nay" : "Đã được phụ huynh xác nhận";
        else if (!canEditToday)
            status = "Đã khóa lịch sử ngày cũ";
        else if (isSaving)
            status = "Đang lưu...";
        else if (logListenerFailed)
            status = "Không thể tải trạng thái. Vui lòng mở lại danh sách.";
        else if (itemErrors.TryGetValue(habitId, out string error))
            status = error;
        else if (!hasLogSnapshot)
            status = "Đang tải trạng thái...";
        else
            status = isCompleted ? "Chờ phụ huynh xác nhận" : "Chưa hoàn thành hôm nay";

        SetItemStatus(item, status);
    }

    private static void SetItemStatus(MissionItem item, string status)
    {
        if (item.statusText != null)
            item.statusText.text = status;
        else if (item.descriptionText != null)
            item.descriptionText.text = string.IsNullOrEmpty(item.description) ? status : $"{item.description}\n{status}";
    }

    private Sprite GetSpriteByKey(string iconKey)
    {
        if (string.Equals(iconKey, "icon_book", StringComparison.OrdinalIgnoreCase))
            return bookIcon != null ? bookIcon : fallbackIcon;
        if (string.Equals(iconKey, "icon_default", StringComparison.OrdinalIgnoreCase))
            return fallbackIcon;
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

