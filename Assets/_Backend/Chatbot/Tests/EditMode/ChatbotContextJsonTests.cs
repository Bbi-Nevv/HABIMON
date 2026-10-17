using NUnit.Framework;
using UnityEngine;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotContextJsonTests
    {
        [Test]
        public void SerializePreservesUnicodeAndEscapesUserText()
        {
            var context = new ChatbotContext
            {
                capturedAt = "2026-10-17T14:00:00+07:00",
                localDate = "2026-10-17",
                timeZone = "Asia/Ho_Chi_Minh",
                child = new ChatChildContext
                {
                    name = "Bé \"An\" \\",
                    age = 7,
                    gender = "female"
                },
                pet = null,
                habits = new[]
                {
                    new ChatHabitContext
                    {
                        title = "Đọc sách",
                        description = "Mỗi tối",
                        schedule = new ChatHabitScheduleContext
                        {
                            kind = "day_period",
                            value = "evening"
                        },
                        repeatDays = new[] { 1, 2, 3, 4, 5 },
                        logs = new[]
                        {
                            new ChatHabitLogContext
                            {
                                dateKey = "2026-10-17",
                                completedAt = "2026-10-17T13:00:00+07:00",
                                isCompleted = true,
                                isApproved = false
                            }
                        }
                    }
                },
                rewards = new ChatRewardContext[0]
            };

            string json = ChatbotContextJson.Serialize(context);

            Assert.That(JsonUtility.FromJson<ChatbotContext>(json), Is.Not.Null);
            StringAssert.Contains("Bé \\\"An\\\" \\\\", json);
            StringAssert.Contains("Đọc sách", json);
            StringAssert.Contains("\"schedule\":{\"kind\":\"day_period\",\"value\":\"evening\"}", json);
            StringAssert.DoesNotContain("timeTarget", json);
            StringAssert.Contains("\"pet\":null", json);
            StringAssert.Contains("\"isApproved\":false", json);
            StringAssert.DoesNotContain("childId", json);
            StringAssert.DoesNotContain("logId", json);
            StringAssert.DoesNotContain("gold", json);
        }
    }
}
