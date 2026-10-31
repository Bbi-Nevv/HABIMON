using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    #region Instance
    public static FirebaseManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private ChildService childService;

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

        currentChildId = await childService.ResolveCurrentChildIdAsync(parentId);
        return currentChildId;
    }

    public async Task<List<ChildModel>> GetChildrenAsync(string parentId)
    {
        if (!IsInitialized || string.IsNullOrEmpty(parentId))
            return new List<ChildModel>();

        return await childService.GetChildrenAsync(parentId);
    }

    public ListenerRegistration ListenChildren(string parentId, Action<List<ChildModel>> onChanged)
    {
        if (!IsInitialized || string.IsNullOrEmpty(parentId) || onChanged == null)
            return null;

        return db.Collection("users").Document(parentId).Collection("children")
            .Listen(snapshot => onChanged(snapshot.Documents.Select(ReadChild).Where(child => child != null).ToList()));
    }

    public async Task<(bool success, string message)> AddChildAsync(string name, int age, int gender)
    {
        if (!IsInitialized)
            return (false, "Firebase chưa sẵn sàng.");

        string parentId = GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.");

        var result = await childService.AddChildAsync(parentId, name, age, gender);
        if (result.success && string.IsNullOrEmpty(currentChildId))
            currentChildId = result.childId;

        return (result.success, result.message);
    }

    public async Task<(bool success, string message)> UpdateChildAsync(string childId, string name, int age, int gender)
    {
        if (!IsInitialized)
            return (false, "Firebase chưa sẵn sàng.");

        return await childService.UpdateChildAsync(GetCurrentParentId(), childId, name, age, gender);
    }

    public async Task<(bool success, string message)> DeleteChildAsync(string childId)
    {
        if (!IsInitialized)
            return (false, "Firebase chưa sẵn sàng.");

        var result = await childService.DeleteChildAsync(GetCurrentParentId(), childId);
        if (result.success && currentChildId == childId)
            currentChildId = string.Empty;

        return result;
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
            childService = new ChildService(db);
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

        string stage = "Tạo tài khoản Firebase Auth";
        string documentPath = string.Empty;
        bool authAccountCreated = false;
        try
        {
            // 1. Tạo tài khoản trên Firebase Auth
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = result.User;
            authAccountCreated = true;
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
            // Firebase Auth quản lý mật khẩu; Firestore chỉ lưu thông tin hồ sơ.
            DocumentReference parentRef = db.Collection("users").Document(parentId);
            ParentModel parentData = new ParentModel
            {
                email = email.Trim(),
                pincode = HashPin("0000"),
                createdAt = Timestamp.GetCurrentTimestamp()
            };
            stage = "Tạo hồ sơ phụ huynh";
            documentPath = parentRef.Path;
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
            stage = "Tạo hồ sơ trẻ";
            documentPath = childRef.Path;
            await childRef.SetAsync(childData);

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
            stage = "Tạo Habimon mặc định";
            documentPath = petRef.Path;
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
            stage = "Tạo thói quen mẫu";
            documentPath = habitRef.Path;
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
            stage = "Tạo phần thưởng mẫu";
            documentPath = rewardRef.Path;
            await rewardRef.SetAsync(defaultReward);

            currentChildId = childId;
            return (true, "Đăng ký thành công!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RegisterParent] Thất bại tại bước '{stage}'. " +
                $"Project: {db.App.Options.ProjectId}; UID: {GetCurrentParentId()}; " +
                $"Document: {documentPath}\n{ex}");

            string accountStatus = authAccountCreated
                ? "Tài khoản xác thực đã được tạo nhưng dữ liệu đăng ký chưa hoàn tất. "
                : string.Empty;

            if (ex is FirestoreException firestoreEx)
            {
                if (firestoreEx.ErrorCode == FirestoreError.PermissionDenied)
                    return (false, accountStatus +
                        "Firestore từ chối quyền lưu dữ liệu. Vui lòng kiểm tra Security Rules theo UID (xem Console để biết đường dẫn lỗi).");

                return (false, accountStatus + $"Lỗi Firestore tại bước '{stage}' ({firestoreEx.ErrorCode}): {firestoreEx.Message}");
            }

            if (ex is FirebaseException fbEx)
            {
                return (false, accountStatus + $"Lỗi Firebase tại bước '{stage}' ({fbEx.ErrorCode}): {fbEx.Message}");
            }

            return (false, accountStatus + $"Lỗi tại bước '{stage}': {ex.Message}");
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

        bool authenticated = false;
        try
        {
            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            authenticated = true;
            // Hoàn thiện hồ sơ nếu lần đăng ký trước chỉ tạo được tài khoản Auth.
            await EnsureParentDataAsync(result.User, result.User.Email);
            await ResolveCurrentChildIdAsync();
            return (true, "Đăng nhập thành công!");
        }
        catch (System.Exception ex)
        {
            if (authenticated)
            {
                Debug.LogError($"[LoginParent] Đã xác thực nhưng không thể hoàn thiện/đọc hồ sơ Firestore.\n{ex}");
                return (false, "Đã xác thực tài khoản nhưng không thể tải hồ sơ. Vui lòng kiểm tra quyền Firestore.");
            }

            Debug.LogWarning("Tài khoản chưa tồn tại");
            return (false, "Tài khoản chưa tồn tại");
        }
    }

    public async Task<(bool success, string message)> LoginWithGoogleAsync()
    {
        if (!IsInitialized) return (false, "Hệ thống chưa sẵn sàng!");

        string stage = "Mở Firebase Google OAuth";
        try
        {
            FederatedOAuthProviderData providerData = new FederatedOAuthProviderData
            {
                ProviderId = GoogleAuthProvider.ProviderId
            };
            FederatedOAuthProvider provider = new FederatedOAuthProvider(providerData);
            await auth.SignInWithProviderAsync(provider);

            stage = "Tạo hồ sơ Google trên Firestore";
            FirebaseUser user = auth.CurrentUser;
            await EnsureParentDataAsync(user, user?.Email);

            stage = "Đọc hồ sơ trẻ trên Firestore";
            await ResolveCurrentChildIdAsync();
            return (true, "Đăng nhập Google thành công!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Google Auth] Thất bại tại bước '{stage}'.\n{ex}");
            if (ex is FirebaseException fbEx)
                return (false, $"Lỗi Firebase tại bước '{stage}' ({fbEx.ErrorCode}): {fbEx.Message}");

            return (false, $"Lỗi tại bước '{stage}': {ex.Message}");
        }
    }

    private async Task EnsureParentDataAsync(FirebaseUser user, string email)
    {
        if (user == null)
            throw new InvalidOperationException("Không lấy được Firebase user sau khi đăng nhập.");

        DocumentReference parentRef = db.Collection("users").Document(user.UserId);
        DocumentSnapshot parentSnapshot = await parentRef.GetSnapshotAsync();
        if (!parentSnapshot.Exists)
        {
            ParentModel parentData = new ParentModel
            {
                email = string.IsNullOrWhiteSpace(email) ? user.Email : email.Trim(),
                pincode = HashPin("0000"),
                createdAt = Timestamp.GetCurrentTimestamp()
            };
            await parentRef.SetAsync(parentData);
        }

        CollectionReference childrenRef = parentRef.Collection("children");
        QuerySnapshot children = await childrenRef.GetSnapshotAsync();
        if (children.Count > 0)
            return;

        string childId = Guid.NewGuid().ToString();
        DocumentReference childRef = childrenRef.Document(childId);
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

        await childRef.Collection("habimon").Document("status").SetAsync(new HabimonModel
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
        });

        await childRef.Collection("habits").Document("habit_001").SetAsync(new HabitModel
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
        });

        await childRef.Collection("rewards").Document("reward_01").SetAsync(new RewardModel
        {
            name = "Móc khóa thú cưng",
            itemType = "item",
            cost = 50,
            isUnlocked = false,
            createdAt = Timestamp.GetCurrentTimestamp()
        });
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

    public Task<bool> AddHabitLogAsync(string parentId, string childId, HabitLogModel habitLog)
    {
        if (habitLog == null || habitLog.isApproved)
            return Task.FromResult(false);

        return SetHabitCompletionAsync(parentId, childId, habitLog.habitId, habitLog.dateKey, habitLog.isCompleted);
    }

    public async Task<bool> UpdateHabitLogAsync(string parentId, string childId, string logId, HabitLogModel habitLog)
    {
        if (!IsInitialized || habitLog == null || habitLog.isApproved || string.IsNullOrEmpty(logId)
            || !HasHabitLogContext(parentId, childId, habitLog.habitId, habitLog.dateKey))
            return false;

        try
        {
            DocumentSnapshot snapshot = await GetHabitLogsReference(parentId, childId).Document(logId).GetSnapshotAsync();
            HabitLogModel existingLog = ReadHabitLog(snapshot);
            if (existingLog == null || existingLog.habitId != habitLog.habitId || existingLog.dateKey != habitLog.dateKey)
                return false;

            return await SetHabitCompletionAsync(parentId, childId, habitLog.habitId, habitLog.dateKey, habitLog.isCompleted);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể cập nhật log thói quen: {ex.Message}");
            return false;
        }
    }

    public async Task<HabitLogModel> GetHabitLogAsync(string parentId, string childId, string habitId, string dateKey)
    {
        if (!IsInitialized || !HasHabitLogContext(parentId, childId, habitId, dateKey))
            return null;

        QuerySnapshot snapshot = await GetHabitLogsReference(parentId, childId)
            .WhereEqualTo("habitId", habitId)
            .WhereEqualTo("dateKey", dateKey)
            .GetSnapshotAsync();

        return SelectHabitLog(snapshot.Documents.Select(ReadHabitLog));
    }

    public ListenerRegistration ListenHabitLogsForDate(string parentId, string childId, string dateKey, Action<List<HabitLogModel>> onChanged)
    {
        if (!IsInitialized || string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId)
            || string.IsNullOrEmpty(dateKey) || onChanged == null)
            return null;

        // Include canceled logs so a daily document can supersede a pending legacy log.
        return GetHabitLogsReference(parentId, childId)
            .WhereEqualTo("dateKey", dateKey)
            .Listen(snapshot => onChanged(NormalizeHabitLogs(snapshot.Documents.Select(ReadHabitLog))));
    }

    public ListenerRegistration ListenHabitLogs(string parentId, string childId, Action<List<HabitLogModel>> onChanged)
    {
        if (!IsInitialized || string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || onChanged == null)
            return null;

        // Normalize every day before callers filter pending requests; canceled canonical logs must win.
        return GetHabitLogsReference(parentId, childId)
            .Listen(snapshot => onChanged(NormalizeHabitLogs(snapshot.Documents.Select(ReadHabitLog))));
    }

    public async Task<bool> SetHabitCompletionAsync(string parentId, string childId, string habitId, string dateKey, bool isCompleted)
    {
        if (!IsInitialized || !HasHabitLogContext(parentId, childId, habitId, dateKey) || !HabitLog.CanEditToday(dateKey))
            return false;

        try
        {
            Timestamp completedAt = Timestamp.GetCurrentTimestamp();
            CollectionReference logsRef = GetHabitLogsReference(parentId, childId);
            DocumentReference dailyLogRef = logsRef.Document(GetHabitLogDocumentId(habitId, dateKey));
            List<DocumentReference> references = await GetHabitLogReferencesAsync(logsRef, dailyLogRef, habitId, dateKey);

            return await db.RunTransactionAsync(async transaction =>
            {
                HabitLogModel existingLog = await ReadHabitLogsInTransactionAsync(transaction, references);
                if (!HabitLog.CanEditToday(dateKey) || (existingLog != null && existingLog.isApproved))
                    return false;

                // Repeated requests keep the original completion time; canceling an empty day is a no-op.
                if (existingLog == null && !isCompleted)
                    return true;
                if (existingLog != null && existingLog.isCompleted == isCompleted)
                    return true;

                HabitLogModel updatedLog = new HabitLogModel
                {
                    habitId = habitId,
                    dateKey = dateKey,
                    isCompleted = isCompleted,
                    isApproved = false,
                    completedAt = isCompleted ? completedAt : existingLog.completedAt
                };

                transaction.Set(dailyLogRef, updatedLog, SetOptions.MergeAll);
                return true;
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể lưu trạng thái hoàn thành: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ApproveHabitLogAsync(string parentId, string childId, string habitId, string dateKey, Timestamp expectedCompletedAt)
    {
        if (!IsInitialized || !HasHabitLogContext(parentId, childId, habitId, dateKey))
            return false;

        try
        {
            CollectionReference logsRef = GetHabitLogsReference(parentId, childId);
            DocumentReference dailyLogRef = logsRef.Document(GetHabitLogDocumentId(habitId, dateKey));
            List<DocumentReference> references = await GetHabitLogReferencesAsync(logsRef, dailyLogRef, habitId, dateKey);

            return await db.RunTransactionAsync(async transaction =>
            {
                HabitLogModel existingLog = await ReadHabitLogsInTransactionAsync(transaction, references);
                if (existingLog == null || !existingLog.isCompleted || existingLog.completedAt != expectedCompletedAt)
                    return false;

                if (existingLog.isApproved)
                    return true;

                transaction.Set(dailyLogRef, new HabitLogModel
                {
                    habitId = habitId,
                    dateKey = dateKey,
                    isCompleted = true,
                    isApproved = true,
                    completedAt = existingLog.completedAt
                }, SetOptions.MergeAll);
                return true;
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể xác nhận thói quen: {ex.Message}");
            return false;
        }
    }

    private static bool HasHabitLogContext(string parentId, string childId, string habitId, string dateKey)
    {
        return !string.IsNullOrEmpty(parentId) && !string.IsNullOrEmpty(childId)
            && !string.IsNullOrEmpty(habitId) && !string.IsNullOrEmpty(dateKey);
    }

    private CollectionReference GetHabitLogsReference(string parentId, string childId)
    {
        return db.Collection("users").Document(parentId).Collection("children").Document(childId).Collection("habit_logs");
    }

    private static string GetHabitLogDocumentId(string habitId, string dateKey)
    {
        return $"{dateKey}_{habitId}";
    }

    private static ChildModel ReadChild(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists)
            return null;

        ChildModel child = snapshot.ConvertTo<ChildModel>();
        if (child != null)
            child.childId = snapshot.Id;
        return child;
    }

    private static HabitModel ReadHabit(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists)
            return null;

        HabitModel habit = snapshot.ConvertTo<HabitModel>();
        if (habit != null)
            habit.id = snapshot.Id;
        return habit;
    }

    private static HabitLogModel ReadHabitLog(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists)
            return null;

        HabitLogModel log = snapshot.ConvertTo<HabitLogModel>();
        if (log != null)
            log.logId = snapshot.Id;
        return log;
    }

    private static HabitLogModel SelectHabitLog(IEnumerable<HabitLogModel> logs)
    {
        // Keep legacy random-ID logs. An approved log always wins; otherwise prefer the daily document.
        return logs.Where(log => log != null)
            .OrderByDescending(log => log.isApproved)
            .ThenByDescending(log => log.logId == GetHabitLogDocumentId(log.habitId, log.dateKey))
            .ThenByDescending(log => log.completedAt)
            .ThenBy(log => log.logId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static List<HabitLogModel> NormalizeHabitLogs(IEnumerable<HabitLogModel> logs)
    {
        return logs.Where(log => log != null)
            .GroupBy(log => (log.habitId, log.dateKey))
            .Select(group => SelectHabitLog(group))
            .ToList();
    }

    private async Task<List<DocumentReference>> GetHabitLogReferencesAsync(CollectionReference logsRef, DocumentReference dailyLogRef, string habitId, string dateKey)
    {
        QuerySnapshot snapshot = await logsRef.WhereEqualTo("habitId", habitId).WhereEqualTo("dateKey", dateKey).GetSnapshotAsync();
        List<DocumentReference> references = new List<DocumentReference> { dailyLogRef };
        references.AddRange(snapshot.Documents.Where(document => document.Id != dailyLogRef.Id).Select(document => document.Reference));
        return references;
    }

    private static async Task<HabitLogModel> ReadHabitLogsInTransactionAsync(Transaction transaction, List<DocumentReference> references)
    {
        List<HabitLogModel> logs = new List<HabitLogModel>();
        // Every mutation reads the same daily document, including when it does not exist yet.
        foreach (DocumentReference reference in references)
            logs.Add(ReadHabitLog(await transaction.GetSnapshotAsync(reference)));
        return SelectHabitLog(logs);
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

    public ListenerRegistration ListenHabits(string parentId, string childId, Action<List<HabitModel>> onChanged)
    {
        if (!IsInitialized || string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || onChanged == null)
            return null;

        return db.Collection("users").Document(parentId).Collection("children").Document(childId).Collection("habits")
            .Listen(snapshot => onChanged(snapshot.Documents.Select(ReadHabit).Where(habit => habit != null).ToList()));
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

        return NormalizeHabitLogs(logs);
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
