using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HABIMON.Chatbot
{
    public static class ChatbotContextJson
    {
        public static string Serialize(ChatbotContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var json = new StringBuilder();
            json.Append('{');
            AppendStringProperty(json, "capturedAt", context.capturedAt);
            AppendStringProperty(json, "localDate", context.localDate);
            AppendStringProperty(json, "timeZone", context.timeZone);
            AppendPropertyName(json, "child");
            AppendChild(json, context.child);
            json.Append(',');
            AppendPropertyName(json, "pet");
            AppendPet(json, context.pet);
            json.Append(',');
            AppendPropertyName(json, "habits");
            AppendArray(json, context.habits, AppendHabit);
            json.Append(',');
            AppendPropertyName(json, "rewards");
            AppendArray(json, context.rewards, AppendReward);
            json.Append('}');
            return json.ToString();
        }

        internal static string Quote(string value)
        {
            var json = new StringBuilder();
            if (value == null)
            {
                json.Append("null");
            }
            else
            {
                AppendQuoted(json, value);
            }
            return json.ToString();
        }

        private static void AppendChild(StringBuilder json, ChatChildContext child)
        {
            if (child == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "name", child.name);
            AppendNumberProperty(json, "age", child.age);
            AppendStringProperty(json, "gender", child.gender);
            RemoveTrailingComma(json);
            json.Append('}');
        }

        private static void AppendPet(StringBuilder json, ChatPetContext pet)
        {
            if (pet == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "petType", pet.petType);
            AppendStringProperty(json, "petName", pet.petName);
            AppendNumberProperty(json, "level", pet.level);
            AppendNumberProperty(json, "currentExp", pet.currentExp);
            AppendNumberProperty(json, "maxExp", pet.maxExp);
            AppendStringProperty(json, "stage", pet.stage);
            AppendNumberProperty(json, "health", pet.health);
            AppendNumberProperty(json, "hunger", pet.hunger);
            AppendNumberProperty(json, "happiness", pet.happiness);
            RemoveTrailingComma(json);
            json.Append('}');
        }

        private static void AppendHabit(StringBuilder json, ChatHabitContext habit)
        {
            if (habit == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "title", habit.title);
            AppendStringProperty(json, "description", habit.description);
            AppendPropertyName(json, "schedule");
            AppendSchedule(json, habit.schedule);
            json.Append(',');
            AppendPropertyName(json, "repeatDays");
            AppendArray(json, habit.repeatDays, AppendInteger);
            json.Append(',');
            AppendNumberProperty(json, "rewardGold", habit.rewardGold);
            AppendNumberProperty(json, "rewardExp", habit.rewardExp);
            AppendBooleanProperty(json, "requireApproval", habit.requireApproval);
            AppendBooleanProperty(json, "isActive", habit.isActive);
            AppendPropertyName(json, "logs");
            AppendArray(json, habit.logs, AppendLog);
            json.Append('}');
        }

        private static void AppendSchedule(StringBuilder json, ChatHabitScheduleContext schedule)
        {
            if (schedule == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "kind", schedule.kind);
            AppendStringProperty(json, "value", schedule.value);
            RemoveTrailingComma(json);
            json.Append('}');
        }

        private static void AppendLog(StringBuilder json, ChatHabitLogContext log)
        {
            if (log == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "dateKey", log.dateKey);
            AppendStringProperty(json, "completedAt", log.completedAt);
            AppendBooleanProperty(json, "isCompleted", log.isCompleted);
            AppendBooleanProperty(json, "isApproved", log.isApproved);
            RemoveTrailingComma(json);
            json.Append('}');
        }

        private static void AppendReward(StringBuilder json, ChatRewardContext reward)
        {
            if (reward == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            AppendStringProperty(json, "name", reward.name);
            AppendStringProperty(json, "itemType", reward.itemType);
            AppendNumberProperty(json, "cost", reward.cost);
            AppendBooleanProperty(json, "isUnlocked", reward.isUnlocked);
            RemoveTrailingComma(json);
            json.Append('}');
        }

        private static void AppendStringProperty(StringBuilder json, string name, string value)
        {
            AppendPropertyName(json, name);
            if (value == null)
                json.Append("null");
            else
                AppendQuoted(json, value);
            json.Append(',');
        }

        private static void AppendNumberProperty(StringBuilder json, string name, int value)
        {
            AppendPropertyName(json, name);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
            json.Append(',');
        }

        private static void AppendBooleanProperty(StringBuilder json, string name, bool value)
        {
            AppendPropertyName(json, name);
            json.Append(value ? "true" : "false");
            json.Append(',');
        }

        private static void AppendPropertyName(StringBuilder json, string name)
        {
            AppendQuoted(json, name);
            json.Append(':');
        }

        private static void AppendArray<T>(StringBuilder json, IReadOnlyList<T> values, Action<StringBuilder, T> appendValue)
        {
            if (values == null)
            {
                json.Append("null");
                return;
            }

            json.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                appendValue(json, values[i]);
            }
            json.Append(']');
        }

        private static void AppendInteger(StringBuilder json, int value)
        {
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void RemoveTrailingComma(StringBuilder json)
        {
            if (json.Length > 0 && json[json.Length - 1] == ',')
                json.Length--;
        }

        private static void AppendQuoted(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            json.Append("\\u");
                            json.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            json.Append(character);
                        }
                        break;
                }
            }
            json.Append('"');
        }
    }
}
