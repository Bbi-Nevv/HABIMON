using System;

namespace HABIMON.Core
{
    public sealed class PendingChildCreate
    {
        public FirebaseWriteScope Scope { get; }
        public string OperationId { get; }
        public string Name { get; }
        public int Age { get; }
        public int Gender { get; }

        public PendingChildCreate(
            FirebaseWriteScope scope,
            string operationId,
            string name,
            int age,
            int gender)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            OperationId = Required(operationId, nameof(operationId));
            Name = Required(name, nameof(name));
            if (age <= 0)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (gender != 0 && gender != 1)
                throw new ArgumentOutOfRangeException(nameof(gender));

            Age = age;
            Gender = gender;
        }

        public bool IsCurrent(string parentId, int sessionGeneration)
        {
            return Scope.IsCurrentParent(parentId, sessionGeneration);
        }

        public PendingChildCreate ResumeInSameParent(FirebaseWriteScope currentScope)
        {
            if (!Scope.HasSameParent(currentScope))
                throw new InvalidOperationException("A pending child create cannot move to another parent.");
            return new PendingChildCreate(currentScope, OperationId, Name, Age, Gender);
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A value is required.", parameterName);
            return value;
        }
    }

    public sealed class PendingHabitCreate
    {
        public FirebaseWriteScope Scope { get; }
        public string OperationId { get; }
        public string Title { get; }
        public string Description { get; }
        public string TimeTarget { get; }
        public string IconKey { get; }
        public string EffectiveFrom { get; }

        public PendingHabitCreate(
            FirebaseWriteScope scope,
            string operationId,
            string title,
            string description,
            string timeTarget,
            string iconKey,
            string effectiveFrom)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            if (string.IsNullOrWhiteSpace(scope.ChildId))
                throw new ArgumentException("A child ID is required for habit creation.", nameof(scope));

            OperationId = Required(operationId, nameof(operationId));
            Title = Required(title, nameof(title));
            Description = Required(description, nameof(description));
            TimeTarget = Required(timeTarget, nameof(timeTarget));
            IconKey = Required(iconKey, nameof(iconKey));
            EffectiveFrom = Required(effectiveFrom, nameof(effectiveFrom));
        }

        public bool IsCurrent(string parentId, string childId, int sessionGeneration)
        {
            return Scope.IsCurrent(parentId, childId, sessionGeneration);
        }

        public PendingHabitCreate ResumeInSameTarget(FirebaseWriteScope currentScope)
        {
            if (!Scope.HasSameTarget(currentScope))
                throw new InvalidOperationException("A pending habit create cannot move to another target.");
            return new PendingHabitCreate(
                currentScope,
                OperationId,
                Title,
                Description,
                TimeTarget,
                IconKey,
                EffectiveFrom);
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A value is required.", parameterName);
            return value;
        }
    }
}
