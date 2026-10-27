using System;

namespace HABIMON.Core
{
    public sealed class PetProgressionResult
    {
        public int Level { get; }
        public int CurrentExp { get; }
        public int MaxExp { get; }

        internal PetProgressionResult(int level, int currentExp, int maxExp)
        {
            Level = level;
            CurrentExp = currentExp;
            MaxExp = maxExp;
        }
    }

    public static class HabitRewardProgression
    {
        private const int DefaultMaxExp = 100;

        public static PetProgressionResult Apply(int level, int currentExp, int maxExp, int rewardExp)
        {
            if (rewardExp < 0)
                throw new ArgumentOutOfRangeException(nameof(rewardExp));

            int normalizedMaxExp = maxExp > 0 ? maxExp : DefaultMaxExp;
            int normalizedLevel = Math.Max(1, level);
            long remainingExp = Math.Max(0, currentExp) + (long)rewardExp;
            long levelsGained = remainingExp / normalizedMaxExp;
            remainingExp %= normalizedMaxExp;
            normalizedLevel = checked(normalizedLevel + checked((int)levelsGained));

            return new PetProgressionResult(normalizedLevel, (int)remainingExp, normalizedMaxExp);
        }
    }
}
