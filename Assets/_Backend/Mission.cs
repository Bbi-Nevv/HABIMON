using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private MissionUI missionUI;

    private string currentParentId;
    private string currentChildId;
    private bool hasStarted;

    public bool HasStarted => hasStarted;

    private async void Start()
    {
        hasStarted = true;

        if (missionUI == null)
            missionUI = UnityEngine.Object.FindFirstObjectByType<MissionUI>();

        await RefreshCurrentUserContextAsync();
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

                missionUI.SpawnHabitItem(sampleHabit, sampleHabit.id, DeleteHabitById, UpdateHabitActiveById);
    }

    public void AddDataToUi()
    {
        _ = RefreshCurrentUserContextAsync();
        _ = LoadHabitsForCurrentUser();
    }

    private async Task RefreshCurrentUserContextAsync()
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[Mission] Firebase chưa sẵn sàng để xác định user đang đăng nhập.");
            return;
        }

        currentParentId = FirebaseManager.Instance.GetCurrentParentId();
        currentChildId = FirebaseManager.Instance.GetCurrentChildId();

        if (string.IsNullOrEmpty(currentChildId))
        {
            currentChildId = await FirebaseManager.Instance.ResolveCurrentChildIdAsync();
        }
    }

    public async Task LoadHabitsForCurrentUser()
    {
        await RefreshCurrentUserContextAsync();

        if (string.IsNullOrEmpty(currentParentId))
        {
            SceneManager.LoadScene("Menu");
            Debug.LogWarning("[Mission] Chưa có parentId từ tài khoản đang đăng nhập.");
            return;
        }

        try
        {
            var habits = await FirebaseManager.Instance.GetHabitsAsync(currentParentId, currentChildId);
            Debug.Log($"[Mission] Đã lấy được {habits.Count} habit từ Firebase. missionUI null? {missionUI == null}");

            if (missionUI != null)
            {
                missionUI.ClearHabitItems();

                foreach (var habit in habits)
                {
                    Debug.Log($"[Mission] Render habit: {habit.title} | id={habit.id}");
                    missionUI.SpawnHabitItem(habit, habit.id, DeleteHabitById, UpdateHabitActiveById);
                }
            }
            else
            {
                Debug.LogError("[Mission] missionUI == null => không render được gì lên UI.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mission] Lỗi khi tải thói quen cho user: {ex.Message}");
        }
    }

    public async Task SubmitHabitFromPopup(string title, string description, string timeTarget, string iconKey = "icon_default")
    {
        await RefreshCurrentUserContextAsync();

        if (string.IsNullOrEmpty(currentParentId) || string.IsNullOrEmpty(currentChildId))
        {
            Debug.LogWarning("[Mission] Không thể submit thói quen vì không xác định được user/child.");
            return;
        }

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(timeTarget))
        {
            Debug.LogWarning("[Mission] Tên, mô tả và giờ cần được nhập đầy đủ.");
            return;
        }

        if (string.IsNullOrEmpty(iconKey))
            iconKey = "icon_default";

        HabitModel habit = new HabitModel
        {
            title = title,
            description = description,
            icon = iconKey,
            timeTarget = timeTarget,
            repeatDays = new List<int> { 1, 2, 3, 4, 5, 6, 7 },
            rewardGold = 10,
            rewardExp = 20,
            requireApproval = true,
            isActive = true
        };

        await FirebaseManager.Instance.AddHabitAsync(currentParentId, currentChildId, habit);
        await LoadHabitsForCurrentUser();
    }

    public void DeleteHabitById(string habitId)
    {
        _ = DeleteHabitByIdAsync(habitId);
    }

    public void UpdateHabitActiveById(string habitId, bool isActive)
    {
        _ = UpdateHabitActiveByIdAsync(habitId, isActive);
    }

    private async Task UpdateHabitActiveByIdAsync(string habitId, bool isActive)
    {
        await RefreshCurrentUserContextAsync();

        if (string.IsNullOrEmpty(currentParentId) || string.IsNullOrEmpty(currentChildId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Mission] Không thể cập nhật trạng thái vì thiếu thông tin user/child/habit.");
            return;
        }

        await FirebaseManager.Instance.UpdateHabitActiveAsync(currentParentId, currentChildId, habitId, isActive);
    }

    public async Task DeleteHabitByIdAsync(string habitId)
    {
        await RefreshCurrentUserContextAsync();

        if (string.IsNullOrEmpty(currentParentId) || string.IsNullOrEmpty(currentChildId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Mission] Không thể xóa thói quen vì thiếu thông tin user/child/habit.");
            return;
        }

        await FirebaseManager.Instance.DeleteHabitAsync(currentParentId, currentChildId, habitId);
        await LoadHabitsForCurrentUser();
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
