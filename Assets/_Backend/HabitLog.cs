using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HABIMON.Core;
using UnityEngine;

public class HabitLog : MonoBehaviour
{
    public static DateTime GetBusinessDate(DateTimeOffset utcNow)
    {
        return BusinessCalendar.GetDate(utcNow);
    }

    public static string GetTodayDateKey()
    {
        return BusinessCalendar.GetDateKey(DateTimeOffset.UtcNow);
    }

    public static bool CanEditToday(string dateKey)
    {
        if (string.IsNullOrEmpty(dateKey))
            return true;

        return string.Equals(dateKey, GetTodayDateKey(), StringComparison.Ordinal);
    }

    public static bool IsScheduledForDate(HabitModel habit, DateTime date)
    {
        if (habit == null || !habit.isActive)
            return false;

        return HabitSchedule.IsRepeatDayScheduled(date, habit.repeatDays?.ToArray());
    }

    public static bool MatchesPeriod(string timeTarget, string targetPeriod)
    {
        if (string.IsNullOrWhiteSpace(timeTarget) || string.IsNullOrWhiteSpace(targetPeriod))
            return false;

        int target = ParsePeriod(targetPeriod);
        if (target < 0)
            return false;

        int value = ParsePeriod(timeTarget);
        if (value >= 0)
            return value == target;

        if (!TimeSpan.TryParseExact(timeTarget.Trim(), new[] { "h\\:mm", "hh\\:mm" },
                CultureInfo.InvariantCulture, out TimeSpan localTime))
            return false;

        int period = localTime.Hours >= 5 && localTime.Hours < 12 ? 0 :
            localTime.Hours >= 12 && localTime.Hours < 18 ? 1 : 2;
        return period == target;
    }

    private static int ParsePeriod(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "all" || normalized == "tất cả")
            return 3;
        if (normalized.Contains("sáng") || normalized.Contains("morning"))
            return 0;
        if (normalized.Contains("chiều") || normalized.Contains("afternoon"))
            return 1;
        if (normalized.Contains("tối") || normalized.Contains("evening") || normalized.Contains("night"))
            return 2;
        return -1;
    }

    public static async Task<HabitLogModel> GetTodayHabitLogAsync(string parentId, string childId, string habitId)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || !await manager.EnsureInitializedAsync())
            return null;

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
            return null;

        List<HabitLogModel> logs = await manager.GetHabitLogsForDateAsync(
            parentId, childId, GetTodayDateKey());
        return FindPreferredLog(logs, habitId, GetTodayDateKey());
    }

    public static async Task<bool> GetTodayCompletionStateAsync(string parentId, string childId, string habitId)
    {
        var log = await GetTodayHabitLogAsync(parentId, childId, habitId);
        return log != null && log.isCompleted;
    }

    public static async Task<HabitLogModel> GetLatestHabitLogAsync(string parentId, string childId, string habitId)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || !await manager.EnsureInitializedAsync())
            return null;

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
            return null;

        var logs = await manager.GetHabitLogsAsync(parentId, childId);
        return logs
            .Where(log => log != null && log.habitId == habitId)
            .OrderByDescending(log => log.dateKey)
            .FirstOrDefault();
    }

    public static async Task<bool> ToggleHabitForTodayAsync(string parentId, string childId, string habitId, bool isCompleted)
    {
        return await HabitRewardService.SetTodayCompletionAsync(
            parentId, childId, habitId, GetTodayDateKey(), isCompleted);
    }

    public static string GetDailyLogDocumentId(string habitId, string dateKey)
    {
        return DailyHabitLogIdentity.GetDocumentId(habitId, dateKey);
    }

    private static HabitLogModel FindPreferredLog(List<HabitLogModel> logs, string habitId, string dateKey)
    {
        if (logs == null)
            return null;

        string deterministicId = GetDailyLogDocumentId(habitId, dateKey);
        HabitLogModel preferred = null;
        foreach (HabitLogModel log in logs)
        {
            if (log == null || log.habitId != habitId || log.dateKey != dateKey)
                continue;
            if (log.logId == deterministicId)
                return log;
            if (preferred == null || string.CompareOrdinal(log.logId, preferred.logId) < 0)
                preferred = log;
        }

        return preferred;
    }
}
