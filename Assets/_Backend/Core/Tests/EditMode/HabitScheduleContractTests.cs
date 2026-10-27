using System;
using System.Collections.Generic;
using HABIMON.Core;
using NUnit.Framework;

namespace HABIMON.Core.Tests
{
    public class HabitScheduleContractTests
    {
        [Test]
        public void LegacyClockTwentyMapsToEvening()
        {
            HabitSchedule schedule = HabitSchedule.FromStorage(null, null, "20:00");

            Assert.That(schedule.Kind, Is.EqualTo("local_time"));
            Assert.That(schedule.Value, Is.EqualTo("20:00"));
            Assert.That(schedule.MatchesPeriod("Buổi Tối"), Is.True);
            Assert.That(schedule.MatchesPeriod("Buổi Chiều"), Is.False);
        }

        [TestCase("04:59", "Buổi Tối")]
        [TestCase("05:00", "Buổi Sáng")]
        [TestCase("11:59", "Buổi Sáng")]
        [TestCase("12:00", "Buổi Chiều")]
        [TestCase("17:59", "Buổi Chiều")]
        [TestCase("18:00", "Buổi Tối")]
        public void LocalTimePeriodBoundariesAreConsistent(string localTime, string expectedPeriod)
        {
            HabitSchedule schedule = HabitSchedule.FromStorage("local_time", localTime, null);
            Assert.That(schedule.MatchesPeriod(expectedPeriod), Is.True);
        }

        [Test]
        public void BusinessDateChangesAtVietnamMidnightAndUsesInvariantKeys()
        {
            DateTimeOffset beforeMidnightUtc = new DateTimeOffset(2026, 10, 17, 16, 59, 0, TimeSpan.Zero);
            DateTimeOffset midnightUtc = new DateTimeOffset(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);

            Assert.That(BusinessCalendar.GetDateKey(beforeMidnightUtc), Is.EqualTo("2026-10-17"));
            Assert.That(BusinessCalendar.GetDateKey(midnightUtc), Is.EqualTo("2026-10-18"));
        }

