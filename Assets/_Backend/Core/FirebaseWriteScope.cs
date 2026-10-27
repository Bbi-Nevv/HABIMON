using System;

namespace HABIMON.Core
{
    public sealed class FirebaseWriteScope
    {
        public string ParentId { get; }
        public string ChildId { get; }
        public int SessionGeneration { get; }

        public FirebaseWriteScope(string parentId, string childId, int sessionGeneration)
        {
            if (string.IsNullOrWhiteSpace(parentId))
                throw new ArgumentException("A parent ID is required.", nameof(parentId));

            ParentId = parentId;
            ChildId = childId ?? string.Empty;
            SessionGeneration = sessionGeneration;
        }

        public bool IsCurrent(string parentId, string childId, int sessionGeneration)
        {
            return string.Equals(ParentId, parentId, StringComparison.Ordinal) &&
                   string.Equals(ChildId, childId ?? string.Empty, StringComparison.Ordinal) &&
                   SessionGeneration == sessionGeneration;
        }

        public bool IsCurrentParent(string parentId, int sessionGeneration)
        {
            return string.Equals(ParentId, parentId, StringComparison.Ordinal) &&
                   SessionGeneration == sessionGeneration;
        }

        public bool HasSameTarget(FirebaseWriteScope other)
        {
            return other != null &&
                   HasSameParent(other) &&
                   string.Equals(ChildId, other.ChildId, StringComparison.Ordinal);
        }

        public bool HasSameParent(FirebaseWriteScope other)
        {
            return other != null && string.Equals(ParentId, other.ParentId, StringComparison.Ordinal);
        }
    }
}
