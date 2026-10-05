using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    [SerializeField] private GameObject chartBarPrefab;

    private enum ViewMode
    {
        Day,
        Month,
        Year
    }

    private readonly List<HabitModel> habits = new List<HabitModel>();
    private readonly List<HabitLogModel> logs = new List<HabitLogModel>();

    private ViewMode currentMode = ViewMode.Day;
    private DateTime currentDate = DateTime.Today;

    private class ProgressPoint
    {
        public string Label { get; set; }
        public int Total { get; set; }
        public int Completed { get; set; }
        public float Ratio => Total > 0 ? (float)Completed / Total : 0f;
    }

    private void Start()
    {
        BindButtons();
        _ = RefreshAsync();
    }

    private void BindButtons()
    {
        if (dayFilterButton != null)
            dayFilterButton.onClick.RemoveAllListeners();
        if (monthFilterButton != null)
            monthFilterButton.onClick.RemoveAllListeners();
        if (yearFilterButton != null)
            yearFilterButton.onClick.RemoveAllListeners();
        if (previousButton != null)
            previousButton.onClick.RemoveAllListeners();
        if (nextButton != null)
            nextButton.onClick.RemoveAllListeners();

        if (dayFilterButton != null)
            dayFilterButton.onClick.AddListener(() => { currentMode = ViewMode.Day; Render(); });
        if (monthFilterButton != null)
            monthFilterButton.onClick.AddListener(() => { currentMode = ViewMode.Month; Render(); });
        if (yearFilterButton != null)
            yearFilterButton.onClick.AddListener(() => { currentMode = ViewMode.Year; Render(); });
        if (previousButton != null)
            previousButton.onClick.AddListener(GoToPreviousPeriod);
        if (nextButton != null)
            nextButton.onClick.AddListener(GoToNextPeriod);
    }

    private void GoToPreviousPeriod()
    {
        switch (currentMode)
        {
            case ViewMode.Day:
                currentDate = currentDate.AddDays(-1);
                break;
            case ViewMode.Month:
                currentDate = currentDate.AddMonths(-1);
                break;
            case ViewMode.Year:
                currentDate = currentDate.AddYears(-1);
                break;
        }

        Render();
    }

    private void GoToNextPeriod()
    {
        switch (currentMode)
        {
            case ViewMode.Day:
                currentDate = currentDate.AddDays(1);
                break;
            case ViewMode.Month:
                currentDate = currentDate.AddMonths(1);
                break;
            case ViewMode.Year:
                currentDate = currentDate.AddYears(1);
                break;
        }

        Render();
    }

    public async Task RefreshAsync()
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            if (summaryText != null)
                summaryText.text = "Chưa kết nối Firebase";
            return;
        }

        string parentId = FirebaseManager.Instance.GetCurrentParentId();
        string childId = FirebaseManager.Instance.GetCurrentChildId();

        if (string.IsNullOrEmpty(childId))
            childId = await FirebaseManager.Instance.ResolveCurrentChildIdAsync();

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
        {
            if (summaryText != null)
                summaryText.text = "Thiếu thông tin phụ huynh / trẻ";
            return;
        }

        habits.Clear();
        logs.Clear();

        habits.AddRange(await FirebaseManager.Instance.GetHabitsAsync(parentId, childId));
        logs.AddRange(await FirebaseManager.Instance.GetHabitLogsAsync(parentId, childId));

        Render();
    }

    private void Render()
    {
        UpdateFilterButtonState();

        List<ProgressPoint> points = BuildProgressPoints();

        if (summaryText != null)
        {
            int total = points.Sum(p => p.Total);
            int completed = points.Sum(p => p.Completed);
            summaryText.text = total > 0 ? $"{completed}/{total} nhiệm vụ" : "0/0 nhiệm vụ";
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

    private List<ProgressPoint> BuildProgressPoints()
    {
        var result = new List<ProgressPoint>();

        if (currentMode == ViewMode.Day)
        {
            result.Add(CreatePointForDate(currentDate, GetDayLabel(currentDate)));
            return result;
        }

        if (currentMode == ViewMode.Month)
        {
            DateTime start = new DateTime(currentDate.Year, currentDate.Month, 1);
            DateTime end = start.AddMonths(1).AddDays(-1);

            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                result.Add(CreatePointForDate(date, GetShortDayLabel(date)));
            }

            return result;
        }

        DateTime yearStart = new DateTime(currentDate.Year, 1, 1);
        for (int month = 1; month <= 12; month++)
        {
            DateTime monthDate = new DateTime(currentDate.Year, month, 1);
            result.Add(CreatePointForDate(monthDate, GetMonthShortLabel(monthDate)));
        }

        return result;
    }

    private ProgressPoint CreatePointForDate(DateTime date, string label)
    {
        int total = CountTotalTasksForDate(date);
        int completed = CountCompletedTasksForDate(date);

        return new ProgressPoint
        {
            Label = label,
            Total = total,
            Completed = completed
        };
    }

    private int CountTotalTasksForDate(DateTime date)
    {
        if (habits == null || habits.Count == 0)
            return 0;

        return habits.Count(h => h != null && h.isActive && IsHabitScheduledForDate(h, date));
    }

    private int CountCompletedTasksForDate(DateTime date)
    {
        if (logs == null || logs.Count == 0 || habits == null || habits.Count == 0)
            return 0;

        string dateKey = date.ToString("yyyy-MM-dd");
        HashSet<string> completedHabitIds = new HashSet<string>(
            logs.Where(log => log != null && log.isCompleted && log.dateKey == dateKey)
                .Select(log => log.habitId)
                .Where(id => !string.IsNullOrEmpty(id))
        );

        return habits.Count(h =>
            h != null &&
            h.isActive &&
            IsHabitScheduledForDate(h, date) &&
            !string.IsNullOrEmpty(h.id) &&
            completedHabitIds.Contains(h.id));
    }

    private bool IsHabitScheduledForDate(HabitModel habit, DateTime date)
    {
        if (habit == null || !habit.isActive)
            return false;

        if (habit.repeatDays == null || habit.repeatDays.Count == 0)
            return true;

        int weekdayNumber = NormalizeDayOfWeek((int)date.DayOfWeek);

        foreach (int day in habit.repeatDays)
        {
            if (NormalizeDayOfWeek(day) == weekdayNumber)
                return true;
        }

        return false;
    }

    private int NormalizeDayOfWeek(int value)
    {
        if (value == 7)
            return 7;

        if (value == 0)
            return 7;

        if (value < 0)
            return 7;

        if (value > 7)
            return value % 7 == 0 ? 7 : value % 7;

        return value;
    }

    private string GetPeriodTitle()
    {
        if (currentMode == ViewMode.Day)
            return currentDate.ToString("dd/MM/yyyy");

        if (currentMode == ViewMode.Month)
            return currentDate.ToString("MM/yyyy");

        return currentDate.ToString("yyyy");
    }

    private string BuildDetailText(List<ProgressPoint> points)
    {
        if (points == null || points.Count == 0)
            return "Không có dữ liệu";

        if (currentMode == ViewMode.Day)
        {
            ProgressPoint dayPoint = points[0];
            return dayPoint.Total > 0
                ? $"{dayPoint.Label}: {dayPoint.Completed}/{dayPoint.Total} nhiệm vụ hoàn thành"
                : "Chưa có nhiệm vụ được lên lịch cho ngày này.";
        }

        return string.Join("\n", points.Select(p => $"{p.Label}: {p.Completed}/{p.Total}"));
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
            _ => date.ToString("dd")
        };
    }

    private string GetShortDayLabel(DateTime date)
    {
        return date.Day.ToString();
    }

    private string GetMonthShortLabel(DateTime date)
    {
        return date.ToString("MM");
    }

    private void RenderChart(List<ProgressPoint> points)
    {
        if (chartRoot == null)
            return;

        for (int i = chartRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(chartRoot.GetChild(i).gameObject);
        }

        if (points == null || points.Count == 0)
            return;

        float totalHeight = chartRoot.rect.height > 0 ? chartRoot.rect.height : 220f;
        float totalWidth = chartRoot.rect.width > 0 ? chartRoot.rect.width : 600f;
        float spacing = totalWidth / Mathf.Max(points.Count, 1);

        for (int i = 0; i < points.Count; i++)
        {
            ProgressPoint point = points[i];

            GameObject barObject = chartBarPrefab != null ? Instantiate(chartBarPrefab, chartRoot) : new GameObject($"Bar_{i}", typeof(RectTransform), typeof(Image));
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            Image barImage = barObject.GetComponent<Image>();

            if (barImage == null)
                barImage = barObject.AddComponent<Image>();

            barImage.color = GetBarColor(point.Ratio);

            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(Mathf.Min(28f, spacing * 0.7f), Mathf.Max(10f, point.Ratio * totalHeight * 0.8f));
            barRect.anchoredPosition = new Vector2((i + 0.5f) * spacing, 0f);

            GameObject labelObject = new GameObject($"Label_{i}", typeof(RectTransform), typeof(TMP_Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(chartRoot, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(50f, 28f);
            labelRect.anchoredPosition = new Vector2((i + 0.5f) * spacing, -28f);

            TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
            labelText.text = point.Label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 16;
            labelText.color = Color.white;

            GameObject valueObject = new GameObject($"Value_{i}", typeof(RectTransform), typeof(TMP_Text));
            RectTransform valueRect = valueObject.GetComponent<RectTransform>();
            valueRect.SetParent(chartRoot, false);
            valueRect.anchorMin = new Vector2(0f, 0f);
            valueRect.anchorMax = new Vector2(0f, 0f);
            valueRect.pivot = new Vector2(0.5f, 0f);
            valueRect.sizeDelta = new Vector2(60f, 24f);
            valueRect.anchoredPosition = new Vector2((i + 0.5f) * spacing, barRect.sizeDelta.y + 8f);

            TMP_Text valueText = valueObject.GetComponent<TMP_Text>();
            valueText.text = point.Total > 0 ? $"{point.Completed}/{point.Total}" : "0/0";
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.fontSize = 14;
            valueText.color = new Color(1f, 1f, 1f, 0.9f);
        }
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
