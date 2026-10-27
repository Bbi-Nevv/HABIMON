using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DG.Tweening;
using Firebase.Firestore;
using HABIMON.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HabitLogUI : MonoBehaviour
{
    [Header("Weekly summary")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text periodLabelText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button todayButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text completionRateText;
    [SerializeField] private TMP_Text childrenCountText;
    [SerializeField] private TMP_Text selectedDayText;
    [SerializeField] private TMP_Text detailCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image weeklyProgressFill;
    [SerializeField] private RectTransform detailContentRoot;
    [SerializeField] private Sprite roundedSprite;

    [Header("Chart (completed / scheduled tasks per child)")]
    [SerializeField] private RectTransform chartRoot;
    [SerializeField] private GameObject chartBarPrefab;
    [SerializeField, Min(0f)] private float animationDuration = 0.4f;
    [SerializeField] private Ease animationEase = Ease.OutCubic;
    [SerializeField] private Color chartTextColor = Color.white;
    [SerializeField] private Color selectedDayColor = new Color(1f, 1f, 1f, 0.12f);

    // Retain old scene references only to hide the obsolete filter controls.
    [SerializeField, HideInInspector] private Button dayFilterButton;
    [SerializeField, HideInInspector] private Button monthFilterButton;
    [SerializeField, HideInInspector] private Button yearFilterButton;

    private sealed class ChildProgress
    {
        public ChildModel child;
        public readonly List<HabitModel> habits = new List<HabitModel>();
        public readonly List<HabitOccurrence> occurrences = new List<HabitOccurrence>();
        public readonly List<HabitLogModel> logs = new List<HabitLogModel>();
        public readonly List<HabitCompletion> completions = new List<HabitCompletion>();
        public ListenerRegistration habitsListener;
        public ListenerRegistration logsListener;
        public bool hasHabits;
        public bool hasLogs;
        public bool syncFailed;
        public int logRequestVersion;
    }

    private sealed class ProgressPoint
    {
        public ChildProgress owner;
        public int total;
        public int completed;
        public bool hasUnknownCoverage;
        public bool hasConflictingRecords;
        public float Ratio => total > 0 ? (float)completed / total : 0f;
    }

    private sealed class HabitDayDetail
    {
        public HabitModel habit;
        public HabitProgress progress;
        public HabitLogModel log;
    }

    private sealed class BarView
    {
        public RectTransform track;
        public RectTransform fill;
        public TMP_Text value;
    }

    private sealed class DayView
    {
        public RectTransform root;
        public Image background;
        public TMP_Text label;
        public Image dateChip;
        public readonly Dictionary<string, BarView> bars = new Dictionary<string, BarView>();
    }

    private readonly Dictionary<string, ChildProgress> children = new Dictionary<string, ChildProgress>();
    private readonly List<DayView> dayViews = new List<DayView>();
    private readonly Dictionary<string, Color> seriesColors = new Dictionary<string, Color>();
    private string colorParentId;
    private ListenerRegistration childrenListener;
    private RectTransform generatedChart;
    private TMP_Text legend;
    private readonly List<TMP_Text> axisLabels = new List<TMP_Text>();
    private readonly List<Image> gridLines = new List<Image>();
    private ScrollRect chartScroll;
    private ScrollRect detailScroll;
    private DateTime weekStart;
    private DateTime selectedDate;
    private string loadedParentId;
    private int loadedSessionGeneration = -1;
    private FirebaseManager subscribedManager;
    private bool hasChildren;
    private bool syncFailed;
    private bool renderPending;
    private bool resetDetailScroll;
    private int sessionVersion;
    private Vector2 viewportSize;
    private bool animateDetails;

    private void Awake()
    {
        selectedDate = HabitLog.GetBusinessDate(DateTimeOffset.UtcNow);
        weekStart = GetWeekStart(selectedDate);
        HideFilter(dayFilterButton);
        HideFilter(monthFilterButton);
        HideFilter(yearFilterButton);
        if (previousButton != null)
            previousButton.onClick.AddListener(GoToPreviousPeriod);
        if (nextButton != null)
            nextButton.onClick.AddListener(GoToNextPeriod);
        if (todayButton != null)
            todayButton.onClick.AddListener(GoToCurrentWeek);
        if (retryButton != null)
            retryButton.onClick.AddListener(Retry);
        if (chartRoot != null)
        {
            chartScroll = chartRoot.GetComponentInParent<ScrollRect>();
            // A scene template must survive refreshes. Invalid (e.g. TMP) templates use plain Images.
            if (chartBarPrefab != null && chartBarPrefab.transform.IsChildOf(chartRoot)
                && chartBarPrefab != chartRoot.gameObject)
                chartBarPrefab.SetActive(false);
        }
        PrepareDetailScroll();
    }

    private void OnEnable()
    {
        SubscribeToSession();
        _ = RefreshAsync();
    }

    private void OnDisable()
    {
        ++sessionVersion;
        StopListeners();
        UnsubscribeFromSession();
        KillBarTweens();
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(GoToPreviousPeriod);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(GoToNextPeriod);
        if (todayButton != null)
            todayButton.onClick.RemoveListener(GoToCurrentWeek);
        if (retryButton != null)
            retryButton.onClick.RemoveListener(Retry);
    }

    private void SubscribeToSession()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || subscribedManager == manager)
            return;
        UnsubscribeFromSession();
        subscribedManager = manager;
        subscribedManager.SessionChanged += OnSessionChanged;
    }

    private void UnsubscribeFromSession()
    {
        if (subscribedManager == null)
            return;
        subscribedManager.SessionChanged -= OnSessionChanged;
        subscribedManager = null;
    }

    private void OnSessionChanged()
    {
        if (isActiveAndEnabled)
            _ = RefreshAsync();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled && (syncFailed || children.Values.Any(child => child.syncFailed)))
            _ = RefreshAsync();
    }

    private void LateUpdate()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager != subscribedManager)
        {
            SubscribeToSession();
            if (manager != null && isActiveAndEnabled)
            {
                _ = RefreshAsync();
                return;
            }
        }
        if (!string.IsNullOrEmpty(loadedParentId) && manager != null
            && (manager.GetCurrentParentId() != loadedParentId || manager.SessionGeneration != loadedSessionGeneration))
        {
            _ = RefreshAsync();
            return;
        }
        Vector2 size = GetViewportSize();
        if ((size - viewportSize).sqrMagnitude > 0.25f)
            renderPending = true;
        if (!renderPending)
            return;
        renderPending = false;
        viewportSize = size;
        Render();
    }

    private static void HideFilter(Button button)
    {
        if (button != null)
            button.gameObject.SetActive(false);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        return date.Date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7));
    }

    private void GoToPreviousPeriod() => MoveWeek(-1);
    private void GoToNextPeriod() => MoveWeek(1);
    private void Retry() => _ = RefreshAsync();

    private void GoToCurrentWeek()
    {
        DateTime oldWeekStart = weekStart;
        selectedDate = HabitLog.GetBusinessDate(DateTimeOffset.UtcNow);
        weekStart = GetWeekStart(selectedDate);
        if (weekStart != oldWeekStart)
            RefreshLogRanges();
        resetDetailScroll = animateDetails = renderPending = true;
    }

    private void MoveWeek(int direction)
    {
        weekStart = weekStart.AddDays(direction * 7);
        selectedDate = selectedDate.AddDays(direction * 7);
        RefreshLogRanges();
        if (generatedChart != null)
        {
            generatedChart.DOKill();
            generatedChart.anchoredPosition = new Vector2(direction * 22f, 0f);
            generatedChart.DOAnchorPosX(0f, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
        resetDetailScroll = true;
        animateDetails = true;
        renderPending = true;
    }

    private void RefreshLogRanges()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || string.IsNullOrEmpty(loadedParentId))
            return;
        foreach (ChildProgress state in children.Values)
        {
            state.hasLogs = false;
            _ = RefreshRawLogsForCurrentWeekAsync(manager, state, sessionVersion, loadedParentId);
        }
        renderPending = true;
    }

    private void SelectDay(int dayIndex)
    {
        selectedDate = weekStart.AddDays(dayIndex);
        resetDetailScroll = true;
        animateDetails = true;
        renderPending = true;
    }

    /// <summary>Restarts live subscriptions for all children of the signed-in parent.</summary>
    public async Task RefreshAsync()
    {
        if (!isActiveAndEnabled)
            return;
        int version = ++sessionVersion;
        StopListeners();
        children.Clear();
        loadedParentId = null;
        loadedSessionGeneration = -1;
        hasChildren = false;
        syncFailed = false;
        renderPending = true;
        try
        {
            FirebaseManager manager = FirebaseManager.Instance;
            if (manager == null)
                throw new InvalidOperationException("Không tìm thấy FirebaseManager.");
            if (!await manager.EnsureInitializedAsync())
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(manager.InitializationError)
                    ? "Firebase chưa sẵn sàng."
                    : manager.InitializationError);
            if (!IsCurrentSession(version) || manager != FirebaseManager.Instance)
                return;
            loadedSessionGeneration = manager.SessionGeneration;
            loadedParentId = manager.GetCurrentParentId();
            if (string.IsNullOrEmpty(loadedParentId))
                throw new InvalidOperationException("Không xác định được tài khoản phụ huynh.");
            if (colorParentId != loadedParentId)
            {
                seriesColors.Clear();
                colorParentId = loadedParentId;
            }
            childrenListener = manager.ListenChildren(loadedParentId, items =>
            {
                if (!IsCurrentSession(version))
                    return;
                hasChildren = true;
                items = items ?? new List<ChildModel>();
                HashSet<string> ids = new HashSet<string>(items
                    .Where(child => child != null && !string.IsNullOrEmpty(child.childId))
                    .Select(child => child.childId));
                foreach (string id in children.Keys.Where(id => !ids.Contains(id)).ToList())
                {
                    ChildProgress removed = children[id];
                    children.Remove(id);
                    StopChildListeners(removed);
                }
                foreach (ChildModel child in items)
                {
                    if (child == null || string.IsNullOrEmpty(child.childId))
                        continue;
                    if (children.TryGetValue(child.childId, out ChildProgress existing))
                        existing.child = child;
                    else
                    {
                        ChildProgress state = new ChildProgress { child = child };
                        children.Add(child.childId, state);
                        StartChildListeners(manager, state, version);
                    }
                }
                renderPending = true;
            });
            _ = ObserveListenerAsync(childrenListener, version);
        }
        catch (Exception ex)
        {
            if (IsCurrentSession(version))
            {
                syncFailed = true;
                Debug.LogError($"[HabitLogUI] Không thể tải biểu đồ tuần: {ex.Message}");
                renderPending = true;
            }
        }
    }

    private void StartChildListeners(FirebaseManager manager, ChildProgress state, int version)
    {
        try
        {
            state.habitsListener = manager.ListenHabits(loadedParentId, state.child.childId, habits =>
            {
                if (!IsCurrentChild(state, version))
                    return;
                state.habits.Clear();
                state.habits.AddRange((habits ?? new List<HabitModel>())
                    .Where(habit => habit != null && !string.IsNullOrEmpty(habit.id)));
                RebuildOccurrences(state);
                state.hasHabits = true;
                renderPending = true;
            });
            _ = ObserveListenerAsync(state.habitsListener, version, state);
            state.logsListener = manager.ListenHabitLogs(loadedParentId, state.child.childId, logs =>
            {
                if (!IsCurrentChild(state, version))
                    return;
                // The shared listener intentionally emits one preferred log per habit/day.
                // Use its updates as invalidation signals, then fetch this visible week raw so
                // HabitAnalytics can identify conflicting legacy records instead of hiding them.
                _ = RefreshRawLogsForCurrentWeekAsync(manager, state, version, loadedParentId);
            });
            _ = ObserveListenerAsync(state.logsListener, version, state);
        }
        catch (Exception ex)
        {
            state.syncFailed = true;
            Debug.LogError($"[HabitLogUI] Không thể theo dõi nhiệm vụ: {ex.Message}");
            renderPending = true;
        }
    }

    private async Task ObserveListenerAsync(ListenerRegistration listener, int version, ChildProgress state = null)
    {
        try
        {
            if (listener == null)
                throw new InvalidOperationException("Không tạo được listener Firebase.");
            await listener.ListenerTask;
        }
        catch (Exception ex)
        {
            if (IsCurrentSession(version) && (state == null || IsCurrentChild(state, version)))
                Debug.LogWarning($"[HabitLogUI] Đồng bộ biểu đồ bị gián đoạn: {ex.Message}");
        }
        if (!IsCurrentSession(version) || (state != null && !IsCurrentChild(state, version)))
            return;
        if (state == null)
            syncFailed = true;
        else
            state.syncFailed = true;
        renderPending = true;
    }

    private async Task RefreshRawLogsForCurrentWeekAsync(
        FirebaseManager manager,
        ChildProgress state,
        int version,
        string parentId)
    {
        int requestVersion = ++state.logRequestVersion;
        DateTime requestedWeekStart = weekStart.Date;
        string startDateKey = requestedWeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string endDateKey = requestedWeekStart.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        try
        {
            List<HabitLogModel> rawLogs = await manager.GetHabitLogsInRangeAsync(
                parentId, state.child.childId, startDateKey, endDateKey);
            if (!IsCurrentChild(state, version) || manager != FirebaseManager.Instance ||
                manager.SessionGeneration != loadedSessionGeneration || parentId != loadedParentId ||
                requestedWeekStart != weekStart.Date || requestVersion != state.logRequestVersion)
                return;

            state.logs.Clear();
            state.logs.AddRange(rawLogs.Where(log => log != null && !string.IsNullOrEmpty(log.habitId)));
            RebuildCompletions(state);
            state.hasLogs = true;
            renderPending = true;
        }
        catch (Exception ex)
        {
            if (!IsCurrentChild(state, version) || requestVersion != state.logRequestVersion)
                return;
            state.syncFailed = true;
            Debug.LogWarning($"[HabitLogUI] Không thể tải log tuần của {state.child.name}: {ex.Message}");
            renderPending = true;
        }
    }

    private static void RebuildOccurrences(ChildProgress state)
    {
        state.occurrences.Clear();
        foreach (HabitModel habit in state.habits)
        {
            if (habit == null || string.IsNullOrEmpty(habit.id))
                continue;
            DateTime? archivedAt = habit.archivedAt.HasValue
                ? HabitLog.GetBusinessDate(new DateTimeOffset(habit.archivedAt.Value.ToDateTime().ToUniversalTime()))
                : (DateTime?)null;
            state.occurrences.Add(new HabitOccurrence
            {
                HabitId = habit.id,
                Schedule = HabitSchedule.FromStorage(habit.scheduleKind, habit.scheduleValue, habit.timeTarget),
                RepeatDays = habit.repeatDays?.ToArray(),
                EffectiveFrom = ParseBusinessDate(habit.effectiveFrom),
                ArchivedAt = archivedAt,
                IsActive = habit.isActive,
                RequireApproval = habit.requireApproval
            });
        }
    }

    private static void RebuildCompletions(ChildProgress state)
    {
        state.completions.Clear();
        foreach (HabitLogModel log in state.logs)
        {
            if (log == null || string.IsNullOrEmpty(log.habitId) ||
                !DateTime.TryParseExact(log.dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
                continue;
            state.completions.Add(new HabitCompletion
            {
                HabitId = log.habitId,
                Date = date,
                IsCompleted = log.isCompleted,
                IsApproved = log.isApproved,
                RequiresApprovalAtCompletion = log.requiresApprovalAtCompletion
            });
        }
    }

    private static DateTime? ParseBusinessDate(string value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime date) ? date.Date : (DateTime?)null;
    }

    private bool IsCurrentSession(int version)
    {
        if (this == null || !isActiveAndEnabled || version != sessionVersion)
            return false;
        if (loadedSessionGeneration < 0)
            return true;
        FirebaseManager manager = FirebaseManager.Instance;
        return manager != null && manager.SessionGeneration == loadedSessionGeneration
            && manager.GetCurrentParentId() == loadedParentId;
    }

    private bool IsCurrentChild(ChildProgress state, int version)
    {
        return IsCurrentSession(version) && children.TryGetValue(state.child.childId, out ChildProgress current)
            && ReferenceEquals(state, current);
    }

    private void StopListeners()
    {
        childrenListener?.Stop();
        childrenListener = null;
        foreach (ChildProgress state in children.Values)
            StopChildListeners(state);
    }

    private static void StopChildListeners(ChildProgress state)
    {
        state.habitsListener?.Stop();
        state.logsListener?.Stop();
        state.habitsListener = null;
        state.logsListener = null;
    }

    private void Render()
    {
        List<ChildProgress> ordered = children.Values.OrderBy(state => state.child.name, StringComparer.Ordinal)
            .ThenBy(state => state.child.childId, StringComparer.Ordinal).ToList();
        List<ProgressPoint>[] days = new List<ProgressPoint>[7];
        for (int day = 0; day < 7; day++)
            days[day] = ordered.Select(state => CreatePoint(state, weekStart.AddDays(day))).ToList();
        bool failed = syncFailed || ordered.Any(state => state.syncFailed);
        bool loading = !hasChildren || ordered.Any(state => !state.hasHabits || !state.hasLogs);
        string status = failed ? "Chưa tải được tiến độ. Chạm Thử lại để kết nối."
            : loading ? "Đang tải dữ liệu tuần..."
                : ordered.Count == 0 ? "Chưa có trẻ trong danh sách." : null;
        int completed = days.Sum(day => day.Sum(point => point.completed));
        int total = days.Sum(day => day.Sum(point => point.total));
        bool hasUnknownCoverage = days.Any(day => day.Any(point => point.hasUnknownCoverage));
        bool hasConflictingRecords = days.Any(day => day.Any(point => point.hasConflictingRecords));
        float ratio = total > 0 ? (float)completed / total : 0f;
        string qualitySuffix = (hasUnknownCoverage ? " · lịch sử chưa đầy đủ" : string.Empty)
            + (hasConflictingRecords ? " · có log trùng cần kiểm tra" : string.Empty);
        if (periodLabelText != null)
            periodLabelText.text = $"{weekStart:dd/MM} – {weekStart.AddDays(6):dd/MM/yyyy}";
        if (summaryText != null)
            summaryText.text = completionRateText != null
                ? (loading || failed ? "— / —" : $"{completed} / {total}{qualitySuffix}")
                : status ?? $"{completed}/{total} nhiệm vụ hoàn thành{qualitySuffix}";
        if (completionRateText != null)
            completionRateText.text = loading || failed || hasUnknownCoverage || hasConflictingRecords
                ? "—" : $"{ratio * 100f:0}%";
        if (childrenCountText != null)
            childrenCountText.text = hasChildren ? ordered.Count.ToString() : "—";
        float displayedRatio = loading || failed ? 0f : ratio;
        if (weeklyProgressFill != null && !Mathf.Approximately(weeklyProgressFill.fillAmount, displayedRatio))
        {
            weeklyProgressFill.DOKill();
            weeklyProgressFill.DOFillAmount(displayedRatio, animationDuration).SetUpdate(true);
        }
        if (todayButton != null)
        {
            DateTime businessToday = HabitLog.GetBusinessDate(DateTimeOffset.UtcNow);
            todayButton.interactable = weekStart != GetWeekStart(businessToday) || selectedDate != businessToday;
        }
        if (retryButton != null)
            retryButton.gameObject.SetActive(failed);
        if (statusText != null)
        {
            statusText.text = status ?? string.Empty;
            statusText.gameObject.SetActive(status != null);
        }
        if (selectedDayText != null)
            selectedDayText.text = $"{GetDayLabel(selectedDate)} · {selectedDate:dd/MM/yyyy}";
        if (detailCountText != null)
        {
            List<ProgressPoint> selected = days[(selectedDate - weekStart).Days];
            bool selectedUnknown = selected.Any(point => point.hasUnknownCoverage);
            bool selectedConflict = selected.Any(point => point.hasConflictingRecords);
            string selectedSuffix = (selectedUnknown ? " + ?" : string.Empty)
                + (selectedConflict ? " · cần đối soát" : string.Empty);
            detailCountText.text = loading || failed ? "Đang đồng bộ"
                : $"{selected.Sum(point => point.completed)}/{selected.Sum(point => point.total)} đã xong{selectedSuffix}";
        }
        if (detailText != null)
            detailText.text = status ?? BuildDetailText(ordered);
        RenderChart(days, ordered);
        if (detailContentRoot != null)
            RenderDayDetails(ordered, status);
        if (resetDetailScroll && detailScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            detailScroll.verticalNormalizedPosition = 1f;
        }
        resetDetailScroll = false;
        animateDetails = false;
    }

    private static ProgressPoint CreatePoint(ChildProgress state, DateTime date)
    {
        HabitProgress progress = HabitAnalytics.Calculate(
            date.Date, date.Date.AddDays(1), state.occurrences, state.completions);
        return new ProgressPoint
        {
            owner = state,
            total = progress.Total,
            completed = progress.Completed,
            hasUnknownCoverage = progress.HasUnknownCoverage,
            hasConflictingRecords = progress.HasConflictingRecords
        };
    }

    private static string GetPointLabel(ProgressPoint point)
    {
        string value = point.total > 0 ? $"{point.completed}/{point.total}"
            : point.hasUnknownCoverage ? "?" : "—";
        if (point.hasUnknownCoverage)
        {
            if (point.total > 0)
                value += " + ?";
        }
        if (point.hasConflictingRecords)
            value += " !";
        return value;
    }

    private static List<HabitDayDetail> GetDayDetails(ChildProgress state, DateTime date)
    {
        var result = new List<HabitDayDetail>();
        string dateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (HabitModel habit in state.habits
                     .OrderBy(item => item.timeTarget, StringComparer.Ordinal)
                     .ThenBy(item => item.title, StringComparer.Ordinal))
        {
            if (habit == null || string.IsNullOrEmpty(habit.id))
                continue;
            HabitOccurrence occurrence = state.occurrences.FirstOrDefault(item => item.HabitId == habit.id);
            if (occurrence == null)
                continue;
            HabitProgress progress = HabitAnalytics.Calculate(
                date.Date,
                date.Date.AddDays(1),
                new[] { occurrence },
                state.completions.Where(completion => completion.HabitId == habit.id));
            if (progress.Total == 0 && !progress.HasUnknownCoverage)
                continue;
            result.Add(new HabitDayDetail
            {
                habit = habit,
                progress = progress,
                log = GetDisplayLog(state, habit.id, dateKey)
            });
        }
        return result;
    }

    private static HabitLogModel GetDisplayLog(ChildProgress state, string habitId, string dateKey)
    {
        return state.logs
            .Where(log => log != null && log.habitId == habitId && log.dateKey == dateKey)
            .OrderByDescending(log => log.logId == HabitLog.GetDailyLogDocumentId(habitId, dateKey))
            .ThenByDescending(log => log.rewardGranted)
            .ThenByDescending(log => log.isApproved)
            .ThenByDescending(log => log.completedAt)
            .ThenBy(log => log.logId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool RequiresApproval(HabitModel habit, HabitLogModel log)
    {
        return log != null && log.requiresApprovalAtCompletion.HasValue
            ? log.requiresApprovalAtCompletion.Value
            : habit.requireApproval;
    }

    private static string GetCompletionTimeLabel(HabitLogModel log)
    {
        if (log == null || !log.completedAt.HasValue)
            return "Đã hoàn thành (chưa có thời gian)";
        DateTime utcTime = DateTime.SpecifyKind(log.completedAt.Value.ToDateTime(), DateTimeKind.Utc);
        return $"Hoàn thành lúc {utcTime.AddHours(7).ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
    }

    private string BuildDetailText(List<ChildProgress> ordered)
    {
        StringBuilder text = new StringBuilder($"<b>{GetDayLabel(selectedDate)} · {selectedDate:dd/MM/yyyy}</b>");
        foreach (ChildProgress state in ordered)
        {
            ProgressPoint point = CreatePoint(state, selectedDate);
            string quality = (point.hasUnknownCoverage ? " · lịch sử chưa đầy đủ" : string.Empty)
                + (point.hasConflictingRecords ? " · log trùng cần đối soát" : string.Empty);
            text.Append($"\n\n<color=#{ColorUtility.ToHtmlStringRGB(GetSeriesColor(state.child.childId))}>");
            text.Append($"<b>{EscapeText(GetChildName(state.child))}</b></color> — {point.completed}/{point.total}{quality}");
            List<HabitDayDetail> details = GetDayDetails(state, selectedDate);
            if (details.Count == 0)
                text.Append("\nChưa có nhiệm vụ được lên lịch.");
            foreach (HabitDayDetail detail in details)
            {
                HabitModel habit = detail.habit;
                HabitLogModel log = detail.log;
                text.Append($"\n• {EscapeText(habit.title)}: ");
                if (detail.progress.HasConflictingRecords)
                    text.Append("Log trùng cần đối soát");
                else if (detail.progress.HasUnknownCoverage && detail.progress.Total == 0)
                    text.Append(log?.isCompleted == true
                        ? "Đã ghi nhận, nhưng lịch sử chưa đủ để xác định nhiệm vụ"
                        : "Chưa đủ dữ liệu lịch sử để xác định nhiệm vụ");
                else if (log?.isCompleted == true)
                {
                    text.Append(GetCompletionTimeLabel(log));
                    if (RequiresApproval(habit, log) && !log.isApproved)
                        text.Append(" · Chờ xác nhận");
                }
                else
                    text.Append("Chưa hoàn thành");
            }
        }
        return text.ToString();
    }

    private Vector2 GetViewportSize()
    {
        RectTransform viewport = chartScroll != null ? chartScroll.viewport : chartRoot;
        return viewport != null ? viewport.rect.size : Vector2.zero;
    }

    private void RenderDayDetails(List<ChildProgress> ordered, string status)
    {
        for (int i = detailContentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject row = detailContentRoot.GetChild(i).gameObject;
            RemoveView(row);
        }
        float width = ((RectTransform)detailContentRoot.parent).rect.width;
        float y = 0f;
        if (status != null)
        {
            TMP_Text message = NewText("DetailStatus", detailContentRoot, TextAlignmentOptions.TopLeft);
            message.fontSize = 26f;
            message.color = chartTextColor;
            PlaceTop(message.rectTransform, 4f, 12f, width - 8f, 90f);
            message.text = status;
            y = 110f;
        }
        else
        {
            foreach (ChildProgress state in ordered)
            {
                Color color = GetSeriesColor(state.child.childId);
                ProgressPoint point = CreatePoint(state, selectedDate);
                string quality = (point.hasUnknownCoverage ? "  ·  lịch sử chưa đầy đủ" : string.Empty)
                    + (point.hasConflictingRecords ? "  ·  log trùng cần đối soát" : string.Empty);
                TMP_Text name = NewText("ChildName", detailContentRoot, TextAlignmentOptions.MidlineLeft);
                name.fontSize = 27f;
                name.fontStyle = FontStyles.Bold;
                name.overflowMode = TextOverflowModes.Ellipsis;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.color = color;
                name.text = $"{EscapeText(GetChildName(state.child))}  <size=22><color=#789089>{point.completed}/{point.total} nhiệm vụ{quality}</color></size>";
                PlaceTop(name.rectTransform, 8f, y, width - 16f, 44f);
                y += 50f;
                List<HabitDayDetail> details = GetDayDetails(state, selectedDate);
                if (details.Count == 0)
                {
                    TMP_Text empty = NewText("NoTasks", detailContentRoot, TextAlignmentOptions.MidlineLeft);
                    empty.text = "Ngày này chưa có nhiệm vụ. Cùng con nghỉ ngơi nhé!";
                    empty.fontSize = 24f;
                    PlaceTop(empty.rectTransform, 8f, y, width - 16f, 64f);
                    y += 72f;
                }
                foreach (HabitDayDetail detail in details)
                {
                    HabitModel habit = detail.habit;
                    HabitLogModel log = detail.log;
                    bool recordedCompleted = log?.isCompleted == true;
                    bool completed = detail.progress.Completed > 0;
                    bool pending = recordedCompleted && RequiresApproval(habit, log) && !log.isApproved;
                    bool conflict = detail.progress.HasConflictingRecords;
                    bool unknown = detail.progress.HasUnknownCoverage && detail.progress.Total == 0;
                    Color stateColor = conflict || pending ? new Color(0.63f, 0.42f, 0.12f)
                        : completed ? new Color(0.14f, 0.5f, 0.38f) : new Color(0.49f, 0.55f, 0.53f);
                    RectTransform row = NewRect("Task_" + habit.id, detailContentRoot);
                    PlaceTop(row, 0f, y, width, 88f);
                    Image background = row.gameObject.AddComponent<Image>();
                    background.sprite = roundedSprite;
                    background.type = Image.Type.Sliced;
                    background.pixelsPerUnitMultiplier = 0.5f;
                    background.color = completed ? new Color(0.91f, 0.97f, 0.92f)
                        : pending || conflict ? new Color(1f, 0.96f, 0.86f)
                        : new Color(0.96f, 0.96f, 0.92f);
                    background.raycastTarget = false;
                    TMP_Text title = NewText("TaskTitle", row, TextAlignmentOptions.MidlineLeft);
                    title.text = EscapeText(habit.title);
                    title.fontSize = 26f;
                    title.fontStyle = FontStyles.Bold;
                    title.overflowMode = TextOverflowModes.Ellipsis;
                    title.textWrappingMode = TextWrappingModes.NoWrap;
                    PlaceTop(title.rectTransform, 20f, 8f, width - 250f, 34f);
                    TMP_Text stateLabel = NewText("TaskState", row, TextAlignmentOptions.MidlineLeft);
                    stateLabel.text = conflict ? "Log trùng cần đối soát"
                        : unknown ? "Chưa đủ dữ liệu lịch sử"
                        : pending ? "Chờ phụ huynh xác nhận"
                        : completed ? "Đã hoàn thành" : "Chưa hoàn thành";
                    stateLabel.fontSize = 21f;
                    stateLabel.color = stateColor;
                    PlaceTop(stateLabel.rectTransform, 20f, 45f, width - 250f, 28f);
                    TMP_Text time = NewText("CompletionTime", row, TextAlignmentOptions.MidlineRight);
                    time.fontSize = 27f;
                    time.fontStyle = FontStyles.Bold;
                    time.color = stateColor;
                    time.text = conflict ? "?"
                        : recordedCompleted && log.completedAt.HasValue
                            ? DateTime.SpecifyKind(log.completedAt.Value.ToDateTime(), DateTimeKind.Utc)
                                .AddHours(7).ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                            : completed ? "Đã xong" : recordedCompleted ? "Đã ghi nhận" : "—";
                    PlaceTop(time.rectTransform, width - 214f, 22f, 190f, 42f);
                    y += 98f;
                }
                y += 18f;
            }
        }
        detailContentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
        if (animateDetails)
        {
            CanvasGroup group = detailContentRoot.GetComponent<CanvasGroup>();
            if (group == null)
                group = detailContentRoot.gameObject.AddComponent<CanvasGroup>();
            group.DOKill();
            group.alpha = 0f;
            group.DOFade(1f, 0.25f).SetUpdate(true);
        }
    }

    private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void RenderChart(List<ProgressPoint>[] days, List<ChildProgress> ordered)
    {
        if (chartRoot == null)
            return;
        EnsureChartViews();
        const float axisWidth = 62f;
        float width = Mathf.Max(864f, GetViewportSize().x, axisWidth + 7 * (ordered.Count * 34f + 16f));
        float dayWidth = (width - axisWidth) / 7f;
        legend.text = ordered.Count == 0 ? "Chưa có trẻ" : string.Join("  ·  ", ordered.Select(state =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(GetSeriesColor(state.child.childId))}>■ {EscapeText(GetChildName(state.child))}</color>"));
        float legendHeight = Mathf.Clamp(legend.GetPreferredValues(legend.text, width, Mathf.Infinity).y + 8f, 38f, 64f);
        float height = Mathf.Max(330f, GetViewportSize().y, legendHeight + 280f);
        float plotHeight = height - legendHeight - 100f;
        chartRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        chartRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        Place(legend.rectTransform, 0f, height - legendHeight, width, legendHeight);
        for (int tick = 0; tick < axisLabels.Count; tick++)
        {
            Place(axisLabels[tick].rectTransform, 0f, 52f + tick * plotHeight / 2f, axisWidth - 10f, 24f);
            Place(gridLines[tick].rectTransform, axisWidth, 64f + tick * plotHeight / 2f, width - axisWidth, 1.5f);
        }
        HashSet<string> childIds = new HashSet<string>(ordered.Select(state => state.child.childId));
        for (int day = 0; day < 7; day++)
        {
            DayView view = dayViews[day];
            Place(view.root, axisWidth + day * dayWidth, 0f, dayWidth, height - legendHeight);
            bool selected = selectedDate == weekStart.AddDays(day);
            view.background.color = selected ? selectedDayColor : Color.clear;
            Place(view.dateChip.rectTransform, 6f, 0f, dayWidth - 12f, 54f);
            view.dateChip.color = selected ? new Color(0.18f, 0.49f, 0.38f) : new Color(0.95f, 0.96f, 0.9f);
            view.label.color = selected ? Color.white : chartTextColor;
            Place(view.label.rectTransform, 0f, 0f, dayWidth - 12f, 54f);
            view.label.text = $"{GetDayLabel(weekStart.AddDays(day))}\n{weekStart.AddDays(day):dd/MM}";
            foreach (string id in view.bars.Keys.Where(id => !childIds.Contains(id)).ToList())
            {
                BarView removed = view.bars[id];
                removed.fill.DOKill();
                RemoveView(removed.track.gameObject);
                RemoveView(removed.value.gameObject);
                view.bars.Remove(id);
            }
            float slotWidth = (dayWidth - 12f) / Mathf.Max(1, ordered.Count);
            for (int index = 0; index < days[day].Count; index++)
            {
                ProgressPoint point = days[day][index];
                string id = point.owner.child.childId;
                if (!view.bars.TryGetValue(id, out BarView bar))
                {
                    bar = CreateBar(view.root, id);
                    view.bars.Add(id, bar);
                }
                Color color = GetSeriesColor(id);
                float barWidth = Mathf.Min(38f, slotWidth * 0.62f);
                Place(bar.track, 6f + index * slotWidth + (slotWidth - barWidth) / 2f, 64f, barWidth, plotHeight);
                bar.track.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.12f);
                bar.fill.GetComponent<Image>().color = color;
                Vector2 target = new Vector2(barWidth, point.Ratio * plotHeight);
                if ((bar.fill.sizeDelta - target).sqrMagnitude > 0.01f)
                {
                    bar.fill.DOKill();
                    bar.fill.DOSizeDelta(target, animationDuration).SetEase(animationEase).SetUpdate(true);
                }
                Place(bar.value.rectTransform, 6f + index * slotWidth, target.y + 66f, slotWidth, 28f);
                bar.value.text = point.owner.hasHabits && point.owner.hasLogs && !point.owner.syncFailed
                    ? GetPointLabel(point)
                    : "…";
            }
        }
    }

    private void EnsureChartViews()
    {
        if (generatedChart != null)
            return;
        generatedChart = NewRect("WeeklyChart", chartRoot);
        generatedChart.anchorMin = Vector2.zero;
        generatedChart.anchorMax = Vector2.one;
        generatedChart.offsetMin = generatedChart.offsetMax = Vector2.zero;
        generatedChart.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        legend = NewText("ChildrenLegend", generatedChart, TextAlignmentOptions.TopLeft);
        legend.fontSize = 23f;
        legend.overflowMode = TextOverflowModes.Ellipsis;
        legend.maxVisibleLines = 2;
        for (int tick = 0; tick < 3; tick++)
        {
            TMP_Text label = NewText("Percentage", generatedChart, TextAlignmentOptions.MidlineRight);
            label.text = $"{tick * 50}%";
            label.fontSize = 18f;
            axisLabels.Add(label);
            Image line = NewRect("GridLine", generatedChart).gameObject.AddComponent<Image>();
            line.color = new Color(0.28f, 0.47f, 0.38f, 0.13f);
            line.raycastTarget = false;
            gridLines.Add(line);
        }
        for (int i = 0; i < 7; i++)
        {
            int dayIndex = i;
            RectTransform root = NewRect($"Day_{i}", generatedChart);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.25f;
            image.color = Color.clear;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectDay(dayIndex));
            button.gameObject.AddComponent<HabitLogButtonFeedback>();
            Image chip = NewRect("DayChip", root).gameObject.AddComponent<Image>();
            chip.sprite = roundedSprite;
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 0.5f;
            chip.raycastTarget = false;
            TMP_Text dateLabel = NewText("Date", chip.transform, TextAlignmentOptions.Center);
            dateLabel.fontSize = 21f;
            dateLabel.fontStyle = FontStyles.Bold;
            dayViews.Add(new DayView
            {
                root = root,
                background = image,
                label = dateLabel,
                dateChip = chip
            });
        }
    }

    private BarView CreateBar(RectTransform parent, string childId)
    {
        RectTransform track = NewRect($"Child_{childId}", parent);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.raycastTarget = false;
        trackImage.sprite = roundedSprite;
        trackImage.type = Image.Type.Sliced;
        trackImage.pixelsPerUnitMultiplier = 0.5f;
        RectTransform fill;
        if (chartBarPrefab != null && chartBarPrefab.GetComponent<RectTransform>() != null
            && chartBarPrefab.GetComponent<Image>() != null)
        {
            GameObject instance = Instantiate(chartBarPrefab, track);
            instance.SetActive(true);
            fill = instance.GetComponent<RectTransform>();
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        else
        {
            fill = NewRect("Completed", track);
            fill.gameObject.AddComponent<Image>().raycastTarget = false;
        }
        fill.localScale = Vector3.one;
        fill.GetComponent<Image>().sprite = roundedSprite;
        fill.GetComponent<Image>().type = Image.Type.Sliced;
        fill.GetComponent<Image>().pixelsPerUnitMultiplier = 0.5f;
        fill.localRotation = Quaternion.identity;
        Place(fill, 0f, 0f, 0f, 0f);
        TMP_Text value = NewText("Count", parent, TextAlignmentOptions.Center);
        value.enableAutoSizing = true;
        value.fontSizeMin = 16f;
        value.fontSizeMax = 22f;
        value.fontStyle = FontStyles.Bold;
        value.textWrappingMode = TextWrappingModes.NoWrap;
        return new BarView { track = track, fill = fill, value = value };
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void RemoveView(GameObject view)
    {
        view.SetActive(false);
        if (Application.isPlaying)
            Destroy(view);
        else
            DestroyImmediate(view);
    }

    private TMP_Text NewText(string name, Transform parent, TextAlignmentOptions alignment)
    {
        TMP_Text text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (summaryText != null && summaryText.font != null)
            text.font = summaryText.font;
        text.fontSize = 16f;
        text.color = chartTextColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void PrepareDetailScroll()
    {
        if (detailContentRoot != null)
        {
            detailScroll = detailContentRoot.GetComponentInParent<ScrollRect>();
            return;
        }
        if (detailText == null)
            return;
        detailText.richText = true;
        detailScroll = detailText.GetComponentInParent<ScrollRect>();
        if (detailScroll != null)
            return;
        // Older scenes used a single-line title. Give it a scrollable body.
        RectTransform content = detailText.rectTransform;
        RectTransform viewport = NewRect("DayDetails", content.parent);
        viewport.SetSiblingIndex(content.GetSiblingIndex());
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = content.anchoredPosition;
        viewport.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(100f, content.sizeDelta.y));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        LayoutElement layout = viewport.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 80f;
        layout.preferredHeight = 120f;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;
        if (summaryText != null && summaryText.transform.parent == content.parent)
        {
            VerticalLayoutGroup cardLayout = content.parent.GetComponent<VerticalLayoutGroup>();
            if (cardLayout != null)
            {
                cardLayout.childControlWidth = true;
                cardLayout.childControlHeight = true;
                cardLayout.childForceExpandHeight = false;
                cardLayout.padding.right = cardLayout.padding.left;
                summaryText.enableAutoSizing = false;
                summaryText.fontSize = Mathf.Min(summaryText.fontSize, 24f);
                LayoutElement summaryLayout = summaryText.GetComponent<LayoutElement>();
                if (summaryLayout == null)
                    summaryLayout = summaryText.gameObject.AddComponent<LayoutElement>();
                summaryLayout.minHeight = 32f;
                summaryLayout.preferredHeight = 32f;
            }
        }
        detailScroll = viewport.gameObject.AddComponent<ScrollRect>();
        detailScroll.viewport = viewport;
        detailScroll.content = content;
        detailScroll.horizontal = false;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 25f;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = content.sizeDelta = Vector2.zero;
        detailText.alignment = TextAlignmentOptions.TopLeft;
        detailText.enableAutoSizing = false;
        detailText.fontSize = Mathf.Min(detailText.fontSize, 24f);
        detailText.textWrappingMode = TextWrappingModes.Normal;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void KillBarTweens()
    {
        if (generatedChart != null)
        {
            generatedChart.DOKill();
            generatedChart.anchoredPosition = Vector2.zero;
        }
        foreach (DayView day in dayViews)
            foreach (BarView bar in day.bars.Values)
                if (bar.fill != null)
                    bar.fill.DOKill();
        if (weeklyProgressFill != null)
            weeklyProgressFill.DOKill();
        if (detailContentRoot != null)
            detailContentRoot.GetComponent<CanvasGroup>()?.DOKill();
    }

    private static string GetChildName(ChildModel child) => string.IsNullOrWhiteSpace(child.name) ? "Chưa đặt tên" : child.name;
    private static string EscapeText(string value) => (value ?? string.Empty).Replace("<", "＜").Replace(">", "＞");

    private static Color GetChildColor(string childId)
    {
        // A deterministic hash keeps each child's colour when ordering/names change.
        uint hash = 2166136261;
        foreach (char character in childId)
            hash = unchecked((hash ^ character) * 16777619);
        return GetPaletteColor((int)(hash % 6));
    }

    private Color GetSeriesColor(string childId)
    {
        if (seriesColors == null)
            return GetChildColor(childId);
        if (seriesColors.TryGetValue(childId, out Color existing))
            return existing;
        HashSet<Color> used = new HashSet<Color>(children.Keys.Where(seriesColors.ContainsKey).Select(id => seriesColors[id]));
        Color color = GetChildColor(childId);
        for (int candidate = 0; used.Contains(color) && candidate < 6; candidate++)
            color = GetPaletteColor(candidate);
        if (used.Contains(color))
            color = Color.HSVToRGB(Mathf.Repeat(seriesColors.Count * 0.618034f, 1f), 0.45f, 0.7f);
        seriesColors[childId] = color;
        return color;
    }

    private static Color GetPaletteColor(int index)
    {
        return index switch
        {
            0 => new Color(0.55f, 0.50f, 0.71f),
            1 => new Color(0.85f, 0.52f, 0.43f),
            2 => new Color(0.25f, 0.60f, 0.52f),
            3 => new Color(0.68f, 0.52f, 0.25f),
            4 => new Color(0.33f, 0.57f, 0.72f),
            _ => new Color(0.50f, 0.61f, 0.36f)
        };
    }

    private static string GetDayLabel(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)date.DayOfWeek + 1}";
    }
}
