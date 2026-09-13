using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
public class FirebaseTester : MonoBehaviour
{
    // Dùng ID mẫu trùng với dữ liệu đã tạo trên Firebase Console
    private string testParentId = "parent_001";
    private string testChildId = "child_001";

    private async void Start()
    {
        // Đợi 2 giây để chắc chắn FirebaseManager đã khởi tạo xong
        await System.Threading.Tasks.Task.Delay(2000);

        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsInitialized)
        {
            Debug.Log("<color=yellow>[TEST] Đang kiểm tra kết nối Firestore...</color>");

            // 1. Test ghi thói quen mới
            await TestAddHabit();

            // 2. Test đọc danh sách thói quen
            await TestGetHabits();
        }
        else
        {
            Debug.LogError("[TEST] FirebaseManager chưa khởi tạo xong!");
        }
    }

    private async Task TestAddHabit()
    {
        HabitModel habitTest = new HabitModel
        {
            title = "Đọc sách 15 phút",
            description = "Đọc truyện hoặc sách khoa học trước khi đi ngủ",
            icon = "icon_book",
            timeTarget = "20:00",
            repeatDays = new List<int> { 1, 2, 3, 4, 5 },
            rewardGold = 15,
            rewardExp = 25,
            requireApproval = true,
            isActive = true
        };

        Debug.Log("[TEST] Đang gửi thói quen mới lên Firebase...");
        await FirebaseManager.Instance.AddHabitAsync(testParentId, testChildId, habitTest);
    }

    private async Task TestGetHabits()
    {
        Debug.Log("[TEST] Đang lấy danh sách thói quen từ Firebase...");
        List<HabitModel> habits = await FirebaseManager.Instance.GetHabitsAsync(testParentId, testChildId);

        Debug.Log($"<color=green>[TEST THÀNH CÔNG] Lấy được {habits.Count} thói quen:</color>");
        foreach (var habit in habits)
        {
            Debug.Log($"- ID: {habit.id} | Thói quen: {habit.title} | Thưởng: {habit.rewardGold} Vàng");
        }
    }
}