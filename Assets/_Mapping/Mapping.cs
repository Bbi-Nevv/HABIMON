using Firebase.Firestore;
using System.Collections.Generic;

#region HabitModel    
[FirestoreData]
public class HabitModel
{
    [FirestoreDocumentId] public string id { get; set; }
    [FirestoreProperty] public string title { get; set; }
    [FirestoreProperty] public string description { get; set; }
    [FirestoreProperty] public string icon { get; set; }
    [FirestoreProperty] public string timeTarget { get; set; }
    [FirestoreProperty] public List<int> repeatDays { get; set; }
    [FirestoreProperty] public int rewardGold { get; set; }
    [FirestoreProperty] public int rewardExp { get; set; }
    [FirestoreProperty] public bool requireApproval { get; set; }
    [FirestoreProperty] public bool isActive { get; set; }
}
#endregion

#region HabimonModel
[FirestoreData]
public class HabimonModel
{
    [FirestoreProperty] public string petType { get; set; }
    [FirestoreProperty] public string petName { get; set; }
    [FirestoreProperty] public int level { get; set; }
    [FirestoreProperty] public int currentExp { get; set; }
    [FirestoreProperty] public int maxExp { get; set; }
    [FirestoreProperty] public string stage { get; set; }
    [FirestoreProperty] public int health { get; set; }
    [FirestoreProperty] public int hunger { get; set; }
    [FirestoreProperty] public int happiness { get; set; }
}
#endregion

#region ParentModel
[FirestoreData]
public class ParentModel
{
    [FirestoreDocumentId] public string parentId { get; set; }
    [FirestoreProperty] public string email { get; set; }
    [FirestoreProperty] public string passwordHash { get; set; }
    [FirestoreProperty] public string passwordSalt { get; set; }
    [FirestoreProperty] public string pincode { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }

}
#endregion

#region ChildModel
[FirestoreData]
public class ChildModel
{
    [FirestoreDocumentId] public string childId { get; set; }
    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public int gender { get; set; }
    [FirestoreProperty] public int age { get; set; }
    [FirestoreProperty] public int gold { get; set; }
    [FirestoreProperty] public int totalExp { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
}
#endregion

#region RewardModel
[FirestoreData]
public class RewardModel
{
    [FirestoreDocumentId] public string rewardId { get; set; }
    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public string itemType { get; set; }
    [FirestoreProperty] public int cost { get; set; }
    [FirestoreProperty] public bool isUnlocked { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
}
#endregion

#region HabitLogModel
[FirestoreData] 
public class HabitLogModel
{
    [FirestoreDocumentId] public string logId { get; set; }
    [FirestoreProperty] public string habitId { get; set; }
    [FirestoreProperty] public string dateKey { get; set; }
    [FirestoreProperty] public Timestamp completedAt { get; set; }
    [FirestoreProperty] public bool isCompleted { get; set; }
    [FirestoreProperty] public bool isApproved { get; set; }
}
#endregion