using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParentConfirm : MonoBehaviour
{
    [Header("Confirmation Popup")]
    // The root GameObject of the confirmation popup.
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private List<Sprite> iconSprites;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openButton;

    private readonly Dictionary<string, ChildRequests> children = new Dictionary<string, ChildRequests>();
    private readonly Dictionary<string, RequestView> views = new Dictionary<string, RequestView>();
    private readonly HashSet<string> approvingRequests = new HashSet<string>();
    private readonly HashSet<string> confirmedRequests = new HashSet<string>();
    private readonly Dictionary<string, RequestFailure> failures = new Dictionary<string, RequestFailure>();
    private ListenerRegistration childrenListener;
    private string loadedParentId;
    private int sessionVersion;
    private bool hasStarted;
    private bool hasChildrenSnapshot;
    private bool childrenListenerFailed;

    private sealed class ChildRequests
    {
        public ChildModel child;
        public readonly Dictionary<string, HabitModel> habits = new Dictionary<string, HabitModel>();
        public List<HabitLogModel> logs = new List<HabitLogModel>();
        public ListenerRegistration habitsListener;
        public ListenerRegistration logsListener;
        public bool hasHabitsSnapshot;
        public bool hasLogsSnapshot;
        public bool habitsFailed;
        public bool logsFailed;
        public int logRevision;
    }

    private sealed class Request
    {
        public string key;
        public ChildRequests owner;
        public HabitModel habit;
        public HabitLogModel log;
    }

    private sealed class RequestView
    {
        public GameObject root;
        public Image icon;
        public TMP_Text title;
        public TMP_Text completedAt;
        public Toggle toggle;
        public Request request;
    }

    private sealed class RequestFailure
    {
        public Timestamp completedAt;
        public string message;
    }

    private void Start()
    {
        hasStarted = true;
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePopup);
        if (openButton != null)
            openButton.onClick.AddListener(OpenPopup);
        _ = StartListeningAsync();
    }

    private void OnEnable()
    {
        if (hasStarted)
            _ = StartListeningAsync();
    }

    private void OnDisable()
    {
        ++sessionVersion;
        StopListeners();
        foreach (RequestView view in views.Values)
        {
            if (view.toggle != null)
                view.toggle.interactable = false;
        }
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        ++sessionVersion;
        StopListeners();
        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePopup);
        if (openButton != null)
            openButton.onClick.RemoveListener(OpenPopup);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && hasStarted && isActiveAndEnabled)
            _ = StartListeningAsync();
    }

    public void OpenPopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);
        if (HasSyncError())
            _ = StartListeningAsync();
    }

    public void ClosePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private async Task StartListeningAsync()
    {
        int version = ++sessionVersion;
        StopListeners();
        ClearViews();
        failures.Clear();
        loadedParentId = string.Empty;
        hasChildrenSnapshot = false;
        childrenListenerFailed = false;
        if (summaryText != null)
            summaryText.text = "Đang tải yêu cầu xác nhận...";
        if (openButton != null)
            openButton.gameObject.SetActive(false);

        if (popupRoot == null || itemPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("[ParentConfirm] Chưa gán popupRoot, itemPrefab hoặc contentRoot.");
            return;
        }

        try
        {
            while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
            {
                if (!IsCurrentSession(version))
                    return;
                await Task.Delay(100);
            }

            if (!IsCurrentSession(version))
                return;

            FirebaseManager manager = FirebaseManager.Instance;
            loadedParentId = manager.GetCurrentParentId();
            if (string.IsNullOrEmpty(loadedParentId))
                throw new InvalidOperationException("Không xác định được tài khoản phụ huynh.");

            childrenListener = manager.ListenChildren(loadedParentId, items =>
            {
                if (!IsCurrentSession(version))
                    return;

                hasChildrenSnapshot = true;
                childrenListenerFailed = false;
                HashSet<string> childIds = new HashSet<string>(items.Where(child => child != null && !string.IsNullOrEmpty(child.childId))
                    .Select(child => child.childId));

                foreach (string childId in children.Keys.Where(id => !childIds.Contains(id)).ToList())
                {
                    ChildRequests removed = children[childId];
                    children.Remove(childId);
                    StopChildListeners(removed);
                }

                foreach (ChildModel child in items)
                {
                    if (child == null || string.IsNullOrEmpty(child.childId))
                        continue;

                    if (children.TryGetValue(child.childId, out ChildRequests existing))
                        existing.child = child;
                    else
                    {
                        ChildRequests state = new ChildRequests { child = child };
                        children[child.childId] = state;
                        StartChildListeners(manager, state, version);
                    }
                }
                RefreshViews();
            });

            ObserveListener(childrenListener, version, () => childrenListenerFailed = true);
        }
        catch (Exception ex)
        {
            if (IsCurrentSession(version))
            {
                childrenListenerFailed = true;
                Debug.LogError($"[ParentConfirm] Không thể tải yêu cầu: {ex.Message}");
                RefreshViews();
            }
        }
    }

    private void StartChildListeners(FirebaseManager manager, ChildRequests state, int version)
    {
        string childId = state.child.childId;
        try
        {
            state.habitsListener = manager.ListenHabits(loadedParentId, childId, habits =>
            {
                if (!IsCurrentChild(state, version))
                    return;
        

                state.habits.Clear();
                foreach (HabitModel habit in habits)
                {
                    if (habit != null && !string.IsNullOrEmpty(habit.id))
                        state.habits[habit.id] = habit;
                }
                state.hasHabitsSnapshot = true;
                state.habitsFailed = false;
                RefreshViews();
            });
            ObserveListener(state.habitsListener, version, () => state.habitsFailed = true, state);
        }
        catch (Exception ex)
        {
            state.habitsFailed = true;
            Debug.LogError($"[ParentConfirm] Không thể theo dõi thói quen: {ex.Message}");
        }

        try
        {
            state.logsListener = manager.ListenHabitLogs(loadedParentId, childId, logs =>
            {
                if (!IsCurrentChild(state, version))
                    return;

                ++state.logRevision;
                state.logs = logs.Where(log => log != null).ToList();
                state.hasLogsSnapshot = true;
                state.logsFailed = false;
                foreach (HabitLogModel log in state.logs.Where(log => log.isApproved))
                    confirmedRequests.Add(GetRequestKey(childId, log));
                RefreshViews();
            });
            ObserveListener(state.logsListener, version, () => state.logsFailed = true, state);
        }
        catch (Exception ex)
        {
            state.logsFailed = true;
            Debug.LogError($"[ParentConfirm] Không thể theo dõi yêu cầu: {ex.Message}");
        }
    }

    private void ObserveListener(ListenerRegistration listener, int version, Action onFailure, ChildRequests state = null)
    {
        if (listener == null)
        {
            onFailure();
            RefreshViews();
            return;
        }
        _ = ObserveListenerAsync(listener, version, onFailure, state);
    }
        

    private async Task ObserveListenerAsync(ListenerRegistration listener, int version, Action onFailure, ChildRequests state)
    {
        try
        {
            await listener.ListenerTask;
        }
        catch (Exception ex)
        {
            if (IsCurrentSession(version) && (state == null || IsCurrentChild(state, version)))
                Debug.LogError($"[ParentConfirm] Đồng bộ yêu cầu bị gián đoạn: {ex.Message}");
        }

        if (IsCurrentSession(version) && (state == null || IsCurrentChild(state, version)))
        {
            onFailure();
            RefreshViews();
        }
    }

    private bool IsCurrentSession(int version)
    {
        return this != null && isActiveAndEnabled && version == sessionVersion;
    }

    private bool IsCurrentChild(ChildRequests state, int version)
    {
        return IsCurrentSession(version) && children.TryGetValue(state.child.childId, out ChildRequests current)
            && ReferenceEquals(current, state);
    }

    private string GetRequestKey(string childId, HabitLogModel log)
    {
        return $"{loadedParentId}/{childId}/{log.dateKey}/{log.habitId}";
    }

    private bool HasSyncError()
    {
        return childrenListenerFailed || children.Values.Any(state => state.habitsFailed || state.logsFailed);
    }

    private List<Request> GetPendingRequests()
    {
        List<Request> pending = new List<Request>();
        foreach (ChildRequests state in children.Values)
        {
            foreach (HabitLogModel log in state.logs)
            {
                if (!log.isCompleted || log.isApproved || string.IsNullOrEmpty(log.habitId) || string.IsNullOrEmpty(log.dateKey)
                    || !state.habits.TryGetValue(log.habitId, out HabitModel habit))
                    continue;

                string key = GetRequestKey(state.child.childId, log);
                if (!confirmedRequests.Contains(key))
                    pending.Add(new Request { key = key, owner = state, habit = habit, log = log });
            }
        }
        return pending.OrderBy(request => request.log.completedAt).ThenBy(request => request.key, StringComparer.Ordinal).ToList();
    }

    private void RefreshViews()
    {
        if (this == null || !isActiveAndEnabled || contentRoot == null || itemPrefab == null)
            return;

        List<Request> pending = GetPendingRequests();
        HashSet<string> pendingKeys = new HashSet<string>(pending.Select(request => request.key));
        foreach (string key in views.Keys.Where(key => !pendingKeys.Contains(key)).ToList())
            RemoveView(key);

        bool hasNewRequest = false;
        for (int index = 0; index < pending.Count; index++)
        {
            Request request = pending[index];
            if (!views.TryGetValue(request.key, out RequestView view))
            {
                view = SpawnView(request);
                views[request.key] = view;
                hasNewRequest = true;
            }
            else if (view.request.log.completedAt != request.log.completedAt)
                hasNewRequest = true;

            view.request = request;
            view.root.transform.SetSiblingIndex(index);
            RenderView(view);
        }

        bool hasError = HasSyncError();
        bool isLoading = !hasChildrenSnapshot || children.Values.Any(state => !state.hasHabitsSnapshot || !state.hasLogsSnapshot);
        if (summaryText != null)
        {
            if (hasError)
                summaryText.text = "Chưa thể đồng bộ đầy đủ. Đóng rồi mở lại danh sách để thử lại.";
            else if (pending.Count > 0)
                summaryText.text = $"Chờ phụ huynh xác nhận ({pending.Count})";
            else
                summaryText.text = isLoading ? "Đang tải yêu cầu xác nhận..." : "Chưa có yêu cầu xác nhận.";
        }

        if (openButton != null)
        {
            openButton.gameObject.SetActive(pending.Count > 0 || hasError);
            TMP_Text label = openButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = hasError ? "Xác nhận · Thử lại" : $"Chờ xác nhận ({pending.Count})";
        }

        if (popupRoot != null)
        {
            if (hasNewRequest)
                popupRoot.SetActive(true);
            else if (pending.Count == 0 && !hasError && !isLoading)
                popupRoot.SetActive(false);
        }
    }

    private RequestView SpawnView(Request request)
    {
        GameObject item = Instantiate(itemPrefab, contentRoot);
        RequestView view = new RequestView
        {
            root = item,
            icon = item.transform.Find("icon")?.GetComponent<Image>(),
            title = item.transform.Find("Title")?.GetComponent<TMP_Text>(),
            completedAt = item.transform.Find("CompletedAt")?.GetComponent<TMP_Text>(),
            toggle = item.GetComponentInChildren<Toggle>(true),
            request = request
        };

        if (view.toggle != null)
        {
            view.toggle.interactable = false;
            view.toggle.group = null;
            view.toggle.onValueChanged = new Toggle.ToggleEvent();
            view.toggle.SetIsOnWithoutNotify(false);
            view.toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    _ = ApproveRequestAsync(view.request);
            });
        }
        return view;
    }

    private void RenderView(RequestView view)
    {
        Request request = view.request;
        bool isApproving = approvingRequests.Contains(request.key);
        if (view.title != null)
            view.title.text = request.habit.title;
        if (view.icon != null && iconSprites != null)
            view.icon.sprite = iconSprites.FirstOrDefault(sprite => sprite != null && string.Equals(sprite.name, request.habit.icon, StringComparison.OrdinalIgnoreCase))
                ?? iconSprites.FirstOrDefault(sprite => sprite != null && sprite.name == "icon_default")
                ?? iconSprites.FirstOrDefault(sprite => sprite != null);

        if (view.completedAt != null)
        {
            string childName = string.IsNullOrWhiteSpace(request.owner.child.name) ? "Trẻ" : request.owner.child.name;
            string time = request.log.completedAt.ToDateTime().ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            string label = $"{childName} · {time}";
            if (isApproving)
                label += "\nĐang xác nhận...";
            else if (failures.TryGetValue(request.key, out RequestFailure failure) && failure.completedAt == request.log.completedAt)
                label += $"\n{failure.message}";
            view.completedAt.text = label;
        }

        if (view.toggle != null)
        {
            view.toggle.SetIsOnWithoutNotify(isApproving);
            view.toggle.interactable = !isApproving && !childrenListenerFailed
                && request.owner.hasHabitsSnapshot && request.owner.hasLogsSnapshot
                && !request.owner.habitsFailed && !request.owner.logsFailed;
        }
    }

    private async Task ApproveRequestAsync(Request request)
    {
        int version = sessionVersion;
        if (!IsCurrentChild(request.owner, version) || childrenListenerFailed || request.owner.habitsFailed || request.owner.logsFailed
            || !approvingRequests.Add(request.key))
            return;

        string parentId = loadedParentId;
        string childId = request.owner.child.childId;
        string habitId = request.log.habitId;
        string dateKey = request.log.dateKey;
        Timestamp expectedCompletedAt = request.log.completedAt;
        failures.Remove(request.key);
        RefreshViews();

        try
        {
            bool success = await HabitLog.ApproveHabitLogAsync(parentId, childId, habitId, dateKey, expectedCompletedAt);
            if (success)
                confirmedRequests.Add(request.key);
            if (!IsCurrentChild(request.owner, version))
                return;

            if (!success)
            {
                int revision = request.owner.logRevision;
                HabitLogModel freshLog = await FirebaseManager.Instance.GetHabitLogAsync(parentId, childId, habitId, dateKey);
                if (!IsCurrentChild(request.owner, version))
                    return;

                if (revision == request.owner.logRevision)
                {
                    request.owner.logs.RemoveAll(log => log.habitId == habitId && log.dateKey == dateKey);
                    if (freshLog != null)
                        request.owner.logs.Add(freshLog);
                    if (freshLog != null && freshLog.isApproved)
                        confirmedRequests.Add(request.key);
                }

                failures[request.key] = new RequestFailure
                {
                    completedAt = expectedCompletedAt,
                    message = "Chưa thể xác nhận. Vui lòng thử lại."
                };
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentChild(request.owner, version))
            {
                failures[request.key] = new RequestFailure
                {
                    completedAt = expectedCompletedAt,
                    message = "Không thể kết nối. Vui lòng thử lại."
                };
                Debug.LogError($"[ParentConfirm] Không thể xác nhận yêu cầu: {ex.Message}");
            }
        }
        finally
        {
            approvingRequests.Remove(request.key);
            if (this != null && isActiveAndEnabled)
                RefreshViews();
        }
    }

    private void RemoveView(string key)
    {
        RequestView view = views[key];
        views.Remove(key);
        if (view.root == null)
            return;
        if (view.toggle != null)
        {
            view.toggle.interactable = false;
            view.toggle.onValueChanged.RemoveAllListeners();
        }
        view.root.SetActive(false);
        Destroy(view.root);
    }

    private void ClearViews()
    {
        foreach (string key in views.Keys.ToList())
            RemoveView(key);
    }

    private void StopListeners()
    {
        if (childrenListener != null)
        {
            childrenListener.Stop();
            childrenListener = null;
        }
        List<ChildRequests> previousChildren = children.Values.ToList();
        children.Clear();
        foreach (ChildRequests state in previousChildren)
            StopChildListeners(state);
    }

    private static void StopChildListeners(ChildRequests state)
    {
        state.habitsListener?.Stop();
        state.logsListener?.Stop();
    }
}