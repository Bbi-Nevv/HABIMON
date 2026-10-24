using Firebase.Firestore;
using HABIMON.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private MissionUI missionUI;

    private string currentParentId;
    private string currentChildId;
    private int currentSessionGeneration;
    private int habitsLoadVersion;
    private bool hasStarted;

    public bool HasStarted => hasStarted;

    private async void Start()
    {
        hasStarted = true;

        if (missionUI == null)
            missionUI = UnityEngine.Object.FindFirstObjectByType<MissionUI>();

        await LoadHabitsForCurrentUser();
    }

    public void AddItem()
    {
        if (missionUI == null)
            return;

        var sampleHabit = new HabitModel
        {
            title = "Thói quen mới",
            description = "Mô tả thử nghiệm",
            timeTarget = "21:00",
            rewardGold = 5,
            rewardExp = 10
        };

                missionUI.SpawnHabitItem(sampleHabit, sampleHabit.id, DeleteHabitById, UpdateHabitActiveByIdAsync);
    }

    public void AddDataToUi()
    {
        _ = LoadHabitsForCurrentUser();
    }

    public async Task<bool> LoadHabitsForCurrentUser()
    {
        int requestVersion = ++habitsLoadVersion;
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[Mission] Firebase chưa sẵn sàng để tải nhiệm vụ.");
            return false;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            Debug.LogError($"[Mission] Firebase initialization failed: {manager.InitializationError}");
            return false;
        }

        string parentId = manager.GetCurrentParentId();
        string childId = manager.GetCurrentChildId();
        if (string.IsNullOrEmpty(childId))
        {
            try
            {
                childId = await manager.ResolveCurrentChildIdAsync();
            }
            catch (System.Exception ex)
            {
                if (this != null && requestVersion == habitsLoadVersion)
                {
                    missionUI?.ClearHabitItems();
                    Debug.LogError($"[Mission] Không thể xác định trẻ đang chọn: {ex.Message}");
                }
                return false;
            }
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId))
        {
            currentParentId = string.Empty;
            currentChildId = string.Empty;
            if (missionUI != null)
                missionUI.ClearHabitItems();
            Debug.LogWarning("[Mission] Chưa có parentId hoặc childId từ tài khoản đang đăng nhập.");
            return false;
        }

        if (currentParentId != parentId || currentChildId != childId)
        {
            currentParentId = parentId;
            currentChildId = childId;
            currentSessionGeneration = manager.SessionGeneration;
            missionUI?.ClearHabitItems();
        }

        try
        {
            int requestGeneration = manager.SessionGeneration;
            var habits = await manager.GetHabitsAsync(parentId, childId);
            if (this == null || requestVersion != habitsLoadVersion ||
                FirebaseManager.Instance != manager || manager.SessionGeneration != requestGeneration ||
                manager.GetCurrentParentId() != parentId || manager.GetCurrentChildId() != childId)
                return false;

            currentParentId = parentId;
            currentChildId = childId;
            currentSessionGeneration = requestGeneration;
            Debug.Log($"[Mission] Đã lấy được {habits.Count} habit từ Firebase. missionUI null? {missionUI == null}");

            if (missionUI != null)
            {
                missionUI.ClearHabitItems();

                foreach (var habit in habits)
                {
                    Debug.Log($"[Mission] Render habit: {habit.title} | id={habit.id}");
                    missionUI.SpawnHabitItem(habit, habit.id, DeleteHabitById, UpdateHabitActiveByIdAsync);
                }
            }
            else
            {
                Debug.LogError("[Mission] missionUI == null => không render được gì lên UI.");
                return false;
            }

            return true;
        }
        catch (System.Exception ex)
        {
            if (this != null && requestVersion == habitsLoadVersion)
                Debug.LogError($"[Mission] Lỗi khi tải thói quen cho user: {ex.Message}");
            return false;
        }
    }

    public async Task<PendingHabitCreate> PrepareHabitCreateFromPopupAsync(
        string title,
        string description,
        string timeTarget,
        string iconKey,
        string operationId)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
            return null;

        string parentId = manager.GetCurrentParentId();
        int startingGeneration = manager.SessionGeneration;
        if (string.IsNullOrEmpty(parentId))
            return null;
        if (!await manager.EnsureInitializedAsync())
            return null;
        if (this == null || manager != FirebaseManager.Instance ||
            parentId != manager.GetCurrentParentId() || startingGeneration != manager.SessionGeneration)
            return null;

        string childId = manager.GetCurrentChildId();
        if (string.IsNullOrEmpty(childId))
        {
            int generationBeforeResolve = manager.SessionGeneration;
            childId = await manager.ResolveCurrentChildIdAsync();
            int resultingGeneration = manager.SessionGeneration;
            if (resultingGeneration != generationBeforeResolve &&
                resultingGeneration != generationBeforeResolve + 1)
                return null;
        }

        if (this == null || manager != FirebaseManager.Instance || parentId != manager.GetCurrentParentId() ||
            string.IsNullOrEmpty(childId) || childId != manager.GetCurrentChildId())
            return null;

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) ||
            string.IsNullOrEmpty(timeTarget) || string.IsNullOrWhiteSpace(operationId))
            return null;

        if (string.IsNullOrEmpty(iconKey))
            iconKey = "icon_default";

        return new PendingHabitCreate(
            new FirebaseWriteScope(parentId, childId, manager.SessionGeneration),
            operationId,
            title,
            description,
            timeTarget,
            iconKey,
            HabitLog.GetTodayDateKey());
    }

    public async Task<(bool success, string message)> SubmitHabitFromPopup(PendingHabitCreate pending)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
            return (false, "Firebase chưa sẵn sàng.");
        if (pending == null)
            return (false, "Thiếu thông tin để lưu thói quen.");
        if (!await manager.EnsureInitializedAsync())
            return (false, "Firebase chưa sẵn sàng.");

        if (this == null || manager != FirebaseManager.Instance ||
            !pending.IsCurrent(manager.GetCurrentParentId(), manager.GetCurrentChildId(), manager.SessionGeneration))
            return (false, "Phiên hoặc trẻ đã thay đổi. Không thể chuyển thao tác này sang hồ sơ khác.");

        HabitSchedule schedule = HabitSchedule.FromStorage(null, null, pending.TimeTarget);

        HabitModel habit = new HabitModel
        {
            title = pending.Title,
            description = pending.Description,
            icon = pending.IconKey,
            timeTarget = pending.TimeTarget,
            scheduleKind = schedule.Kind,
            scheduleValue = schedule.Value,
            effectiveFrom = pending.EffectiveFrom,
            createdAt = Timestamp.GetCurrentTimestamp(),
            repeatDays = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            rewardGold = 10,
            rewardExp = 20,
            requireApproval = true,
            isActive = true
        };

        try
        {
            var result = await manager.AddHabitAsync(pending.Scope, habit, pending.OperationId);
            if (this == null)
                return (result.success, result.message);
            if (!result.success)
                return (false, result.message);

            if (!pending.IsCurrent(manager.GetCurrentParentId(), manager.GetCurrentChildId(), manager.SessionGeneration))
                return (false, "Đã lưu thói quen vào hồ sơ ban đầu. Hãy quay lại đúng hồ sơ để làm mới danh sách.");

            if (!await LoadHabitsForCurrentUser())
                return (false, "Đã lưu thói quen nhưng chưa thể làm mới danh sách. Nhấn Thử lại để tiếp tục.");

            return (true, "Đã lưu thói quen.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mission] Không thể lưu thói quen: {ex.Message}");
            return (false, "Không thể lưu thói quen. Vui lòng thử lại.");
        }
    }

    public void DeleteHabitById(string habitId)
    {
        _ = DeleteHabitByIdAsync(habitId);
    }

    public void UpdateHabitActiveById(string habitId, bool isActive)
    {
        _ = UpdateHabitActiveByIdAsync(habitId, isActive);
    }

    public async Task<bool> UpdateHabitActiveByIdAsync(string habitId, bool isActive)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        string parentId = currentParentId;
        string childId = currentChildId;
        if (manager == null || string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) ||
            string.IsNullOrEmpty(habitId) || manager.SessionGeneration != currentSessionGeneration ||
            manager.GetCurrentParentId() != parentId || manager.GetCurrentChildId() != childId)
        {
            Debug.LogWarning("[Mission] Không thể cập nhật trạng thái vì thiếu thông tin user/child/habit.");
            return false;
        }

        try
        {
            return await manager.UpdateHabitActiveAsync(parentId, childId, habitId, isActive);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mission] Không thể cập nhật trạng thái thói quen: {ex.Message}");
            return false;
        }
    }

    public async Task DeleteHabitByIdAsync(string habitId)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        string parentId = currentParentId;
        string childId = currentChildId;
        if (manager == null || string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Mission] Không thể xóa thói quen vì thiếu thông tin user/child/habit.");
            return;
        }

        try
        {
            await manager.DeleteHabitAsync(parentId, childId, habitId);
            if (this == null)
                return;
            if (manager == FirebaseManager.Instance && manager.GetCurrentParentId() == parentId &&
                manager.GetCurrentChildId() == childId)
                await LoadHabitsForCurrentUser();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mission] Không thể xóa thói quen: {ex.Message}");
        }
    }

    // private void SetHeader()
    // {
    //     if (missionUI != null)
    //     {
    //         missionUI.SetMissionHeader("Nhiệm vụ", "Danh sách thói quen của con");
    //     }
    // }

    // private void Awake()
    // {
    //     SetHeader();
    // }

}
