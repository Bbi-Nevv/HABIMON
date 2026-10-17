using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Firebase.Firestore;
using HABIMON.Chatbot;

public static class FirebaseChatbotContextAdapter
{
    public static ChatbotContext Build(
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

    private static string TimestampToIso8601(Timestamp timestamp)
    {
        if (timestamp == null)
            return null;

        return new DateTimeOffset(timestamp.ToDateTime().ToUniversalTime())
            .ToString("O", CultureInfo.InvariantCulture);
    }
}
