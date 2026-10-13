using Firebase.Firestore;
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

            await childRef.DeleteAsync();
            return (true, "Đã xóa trẻ.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể xóa trẻ: {ex.Message}");
            return (false, "Không thể xóa trẻ. Vui lòng thử lại.");
        }
    }

    public async Task<(bool success, string message, string childId)> AddChildAsync(string parentId, string name, int age, int gender)
    {
        if (string.IsNullOrEmpty(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.", string.Empty);

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Vui lòng nhập tên trẻ.", string.Empty);

        if (age <= 0)
            return (false, "Tuổi phải lớn hơn 0.", string.Empty);

        if (gender != 0 && gender != 1)
            return (false, "Vui lòng chọn giới tính.", string.Empty);

        string childId = Guid.NewGuid().ToString();
        DocumentReference childRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId);

        ChildModel child = new ChildModel
        {
            childId = childId,
            name = name.Trim(),
            age = age,
            gender = gender,
            gold = 0,
            totalExp = 0,
            createdAt = Timestamp.GetCurrentTimestamp()
        };

        try
        {
            await childRef.SetAsync(child);
            return (true, "Đã thêm trẻ.", childId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể thêm trẻ: {ex.Message}");
            return (false, "Không thể thêm trẻ. Vui lòng thử lại.", string.Empty);
        }
    }
}
