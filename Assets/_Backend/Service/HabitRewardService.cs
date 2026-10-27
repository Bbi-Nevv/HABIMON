using Firebase.Firestore;
using HABIMON.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed class PendingHabitApproval
{
    public HabitLogModel Log { get; set; }
    public HabitModel Habit { get; set; }
}

public static class HabitRewardService
{
    public static async Task<List<PendingHabitApproval>> GetPendingApprovalsAsync(string parentId, string childId)
    {
        FirebaseManager manager = await EnsureCurrentScopeAsync(parentId, childId);
        if (manager == null)
            return new List<PendingHabitApproval>();

        int sessionGeneration = manager.SessionGeneration;
        DocumentReference childRef = GetChildReference(FirebaseFirestore.DefaultInstance, parentId, childId);
        QuerySnapshot logSnapshot = await childRef.Collection("habit_logs")
            .WhereEqualTo("isCompleted", true)
            .GetSnapshotAsync();
        if (!IsCurrentScope(manager, parentId, childId, sessionGeneration))
            return new List<PendingHabitApproval>();

        List<HabitModel> habits = await manager.GetHabitsAsync(parentId, childId);
        var habitsById = new Dictionary<string, HabitModel>(StringComparer.Ordinal);
        foreach (HabitModel habit in habits)
        {
            if (habit != null && !string.IsNullOrEmpty(habit.id))
                habitsById[habit.id] = habit;
        }

        var pending = new List<PendingHabitApproval>();
        foreach (DocumentSnapshot document in logSnapshot.Documents)
        {
            if (!document.Exists)
                continue;

            HabitLogModel log = document.ConvertTo<HabitLogModel>();
            if (log == null || !log.isCompleted || log.isApproved)
                continue;

            log.logId = document.Id;
            habitsById.TryGetValue(log.habitId ?? string.Empty, out HabitModel habit);
            bool requiresApproval = log.requiresApprovalAtCompletion ?? (habit != null && habit.requireApproval);
            if (requiresApproval)
                pending.Add(new PendingHabitApproval { Log = log, Habit = habit });
        }

        return pending.OrderByDescending(item => item.Log.dateKey, StringComparer.Ordinal)
            .ThenBy(item => item.Habit?.title ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    public static async Task<bool> SetTodayCompletionAsync(
        string parentId, string childId, string habitId, string dateKey, bool isCompleted)
    {
        if (string.IsNullOrWhiteSpace(habitId) || dateKey != HabitLog.GetTodayDateKey() ||
            !DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime businessDate))
            return false;

        FirebaseManager manager = await EnsureCurrentScopeAsync(parentId, childId);
        if (manager == null)
            return false;

        int sessionGeneration = manager.SessionGeneration;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference childRef = GetChildReference(db, parentId, childId);
        DocumentReference habitRef = childRef.Collection("habits").Document(habitId);
        string logId = HabitLog.GetDailyLogDocumentId(habitId, dateKey);
        DocumentReference logRef = childRef.Collection("habit_logs").Document(logId);

        try
        {
            List<DocumentReference> legacyReferences = await GetLegacyDailyReferencesAsync(childRef, habitId, dateKey, logId);
            return await db.RunTransactionAsync(async transaction =>
            {
                if (!IsCurrentScope(manager, parentId, childId, sessionGeneration))
                    return false;

                if (await HasExistingLegacyLogAsync(transaction, legacyReferences))
                    return false;
                DocumentSnapshot logSnapshot = await transaction.GetSnapshotAsync(logRef);
                if (!IsCurrentScope(manager, parentId, childId, sessionGeneration))
                    return false;
                HabitLogModel existingLog = logSnapshot.Exists ? logSnapshot.ConvertTo<HabitLogModel>() : null;
                if (existingLog != null && existingLog.isCompleted &&
                    (existingLog.rewardGranted || existingLog.isApproved))
                    return isCompleted;

                if (!isCompleted)
                {
                    if (existingLog == null || !existingLog.isCompleted)
                        return true;
                    if (!IsCurrentScope(manager, parentId, childId, sessionGeneration) || dateKey != HabitLog.GetTodayDateKey())
                        return false;

                    transaction.Update(logRef, new Dictionary<string, object>
                    {
                        { "isCompleted", false }, { "isApproved", false },
                        { "approvedAt", null }, { "completedAt", null },
                        { "requiresApprovalAtCompletion", null },
                        { "rewardGoldAtCompletion", null }, { "rewardExpAtCompletion", null },
                        { "rewardGranted", false }, { "rewardGoldGranted", 0 },
                        { "rewardExpGranted", 0 }, { "rewardedAt", null },
                        { "updatedAt", FieldValue.ServerTimestamp }
                    });
                    return true;
                }

                DocumentSnapshot habitSnapshot = await transaction.GetSnapshotAsync(habitRef);
                if (!habitSnapshot.Exists)
                    return false;
                HabitModel habit = habitSnapshot.ConvertTo<HabitModel>();
                if (!HabitLog.IsScheduledForDate(habit, businessDate) ||
                    habit.rewardGold < 0 || habit.rewardExp < 0)
                    return false;
                if (existingLog != null && existingLog.isCompleted)
                    return true;

                bool requiresApproval = habit.requireApproval;
                bool rewardGranted = !requiresApproval;
                int grantedGold = rewardGranted ? habit.rewardGold : 0;
                int grantedExp = rewardGranted ? habit.rewardExp : 0;
                PetProgressionResult progression = null;
                int newGold = 0;
                int newTotalExp = 0;

                DocumentReference petRef = childRef.Collection("habimon").Document("status");
                if (rewardGranted)
                {
                    DocumentSnapshot childSnapshot = await transaction.GetSnapshotAsync(childRef);
                    DocumentSnapshot petSnapshot = await transaction.GetSnapshotAsync(petRef);
                    if (!childSnapshot.Exists || !petSnapshot.Exists)
                        return false;

                    ChildModel child = childSnapshot.ConvertTo<ChildModel>();
                    HabimonModel pet = petSnapshot.ConvertTo<HabimonModel>();
                    newGold = checked(child.gold + grantedGold);
                    newTotalExp = checked(child.totalExp + grantedExp);
                    progression = HabitRewardProgression.Apply(pet.level, pet.currentExp, pet.maxExp, grantedExp);
                }

                if (!IsCurrentScope(manager, parentId, childId, sessionGeneration) || dateKey != HabitLog.GetTodayDateKey())
                    return false;
                if (rewardGranted)
                {
                    transaction.Update(childRef, new Dictionary<string, object>
                    {
                        { "gold", newGold }, { "totalExp", newTotalExp }
                    });
                    transaction.Update(petRef, new Dictionary<string, object>
                    {
                        { "level", progression.Level }, { "currentExp", progression.CurrentExp },
                        { "maxExp", progression.MaxExp }
                    });
                }
                transaction.Set(logRef, CreateCompletedLogValues(
                    existingLog, habit, dateKey, requiresApproval, rewardGranted, grantedGold, grantedExp),
                    SetOptions.MergeAll);
                return true;
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HabitRewardService] Không thể lưu completion: {ex.Message}");
            return false;
        }
    }

    public static Task<bool> ApprovePendingLogAsync(string parentId, string childId, string logId)
    {
        return ApprovePendingLogCoreAsync(parentId, childId, logId, null, true);
    }

    public static Task<bool> ApprovePendingLogForChildAsync(
        string parentId, string childId, string logId, Timestamp expectedCompletedAt)
    {
        return ApprovePendingLogCoreAsync(parentId, childId, logId, expectedCompletedAt, false);
    }

    private static async Task<bool> ApprovePendingLogCoreAsync(
        string parentId, string childId, string logId, Timestamp? expectedCompletedAt, bool requireSelectedChild)
    {
        if (string.IsNullOrWhiteSpace(logId) || string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(parentId))
            return false;

        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || !await manager.EnsureInitializedAsync() || manager.GetCurrentParentId() != parentId ||
            (requireSelectedChild && manager.GetCurrentChildId() != childId))
            return false;

        int sessionGeneration = manager.SessionGeneration;
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference childRef = GetChildReference(db, parentId, childId);
        DocumentReference logRef = childRef.Collection("habit_logs").Document(logId);
        try
        {
            DocumentSnapshot candidateSnapshot = await logRef.GetSnapshotAsync();
            if (!candidateSnapshot.Exists) return false;
            HabitLogModel candidate = candidateSnapshot.ConvertTo<HabitLogModel>();
            if (string.IsNullOrWhiteSpace(candidate.habitId) || string.IsNullOrWhiteSpace(candidate.dateKey) ||
                logId != HabitLog.GetDailyLogDocumentId(candidate.habitId, candidate.dateKey))
                return false;
            List<DocumentReference> legacyReferences = await GetLegacyDailyReferencesAsync(
                childRef, candidate.habitId, candidate.dateKey, logId);
            return await db.RunTransactionAsync(async transaction =>
            {
                if (!IsApprovalScopeCurrent(manager, parentId, childId, sessionGeneration, requireSelectedChild))
                    return false;

                if (await HasExistingLegacyLogAsync(transaction, legacyReferences))
                    return false;
                DocumentSnapshot logSnapshot = await transaction.GetSnapshotAsync(logRef);
                if (!logSnapshot.Exists)
                    return false;
                HabitLogModel log = logSnapshot.ConvertTo<HabitLogModel>();
                if (log.habitId != candidate.habitId || log.dateKey != candidate.dateKey ||
                    !log.completedAt.HasValue || (expectedCompletedAt.HasValue && log.completedAt != expectedCompletedAt))
                    return false;
                if (log.rewardGranted)
                    return log.isApproved;
                if (!log.isCompleted || log.isApproved || string.IsNullOrWhiteSpace(log.habitId))
                    return false;

                DocumentReference habitRef = childRef.Collection("habits").Document(log.habitId);
                DocumentReference petRef = childRef.Collection("habimon").Document("status");
                DocumentSnapshot habitSnapshot = await transaction.GetSnapshotAsync(habitRef);
                DocumentSnapshot childSnapshot = await transaction.GetSnapshotAsync(childRef);
                DocumentSnapshot petSnapshot = await transaction.GetSnapshotAsync(petRef);
                if (!habitSnapshot.Exists || !childSnapshot.Exists || !petSnapshot.Exists)
                    return false;

                HabitModel habit = habitSnapshot.ConvertTo<HabitModel>();
                bool requiresApproval = log.requiresApprovalAtCompletion ?? habit.requireApproval;
                if (!requiresApproval)
                    return false;
                int rewardGold = log.rewardGoldAtCompletion ?? habit.rewardGold;
                int rewardExp = log.rewardExpAtCompletion ?? habit.rewardExp;
                if (rewardGold < 0 || rewardExp < 0)
                    return false;

                ChildModel child = childSnapshot.ConvertTo<ChildModel>();
                HabimonModel pet = petSnapshot.ConvertTo<HabimonModel>();
                int newGold = checked(child.gold + rewardGold);
                int newTotalExp = checked(child.totalExp + rewardExp);
                PetProgressionResult progression = HabitRewardProgression.Apply(
                    pet.level, pet.currentExp, pet.maxExp, rewardExp);

                if (!IsApprovalScopeCurrent(manager, parentId, childId, sessionGeneration, requireSelectedChild))
                    return false;
                transaction.Update(logRef, new Dictionary<string, object>
                {
                    { "isApproved", true }, { "approvedAt", FieldValue.ServerTimestamp },
                    { "updatedAt", FieldValue.ServerTimestamp }, { "rewardGranted", true },
                    { "rewardGoldGranted", rewardGold }, { "rewardExpGranted", rewardExp },
                    { "rewardedAt", FieldValue.ServerTimestamp },
                    { "rewardGoldAtCompletion", rewardGold }, { "rewardExpAtCompletion", rewardExp }
                });
                transaction.Update(childRef, new Dictionary<string, object>
                {
                    { "gold", newGold }, { "totalExp", newTotalExp }
                });
                transaction.Update(petRef, new Dictionary<string, object>
                {
                    { "level", progression.Level }, { "currentExp", progression.CurrentExp },
                    { "maxExp", progression.MaxExp }
                });
                return true;
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HabitRewardService] Không thể duyệt completion: {ex.Message}");
            return false;
        }
    }

    private static bool IsApprovalScopeCurrent(
        FirebaseManager manager, string parentId, string childId, int generation, bool requireSelectedChild)
    {
        return manager != null && manager == FirebaseManager.Instance &&
            manager.SessionGeneration == generation && manager.GetCurrentParentId() == parentId &&
            (!requireSelectedChild || manager.GetCurrentChildId() == childId);
    }

    private static async Task<List<DocumentReference>> GetLegacyDailyReferencesAsync(
        DocumentReference childRef, string habitId, string dateKey, string canonicalId)
    {
        QuerySnapshot snapshot = await childRef.Collection("habit_logs")
            .WhereEqualTo("habitId", habitId).WhereEqualTo("dateKey", dateKey).GetSnapshotAsync();
        return snapshot.Documents.Where(document => document.Id != canonicalId)
            .Select(document => document.Reference).ToList();
    }

    private static async Task<bool> HasExistingLegacyLogAsync(
        Transaction transaction, List<DocumentReference> references)
    {
        foreach (DocumentReference reference in references)
        {
            if ((await transaction.GetSnapshotAsync(reference)).Exists)
                return true;
        }
        return false;
    }

    private static async Task<FirebaseManager> EnsureCurrentScopeAsync(string parentId, string childId)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || !await manager.EnsureInitializedAsync())
            return null;
        return IsCurrentScope(manager, parentId, childId, manager.SessionGeneration) ? manager : null;
    }

