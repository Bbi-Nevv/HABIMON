using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    #region Instance
    public static FirebaseManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    public FirebaseUser CurrentUser => auth?.CurrentUser;
    public bool IsInitialized { get; private set; } = false;

    private string currentChildId = string.Empty;
    public string CurrentChildId => currentChildId;

    public string GetCurrentParentId()
    {
        return auth != null && auth.CurrentUser != null ? auth.CurrentUser.UserId : string.Empty;
    }

    public string GetCurrentChildId()
    {
        return currentChildId;
    }

    public void SetCurrentChildId(string childId)
    {
        currentChildId = string.IsNullOrEmpty(childId) ? string.Empty : childId;
    }

    public async Task<string> ResolveCurrentChildIdAsync()
    {
        string parentId = GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId) || !IsInitialized)
            return string.Empty;

        CollectionReference childrenRef = db.Collection("users")
            .Document(parentId)
            .Collection("children");

        QuerySnapshot snapshot = await childrenRef.GetSnapshotAsync();
        var firstChild = snapshot.Documents.FirstOrDefault();

        if (firstChild == null || !firstChild.Exists)
        {
            currentChildId = string.Empty;
            return string.Empty;
        }

        currentChildId = firstChild.Id;
        return currentChildId;
    }

    public async Task<List<ChildModel>> GetChildrenAsync(string parentId)
    {
        List<ChildModel> children = new List<ChildModel>();

        if (!IsInitialized || string.IsNullOrEmpty(parentId))
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
            if (child != null)
            {
                child.childId = doc.Id;
                if (!doc.ContainsField("gender"))
                    child.gender = -1;
                children.Add(child);
            }
        }

        return children;
    }

    public async Task<(bool success, string message)> AddChildAsync(string name, int age, int gender)
    {
        if (!IsInitialized)
            return (false, "Firebase chưa sẵn sàng.");

        string parentId = GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.");

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Vui lòng nhập tên trẻ.");

        if (age <= 0)
            return (false, "Tuổi phải lớn hơn 0.");

        if (gender != 0 && gender != 1)
            return (false, "Vui lòng chọn giới tính.");

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
            if (string.IsNullOrEmpty(currentChildId))
                currentChildId = childId;

            return (true, "Đã thêm trẻ.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể thêm trẻ: {ex.Message}");
            return (false, "Không thể thêm trẻ. Vui lòng thử lại.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private async void Start()
    {
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            IsInitialized = true;
            Debug.Log("<color=green>[Firebase]</color> Khởi tạo Auth & Firestore thành công!");
        }
    }
    #endregion

    #region Register New Account
    // --- HÀM ĐĂNG KÝ TÀI KHOẢN MỚI ---
    public async Task<(bool success, string message)> RegisterParentAsync(string email, string password)
    {
        if (!IsInitialized) return (false, "Hệ thống chưa sẵn sàng!");

        try
        {
            // 1. Tạo tài khoản trên Firebase Auth
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = result.User;
            string parentId = newUser.UserId;
            if (parentId != null)
            {
                Debug.Log($"<color=green>[Firebase]</color> Tạo tài khoản thành công! Parent ID: {parentId}");
            }
            else
            {
                Debug.LogError("<color=red>[Firebase]</color> Không thể lấy Parent ID sau khi tạo tài khoản!");
                return (false, "Đăng ký thất bại!");
            }

            // 2. Tạo thông tin Phụ huynh trên Firestore
            // Lưu password dưới dạng hash + salt thay vì plain text để tránh rò rỉ dữ liệu nhạy cảm.
            string passwordSalt = GenerateSalt();
            string passwordHash = HashPassword(password, passwordSalt);

            DocumentReference parentRef = db.Collection("users").Document(parentId);
            ParentModel parentData = new ParentModel
            {
                email = email.Trim(),
                passwordHash = passwordHash,
                passwordSalt = passwordSalt,
                pincode = HashPin("0000"),
                createdAt = Timestamp.GetCurrentTimestamp()
            };
            await parentRef.SetAsync(parentData);

            // 3. Tạo sẵn 1 em bé mặc định + Thú cưng ban đầu cho bé
            string childId = System.Guid.NewGuid().ToString();
            DocumentReference childRef = parentRef.Collection("children").Document(childId);
            ChildModel childData = new ChildModel
            {
                childId = childId,
                name = "Unnull",
                gender = 1,
                age = 0,
                gold = 0,
                totalExp = 0,
                createdAt = Timestamp.GetCurrentTimestamp()
            };
            await childRef.SetAsync(childData);
            currentChildId = childId;

            // 4. Tạo Habimon mặc định cho bé
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
            await petRef.SetAsync(defaultPet);

            // 5. Tạo 1 thói quen mẫu và 1 reward mẫu nếu cần
            DocumentReference habitRef = childRef.Collection("habits").Document("habit_001");
            HabitModel defaultHabit = new HabitModel
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
            await habitRef.SetAsync(defaultHabit);

            DocumentReference rewardRef = childRef.Collection("rewards").Document("reward_01");
            RewardModel defaultReward = new RewardModel
            {
                name = "Móc khóa thú cưng",
                itemType = "item",
                cost = 50,
                isUnlocked = false,
                createdAt = Timestamp.GetCurrentTimestamp()
            };
            await rewardRef.SetAsync(defaultReward);

            return (true, "Đăng ký thành công!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Firebase Auth Error] {ex.Message}");

            if (ex is FirebaseException fbEx)
            {
                return (false, $"Lỗi Firebase ({fbEx.ErrorCode}): {fbEx.Message}");
            }

            return (false, ex.Message);
        }
    }
    #endregion

    #region Parent PIN
    public static string HashPin(string pin)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);

            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }

        return Convert.ToBase64String(saltBytes);
    }

    public static string HashPassword(string password, string salt)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        string combined = password + salt;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);

            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        return HashPassword(password, storedSalt) == storedHash;
    }

    public async Task<bool> VerifyParentPinAsync(string pin)
    {
        if (!IsInitialized || string.IsNullOrEmpty(GetCurrentParentId()))
            return false;

        DocumentSnapshot snapshot = await db.Collection("users")
            .Document(GetCurrentParentId())
            .GetSnapshotAsync();

        if (!snapshot.Exists || !snapshot.ContainsField("pincode"))
            return false;

        string storedHash = snapshot.GetValue<string>("pincode");
        return storedHash == HashPin(pin);
    }

    public async Task<bool> ChangeParentPinAsync(string currentPin, string newPin)
    {
        if (!IsInitialized || string.IsNullOrEmpty(GetCurrentParentId()))
            return false;

        if (!await VerifyParentPinAsync(currentPin))
            return false;

        await db.Collection("users")
            .Document(GetCurrentParentId())
            .UpdateAsync("pincode", HashPin(newPin));

        return true;
    }
    #endregion

    #region Login
    public async Task<(bool success, string message)> LoginParentAsync(string email, string password)
    {
        if (!IsInitialized) return (false, "Hệ thống chưa sẵn sàng!");

        try
        {
            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            await ResolveCurrentChildIdAsync();
            return (true, "Đăng nhập thành công!");
        }
        catch (System.Exception)
        {
            Debug.LogWarning("Tài khoản chưa tồn tại");
            return (false, "Tài khoản chưa tồn tại");
        }
    }

    public async Task<(bool success, string message)> SendPasswordResetEmailAsync(string email)
    {
        if (!IsInitialized) return (false, "Hệ thống chưa sẵn sàng!");

        if (string.IsNullOrWhiteSpace(email))
            return (false, "Vui lòng nhập email.");

        try
        {
            await auth.SendPasswordResetEmailAsync(email);
            return (true, "Email đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra hộp thư.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Firebase Reset Password] {ex.Message}");

            if (ex is FirebaseException fbEx)
            {
                return (false, fbEx.Message);
            }

            return (false, ex.Message);
        }
    }
    #endregion

    #region Logout
    public void LogoutParent()
    {
        if (auth != null && auth.CurrentUser != null)
        {
            auth.SignOut();
            Debug.Log("<color=yellow>[Firebase Auth]</color> Đã đăng xuất thành công!");
        }
    }
    #endregion

    #region Habit
    // --- HÀM THÊM THÓI QUEN MỚI ---
    public async Task AddHabitAsync(string parentId, string childId, HabitModel habit)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        CollectionReference habitsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habits");

        await habitsRef.AddAsync(habit);
        Debug.Log("<color=cyan>[Firebase]</color> Đã thêm thói quen mới thành công!");
    }

    public async Task UpdateHabitActiveAsync(string parentId, string childId, string habitId, bool isActive)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Firebase] Thiếu parentId, childId hoặc habitId để cập nhật trạng thái thói quen.");
            return;
        }

        DocumentReference habitRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habits")
            .Document(habitId);

        await habitRef.UpdateAsync("isActive", isActive);
    }

    public async Task DeleteHabitAsync(string parentId, string childId, string habitId)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Firebase] Thiếu parentId, childId hoặc habitId để xóa thói quen.");
            return;
        }

        DocumentReference habitRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habits")
            .Document(habitId);

        await habitRef.DeleteAsync();
        Debug.Log("<color=cyan>[Firebase]</color> Đã xóa thói quen thành công!");
    }

    public async Task AddHabitLogAsync(string parentId, string childId, HabitLogModel habitLog)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        CollectionReference habitLogsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs");

        await habitLogsRef.AddAsync(habitLog);
        Debug.Log("<color=cyan>[Firebase]</color> Đã ghi log thói quen thành công!");
    }

    public async Task UpdateHabitLogAsync(string parentId, string childId, string logId, HabitLogModel habitLog)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(logId))
        {
            Debug.LogWarning("[Firebase] Thiếu parentId, childId hoặc logId để cập nhật log thói quen.");
            return;
        }

        DocumentReference habitLogRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs")
            .Document(logId);

        await habitLogRef.SetAsync(habitLog, SetOptions.MergeAll);
        Debug.Log("<color=cyan>[Firebase]</color> Đã cập nhật log thói quen thành công!");
    }

    public async Task AddRewardAsync(string parentId, string childId, RewardModel reward)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        CollectionReference rewardsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("rewards");

        await rewardsRef.AddAsync(reward);
        Debug.Log("<color=cyan>[Firebase]</color> Đã thêm phần thưởng mới thành công!");
    }

    // --- HÀM LẤY DANH SÁCH THÓI QUEN ---
    public async Task<List<HabitModel>> GetHabitsAsync(string parentId, string childId)
    {
        List<HabitModel> habitList = new List<HabitModel>();

        if (!IsInitialized) return habitList;

        CollectionReference habitsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habits");

        QuerySnapshot snapshot = await habitsRef.GetSnapshotAsync();

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (doc.Exists)
            {
                HabitModel habit = doc.ConvertTo<HabitModel>();
                if (habit != null)
                {
                    habit.id = doc.Id;
                }

                habitList.Add(habit);
            }
        }

        Debug.Log($"<color=cyan>[Firebase]</color> Đã tải về {habitList.Count} thói quen.");
        return habitList;
    }

    public async Task<List<HabitLogModel>> GetHabitLogsAsync(string parentId, string childId)
    {
        List<HabitLogModel> logs = new List<HabitLogModel>();

        if (!IsInitialized) return logs;

        CollectionReference logsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs");

        QuerySnapshot snapshot = await logsRef.GetSnapshotAsync();
        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (doc.Exists)
            {
                HabitLogModel log = doc.ConvertTo<HabitLogModel>();
                if (log != null)
                {
                    log.logId = doc.Id;
                }

                logs.Add(log);
            }
        }

        return logs;
    }

    public async Task<List<RewardModel>> GetRewardsAsync(string parentId, string childId)
    {
        List<RewardModel> rewards = new List<RewardModel>();

        if (!IsInitialized) return rewards;

        CollectionReference rewardsRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("rewards");

        QuerySnapshot snapshot = await rewardsRef.GetSnapshotAsync();
        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (doc.Exists)
            {
                rewards.Add(doc.ConvertTo<RewardModel>());
            }
        }

        return rewards;
    }
    #endregion

}