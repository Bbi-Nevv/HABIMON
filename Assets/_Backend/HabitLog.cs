using Firebase.Firestore;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class HabitLog : MonoBehaviour
{
    public static string GetTodayDateKey()
    {
        return DateTime.Today.ToString("yyyy-MM-dd");
    }

    public static bool CanEditToday(string dateKey)
    {
        if (string.IsNullOrEmpty(dateKey))
            return true;

        return string.Equals(dateKey, GetTodayDateKey(), StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<HabitLogModel> GetTodayHabitLogAsync(string parentId, string childId, string habitId)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
            return null;

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
            return null;

        var logs = await FirebaseManager.Instance.GetHabitLogsAsync(parentId, childId);
        string todayKey = GetTodayDateKey();

        return logs.FirstOrDefault(log =>
            log != null &&
            log.habitId == habitId &&
            log.dateKey == todayKey);
    }

    public static async Task<bool> GetTodayCompletionStateAsync(string parentId, string childId, string habitId)
    {
        var log = await GetTodayHabitLogAsync(parentId, childId, habitId);
        return log != null && log.isCompleted;
    }

    public static async Task<HabitLogModel> GetLatestHabitLogAsync(string parentId, string childId, string habitId)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
            return null;

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
            return null;

        var logs = await FirebaseManager.Instance.GetHabitLogsAsync(parentId, childId);
        return logs
            .Where(log => log != null && log.habitId == habitId)
            .OrderByDescending(log => log.dateKey)
            .FirstOrDefault();
    }

    public static async Task<bool> ToggleHabitForTodayAsync(string parentId, string childId, string habitId, bool isCompleted)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[HabitLog] Firebase chưa sẵn sàng.");
            return false;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[HabitLog] Thiếu thông tin parentId, childId hoặc habitId.");
            return false;
        }

        var todayKey = GetTodayDateKey();
        var logs = await FirebaseManager.Instance.GetHabitLogsAsync(parentId, childId);
        var todayLog = logs.FirstOrDefault(log =>
            log != null &&
            log.habitId == habitId &&
            log.dateKey == todayKey);

        if (todayLog != null)
        {
            if (!CanEditToday(todayLog.dateKey))
            {
                Debug.Log("[HabitLog] Không thể sửa log của ngày cũ.");
                return false;
            }

            todayLog.isCompleted = isCompleted;
            todayLog.completedAt = Timestamp.GetCurrentTimestamp();
            todayLog.isApproved = false;

            await FirebaseManager.Instance.UpdateHabitLogAsync(parentId, childId, todayLog.logId, todayLog);
            return true;
        }

        HabitLogModel newLog = new HabitLogModel
        {
            habitId = habitId,
            dateKey = todayKey,
            isCompleted = isCompleted,
            completedAt = Timestamp.GetCurrentTimestamp(),
            isApproved = false
        };

        await FirebaseManager.Instance.AddHabitLogAsync(parentId, childId, newLog);
        return true;
    }
}

