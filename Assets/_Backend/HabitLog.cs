using Firebase.Firestore;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class HabitLog : MonoBehaviour
{
    public static string GetTodayDateKey()
    {
        return DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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

        string todayKey = GetTodayDateKey();
        return await FirebaseManager.Instance.GetHabitLogAsync(parentId, childId, habitId, todayKey);
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

        string todayKey = GetTodayDateKey();
        return await FirebaseManager.Instance.SetHabitCompletionAsync(parentId, childId, habitId, todayKey, isCompleted);
    }

    public static Task<bool> ApproveHabitLogAsync(string parentId, string childId, string habitId, string dateKey, Timestamp expectedCompletedAt)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
            return Task.FromResult(false);

        return FirebaseManager.Instance.ApproveHabitLogAsync(parentId, childId, habitId, dateKey, expectedCompletedAt);
    }
}

