using System;
using System.Collections.Generic;
using System.Globalization;

namespace HABIMON.Chatbot
{
    public class ChatbotContextSource
    {
        public string capturedAt;
        public string localDate;
        public string timeZone;
        public ChatbotChildSource child;
        public ChatPetContext pet;
        public ChatbotHabitSource[] habits;
        public ChatbotHabitLogSource[] logs;
        public ChatbotRewardSource[] rewards;
    }

    public class ChatbotChildSource
    {
        public string name;
        public int age;
        public int genderCode;
    }

    public class ChatbotHabitSource
    {
        public string documentId;
        public string title;
        public string description;
        public string timeTarget;
        public int[] repeatDays;
        public int rewardGold;
        public int rewardExp;
        public bool requireApproval;
        public bool isActive;
    }

    public class ChatbotHabitLogSource
    {
        public string habitId;
        public string dateKey;
        public string completedAt;
        public bool isCompleted;
        public bool isApproved;
    }

    public class ChatbotRewardSource
    {
        public string name;
        public string itemType;
        public int cost;
        public bool isUnlocked;
    }

    public static class ChatbotContextAdapter
    {
        public static ChatbotContext Build(ChatbotContextSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ChatChildContext child = BuildChild(source.child);
            var result = new ChatbotContext
            {
                capturedAt = source.capturedAt,
                localDate = source.localDate,
                timeZone = source.timeZone,
                child = child,
                pet = child == null ? null : source.pet,
                habits = child == null ? null : BuildHabits(source.habits, source.logs),
                rewards = child == null ? null : BuildRewards(source.rewards)
            };
            return result;
        }

        private static ChatChildContext BuildChild(ChatbotChildSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.name) || source.age <= 0 ||
                string.Equals(source.name.Trim(), "Unnull", StringComparison.OrdinalIgnoreCase))
                return null;

            string gender = source.genderCode == 0 ? "female" : source.genderCode == 1 ? "male" : null;
            return new ChatChildContext
            {
                name = source.name.Trim(),
                age = source.age,
                gender = gender
            };
        }

        private static ChatHabitContext[] BuildHabits(
            ChatbotHabitSource[] habits,
            ChatbotHabitLogSource[] logs)
        {
            if (habits == null)
                return null;

            var result = new List<ChatHabitContext>(habits.Length);
            foreach (ChatbotHabitSource habit in habits)
            {
                if (habit == null)
                    continue;

                result.Add(new ChatHabitContext
                {
                    title = habit.title,
                    description = habit.description,
                    schedule = BuildSchedule(habit.timeTarget),
                    repeatDays = habit.repeatDays == null ? null : (int[])habit.repeatDays.Clone(),
                    rewardGold = habit.rewardGold,
                    rewardExp = habit.rewardExp,
                    requireApproval = habit.requireApproval,
                    isActive = habit.isActive,
                    logs = BuildLogsForHabit(habit.documentId, logs)
                });
            }

            return result.ToArray();
        }

        private static ChatHabitScheduleContext BuildSchedule(string timeTarget)
        {
            if (string.IsNullOrWhiteSpace(timeTarget))
                return null;

            string value = timeTarget.Trim();
            string period = value.ToLowerInvariant() switch
            {
                "buổi sáng" => "morning",
                "buổi chiều" => "afternoon",
                "buổi tối" => "evening",
                _ => null
            };
            if (period != null)
                return new ChatHabitScheduleContext { kind = "day_period", value = period };

            string[] timeFormats = { @"h\:mm", @"hh\:mm" };
            if (TimeSpan.TryParseExact(value, timeFormats, CultureInfo.InvariantCulture, out TimeSpan time) &&
                time >= TimeSpan.Zero && time < TimeSpan.FromDays(1))
            {
                return new ChatHabitScheduleContext
                {
                    kind = "local_time",
                    value = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture)
                };
            }

            return new ChatHabitScheduleContext { kind = "unclassified", value = value };
        }

        private static ChatHabitLogContext[] BuildLogsForHabit(
            string habitDocumentId,
            ChatbotHabitLogSource[] logs)
        {
            if (logs == null)
                return null;

            var result = new List<ChatHabitLogContext>();
            foreach (ChatbotHabitLogSource log in logs)
            {
                if (log == null || string.IsNullOrEmpty(habitDocumentId) ||
                    !string.Equals(log.habitId, habitDocumentId, StringComparison.Ordinal))
                    continue;

                result.Add(new ChatHabitLogContext
                {
                    dateKey = log.dateKey,
                    completedAt = log.completedAt,
                    isCompleted = log.isCompleted,
                    isApproved = log.isApproved
                });
            }

            return result.ToArray();
        }

        private static ChatRewardContext[] BuildRewards(ChatbotRewardSource[] rewards)
        {
            if (rewards == null)
                return null;

            var result = new List<ChatRewardContext>(rewards.Length);
            foreach (ChatbotRewardSource reward in rewards)
            {
                if (reward == null)
                    continue;

                result.Add(new ChatRewardContext
                {
                    name = reward.name,
                    itemType = reward.itemType,
                    cost = reward.cost,
                    isUnlocked = reward.isUnlocked
                });
            }

            return result.ToArray();
        }
    }
}
