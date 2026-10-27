using Firebase.Firestore;
using HABIMON.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ChildService
{
    private readonly FirebaseFirestore db;

    public ChildService(FirebaseFirestore firestore)
    {
        db = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }

    public async Task<string> ResolveCurrentChildIdAsync(string parentId)
    {
        if (string.IsNullOrEmpty(parentId))
            return string.Empty;

        CollectionReference childrenRef = db.Collection("users")
            .Document(parentId)
            .Collection("children");

        QuerySnapshot snapshot = await childrenRef.GetSnapshotAsync();
        DocumentSnapshot firstChild = snapshot.Documents.FirstOrDefault();
        return firstChild != null && firstChild.Exists ? firstChild.Id : string.Empty;
    }

    public async Task<List<ChildModel>> GetChildrenAsync(string parentId)
    {
        List<ChildModel> children = new List<ChildModel>();
        if (string.IsNullOrEmpty(parentId))
            return children;

        CollectionReference childrenRef = db.Collection("users")
            .Document(parentId)
            .Collection("children");

        QuerySnapshot snapshot = await childrenRef.GetSnapshotAsync();
        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (!doc.Exists)
                continue;

            ChildModel child = doc.ConvertTo<ChildModel>();
            if (child == null)
                continue;

            child.childId = doc.Id;
            if (!doc.ContainsField("gender"))
                child.gender = -1;
            children.Add(child);
        }

        return children;
    }

    public async Task<(bool success, string message)> UpdateChildAsync(string parentId, string childId, string name, int age, int gender)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.");

        if (string.IsNullOrWhiteSpace(childId))
            return (false, "Không xác định được trẻ cần cập nhật.");

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Vui lòng nhập tên trẻ.");

        if (age <= 0)
            return (false, "Tuổi phải lớn hơn 0.");

        if (gender != 0 && gender != 1)
            return (false, "Vui lòng chọn giới tính.");

        try
        {
            DocumentReference childRef = db.Collection("users")
                .Document(parentId)
                .Collection("children")
                .Document(childId);

            await childRef.UpdateAsync(new Dictionary<string, object>
            {
                { "name", name.Trim() },
                { "age", age },
                { "ageAsOf", Timestamp.GetCurrentTimestamp() },
                { "gender", gender }
            });

            return (true, "Đã cập nhật thông tin trẻ.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể cập nhật trẻ: {ex.Message}");
            return (false, "Không thể cập nhật trẻ. Vui lòng thử lại.");
        }
    }

    public async Task<(bool success, string message)> DeleteChildAsync(string parentId, string childId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.");

        if (string.IsNullOrWhiteSpace(childId))
            return (false, "Không xác định được trẻ cần xóa.");

        try
        {
            DocumentReference childRef = db.Collection("users")
                .Document(parentId)
                .Collection("children")
                .Document(childId);

            foreach (string collectionName in new[] { "habits", "habit_logs", "habimon", "rewards" })
                await DeleteCollectionContentsAsync(childRef.Collection(collectionName));

            await childRef.DeleteAsync();
            return (true, "Đã xóa trẻ.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể xóa trẻ: {ex.Message}");
            return (false, "Không thể xóa trẻ. Vui lòng thử lại.");
        }
    }

    private async Task DeleteCollectionContentsAsync(CollectionReference collection)
    {
        QuerySnapshot snapshot = await collection.GetSnapshotAsync();
        List<DocumentSnapshot> documents = snapshot.Documents.ToList();
        const int batchSize = 400;
        for (int start = 0; start < documents.Count; start += batchSize)
        {
            WriteBatch batch = db.StartBatch();
            int end = Math.Min(start + batchSize, documents.Count);
            for (int index = start; index < end; index++)
                batch.Delete(documents[index].Reference);
            await batch.CommitAsync();
        }
    }

    public async Task<(bool success, string message, string childId)> AddChildAsync(PendingChildCreate pending)
    {
        if (pending == null)
            return (false, "Thiếu mã thao tác thêm trẻ.", string.Empty);

        string parentId = pending.Scope.ParentId;
        string childId = pending.OperationId;
        string name = pending.Name;
        int age = pending.Age;
        int gender = pending.Gender;
        if (!Guid.TryParseExact(childId, "N", out _))
            return (false, "Mã thao tác thêm trẻ không hợp lệ.", string.Empty);

        DocumentReference childRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId);

        ChildModel child = new ChildModel
        {
            childId = childId,
            name = name.Trim(),
            age = age,
            ageAsOf = Timestamp.GetCurrentTimestamp(),
            gender = gender,
            gold = 0,
            totalExp = 0,
            createdAt = Timestamp.GetCurrentTimestamp()
        };

        DocumentReference petRef = childRef.Collection("habimon").Document("status");
        HabimonModel defaultPet = new HabimonModel
        {
            petType = "fire_dragon",
            petName = "Rồng Lửa",
            level = 1,
            currentExp = 0,
            maxExp = 100,
            stage = "Egg",
            health = 100,
            hunger = 100,
            happiness = 100
        };

        try
        {
            bool operationMatched = false;
            bool operationConflicted = false;
            await db.RunTransactionAsync(async transaction =>
            {
                DocumentSnapshot childSnapshot = await transaction.GetSnapshotAsync(childRef);
                DocumentSnapshot petSnapshot = await transaction.GetSnapshotAsync(petRef);

                if (childSnapshot.Exists)
                {
                    ChildModel existingChild = childSnapshot.ConvertTo<ChildModel>();
                    operationMatched = existingChild != null &&
                        string.Equals(existingChild.name, child.name, StringComparison.Ordinal) &&
                        existingChild.age == child.age &&
                        existingChild.gender == child.gender &&
                        petSnapshot.Exists;
                    operationConflicted = !operationMatched;
                    return;
                }

                if (petSnapshot.Exists)
                {
                    operationConflicted = true;
                    return;
                }

                transaction.Set(childRef, child);
                transaction.Set(petRef, defaultPet);
                operationMatched = true;
            });

            if (!operationMatched)
                return (false, operationConflicted
                    ? "Mã thao tác thêm trẻ đã được dùng với dữ liệu khác hoặc hồ sơ chưa hoàn chỉnh."
                    : "Không thể xác nhận thao tác thêm trẻ.", string.Empty);

            return (true, "Đã thêm trẻ.", childId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể thêm trẻ: {ex.Message}");
            return (false, "Không thể thêm trẻ. Vui lòng thử lại.", string.Empty);
        }
    }
}
