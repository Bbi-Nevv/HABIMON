namespace HABIMON.Chatbot
{
    public readonly struct ChatbotSessionScope
    {
        public string ParentId { get; }
        public string ChildId { get; }
        public int Generation { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ParentId) && !string.IsNullOrWhiteSpace(ChildId);

        public ChatbotSessionScope(string parentId, string childId, int generation)
        {
            ParentId = parentId;
            ChildId = childId;
            Generation = generation;
        }

        public bool IsCurrent(string parentId, string childId, int generation)
        {
            return IsValid &&
                generation == Generation &&
                parentId == ParentId &&
                childId == ChildId;
        }
    }
}
