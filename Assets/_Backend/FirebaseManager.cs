using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HABIMON.Core;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    #region Instance
    public static FirebaseManager Instance { get; private set; }

    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private ChildService childService;
    private TaskCompletionSource<bool> initializationCompletion;

    public FirebaseUser CurrentUser => auth?.CurrentUser;
    public bool IsInitialized { get; private set; } = false;
    public string InitializationError { get; private set; }
    public event Action InitializationChanged;
    public int SessionGeneration => sessionGeneration;
    public event Action CurrentChildChanged;
    public event Action SessionChanged;

    private string currentChildId = string.Empty;
    private string sessionParentId = string.Empty;
    private int sessionGeneration;
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
        string normalizedChildId = string.IsNullOrWhiteSpace(childId) ? string.Empty : childId;
        if (currentChildId == normalizedChildId)
            return;

        currentChildId = normalizedChildId;
        sessionGeneration++;

        string parentId = GetCurrentParentId();
        if (!string.IsNullOrEmpty(parentId))
        {
            string selectionKey = GetChildSelectionKey(parentId);
            if (string.IsNullOrEmpty(currentChildId))
                PlayerPrefs.DeleteKey(selectionKey);
            else
                PlayerPrefs.SetString(selectionKey, currentChildId);
            PlayerPrefs.Save();
        }

        CurrentChildChanged?.Invoke();
    }

    public async Task<string> ResolveCurrentChildIdAsync()
    {
        string parentId = GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId) || !await EnsureInitializedAsync())
            return string.Empty;

        int requestGeneration = sessionGeneration;
        string selectedChildId = currentChildId;
        List<ChildModel> children = await childService.GetChildrenAsync(parentId);

        if (parentId != GetCurrentParentId())
            return string.Empty;
        if (requestGeneration != sessionGeneration)
            return currentChildId;

        string rememberedChildId = PlayerPrefs.GetString(GetChildSelectionKey(parentId), string.Empty);
        var availableChildIds = new List<string>();
        foreach (ChildModel child in children)
        {
            if (child != null && !string.IsNullOrEmpty(child.childId))
                availableChildIds.Add(child.childId);
        }

        string chosenChildId = ChildSelectionPolicy.Choose(selectedChildId, rememberedChildId, availableChildIds);
        SetCurrentChildId(chosenChildId);
        return currentChildId;
    }

    private static string GetChildSelectionKey(string parentId)
    {
        return "habimon.selected-child." + parentId;
    }

    private void ApplyAuthenticatedParent(string parentId)
    {
        string normalizedParentId = parentId ?? string.Empty;
        if (sessionParentId == normalizedParentId &&
            (!string.IsNullOrEmpty(normalizedParentId) || string.IsNullOrEmpty(currentChildId)))
            return;

        sessionParentId = normalizedParentId;
        currentChildId = string.Empty;
        sessionGeneration++;
        SessionChanged?.Invoke();
        CurrentChildChanged?.Invoke();
    }

    private void SignOutIfCurrentUser(string expectedUserId)
    {
        if (string.IsNullOrWhiteSpace(expectedUserId) || auth?.CurrentUser?.UserId != expectedUserId)
            return;

        auth.SignOut();
        ApplyAuthenticatedParent(string.Empty);
    }

    public async Task<List<ChildModel>> GetChildrenAsync(string parentId)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrEmpty(parentId))
            return new List<ChildModel>();

        return await childService.GetChildrenAsync(parentId);
    }

    public async Task<(bool success, string message)> AddChildAsync(PendingChildCreate pending)
    {
        if (pending == null)
            return (false, "Thiếu thông tin để thêm trẻ.");

        if (!await EnsureInitializedAsync())
            return (false, "Firebase chưa sẵn sàng.");

        if (!pending.IsCurrent(GetCurrentParentId(), sessionGeneration))
            return (false, "Phiên hoặc hồ sơ đã thay đổi. Hãy kiểm tra danh sách trước khi tạo thao tác mới.");

        var result = await childService.AddChildAsync(pending);
        if (result.success && pending.IsCurrent(GetCurrentParentId(), sessionGeneration) &&
            string.IsNullOrEmpty(currentChildId))
            SetCurrentChildId(result.childId);

        return (result.success, result.message);
    }

    public async Task<(bool success, string message)> UpdateChildAsync(string childId, string name, int age, int gender)
    {
        if (!await EnsureInitializedAsync())
            return (false, "Firebase chưa sẵn sàng.");

        return await childService.UpdateChildAsync(GetCurrentParentId(), childId, name, age, gender);
    }

    public async Task<(bool success, string message)> DeleteChildAsync(string childId)
    {
        if (!await EnsureInitializedAsync())
            return (false, "Firebase chưa sẵn sàng.");

        string parentId = GetCurrentParentId();
        if (string.IsNullOrEmpty(parentId))
            return (false, "Không xác định được phụ huynh đang đăng nhập.");

        var result = await childService.DeleteChildAsync(parentId, childId);
        if (result.success && GetCurrentParentId() == parentId && currentChildId == childId)
            SetCurrentChildId(string.Empty);

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
        await EnsureInitializedAsync();
    }

    public Task<bool> EnsureInitializedAsync()
    {
        if (IsInitialized)
            return Task.FromResult(true);
        if (initializationCompletion != null)
            return initializationCompletion.Task;

        initializationCompletion = new TaskCompletionSource<bool>();
        Task<bool> initializationTask = initializationCompletion.Task;
        _ = InitializeFirebaseAsync(initializationCompletion);
        return initializationTask;
    }

    private async Task InitializeFirebaseAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            InitializationError = null;
            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
                throw new InvalidOperationException($"Firebase dependencies are unavailable: {dependencyStatus}.");

            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            childService = new ChildService(db);
            ApplyAuthenticatedParent(auth.CurrentUser?.UserId);
            IsInitialized = true;
            Debug.Log("<color=green>[Firebase]</color> Khởi tạo Auth & Firestore thành công!");
        }
        catch (Exception ex)
        {
            IsInitialized = false;
            InitializationError = ex.Message;
            Debug.LogError($"[Firebase] Khởi tạo thất bại: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(initializationCompletion, completion))
                initializationCompletion = null;
            InitializationChanged?.Invoke();
            completion.TrySetResult(IsInitialized);
        }
    }
    #endregion

    #region Register New Account
    // --- HÀM ĐĂNG KÝ TÀI KHOẢN MỚI ---
    public async Task<(bool success, string message)> RegisterParentAsync(string email, string password)
    {
        if (!await EnsureInitializedAsync()) return (false, "Hệ thống chưa sẵn sàng!");

        bool authUserCreated = false;
        string createdParentId = string.Empty;
        try
        {
            // 1. Tạo tài khoản trên Firebase Auth
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            authUserCreated = true;
            FirebaseUser newUser = result.User;
            string parentId = newUser.UserId;
            createdParentId = parentId;
            ApplyAuthenticatedParent(parentId);
            if (parentId != null)
            {
                Debug.Log($"<color=green>[Firebase]</color> Tạo tài khoản thành công! Parent ID: {parentId}");
            }
            else
            {
                Debug.LogError("<color=red>[Firebase]</color> Không thể lấy Parent ID sau khi tạo tài khoản!");
                return (false, "Đăng ký thất bại!");
            }

            await EnsureParentDataAsync(parentId, email);
            await ResolveCurrentChildIdAsync();

            return (true, "Đăng ký thành công!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Firebase Auth Error] {ex.Message}");

            if (authUserCreated)
            {
                SignOutIfCurrentUser(createdParentId);

                return (false, "Tài khoản đã được tạo nhưng hồ sơ chưa hoàn tất. Hãy đăng nhập lại để tiếp tục thiết lập.");
            }

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

    public async Task<bool> VerifyParentPinAsync(string pin)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrEmpty(GetCurrentParentId()))
            return false;

        DocumentSnapshot snapshot = await db.Collection("users")
            .Document(GetCurrentParentId())
            .GetSnapshotAsync();

        if (!snapshot.Exists || !snapshot.ContainsField("pincode"))
            return false;

        string storedHash = snapshot.GetValue<string>("pincode");
        return storedHash == HashPin(pin);
    }

    public async Task<bool> HasParentPinAsync()
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrEmpty(GetCurrentParentId()))
            return false;

        DocumentSnapshot snapshot = await db.Collection("users")
            .Document(GetCurrentParentId())
            .GetSnapshotAsync();
        return snapshot.Exists && snapshot.ContainsField("pincode") &&
               !string.IsNullOrWhiteSpace(snapshot.GetValue<string>("pincode"));
    }

    public async Task<bool> ChangeParentPinAsync(string currentPin, string newPin)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrEmpty(GetCurrentParentId()))
            return false;

        bool hasPin = await HasParentPinAsync();
        if (hasPin)
        {
            if (!await VerifyParentPinAsync(currentPin))
                return false;
        }
        else if (!string.IsNullOrEmpty(currentPin))
        {
            return false;
        }

        try
        {
            await db.Collection("users")
                .Document(GetCurrentParentId())
                .UpdateAsync("pincode", HashPin(newPin));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể lưu mã phụ huynh: {ex.Message}");
            return false;
        }

        return true;
    }
    #endregion

    #region Login
    public async Task<(bool success, string message)> LoginParentAsync(string email, string password)
    {
        if (!await EnsureInitializedAsync()) return (false, "Hệ thống chưa sẵn sàng!");

        AuthResult result;
        try
        {
            result = await auth.SignInWithEmailAndPasswordAsync(email, password);
        }
        catch (System.Exception ex)
        {
            string message = GetLoginFailureMessage(ex);
            Debug.LogWarning($"[Firebase Auth] {message}");
            return (false, message);
        }

        ApplyAuthenticatedParent(result.User?.UserId);
        try
        {
            await EnsureParentDataAsync(result.User.UserId, result.User.Email);
            await ResolveCurrentChildIdAsync();
            return (true, "Đăng nhập thành công!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Firebase Profile] Đăng nhập đã xác thực nhưng hồ sơ chưa đồng bộ: {ex.Message}");
            SignOutIfCurrentUser(result.User?.UserId);

            return (false, "Đăng nhập đã xác thực nhưng hồ sơ chưa đồng bộ. Hãy thử đăng nhập lại để tiếp tục thiết lập.");
        }
    }

    private static string GetLoginFailureMessage(Exception exception)
    {
        if (exception is FirebaseException firebaseException)
        {
            string errorName = Enum.GetName(typeof(AuthError), firebaseException.ErrorCode);
            if (errorName == "NetworkRequestFailed" || errorName == "Timeout")
                return "Không thể kết nối Firebase. Kiểm tra mạng rồi thử lại.";
            if (errorName == "TooManyRequests")
                return "Có quá nhiều lần thử. Vui lòng chờ rồi thử lại.";
            if (errorName == "InvalidEmail" || errorName == "WrongPassword" || errorName == "UserNotFound")
                return "Email hoặc mật khẩu không đúng.";

            return $"Firebase không thể đăng nhập (mã lỗi {firebaseException.ErrorCode}).";
        }

        return "Không thể đăng nhập. Vui lòng thử lại.";
    }

    public Task<(bool success, string message)> LoginWithGoogleAsync()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        return LoginWithGoogleOnMobileAsync();
