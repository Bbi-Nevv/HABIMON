using NUnit.Framework;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotContextAdapterTests
    {
        [Test]
        public void BuildMapsChildJoinsLogsAndOmitsFirestoreDocumentIds()
        {
            var source = new ChatbotContextSource
            {
                capturedAt = "2026-10-17T14:00:00+07:00",
                localDate = "2026-10-17",
                timeZone = "Asia/Ho_Chi_Minh",
                child = new ChatbotChildSource
                {
                    name = " An ",
                    age = 7,
                    genderCode = 0
                },
                pet = null,
                habits = new[]
                {
                    new ChatbotHabitSource
                    {
                        documentId = "private-habit-id",
                        title = "Đọc sách",
                        description = "Đọc truyện\ntrước khi ngủ",
                        timeTarget = "Buổi Tối",
                        repeatDays = new[] { 1, 3, 5 },
                        rewardGold = 15,
                        rewardExp = 25,
                        requireApproval = true,
                        isActive = true
                    }
                },
                logs = new[]
                {
                    new ChatbotHabitLogSource
                    {
                        habitId = "private-habit-id",
                        dateKey = "2026-10-17",
                        completedAt = "2026-10-17T13:00:00+07:00",
                        isCompleted = true,
                        isApproved = false
                    },
                    new ChatbotHabitLogSource
                    {
                        habitId = "deleted-habit-id",
                        dateKey = "2026-10-16",
                        isCompleted = false,
                        isApproved = false
                    }
                },
                rewards = new[]
                {
                    new ChatbotRewardSource
                    {
                        name = "Móc khóa thú cưng",
                        itemType = "item",
                        cost = 50,
                        isUnlocked = false
                    }
                }
            };

            ChatbotContext context = ChatbotContextAdapter.Build(source);
            string json = ChatbotContextJson.Serialize(context);

            Assert.That(context.child, Is.Not.Null);
            Assert.That(context.child.name, Is.EqualTo("An"));
            Assert.That(context.child.gender, Is.EqualTo("female"));
            Assert.That(context.pet, Is.Null);
            Assert.That(context.habits, Has.Length.EqualTo(1));
            Assert.That(context.habits[0].schedule.kind, Is.EqualTo("day_period"));
            Assert.That(context.habits[0].schedule.value, Is.EqualTo("evening"));
            Assert.That(context.habits[0].logs, Has.Length.EqualTo(1));
            Assert.That(context.habits[0].logs[0].isApproved, Is.False);
            StringAssert.Contains("\\n", json);
            StringAssert.DoesNotContain("private-habit-id", json);
            StringAssert.DoesNotContain("deleted-habit-id", json);
        }

        [Test]
        public void BuildNormalizesClockAndLeavesUnknownScheduleUnclassified()
        {
            var source = new ChatbotContextSource
            {
                child = new ChatbotChildSource { name = "An", age = 7 },
                habits = new[]
                {
                    new ChatbotHabitSource { timeTarget = "8:05" },
                    new ChatbotHabitSource { timeTarget = "20:00" },
                    new ChatbotHabitSource { timeTarget = "Buổi Sáng" },
                    new ChatbotHabitSource { timeTarget = "Buổi Chiều" },
                    new ChatbotHabitSource { timeTarget = "after dinner" },
                    new ChatbotHabitSource { timeTarget = "  " }
                }
            };

            ChatbotContext context = ChatbotContextAdapter.Build(source);

            Assert.That(context.habits[0].schedule.kind, Is.EqualTo("local_time"));
            Assert.That(context.habits[0].schedule.value, Is.EqualTo("08:05"));
            Assert.That(context.habits[1].schedule.kind, Is.EqualTo("local_time"));
            Assert.That(context.habits[1].schedule.value, Is.EqualTo("20:00"));
            Assert.That(context.habits[2].schedule.value, Is.EqualTo("morning"));
            Assert.That(context.habits[3].schedule.value, Is.EqualTo("afternoon"));
            Assert.That(context.habits[4].schedule.kind, Is.EqualTo("unclassified"));
            Assert.That(context.habits[4].schedule.value, Is.EqualTo("after dinner"));
            Assert.That(context.habits[5].schedule, Is.Null);
        }

        [Test]
        public void BuildTreatsDefaultChildPlaceholderAsUnknown()
        {
            var source = new ChatbotContextSource
            {
                child = new ChatbotChildSource
                {
                    name = "Unnull",
                    age = 0,
                    genderCode = 1
                },
                pet = new ChatPetContext { petType = "fire_dragon" },
                habits = new[] { new ChatbotHabitSource { documentId = "habit-id" } },
                logs = new[] { new ChatbotHabitLogSource { habitId = "habit-id" } },
                rewards = new[] { new ChatbotRewardSource { name = "reward" } }
            };

            ChatbotContext context = ChatbotContextAdapter.Build(source);

            Assert.That(context.child, Is.Null);
            Assert.That(context.pet, Is.Null);
            Assert.That(context.habits, Is.Null);
            Assert.That(context.rewards, Is.Null);
        }
    }
}
