using System;
using System.Collections.Generic;
using System.Globalization;

namespace HABIMON.Core
{
    public static class ChildSelectionPolicy
    {
        public static string Choose(string selectedChildId, string rememberedChildId, IEnumerable<string> availableChildIds)
        {
            var available = new HashSet<string>(StringComparer.Ordinal);
            if (availableChildIds != null)
            {
                foreach (string childId in availableChildIds)
                {
                    if (!string.IsNullOrWhiteSpace(childId))
                        available.Add(childId);
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedChildId) && available.Contains(selectedChildId))
                return selectedChildId;
            if (!string.IsNullOrWhiteSpace(rememberedChildId) && available.Contains(rememberedChildId))
                return rememberedChildId;

            string fallback = string.Empty;
            foreach (string childId in available)
            {
                if (string.IsNullOrEmpty(fallback) || string.CompareOrdinal(childId, fallback) < 0)
                    fallback = childId;
            }

            return fallback;
        }
    }

    public sealed class HabitSchedule
    {
        private HabitSchedule(string kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public string Kind { get; }
        public string Value { get; }

        public static HabitSchedule FromStorage(string scheduleKind, string scheduleValue, string legacyTimeTarget)
        {
            string kind = scheduleKind?.Trim().ToLowerInvariant();
            string value = scheduleValue?.Trim();

            if (kind == "day_period")
            {
                if (TryNormalizePeriod(value, out string period))
                    return new HabitSchedule("day_period", period);
                return new HabitSchedule("unclassified", value ?? string.Empty);
            }

            if (kind == "local_time")
            {
                if (TryParseClock(value, out TimeSpan localTime))
                    return new HabitSchedule("local_time", localTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
                return new HabitSchedule("unclassified", value ?? string.Empty);
            }

            if (kind == "unclassified")
                return new HabitSchedule("unclassified", value ?? string.Empty);

            string legacy = legacyTimeTarget?.Trim();
            if (TryNormalizePeriod(legacy, out string legacyPeriod))
                return new HabitSchedule("day_period", legacyPeriod);
            if (TryParseClock(legacy, out TimeSpan legacyTime))
                return new HabitSchedule("local_time", legacyTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));

            return new HabitSchedule("unclassified", legacy ?? string.Empty);
        }

        public bool MatchesPeriod(string targetPeriod)
        {
            int target = ParsePeriod(targetPeriod);
            if (target == 3)
                return true;
            if (target < 0)
                return false;

            if (Kind == "day_period")
                return ParsePeriod(Value) == target;
            if (Kind != "local_time" || !TryParseClock(Value, out TimeSpan localTime))
                return false;

            int actual = localTime.Hours >= 5 && localTime.Hours < 12 ? 0 :
                localTime.Hours >= 12 && localTime.Hours < 18 ? 1 : 2;
            return actual == target;
        }

        public bool IsScheduledOn(DateTime date, int[] repeatDays)
        {
            return IsRepeatDayScheduled(date, repeatDays);
        }

        public static bool IsRepeatDayScheduled(DateTime date, int[] repeatDays)
        {
            if (repeatDays == null)
                return true;
            if (repeatDays.Length == 0)
                return false;

            int isoWeekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            foreach (int day in repeatDays)
            {
                int normalized = day == 0 ? 7 : day;
                if (normalized >= 1 && normalized <= 7 && normalized == isoWeekday)
                    return true;
            }

            return false;
        }

        private static bool TryNormalizePeriod(string value, out string normalized)
        {
            int period = ParsePeriod(value);
            normalized = period == 0 ? "morning" : period == 1 ? "afternoon" : period == 2 ? "evening" : null;
            return normalized != null;
        }

        private static bool TryParseClock(string value, out TimeSpan localTime)
        {
            return TimeSpan.TryParseExact(value, new[] { @"h\:mm", @"hh\:mm" },
                CultureInfo.InvariantCulture, out localTime) &&
                localTime >= TimeSpan.Zero && localTime < TimeSpan.FromDays(1);
        }

        private static int ParsePeriod(string value)
        {
            string normalized = value?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalized))
                return -1;
            if (normalized == "all" || normalized == "tất cả")
                return 3;
            if (normalized.Contains("morning") || normalized.Contains("sáng"))
                return 0;
            if (normalized.Contains("afternoon") || normalized.Contains("chiều"))
                return 1;
            if (normalized.Contains("evening") || normalized.Contains("night") || normalized.Contains("tối"))
                return 2;
            return -1;
        }
    }

    public sealed class HabitOccurrence
    {
        public string HabitId;
        public HabitSchedule Schedule;
        public int[] RepeatDays;
        public DateTime? EffectiveFrom;
        public DateTime? ArchivedAt;
        public bool IsActive;
        public bool RequireApproval;
    }

    public sealed class HabitCompletion
    {
        public string HabitId;
        public DateTime Date;
        public bool IsCompleted;
        public bool IsApproved;
        public bool? RequiresApprovalAtCompletion;
    }

    public struct HabitProgress
    {
        public int Total { get; internal set; }
        public int Completed { get; internal set; }
        public bool HasUnknownCoverage { get; internal set; }
        public bool HasConflictingRecords { get; internal set; }
    }

    public static class HabitAnalytics
    {
        private sealed class CompletionState
        {
            public bool IsCompleted;
            public bool IsApproved;
            public bool? RequiresApprovalAtCompletion;
            public bool IsConflicting;
        }

        public static HabitProgress Calculate(
            DateTime startInclusive,
            DateTime endExclusive,
            IEnumerable<HabitOccurrence> habits,
            IEnumerable<HabitCompletion> completions)
        {
            var result = new HabitProgress();
            if (endExclusive.Date <= startInclusive.Date || habits == null)
                return result;

            var habitList = new List<HabitOccurrence>(habits);
            var completionList = completions == null
                ? new List<HabitCompletion>()
                : new List<HabitCompletion>(completions);
            var completionStates = new Dictionary<string, CompletionState>();
            foreach (HabitCompletion completion in completionList)
            {
                if (completion == null || string.IsNullOrEmpty(completion.HabitId) ||
                    completion.Date.Date < startInclusive.Date || completion.Date.Date >= endExclusive.Date)
                    continue;

                string key = BuildCompletionKey(completion.HabitId, completion.Date);
                if (!completionStates.TryGetValue(key, out CompletionState state))
                {
                    completionStates[key] = new CompletionState
                    {
                        IsCompleted = completion.IsCompleted,
                        IsApproved = completion.IsApproved,
                        RequiresApprovalAtCompletion = completion.RequiresApprovalAtCompletion
                    };
                    continue;
                }

                if (state.IsCompleted != completion.IsCompleted || state.IsApproved != completion.IsApproved ||
                    state.RequiresApprovalAtCompletion != completion.RequiresApprovalAtCompletion)
                    state.IsConflicting = true;
            }

            for (DateTime date = startInclusive.Date; date < endExclusive.Date; date = date.AddDays(1))
            {
                foreach (HabitOccurrence habit in habitList)
                {
                    if (habit == null || string.IsNullOrEmpty(habit.HabitId))
                        continue;

                    bool scheduled = habit.Schedule == null
                        ? HabitSchedule.IsRepeatDayScheduled(date, habit.RepeatDays)
                        : habit.Schedule.IsScheduledOn(date, habit.RepeatDays);
                    if (!scheduled)
                        continue;

                    if (!habit.EffectiveFrom.HasValue)
                    {
                        result.HasUnknownCoverage = true;
                        continue;
                    }

                    if (date < habit.EffectiveFrom.Value.Date)
                        continue;

                    if (habit.ArchivedAt.HasValue && date >= habit.ArchivedAt.Value.Date)
                        continue;

                    if (!habit.IsActive && !habit.ArchivedAt.HasValue)
                    {
                        result.HasUnknownCoverage = true;
                        continue;
                    }

                    result.Total++;
                    if (completionStates.TryGetValue(BuildCompletionKey(habit.HabitId, date), out CompletionState state))
                    {
                        if (state.IsConflicting)
                        {
                            result.HasConflictingRecords = true;
                            continue;
                        }

                        bool requiresApproval = state.RequiresApprovalAtCompletion ?? habit.RequireApproval;
                        if (state.IsCompleted && (!requiresApproval || state.IsApproved))
                            result.Completed++;
                    }
                }
            }

            return result;
        }

        private static string BuildCompletionKey(string habitId, DateTime date)
        {
            return habitId + "\u001f" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