    private static bool IsCurrentScope(FirebaseManager manager, string parentId, string childId, int generation)
    {
        return manager != null && manager == FirebaseManager.Instance &&
            manager.SessionGeneration == generation &&
            manager.GetCurrentParentId() == parentId && manager.GetCurrentChildId() == childId;
    }

    private static DocumentReference GetChildReference(FirebaseFirestore db, string parentId, string childId)
    {
        return db.Collection("users").Document(parentId).Collection("children").Document(childId);
    }

    private static Dictionary<string, object> CreateCompletedLogValues(
        HabitLogModel existingLog, HabitModel habit, string dateKey, bool requiresApproval,
        bool rewardGranted, int rewardGold, int rewardExp)
    {
        return new Dictionary<string, object>
        {
            { "habitId", habit.id }, { "dateKey", dateKey }, { "isCompleted", true },
            { "isApproved", !requiresApproval },
            { "requiresApprovalAtCompletion", requiresApproval },
            { "rewardGoldAtCompletion", habit.rewardGold }, { "rewardExpAtCompletion", habit.rewardExp },
            { "approvedAt", requiresApproval ? null : (object)FieldValue.ServerTimestamp },
            { "rewardGranted", rewardGranted }, { "rewardGoldGranted", rewardGold },
            { "rewardExpGranted", rewardExp },
            { "rewardedAt", rewardGranted ? (object)FieldValue.ServerTimestamp : null },
            { "createdAt", existingLog != null && existingLog.createdAt.HasValue
                ? (object)existingLog.createdAt.Value : FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp }, { "completedAt", FieldValue.ServerTimestamp }
        };
    }
}
