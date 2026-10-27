using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Globalization;

#region HabitModel    
[FirestoreData]
public class HabitModel
{
    [FirestoreDocumentId] public string id { get; set; }
    [FirestoreProperty] public string title { get; set; }
    [FirestoreProperty] public string description { get; set; }
    [FirestoreProperty] public string icon { get; set; }
    [FirestoreProperty] public string timeTarget { get; set; }
    [FirestoreProperty] public string scheduleKind { get; set; }
    [FirestoreProperty] public string scheduleValue { get; set; }
    [FirestoreProperty] public string effectiveFrom { get; set; }
    [FirestoreProperty("archivedAt")] public object archivedAtValue { get; set; }
    public Timestamp? archivedAt
    {
        get { return archivedAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { archivedAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("createdAt")] public object createdAtValue { get; set; }
    public Timestamp? createdAt
    {
        get { return createdAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { createdAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty] public List<int> repeatDays { get; set; }
    [FirestoreProperty] public int rewardGold { get; set; }
    [FirestoreProperty] public int rewardExp { get; set; }
    [FirestoreProperty] public bool requireApproval { get; set; }
    [FirestoreProperty] public bool isActive { get; set; }
}
#endregion

#region ParentModel
[FirestoreData]
public class ParentModel
{
    [FirestoreDocumentId] public string parentId { get; set; }
    [FirestoreProperty] public string email { get; set; }
    [FirestoreProperty] public string pincode { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
    [FirestoreProperty] public string provisioningState { get; set; }

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
    [FirestoreProperty("ageAsOf")] public object ageAsOfValue { get; set; }
    public Timestamp? ageAsOf
    {
        get { return ageAsOfValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { ageAsOfValue = value.HasValue ? (object)value.Value : null; }
    }
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
    [FirestoreProperty("completedAt")] public object completedAtValue { get; set; }
    public Timestamp? completedAt
    {
        get { return completedAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { completedAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("approvedAt")] public object approvedAtValue { get; set; }
    public Timestamp? approvedAt
    {
        get { return approvedAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { approvedAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("createdAt")] public object createdAtValue { get; set; }
    public Timestamp? createdAt
    {
        get { return createdAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { createdAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("updatedAt")] public object updatedAtValue { get; set; }
    public Timestamp? updatedAt
    {
        get { return updatedAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { updatedAtValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty] public bool isCompleted { get; set; }
    [FirestoreProperty] public bool isApproved { get; set; }
    [FirestoreProperty("requiresApprovalAtCompletion")] public object requiresApprovalAtCompletionValue { get; set; }
    public bool? requiresApprovalAtCompletion
    {
        get { return requiresApprovalAtCompletionValue is bool value ? value : (bool?)null; }
        set { requiresApprovalAtCompletionValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("rewardGoldAtCompletion")] public object rewardGoldAtCompletionValue { get; set; }
    public int? rewardGoldAtCompletion
    {
        get { return rewardGoldAtCompletionValue == null ? (int?)null : Convert.ToInt32(rewardGoldAtCompletionValue, CultureInfo.InvariantCulture); }
        set { rewardGoldAtCompletionValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty("rewardExpAtCompletion")] public object rewardExpAtCompletionValue { get; set; }
    public int? rewardExpAtCompletion
    {
        get { return rewardExpAtCompletionValue == null ? (int?)null : Convert.ToInt32(rewardExpAtCompletionValue, CultureInfo.InvariantCulture); }
        set { rewardExpAtCompletionValue = value.HasValue ? (object)value.Value : null; }
    }
    [FirestoreProperty] public bool rewardGranted { get; set; }
    [FirestoreProperty] public int rewardGoldGranted { get; set; }
    [FirestoreProperty] public int rewardExpGranted { get; set; }
    [FirestoreProperty("rewardedAt")] public object rewardedAtValue { get; set; }
    public Timestamp? rewardedAt
    {
        get { return rewardedAtValue is Timestamp timestamp ? timestamp : (Timestamp?)null; }
        set { rewardedAtValue = value.HasValue ? (object)value.Value : null; }
    }
}
#endregion
