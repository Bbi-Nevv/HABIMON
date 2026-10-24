using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using HABIMON.Chatbot;

public sealed class FirebaseChatbotContextSnapshot : IDisposable
{
    private readonly FirebaseManager manager;
    private bool disposed;

    public ChatbotSessionScope Scope { get; }
    public ChatbotContext Context { get; }
    public ChatbotApiCancellation Cancellation { get; } = new ChatbotApiCancellation();
    public bool IsCurrent => !disposed && IsManagerScopeCurrent();

    internal FirebaseChatbotContextSnapshot(
        FirebaseManager manager,
        ChatbotSessionScope scope,
        ChatbotContext context)
    {
        this.manager = manager;
        Scope = scope;
        Context = context;
        manager.CurrentChildChanged += OnScopeChanged;
        manager.SessionChanged += OnScopeChanged;
        OnScopeChanged();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        manager.CurrentChildChanged -= OnScopeChanged;
        manager.SessionChanged -= OnScopeChanged;
        Cancellation.Cancel();
    }

    private void OnScopeChanged()
    {
        if (!IsManagerScopeCurrent())
            Cancellation.Cancel();
    }

    private bool IsManagerScopeCurrent()
    {
        return Scope.IsCurrent(
            manager.GetCurrentParentId(),
            manager.GetCurrentChildId(),
            manager.SessionGeneration);
    }
}

public static class FirebaseChatbotContextAdapter
{
    public static async Task<FirebaseChatbotContextSnapshot> LoadCurrentSnapshotAsync(
        FirebaseManager manager,
        string historyStartDateInclusive,
        string historyEndDateExclusive,
        DateTimeOffset capturedAt,
        TimeZoneInfo timeZone)
    {
        if (!IsValidDateRange(historyStartDateInclusive, historyEndDateExclusive))
            throw new ArgumentException("Chatbot history range must use ascending yyyy-MM-dd dates.");

        if (manager == null || !await manager.EnsureInitializedAsync())
            return null;

        var scope = new ChatbotSessionScope(
            manager.GetCurrentParentId(),
            manager.GetCurrentChildId(),
            manager.SessionGeneration);
        if (!scope.IsValid)
            return null;

        List<ChildModel> children = await manager.GetChildrenAsync(scope.ParentId);
        if (!IsCurrent(manager, scope))
            return null;

        ChildModel selectedChild = children?.FirstOrDefault(child =>
            child != null && string.Equals(child.childId, scope.ChildId, StringComparison.Ordinal));
        if (selectedChild == null)
            return null;

        Task<List<HabitModel>> habitsTask = manager.GetHabitsAsync(scope.ParentId, scope.ChildId);
        Task<List<HabitLogModel>> logsTask = manager.GetHabitLogsInRangeAsync(
            scope.ParentId,
            scope.ChildId,
            historyStartDateInclusive,
            historyEndDateExclusive);
        Task<List<RewardModel>> rewardsTask = manager.GetRewardsAsync(scope.ParentId, scope.ChildId);
        Task<DocumentSnapshot> petTask = FirebaseFirestore.DefaultInstance
            .Collection("users")
            .Document(scope.ParentId)
            .Collection("children")
            .Document(scope.ChildId)
            .Collection("habimon")
            .Document("status")
            .GetSnapshotAsync();

        await Task.WhenAll(habitsTask, logsTask, rewardsTask, petTask);
        if (!IsCurrent(manager, scope))
            return null;

        DocumentSnapshot petSnapshot = await petTask;
        HabimonModel currentPet = petSnapshot.Exists ? petSnapshot.ConvertTo<HabimonModel>() : null;
        ChatbotContext context = Build(
            selectedChild,
            currentPet,
            await habitsTask,
            await logsTask,
            await rewardsTask,
            capturedAt,
            timeZone);

        return new FirebaseChatbotContextSnapshot(manager, scope, context);
    }

    private static bool IsCurrent(FirebaseManager manager, ChatbotSessionScope scope)
    {
        return scope.IsCurrent(
            manager.GetCurrentParentId(),
            manager.GetCurrentChildId(),
            manager.SessionGeneration);
    }

    private static bool IsValidDateRange(string startDateInclusive, string endDateExclusive)
    {
        const string dateFormat = "yyyy-MM-dd";
        return DateTime.TryParseExact(
                startDateInclusive,
                dateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime startDate) &&
            DateTime.TryParseExact(
                endDateExclusive,
                dateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime endDate) &&
            startDate < endDate;
    }

    private static ChatbotContext Build(
        ChildModel selectedChild,
        HabimonModel currentPet,
        IEnumerable<HabitModel> habits,
        IEnumerable<HabitLogModel> logs,
        IEnumerable<RewardModel> rewards,
        DateTimeOffset capturedAt,
        TimeZoneInfo timeZone)
    {
        TimeZoneInfo selectedTimeZone = timeZone ?? TimeZoneInfo.Local;
        DateTimeOffset localCapture = TimeZoneInfo.ConvertTime(capturedAt, selectedTimeZone);
        HabitModel[] habitItems = habits?.Where(item => item != null).ToArray();
        HabitLogModel[] logItems = logs?.Where(item => item != null).ToArray();
        RewardModel[] rewardItems = rewards?.Where(item => item != null).ToArray();

        var source = new ChatbotContextSource
        {
            capturedAt = localCapture.ToString("O", CultureInfo.InvariantCulture),
            localDate = localCapture.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = selectedTimeZone.Id,
            child = selectedChild == null ? null : new ChatbotChildSource
            {
                name = selectedChild.name,
                age = selectedChild.age,
                genderCode = selectedChild.gender
            },
            pet = currentPet == null ? null : new ChatPetContext
            {
                petType = currentPet.petType,
                petName = currentPet.petName,
                level = currentPet.level,
                currentExp = currentPet.currentExp,
                maxExp = currentPet.maxExp,
                stage = currentPet.stage,
                health = currentPet.health,
                hunger = currentPet.hunger,
                happiness = currentPet.happiness
            },
            habits = habitItems == null ? null : habitItems.Select(habit => new ChatbotHabitSource
            {
                documentId = habit.id,
                title = habit.title,
                description = habit.description,
                scheduleKind = habit.scheduleKind,
                scheduleValue = habit.scheduleValue,
                timeTarget = habit.timeTarget,
                repeatDays = habit.repeatDays?.ToArray(),
                rewardGold = habit.rewardGold,
                rewardExp = habit.rewardExp,
                requireApproval = habit.requireApproval,
                isActive = habit.isActive
            }).ToArray(),
            logs = logItems == null ? null : logItems.Select(log => new ChatbotHabitLogSource
            {
                habitId = log.habitId,
                dateKey = log.dateKey,
                completedAt = TimestampToIso8601(log.completedAt),
                isCompleted = log.isCompleted,
                isApproved = log.isApproved
            }).ToArray(),
            rewards = rewardItems == null ? null : rewardItems.Select(reward => new ChatbotRewardSource
            {
                name = reward.name,
                itemType = reward.itemType,
                cost = reward.cost,
                isUnlocked = reward.isUnlocked
            }).ToArray()
        };

        return ChatbotContextAdapter.Build(source);
    }

    private static string TimestampToIso8601(Timestamp? timestamp)
    {
        if (!timestamp.HasValue)
            return null;

        return new DateTimeOffset(timestamp.Value.ToDateTime().ToUniversalTime())
            .ToString("O", CultureInfo.InvariantCulture);
    }
}