#else
        return Task.FromResult((false, "Đăng nhập Google chỉ hỗ trợ trên Android/iOS. Trong Editor và desktop, hãy dùng email."));
#endif
    }

    private async Task<(bool success, string message)> LoginWithGoogleOnMobileAsync()
    {
        if (!await EnsureInitializedAsync()) return (false, "Hệ thống chưa sẵn sàng!");

        string stage = "Mở Firebase Google OAuth";
        FirebaseUser authenticatedUser = null;
        try
        {
            FederatedOAuthProviderData providerData = new FederatedOAuthProviderData
            {
                ProviderId = GoogleAuthProvider.ProviderId
            };
            FederatedOAuthProvider provider = new FederatedOAuthProvider(providerData);
            await auth.SignInWithProviderAsync(provider);

            authenticatedUser = auth.CurrentUser;
            stage = "Tạo hồ sơ Google trên Firestore";
            ApplyAuthenticatedParent(authenticatedUser?.UserId);
            await EnsureGoogleParentDataAsync(authenticatedUser, authenticatedUser?.Email);

            stage = "Đọc hồ sơ trẻ trên Firestore";
            await ResolveCurrentChildIdAsync();
            return (true, "Đăng nhập Google thành công!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Google Auth] Thất bại tại bước '{stage}'.\n{ex}");
            SignOutIfCurrentUser(authenticatedUser?.UserId);

            if (ex is FirebaseException fbEx)
                return (false, $"Lỗi Firebase tại bước '{stage}' ({fbEx.ErrorCode}): {fbEx.Message}");

            return (false, $"Lỗi tại bước '{stage}': {ex.Message}");
        }
    }

    private async Task EnsureGoogleParentDataAsync(FirebaseUser user, string email)
    {
        if (user == null)
            throw new InvalidOperationException("Không lấy được Firebase user sau khi đăng nhập Google.");

        string parentEmail = string.IsNullOrWhiteSpace(email) ? user.Email : email.Trim();
        await EnsureParentDataAsync(user.UserId, parentEmail);
    }

    private async Task EnsureParentDataAsync(string parentId, string email)
    {
        DocumentReference parentRef = db.Collection("users").Document(parentId);
        await db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot parentSnapshot = await transaction.GetSnapshotAsync(parentRef);
            if (!parentSnapshot.Exists)
            {
                transaction.Set(parentRef, new Dictionary<string, object>
                {
                    { "email", email ?? string.Empty },
                    { "createdAt", Timestamp.GetCurrentTimestamp() },
                    { "provisioningState", "pending" }
                });
            }
            else if (!parentSnapshot.ContainsField("email") && !string.IsNullOrWhiteSpace(email))
            {
                transaction.Update(parentRef, "email", email.Trim());
            }
        });

        DocumentSnapshot profile = await parentRef.GetSnapshotAsync();
        bool isProvisioningPending = profile.Exists && profile.ContainsField("provisioningState") &&
                                     profile.GetValue<string>("provisioningState") == "pending";
        CollectionReference childrenRef = parentRef.Collection("children");
        QuerySnapshot children = await childrenRef.GetSnapshotAsync();

        if (isProvisioningPending)
        {
            const string defaultChildId = "default";
            await EnsureDefaultChildDataAsync(childrenRef.Document(defaultChildId));
            if (children.Count == 0 && GetCurrentParentId() == parentId)
                SetCurrentChildId(defaultChildId);
        }

        foreach (DocumentSnapshot child in children.Documents)
        {
            if (child.Exists)
                await EnsurePetExistsAsync(child.Reference);
        }

        if (isProvisioningPending)
            await parentRef.UpdateAsync("provisioningState", "ready");
    }

    private async Task EnsureDefaultChildDataAsync(DocumentReference childRef)
    {
        DocumentReference petRef = childRef.Collection("habimon").Document("status");
        DocumentReference habitRef = childRef.Collection("habits").Document("habit_001");
        DocumentReference rewardRef = childRef.Collection("rewards").Document("reward_01");
        string childId = childRef.Id;

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

        await db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot childSnapshot = await transaction.GetSnapshotAsync(childRef);
            DocumentSnapshot petSnapshot = await transaction.GetSnapshotAsync(petRef);
            DocumentSnapshot habitSnapshot = await transaction.GetSnapshotAsync(habitRef);
            DocumentSnapshot rewardSnapshot = await transaction.GetSnapshotAsync(rewardRef);

            if (!childSnapshot.Exists)
                transaction.Set(childRef, childData);
            if (!petSnapshot.Exists)
                transaction.Set(petRef, CreateDefaultPet());
            if (!habitSnapshot.Exists)
                transaction.Set(habitRef, CreateDefaultHabit());
            if (!rewardSnapshot.Exists)
                transaction.Set(rewardRef, CreateDefaultReward());
        });
    }

    private async Task EnsurePetExistsAsync(DocumentReference childRef)
    {
        DocumentReference petRef = childRef.Collection("habimon").Document("status");
        await db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot petSnapshot = await transaction.GetSnapshotAsync(petRef);
            if (!petSnapshot.Exists)
                transaction.Set(petRef, CreateDefaultPet());
        });
    }

    private static HabimonModel CreateDefaultPet()
    {
        return new HabimonModel
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
    }

    private static HabitModel CreateDefaultHabit()
    {
        HabitSchedule schedule = HabitSchedule.FromStorage(null, null, "20:00");
        return new HabitModel
        {
            title = "Đọc sách 15 phút",
            description = "Đọc truyện hoặc sách khoa học trước khi đi ngủ",
            icon = "icon_book",
            timeTarget = "20:00",
            scheduleKind = schedule.Kind,
            scheduleValue = schedule.Value,
            effectiveFrom = HabitLog.GetTodayDateKey(),
            createdAt = Timestamp.GetCurrentTimestamp(),
            repeatDays = new List<int> { 1, 2, 3, 4, 5 },
            rewardGold = 15,
            rewardExp = 25,
            requireApproval = true,
            isActive = true
        };
    }

    private static RewardModel CreateDefaultReward()
    {
        return new RewardModel
        {
            name = "Móc khóa thú cưng",
            itemType = "item",
            cost = 50,
            isUnlocked = false,
            createdAt = Timestamp.GetCurrentTimestamp()
        };
    }

    public async Task<(bool success, string message)> SendPasswordResetEmailAsync(string email)
    {
        if (!await EnsureInitializedAsync()) return (false, "Hệ thống chưa sẵn sàng!");

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

        ApplyAuthenticatedParent(string.Empty);
    }
    #endregion

    #region Habit
    // --- HÀM THÊM THÓI QUEN MỚI ---
    public async Task<(bool success, string message)> AddHabitAsync(
        FirebaseWriteScope scope,
        HabitModel habit,
        string operationId)
    {
        if (!await EnsureInitializedAsync())
            return (false, "Firebase chưa sẵn sàng.");
        if (scope == null || string.IsNullOrWhiteSpace(scope.ChildId) ||
            habit == null || string.IsNullOrWhiteSpace(operationId))
            return (false, "Thiếu thông tin để lưu thói quen.");
        if (!scope.IsCurrent(GetCurrentParentId(), currentChildId, sessionGeneration))
            return (false, "Phiên hoặc hồ sơ đã thay đổi. Hãy kiểm tra danh sách trước khi thử lại.");

        HabitSchedule schedule = HabitSchedule.FromStorage(habit.scheduleKind, habit.scheduleValue, habit.timeTarget);
        habit.scheduleKind = schedule.Kind;
        habit.scheduleValue = schedule.Value;
        if (string.IsNullOrWhiteSpace(habit.effectiveFrom))
            habit.effectiveFrom = HabitLog.GetTodayDateKey();
        if (!habit.createdAt.HasValue)
            habit.createdAt = Timestamp.GetCurrentTimestamp();

        try
        {
            DocumentReference habitRef = db.Collection("users")
                .Document(scope.ParentId)
                .Collection("children")
                .Document(scope.ChildId)
                .Collection("habits")
                .Document(operationId);

            bool operationMatched = false;
            bool operationConflicted = false;
            await db.RunTransactionAsync(async transaction =>
            {
                DocumentSnapshot existingSnapshot = await transaction.GetSnapshotAsync(habitRef);
                if (existingSnapshot.Exists)
                {
                    HabitModel existingHabit = existingSnapshot.ConvertTo<HabitModel>();
                    operationMatched = HabitCreatePayloadMatches(existingHabit, habit);
                    operationConflicted = !operationMatched;
                    return;
                }

                transaction.Set(habitRef, habit);
                operationMatched = true;
            });

            if (!operationMatched)
                return (false, operationConflicted
                    ? "Mã thao tác lưu thói quen đã được dùng với nội dung khác."
                    : "Không thể xác nhận thao tác lưu thói quen.");

            habit.id = operationId;
            Debug.Log("<color=cyan>[Firebase]</color> Đã thêm thói quen mới thành công!");
            return (true, "Đã lưu thói quen.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể lưu thói quen: {ex.Message}");
            return (false, "Không thể lưu thói quen. Vui lòng thử lại.");
        }
    }

    private static bool HabitCreatePayloadMatches(HabitModel existing, HabitModel requested)
    {
        return existing != null && requested != null &&
            string.Equals(existing.title, requested.title, StringComparison.Ordinal) &&
            string.Equals(existing.description, requested.description, StringComparison.Ordinal) &&
            string.Equals(existing.icon, requested.icon, StringComparison.Ordinal) &&
            string.Equals(existing.timeTarget, requested.timeTarget, StringComparison.Ordinal) &&
            string.Equals(existing.scheduleKind, requested.scheduleKind, StringComparison.Ordinal) &&
            string.Equals(existing.scheduleValue, requested.scheduleValue, StringComparison.Ordinal) &&
            string.Equals(existing.effectiveFrom, requested.effectiveFrom, StringComparison.Ordinal) &&
            existing.repeatDays != null && requested.repeatDays != null &&
            existing.repeatDays.SequenceEqual(requested.repeatDays) &&
            existing.rewardGold == requested.rewardGold &&
            existing.rewardExp == requested.rewardExp &&
            existing.requireApproval == requested.requireApproval &&
            existing.isActive == requested.isActive;
    }

    public async Task<bool> UpdateHabitActiveAsync(string parentId, string childId, string habitId, bool isActive)
    {
        if (!await EnsureInitializedAsync())
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return false;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(habitId))
        {
            Debug.LogWarning("[Firebase] Thiếu parentId, childId hoặc habitId để cập nhật trạng thái thói quen.");
            return false;
        }

        try
        {
            DocumentReference habitRef = db.Collection("users")
                .Document(parentId)
                .Collection("children")
                .Document(childId)
                .Collection("habits")
                .Document(habitId);

            await habitRef.UpdateAsync("isActive", isActive);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể cập nhật trạng thái thói quen: {ex.Message}");
            return false;
        }
    }

    public async Task DeleteHabitAsync(string parentId, string childId, string habitId)
    {
        if (!await EnsureInitializedAsync())
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

        DocumentSnapshot snapshot = await habitRef.GetSnapshotAsync();
        if (!snapshot.Exists || snapshot.ContainsField("archivedAt"))
            return;

        await habitRef.UpdateAsync(new Dictionary<string, object>
        {
            { "isActive", false },
            { "archivedAt", FieldValue.ServerTimestamp }
        });
        Debug.Log("<color=cyan>[Firebase]</color> Đã ẩn thói quen và giữ lại lịch sử.");
    }

    public async Task AddHabitLogAsync(string parentId, string childId, HabitLogModel habitLog)
    {
        if (habitLog == null || string.IsNullOrWhiteSpace(habitLog.habitId) || string.IsNullOrWhiteSpace(habitLog.dateKey))
            throw new ArgumentException("Habit id and date key are required.", nameof(habitLog));

        string logId = HabitLog.GetDailyLogDocumentId(habitLog.habitId, habitLog.dateKey);
        if (!string.IsNullOrWhiteSpace(habitLog.logId) &&
            !string.Equals(habitLog.logId, logId, StringComparison.Ordinal))
            throw new ArgumentException("The daily log id must match its habit and date.", nameof(habitLog));

        if (!await UpsertDailyHabitLogAsync(parentId, childId, logId, habitLog))
            throw new InvalidOperationException("The daily habit log could not be saved.");
    }

    public async Task UpdateHabitLogAsync(string parentId, string childId, string logId, HabitLogModel habitLog)
    {
        if (!await EnsureInitializedAsync())
        {
            Debug.LogWarning("[Firebase] Hệ thống chưa sẵn sàng!");
            return;
        }

        if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(childId) || string.IsNullOrEmpty(logId) ||
            habitLog == null || string.IsNullOrWhiteSpace(habitLog.habitId) || string.IsNullOrWhiteSpace(habitLog.dateKey))
        {
            Debug.LogWarning("[Firebase] Thiếu thông tin để cập nhật log thói quen.");
            return;
        }

        DocumentReference habitLogRef = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs")
            .Document(logId);

        bool updated = await db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(habitLogRef);
            if (!snapshot.Exists)
                return false;

            HabitLogModel existingLog = snapshot.ConvertTo<HabitLogModel>();
            if (existingLog == null || existingLog.habitId != habitLog.habitId ||
                existingLog.dateKey != habitLog.dateKey)
                return false;

            transaction.Set(habitLogRef, habitLog, SetOptions.MergeAll);
            return true;
        });

        if (!updated)
        {
            Debug.LogWarning("[Firebase] Không cập nhật log vì document không tồn tại hoặc không khớp habit/ngày.");
            return;
        }
        Debug.Log("<color=cyan>[Firebase]</color> Đã cập nhật log thói quen thành công!");
    }

    public async Task AddRewardAsync(string parentId, string childId, RewardModel reward)
    {
        if (!await EnsureInitializedAsync())
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

        if (!await EnsureInitializedAsync()) return habitList;

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

    public async Task<HabitModel> GetHabitAsync(string parentId, string childId, string habitId)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrWhiteSpace(parentId) ||
            string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(habitId))
            return null;

        DocumentSnapshot snapshot = await db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habits")
            .Document(habitId)
            .GetSnapshotAsync();

        if (!snapshot.Exists)
            return null;

        HabitModel habit = snapshot.ConvertTo<HabitModel>();
        if (habit != null)
            habit.id = snapshot.Id;
        return habit;
    }

    public async Task<List<HabitLogModel>> GetHabitLogsAsync(string parentId, string childId)
    {
        List<HabitLogModel> logs = new List<HabitLogModel>();

        if (!await EnsureInitializedAsync()) return logs;

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

    public async Task<List<HabitLogModel>> GetHabitLogsForDateAsync(string parentId, string childId, string dateKey)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrWhiteSpace(parentId) ||
            string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(dateKey))
            return new List<HabitLogModel>();

        Query query = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs")
            .WhereEqualTo("dateKey", dateKey);

        return await ConvertHabitLogsAsync(query);
    }

    public async Task<List<HabitLogModel>> GetHabitLogsInRangeAsync(
        string parentId,
        string childId,
        string startDateInclusive,
        string endDateExclusive)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrWhiteSpace(parentId) ||
            string.IsNullOrWhiteSpace(childId) || string.IsNullOrWhiteSpace(startDateInclusive) ||
            string.IsNullOrWhiteSpace(endDateExclusive))
            return new List<HabitLogModel>();

        Query query = db.Collection("users")
            .Document(parentId)
            .Collection("children")
            .Document(childId)
            .Collection("habit_logs")
            .WhereGreaterThanOrEqualTo("dateKey", startDateInclusive)
            .WhereLessThan("dateKey", endDateExclusive);

        return await ConvertHabitLogsAsync(query);
    }

    private static async Task<List<HabitLogModel>> ConvertHabitLogsAsync(Query query)
    {
        QuerySnapshot snapshot = await query.GetSnapshotAsync();
        var logs = new List<HabitLogModel>();
        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (!doc.Exists)
                continue;

            HabitLogModel log = doc.ConvertTo<HabitLogModel>();
            if (log == null)
                continue;

            log.logId = doc.Id;
            logs.Add(log);
        }

        return logs;
    }

    public async Task<bool> UpsertDailyHabitLogAsync(
        string parentId,
        string childId,
        string logId,
        HabitLogModel habitLog)
    {
        if (!await EnsureInitializedAsync() || string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId) ||
            habitLog == null || !DailyHabitLogIdentity.MatchesDocumentId(logId, habitLog.habitId, habitLog.dateKey))
            return false;

        try
        {
            DocumentReference logRef = db.Collection("users")
                .Document(parentId)
                .Collection("children")
                .Document(childId)
                .Collection("habit_logs")
                .Document(logId);

            await logRef.SetAsync(new Dictionary<string, object>
            {
                { "habitId", habitLog.habitId },
                { "dateKey", habitLog.dateKey },
                { "isCompleted", habitLog.isCompleted },
                { "isApproved", habitLog.isApproved },
                { "createdAt", habitLog.createdAt.HasValue ? (object)habitLog.createdAt.Value : FieldValue.ServerTimestamp },
                { "updatedAt", FieldValue.ServerTimestamp },
                { "completedAt", habitLog.isCompleted ? (object)FieldValue.ServerTimestamp : null }
            }, SetOptions.MergeAll);
            habitLog.logId = logId;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase] Không thể lưu log thói quen: {ex.Message}");
            return false;
        }
    }

    public async Task<List<RewardModel>> GetRewardsAsync(string parentId, string childId)
    {
        List<RewardModel> rewards = new List<RewardModel>();

        if (!await EnsureInitializedAsync()) return rewards;

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
