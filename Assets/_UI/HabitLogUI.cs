using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HABIMON.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HabitLogUI : MonoBehaviour
{
    [Header("Summary UI")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text periodLabelText;

    [Header("Filters")]
    [SerializeField] private Button dayFilterButton;
    [SerializeField] private Button monthFilterButton;
    [SerializeField] private Button yearFilterButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Chart")]
    [SerializeField] private RectTransform chartRoot;

    private enum ViewMode
    {
        Day,
        Month,
        Year
    }

    private readonly List<HabitModel> habits = new List<HabitModel>();
    private readonly List<HabitLogModel> logs = new List<HabitLogModel>();
    private readonly List<GameObject> generatedChartObjects = new List<GameObject>();

    private ViewMode currentMode = ViewMode.Day;
    private DateTime currentDate = HabitLog.GetBusinessDate(DateTimeOffset.UtcNow);
    private FirebaseManager subscribedManager;
    private int refreshVersion;

    private sealed class ProgressPoint
    {
        public string Label;
        public int Total;
        public int Completed;
        public bool HasUnknownCoverage;
        public bool HasConflictingRecords;
        public float Ratio => Total > 0 ? Mathf.Clamp01((float)Completed / Total) : 0f;
    }

    private void OnEnable()
    {
        SubscribeToSession();
    }

    private void Start()
    {
        BindButtons();
        SubscribeToSession();
        _ = RefreshAsync();
    }

    private void OnDisable()
    {
        refreshVersion++;
        UnsubscribeFromSession();
    }

    private void SubscribeToSession()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || subscribedManager == manager)
            return;

        UnsubscribeFromSession();
        subscribedManager = manager;
        subscribedManager.CurrentChildChanged += OnCurrentChildChanged;
        subscribedManager.SessionChanged += OnSessionChanged;
    }

    private void UnsubscribeFromSession()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.CurrentChildChanged -= OnCurrentChildChanged;
        subscribedManager.SessionChanged -= OnSessionChanged;
        subscribedManager = null;
    }

    private void OnCurrentChildChanged()
    {
        ClearSnapshot();
        _ = RefreshAsync();
    }

    private void OnSessionChanged()
    {
        ClearSnapshot();
        if (FirebaseManager.Instance?.CurrentUser == null)
        {
            if (summaryText != null)
                summaryText.text = "Vui lòng đăng nhập để xem thống kê.";
            return;
        }

        _ = RefreshAsync();
    }

    private void ClearSnapshot()
    {
        habits.Clear();
        logs.Clear();
        Render();
    }

    private void BindButtons()
    {
        if (dayFilterButton != null)
            dayFilterButton.onClick.AddListener(() => ChangeMode(ViewMode.Day));
        if (monthFilterButton != null)
            monthFilterButton.onClick.AddListener(() => ChangeMode(ViewMode.Month));
        if (yearFilterButton != null)
            yearFilterButton.onClick.AddListener(() => ChangeMode(ViewMode.Year));
        if (previousButton != null)
            previousButton.onClick.AddListener(GoToPreviousPeriod);
        if (nextButton != null)
            nextButton.onClick.AddListener(GoToNextPeriod);
    }

    private void ChangeMode(ViewMode mode)
    {
        currentMode = mode;
        _ = RefreshAsync();
    }

    private void GoToPreviousPeriod()
    {
        switch (currentMode)
        {
            case ViewMode.Day:
                currentDate = currentDate.AddDays(-1);
                break;
            case ViewMode.Month:
                currentDate = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-1);
                break;
            case ViewMode.Year:
                currentDate = new DateTime(currentDate.Year - 1, 1, 1);
                break;
        }

        _ = RefreshAsync();
    }

    private void GoToNextPeriod()
    {
        switch (currentMode)
        {
            case ViewMode.Day:
                currentDate = currentDate.AddDays(1);
                break;
            case ViewMode.Month:
                currentDate = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1);
                break;
            case ViewMode.Year:
                currentDate = new DateTime(currentDate.Year + 1, 1, 1);
                break;
        }

        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        int requestVersion = ++refreshVersion;
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            if (summaryText != null)
                summaryText.text = "Chưa kết nối Firebase";
            return;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            if (summaryText != null)
                summaryText.text = $"Firebase chưa sẵn sàng: {manager.InitializationError}";
            return;
        }

        string parentId = manager.GetCurrentParentId();
        string childId = manager.GetCurrentChildId();
        if (string.IsNullOrEmpty(childId))
            childId = await manager.ResolveCurrentChildIdAsync();

        if (this == null || requestVersion != refreshVersion || manager != FirebaseManager.Instance)
            return;

        if (string.IsNullOrEmpty(parentId) || parentId != manager.GetCurrentParentId() ||
            string.IsNullOrEmpty(childId) || childId != manager.GetCurrentChildId())
        {
            ClearSnapshot();
            if (summaryText != null)
                summaryText.text = "Thiếu thông tin phụ huynh / trẻ";
            return;
        }

        int sessionGeneration = manager.SessionGeneration;
        GetRange(out DateTime startDate, out DateTime endDateExclusive);
        string startDateKey = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string endDateKey = endDateExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            Task<List<HabitModel>> habitsTask = manager.GetHabitsAsync(parentId, childId);
            Task<List<HabitLogModel>> logsTask = manager.GetHabitLogsInRangeAsync(
                parentId, childId, startDateKey, endDateKey);
            await Task.WhenAll(habitsTask, logsTask);

            if (this == null || !isActiveAndEnabled || requestVersion != refreshVersion ||
                manager != FirebaseManager.Instance || manager.SessionGeneration != sessionGeneration ||
                manager.GetCurrentParentId() != parentId || manager.GetCurrentChildId() != childId)
                return;

            habits.Clear();
            habits.AddRange(habitsTask.Result.Where(habit => habit != null));
            logs.Clear();
            logs.AddRange(logsTask.Result.Where(log => log != null));
            Render();
        }
        catch (Exception ex)
        {
            if (this == null || requestVersion != refreshVersion)
                return;

            Debug.LogError($"[HabitLogUI] Không thể tải thống kê: {ex.Message}");
            if (summaryText != null)
                summaryText.text = "Không thể tải thống kê. Vui lòng thử lại.";
        }
    }

    private void GetRange(out DateTime start, out DateTime endExclusive)
    {
        if (currentMode == ViewMode.Day)
        {
            start = currentDate.Date;
            endExclusive = start.AddDays(1);
            return;
        }

        if (currentMode == ViewMode.Month)
        {
            start = new DateTime(currentDate.Year, currentDate.Month, 1);
            endExclusive = start.AddMonths(1);
            return;
        }

        start = new DateTime(currentDate.Year, 1, 1);
        endExclusive = start.AddYears(1);
    }

    private void Render()
    {
        UpdateFilterButtonState();
        List<HabitOccurrence> occurrences = BuildHabitOccurrences();
        List<HabitCompletion> completions = BuildCompletions();
        List<ProgressPoint> points = BuildProgressPoints(occurrences, completions);

        if (summaryText != null)
        {
            int total = points.Sum(point => point.Total);
            int completed = points.Sum(point => point.Completed);
            bool unknown = points.Any(point => point.HasUnknownCoverage);
            bool conflicts = points.Any(point => point.HasConflictingRecords);
            string suffix = unknown ? " (lịch sử chưa đầy đủ)" : conflicts ? " (có log trùng cần đối soát)" : string.Empty;
            summaryText.text = total > 0 ? $"{completed}/{total} nhiệm vụ{suffix}" :
                unknown ? "Chưa đủ dữ liệu lịch sử để tính." : "0/0 nhiệm vụ";
        }

        if (periodLabelText != null)
            periodLabelText.text = GetPeriodTitle();

        if (detailText != null)
            detailText.text = BuildDetailText(points);

        RenderChart(points);
    }

    private void UpdateFilterButtonState()
    {
        if (dayFilterButton != null)
            dayFilterButton.interactable = currentMode != ViewMode.Day;
        if (monthFilterButton != null)
            monthFilterButton.interactable = currentMode != ViewMode.Month;
        if (yearFilterButton != null)
            yearFilterButton.interactable = currentMode != ViewMode.Year;
    }

    private List<ProgressPoint> BuildProgressPoints(
        List<HabitOccurrence> occurrences,
        List<HabitCompletion> completions)
    {
        var result = new List<ProgressPoint>();

        if (currentMode == ViewMode.Day)
        {
            result.Add(CreatePointForRange(currentDate.Date, currentDate.Date.AddDays(1), GetDayLabel(currentDate), occurrences, completions));
            return result;
        }

        if (currentMode == ViewMode.Month)
        {
            DateTime start = new DateTime(currentDate.Year, currentDate.Month, 1);
            DateTime end = start.AddMonths(1);
            for (DateTime date = start; date < end; date = date.AddDays(1))
                result.Add(CreatePointForRange(date, date.AddDays(1), GetShortDayLabel(date), occurrences, completions));
            return result;
        }

        DateTime yearStart = new DateTime(currentDate.Year, 1, 1);
        for (int month = 0; month < 12; month++)
        {
            DateTime start = yearStart.AddMonths(month);
            DateTime end = start.AddMonths(1);
            result.Add(CreatePointForRange(start, end, GetMonthShortLabel(start), occurrences, completions));
        }

        return result;
    }

    private ProgressPoint CreatePointForRange(
        DateTime start,
        DateTime endExclusive,
        string label,
        List<HabitOccurrence> occurrences,
        List<HabitCompletion> completions)
    {
        HabitProgress progress = HabitAnalytics.Calculate(start, endExclusive, occurrences, completions);
        return new ProgressPoint
        {
            Label = label,
            Total = progress.Total,
            Completed = progress.Completed,
            HasUnknownCoverage = progress.HasUnknownCoverage,
            HasConflictingRecords = progress.HasConflictingRecords
        };
    }

    private List<HabitOccurrence> BuildHabitOccurrences()
    {
        var result = new List<HabitOccurrence>(habits.Count);
        foreach (HabitModel habit in habits)
        {
            if (habit == null || string.IsNullOrEmpty(habit.id))
                continue;

            DateTime? effectiveFrom = ParseBusinessDate(habit.effectiveFrom);
            DateTime? archivedAt = habit.archivedAt.HasValue
                ? HabitLog.GetBusinessDate(new DateTimeOffset(habit.archivedAt.Value.ToDateTime().ToUniversalTime()))
                : (DateTime?)null;

            result.Add(new HabitOccurrence
            {
                HabitId = habit.id,
                Schedule = HabitSchedule.FromStorage(habit.scheduleKind, habit.scheduleValue, habit.timeTarget),
                RepeatDays = habit.repeatDays?.ToArray(),
                EffectiveFrom = effectiveFrom,
                ArchivedAt = archivedAt,
                IsActive = habit.isActive,
                RequireApproval = habit.requireApproval
            });
        }

        return result;
    }

    private List<HabitCompletion> BuildCompletions()
    {
        var result = new List<HabitCompletion>(logs.Count);
        foreach (HabitLogModel log in logs)
        {
            if (log == null || string.IsNullOrEmpty(log.habitId) ||
                !DateTime.TryParseExact(log.dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
                continue;

            result.Add(new HabitCompletion
            {
                HabitId = log.habitId,
                Date = date,
                IsCompleted = log.isCompleted,
                IsApproved = log.isApproved,
                RequiresApprovalAtCompletion = log.requiresApprovalAtCompletion
            });
        }

        return result;
    }

    private static DateTime? ParseBusinessDate(string value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime date))
            return date.Date;
        return null;
    }

    private string GetPeriodTitle()
    {
        if (currentMode == ViewMode.Day)
            return currentDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (currentMode == ViewMode.Month)
            return currentDate.ToString("MM/yyyy", CultureInfo.InvariantCulture);
        return currentDate.ToString("yyyy", CultureInfo.InvariantCulture);
    }

    private string BuildDetailText(List<ProgressPoint> points)
    {
        if (points == null || points.Count == 0)
            return "Không có dữ liệu";

        if (currentMode == ViewMode.Day)
        {
            ProgressPoint day = points[0];
            if (day.Total == 0)
                return day.HasUnknownCoverage
                    ? "Chưa đủ dữ liệu lịch sử cho ngày này."
                    : "Chưa có nhiệm vụ được lên lịch cho ngày này.";
            return day.HasUnknownCoverage
                ? $"{day.Label}: {day.Completed}/{day.Total} nhiệm vụ đã biết; lịch sử chưa đầy đủ."
                : $"{day.Label}: {day.Completed}/{day.Total} nhiệm vụ hoàn thành";
        }

        return string.Join("\n", points.Select(point =>
            $"{point.Label}: {(point.Total > 0 ? $"{point.Completed}/{point.Total}" : point.HasUnknownCoverage ? "?" : "—")}"));
    }

    private string GetDayLabel(DateTime date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            DayOfWeek.Sunday => "CN",
            _ => date.ToString("dd", CultureInfo.InvariantCulture)
        };
    }

    private string GetShortDayLabel(DateTime date)
    {
        return date.Day.ToString(CultureInfo.InvariantCulture);
    }

    private string GetMonthShortLabel(DateTime date)
    {
        return date.ToString("MM", CultureInfo.InvariantCulture);
    }

    private void RenderChart(List<ProgressPoint> points)
    {
        ClearGeneratedChartObjects();
        if (chartRoot == null || points == null || points.Count == 0)
            return;

        RectTransform viewport = chartRoot.parent as RectTransform;
        float viewportWidth = viewport != null && viewport.rect.width > 0 ? viewport.rect.width :
            chartRoot.rect.width > 0 ? chartRoot.rect.width : 600f;
        float contentWidth = Mathf.Max(viewportWidth, points.Count * 40f);
        chartRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        float totalHeight = chartRoot.rect.height > 0 ? chartRoot.rect.height : 220f;
        float spacing = contentWidth / points.Count;

        for (int index = 0; index < points.Count; index++)
        {
            ProgressPoint point = points[index];
            float barHeight = point.Ratio * totalHeight * 0.8f;

            GameObject barObject = new GameObject($"Bar_{index}", typeof(RectTransform), typeof(Image));
            barObject.layer = chartRoot.gameObject.layer;
            barObject.transform.SetParent(chartRoot, false);
            generatedChartObjects.Add(barObject);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            Image barImage = barObject.GetComponent<Image>();
            barImage.color = GetBarColor(point.Ratio);
            barImage.raycastTarget = false;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(Mathf.Min(26f, spacing * 0.55f), barHeight);
            barRect.anchoredPosition = new Vector2((index + 0.5f) * spacing, 0f);

            TMP_Text label = CreateChartText($"Label_{index}", point.Label, chartRoot);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(spacing, 24f);
            labelRect.anchoredPosition = new Vector2((index + 0.5f) * spacing, -26f);
            label.fontSize = Mathf.Clamp(spacing * 0.45f, 10f, 16f);

            string value = point.Total > 0 ? $"{point.Completed}/{point.Total}" : point.HasUnknownCoverage ? "?" : "—";
            TMP_Text valueText = CreateChartText($"Value_{index}", value, chartRoot);
            RectTransform valueRect = (RectTransform)valueText.transform;
            valueRect.anchorMin = new Vector2(0f, 0f);
            valueRect.anchorMax = new Vector2(0f, 0f);
            valueRect.pivot = new Vector2(0.5f, 0f);
            valueRect.sizeDelta = new Vector2(spacing, 22f);
            valueRect.anchoredPosition = new Vector2((index + 0.5f) * spacing, barHeight + 6f);
            valueText.fontSize = Mathf.Clamp(spacing * 0.35f, 10f, 14f);
        }
    }

    private TMP_Text CreateChartText(string objectName, string value, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        generatedChartObjects.Add(textObject);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void ClearGeneratedChartObjects()
    {
        foreach (GameObject generated in generatedChartObjects)
        {
            if (generated != null)
            {
                if (Application.isPlaying)
                    Destroy(generated);
                else
                    DestroyImmediate(generated);
            }
        }
        generatedChartObjects.Clear();
    }

    private Color GetBarColor(float ratio)
    {
        if (ratio <= 0.25f)
            return new Color(0.76f, 0.89f, 1f, 1f);
        if (ratio <= 0.5f)
            return new Color(0.51f, 0.78f, 1f, 1f);
        if (ratio <= 0.75f)
            return new Color(0.29f, 0.67f, 1f, 1f);
        return new Color(0.12f, 0.48f, 0.96f, 1f);
    }
}
