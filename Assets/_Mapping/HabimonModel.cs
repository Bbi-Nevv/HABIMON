using Firebase.Firestore;

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