        [Test]
        public void ChildSelectionKeepsValidCurrentThenRememberedThenStableFallback()
        {
            var available = new List<string> { "child-b", "child-a" };

            Assert.That(ChildSelectionPolicy.Choose("child-b", "child-a", available), Is.EqualTo("child-b"));
            Assert.That(ChildSelectionPolicy.Choose("missing", "child-b", available), Is.EqualTo("child-b"));
            Assert.That(ChildSelectionPolicy.Choose("missing", "deleted", available), Is.EqualTo("child-a"));
            Assert.That(ChildSelectionPolicy.Choose(null, null, new List<string>()), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ScheduleUsesIsoWeekdaysAndEmptyScheduleMeansNoDays()
        {
            HabitSchedule schedule = HabitSchedule.FromStorage("day_period", "morning", null);
            DateTime monday = new DateTime(2026, 10, 19);
            DateTime sunday = new DateTime(2026, 10, 25);

            Assert.That(schedule.IsScheduledOn(monday, new[] { 1, 3, 5 }), Is.True);
            Assert.That(schedule.IsScheduledOn(sunday, new[] { 1, 3, 5 }), Is.False);
            Assert.That(schedule.IsScheduledOn(sunday, new int[0]), Is.False);
            Assert.That(schedule.IsScheduledOn(sunday, null), Is.True);
        }

        [Test]
        public void YearRangeCountsCompletionsFromEveryDate()
        {
            var habit = new HabitOccurrence
            {
                HabitId = "daily",
                Schedule = HabitSchedule.FromStorage("day_period", "morning", null),
                RepeatDays = null,
                EffectiveFrom = new DateTime(2026, 1, 1),
                IsActive = true
            };
            var log = new HabitCompletion
            {
                HabitId = "daily",
                Date = new DateTime(2026, 10, 18),
                IsCompleted = true
            };

            HabitProgress year = HabitAnalytics.Calculate(
                new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), new[] { habit }, new[] { log });

            Assert.That(year.Total, Is.EqualTo(365));
            Assert.That(year.Completed, Is.EqualTo(1));
        }

        [Test]
        public void AnalyticsExcludesDaysBeforeEffectiveDateAndReportsUnknownLegacyCoverage()
        {
            var habit = new HabitOccurrence
            {
                HabitId = "new",
                Schedule = HabitSchedule.FromStorage("day_period", "morning", null),
                EffectiveFrom = new DateTime(2026, 10, 19),
                IsActive = true
            };
            var preexistingLog = new HabitCompletion
            {
                HabitId = "new",
                Date = new DateTime(2026, 10, 18),
                IsCompleted = true
            };

            HabitProgress october = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), new[] { habit }, new[] { preexistingLog });
            HabitProgress unknownLegacy = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 1), new DateTime(2026, 11, 1),
                new[] { new HabitOccurrence { HabitId = "legacy", IsActive = true } }, null);

            Assert.That(october.Total, Is.EqualTo(13));
            Assert.That(october.Completed, Is.EqualTo(0));
            Assert.That(unknownLegacy.Total, Is.EqualTo(0));
            Assert.That(unknownLegacy.HasUnknownCoverage, Is.True);
        }

        [Test]
        public void ApprovedHabitCountsOnlyApprovedCompletionsAndKeepsPreArchiveHistory()
        {
            var habit = new HabitOccurrence
            {
                HabitId = "approved",
                Schedule = HabitSchedule.FromStorage("day_period", "evening", null),
                EffectiveFrom = new DateTime(2026, 10, 1),
                ArchivedAt = new DateTime(2026, 10, 20),
                IsActive = false,
                RequireApproval = true
            };
            var logs = new[]
            {
                new HabitCompletion { HabitId = "approved", Date = new DateTime(2026, 10, 18), IsCompleted = true, IsApproved = false },
                new HabitCompletion { HabitId = "approved", Date = new DateTime(2026, 10, 19), IsCompleted = true, IsApproved = true }
            };

            HabitProgress result = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), new[] { habit }, logs);

            Assert.That(result.Total, Is.EqualTo(19));
            Assert.That(result.Completed, Is.EqualTo(1));
        }

        [Test]
        public void AnalyticsUsesApprovalRequirementCapturedWhenCompletionWasCreated()
        {
            HabitProgress requiredAtCompletion = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 18), new DateTime(2026, 10, 19),
                new[]
                {
                    new HabitOccurrence
                    {
                        HabitId = "changed-policy",
                        Schedule = HabitSchedule.FromStorage("day_period", "morning", null),
                        EffectiveFrom = new DateTime(2026, 10, 18),
                        IsActive = true,
                        RequireApproval = false
                    }
                },
                new[]
                {
                    new HabitCompletion
                    {
                        HabitId = "changed-policy",
                        Date = new DateTime(2026, 10, 18),
                        IsCompleted = true,
                        IsApproved = false,
                        RequiresApprovalAtCompletion = true
                    }
                });

            HabitProgress notRequiredAtCompletion = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 18), new DateTime(2026, 10, 19),
                new[]
                {
                    new HabitOccurrence
                    {
                        HabitId = "changed-policy",
                        Schedule = HabitSchedule.FromStorage("day_period", "morning", null),
                        EffectiveFrom = new DateTime(2026, 10, 18),
                        IsActive = true,
                        RequireApproval = true
                    }
                },
                new[]
                {
                    new HabitCompletion
                    {
                        HabitId = "changed-policy",
                        Date = new DateTime(2026, 10, 18),
                        IsCompleted = true,
                        IsApproved = false,
                        RequiresApprovalAtCompletion = false
                    }
                });

            Assert.That(requiredAtCompletion.Completed, Is.Zero);
            Assert.That(notRequiredAtCompletion.Completed, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingDuplicateLogsAreReportedAndNotCountedAsCompleted()
        {
            var habit = new HabitOccurrence
            {
                HabitId = "daily",
                Schedule = HabitSchedule.FromStorage("day_period", "morning", null),
                EffectiveFrom = new DateTime(2026, 10, 18),
                IsActive = true
            };
            var logs = new[]
            {
                new HabitCompletion { HabitId = "daily", Date = new DateTime(2026, 10, 18), IsCompleted = true },
                new HabitCompletion { HabitId = "daily", Date = new DateTime(2026, 10, 18), IsCompleted = false }
            };

            HabitProgress result = HabitAnalytics.Calculate(
                new DateTime(2026, 10, 18), new DateTime(2026, 10, 19), new[] { habit }, logs);

            Assert.That(result.Completed, Is.Zero);
            Assert.That(result.HasConflictingRecords, Is.True);
        }

        [Test]
        public void HabitRewardProgressionCarriesExperienceAcrossMultipleLevels()
        {
            PetProgressionResult result = HabitRewardProgression.Apply(1, 90, 100, 225);

            Assert.That(result.Level, Is.EqualTo(4));
            Assert.That(result.CurrentExp, Is.EqualTo(15));
            Assert.That(result.MaxExp, Is.EqualTo(100));
        }

        [Test]
        public void HabitRewardProgressionUsesDefaultThresholdForLegacyPet()
        {
            PetProgressionResult result = HabitRewardProgression.Apply(1, 90, 0, 20);

            Assert.That(result.Level, Is.EqualTo(2));
            Assert.That(result.CurrentExp, Is.EqualTo(10));
            Assert.That(result.MaxExp, Is.EqualTo(100));
        }

        [Test]
        public void HabitRewardProgressionHandlesLargeRewardsWithoutIteratingPerLevel()
        {
            PetProgressionResult result = HabitRewardProgression.Apply(1, 0, 1, 1_000_000_000);

            Assert.That(result.Level, Is.EqualTo(1_000_000_001));
            Assert.That(result.CurrentExp, Is.Zero);
            Assert.That(result.MaxExp, Is.EqualTo(1));
        }

        [Test]
        public void HabitRewardProgressionThrowsWhenLevelWouldOverflow()
        {
            Assert.Throws<OverflowException>(() => HabitRewardProgression.Apply(1, 0, 1, int.MaxValue));
        }

        [Test]
        public void HabitRewardProgressionRejectsNegativeRewardExperience()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HabitRewardProgression.Apply(1, 0, 100, -1));
        }

        [Test]
        public void DailyHabitLogIdentityKeepsTheExistingHashAndRejectsMismatchedKeys()
        {
            string logId = DailyHabitLogIdentity.GetDocumentId("read", "2026-10-18");

            Assert.That(logId, Is.EqualTo("daily_601395b79dba9d97e52a5a395cc145c0173724c0b615ffe0895f5902f215741c"));
            Assert.That(DailyHabitLogIdentity.MatchesDocumentId(logId, "read", "2026-10-18"), Is.True);
            Assert.That(DailyHabitLogIdentity.MatchesDocumentId(logId, "write", "2026-10-18"), Is.False);
            Assert.That(DailyHabitLogIdentity.MatchesDocumentId(logId, "read", "2026-10-19"), Is.False);
            Assert.That(DailyHabitLogIdentity.MatchesDocumentId("legacy-id", "read", "2026-10-18"), Is.False);
            Assert.Throws<ArgumentException>(() => DailyHabitLogIdentity.GetDocumentId(" ", "2026-10-18"));
        }
    }
}
