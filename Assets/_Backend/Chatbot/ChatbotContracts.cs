using System;

namespace HABIMON.Chatbot
{
    [Serializable]
    public class ChatbotContext
    {
        public string capturedAt;
        public string localDate;
        public string timeZone;
        public ChatChildContext child;
        public ChatPetContext pet;
        public ChatHabitContext[] habits;
        public ChatRewardContext[] rewards;
    }

    [Serializable]
    public class ChatChildContext
    {
        public string name;
        public int age;
        public string gender;
    }

    [Serializable]
    public class ChatPetContext
    {
        public string petType;
        public string petName;
        public int level;
        public int currentExp;
        public int maxExp;
        public string stage;
        public int health;
        public int hunger;
        public int happiness;
    }

    [Serializable]
    public class ChatHabitContext
    {
        public string title;
        public string description;
        public ChatHabitScheduleContext schedule;
        public int[] repeatDays;
        public int rewardGold;
        public int rewardExp;
        public bool requireApproval;
        public bool isActive;
        public ChatHabitLogContext[] logs;
    }

    [Serializable]
    public class ChatHabitScheduleContext
    {
        public string kind;
        public string value;
    }

    [Serializable]
    public class ChatHabitLogContext
    {
        public string dateKey;
        public string completedAt;
        public bool isCompleted;
        public bool isApproved;
    }

    [Serializable]
    public class ChatRewardContext
    {
        public string name;
        public string itemType;
        public int cost;
        public bool isUnlocked;
    }

    public class ChatbotSseEvent
    {
        public string eventName;
        public string data;
    }

    [Serializable]
    public class ChatbotSessionResponse
    {
        public string id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class ChatbotSessionListResponse
    {
        public ChatbotSessionResponse[] sessions;
    }

    [Serializable]
    public class ChatbotMessageResponse
    {
        public long id;
        public string role;
        public string content;
        public string created_at;
    }

    [Serializable]
    public class ChatbotMessageListResponse
    {
        public ChatbotMessageResponse[] messages;
    }

    [Serializable]
    public class ChatbotApiError
    {
        public string code;
        public long statusCode;
    }
}
