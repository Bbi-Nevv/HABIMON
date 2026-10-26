using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HABIMON.Chatbot;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime host for the Parent-scene chatbot. It leaves FirebaseManager and the existing tab
/// events untouched; all API and rendering state is scoped to one captured Firebase account/child.
/// </summary>
public sealed class ChatbotRuntimeHost : MonoBehaviour
{
    private const int PageSize = 50;
    private const int MaximumMessageCharacters = 2000;

    [SerializeField] private ChatbotRuntimeSettings settings;

    private Canvas canvas;
    private Transform taskbar;
    private Button navigationButton;
    private GameObject screen;
    private GameObject historyPanel;
    private GameObject deleteDialog;
    private GameObject childPickerPanel;
    private RectTransform transcriptBodyRect;
    private RectTransform composerRect;
    private RectTransform statusRect;
    private RectTransform noteRect;
    private RectTransform messageContent;
    private RectTransform sessionContent;
    private RectTransform childPickerContent;
    private ScrollRect messageScroll;
    private TMP_InputField input;
    private Button sendButton;
    private Button newConversationButton;
    private Button historyButton;
    private Button historyRetryButton;
    private Button childSelectorButton;
    private TMP_Text headerChildText;
    private TMP_Text statusText;
    private TMP_Text typingIndicatorText;
    private Coroutine typingIndicatorRoutine;
    private TMP_Text historyStatusText;
    private TMP_Text childPickerStatusText;
    private Image headerAvatarImage;
    private Sprite happySprite;
    private Sprite thinkingSprite;
    private Sprite navigationSprite;
    private Texture2D navigationTexture;
    private ChatbotApiCancellation scopeCancellation = new ChatbotApiCancellation();

    private FirebaseManager manager;
    private FirebaseChatbotContextSnapshot activeSnapshot;
    private ChatbotApiClient client;
    private string clientOwnerUid;
    private string parentId = string.Empty;
    private string childId = string.Empty;
    private int scopeGeneration;
    private int operationGeneration;
    private string sessionId;
    private readonly List<ChatbotSessionResponse> sessions = new List<ChatbotSessionResponse>();
    private readonly List<ChatbotMessageResponse> serverMessages = new List<ChatbotMessageResponse>();
    private readonly List<Button> parentTabButtons = new List<Button>();
    private bool sending;
    private bool loadingSessions;
    private bool loadingMessages;
    private bool sessionPageAvailable;
    private int sessionsRequestGeneration;
    private int messagesRequestGeneration;
    private int childPickerRequestGeneration;
    private string currentChildDisplayName;
    private string pendingDeleteSessionId;
    private int historyOffset;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastSafeArea;
    private float lastLayoutBottom = -1f;
#if UNITY_ANDROID && !UNITY_EDITOR
    private int screenHeightWithoutKeyboard;
    private bool androidKeyboardConfigured;
    private bool androidKeyboardWarningLogged;
    private float nextKeyboardInsetPollTime;
    private int cachedKeyboardInsetPixels;
#endif

#pragma warning disable 0649 // Unity JsonUtility populates these transport DTOs reflectively.
    [Serializable] private sealed class MetaPayload { public string session_id; }
    [Serializable] private sealed class DeltaPayload { public int seq; public string text; }

    private bool HasActiveScope => !string.IsNullOrEmpty(parentId) && !string.IsNullOrEmpty(childId);
    private string Plan => settings != null && IsValidPlan(settings.Plan) ? settings.Plan : "free";

    private void OnEnable()
    {
        // OnDisable cancels in-flight API work. A later re-enable must get a fresh scope token.
        if (scopeCancellation == null || scopeCancellation.IsCancellationRequested)
            scopeCancellation = new ChatbotApiCancellation();
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindManager();
        if (SceneManager.GetActiveScene().name == "Parent")
            InitializeParentScene();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Parent")
        {
            InitializeParentScene();
            StartCoroutine(WaitForFirebaseReadyRoutine());
        }
    }

    private void Update()
    {
        UpdateSafeArea();
        UpdateKeyboardLayout();
    }

    private IEnumerator WaitForFirebaseReadyRoutine()
    {
        FirebaseManager current = FirebaseManager.Instance;
        if (current == null) yield break;
        Task<bool> ready = current.EnsureInitializedAsync();
        while (!ready.IsCompleted) yield return null;
        if (ready.IsFaulted || ready.IsCanceled || !ready.Result) yield break;
        BindManager();
        RefreshScopeFromManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindManager();
        UnbindParentTabButtons();
        scopeCancellation.Cancel();
        ReleaseActiveSnapshot();
        operationGeneration++;
        sending = false;
        loadingSessions = false;
        loadingMessages = false;
        sessionsRequestGeneration++;
        messagesRequestGeneration++;
        SetControlsEnabled(true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindManager();
        UnbindParentTabButtons();
        ReleaseActiveSnapshot();
        scopeCancellation.Cancel();
        if (screen != null) Destroy(screen);
        if (navigationButton != null) Destroy(navigationButton.gameObject);
        DestroyNavigationSprite();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Parent")
        {
            BindManager();
            InitializeParentScene();
        }
        else
        {
            CloseChat();
            ResetScope();
        }
    }

    private void BindManager()
    {
        FirebaseManager current = FirebaseManager.Instance;
        if (current == manager) return;
        UnbindManager();
        manager = current;
        if (manager == null) return;
        manager.SessionChanged += OnSessionChanged;
        manager.CurrentChildChanged += OnCurrentChildChanged;
    }

    private void UnbindManager()
    {
        if (manager == null) return;
        manager.SessionChanged -= OnSessionChanged;
        manager.CurrentChildChanged -= OnCurrentChildChanged;
        manager = null;
    }

    private void OnSessionChanged()
    {
        CloseChildPicker();
        RefreshScopeFromManager();
    }

    private void OnCurrentChildChanged()
    {
        CloseChildPicker();
        RefreshScopeFromManager();
    }

    private void InitializeParentScene()
    {
        if (SceneManager.GetActiveScene().name != "Parent") return;
        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Chatbot] Parent scene has no Canvas.");
            return;
        }

        taskbar = canvas.transform.Find("Taskbar");
        if (taskbar == null)
        {
            Debug.LogError("[Chatbot] Could not find Canvas/Taskbar in the Parent scene.");
            return;
        }

        BuildNavigationTab();
        BindOtherParentTabs();
        if (screen == null) BuildChatScreen();
        BindManager();
        RefreshScopeFromManager();
    }

    private void BuildNavigationTab()
    {
        if (taskbar == null) return;
        Transform existing = taskbar.Find("ChatbotTab");
        GameObject tab;
        if (existing != null)
        {
            tab = existing.gameObject;
        }
        else
        {
            Transform template = taskbar.Find("Award");
            if (template == null)
            {
                Debug.LogError("[Chatbot] The existing Parent tab template was not found.");
                return;
            }
            tab = Instantiate(template.gameObject, taskbar, false);
            tab.name = "ChatbotTab";
            tab.transform.SetSiblingIndex(Mathf.Max(0, taskbar.childCount - 2));
        }
        navigationButton = tab.GetComponent<Button>();
        if (navigationButton == null)
        {
            Destroy(tab);
            Debug.LogError("[Chatbot] The existing Parent tab template has no Button.");
            return;
        }

        // The template has serialized panel toggles. Replace the click event before adding ours.
        navigationButton.onClick = new Button.ButtonClickedEvent();
        navigationButton.onClick.AddListener(OpenChat);
        Transform icon = tab.transform.Find("Image");
        Image iconImage = icon != null ? icon.GetComponent<Image>() : tab.GetComponentInChildren<Image>(true);
        if (iconImage != null)
        {
            navigationSprite = ChatbotUiFactory.CreateChatIcon(out navigationTexture);
            iconImage.sprite = navigationSprite;
            iconImage.preserveAspect = true;
            iconImage.color = ChatbotUiFactory.Blue;
        }

        Transform label = tab.transform.Find("Text (TMP)");
        TMP_Text labelText = label != null ? label.GetComponent<TMP_Text>() : tab.GetComponentInChildren<TMP_Text>(true);
        if (labelText != null) labelText.text = "Trợ lý";
        tab.SetActive(true);
    }

    private void BindOtherParentTabs()
    {
        UnbindParentTabButtons();
        if (taskbar == null) return;
        foreach (Transform entry in taskbar)
        {
            if (navigationButton != null && entry == navigationButton.transform) continue;
            Button button = entry.GetComponent<Button>();
            if (button == null) continue;
            // Add a secondary listener; preserve every serialized navigation UnityEvent.
            button.onClick.AddListener(CloseChat);
            parentTabButtons.Add(button);
        }
    }

    private void UnbindParentTabButtons()
    {
        foreach (Button button in parentTabButtons)
            if (button != null) button.onClick.RemoveListener(CloseChat);
        parentTabButtons.Clear();
    }

    private void BuildChatScreen()
    {
        screen = new GameObject("ChatbotScreen", typeof(RectTransform), typeof(Image));
        screen.layer = canvas.gameObject.layer;
        screen.transform.SetParent(canvas.transform, false);
        ChatbotUiFactory.Stretch((RectTransform)screen.transform, Vector2.zero, Vector2.zero);
        Image screenImage = screen.GetComponent<Image>();
        screenImage.color = ChatbotUiFactory.Background;
        screenImage.raycastTarget = true;
        screen.SetActive(false);
        UpdateSafeArea(true);

        BuildMainHeader(screen.transform);
        BuildTranscript(screen.transform);
        BuildComposer(screen.transform);
        ConfigureAndroidKeyboard();
        BuildHistoryPanel(screen.transform);
        BuildDeleteDialog(screen.transform);
        BuildChildPickerPanel(screen.transform);
    }

    private void BuildMainHeader(Transform parent)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.layer = parent.gameObject.layer;
        header.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)header.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(ChatbotUiFactory.U(22f), -ChatbotUiFactory.U(126f));
        rect.offsetMax = new Vector2(-ChatbotUiFactory.U(22f), -ChatbotUiFactory.U(18f));
        var layout = header.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = ChatbotUiFactory.U(12f);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        GameObject avatar = new GameObject("HabiAvatar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        avatar.layer = header.layer;
        avatar.transform.SetParent(header.transform, false);
        headerAvatarImage = avatar.GetComponent<Image>();
        happySprite = LoadHabiSprite("habi-happy");
        thinkingSprite = LoadHabiSprite("habi-thinking");
        headerAvatarImage.sprite = happySprite;
        headerAvatarImage.preserveAspect = true;
        var avatarLayout = avatar.GetComponent<LayoutElement>();
        avatarLayout.preferredWidth = ChatbotUiFactory.U(72f);
        avatarLayout.preferredHeight = ChatbotUiFactory.U(72f);

        GameObject titleGroup = new GameObject("TitleGroup", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        titleGroup.layer = header.layer;
        titleGroup.transform.SetParent(header.transform, false);
        titleGroup.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var titleLayout = titleGroup.GetComponent<VerticalLayoutGroup>();
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = true;
        titleLayout.childForceExpandHeight = false;
        TMP_Text title = ChatbotUiFactory.Text("Title", titleGroup.transform, "Trợ lý Habi", 24f,
            ChatbotUiFactory.Navy);
        title.fontStyle = FontStyles.Bold;
        GameObject childSelector = ChatbotUiFactory.Button("ChildSelectorButton", titleGroup.transform,
            "Chọn bé", ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 32f).gameObject;
        childSelectorButton = childSelector.GetComponent<Button>();
        headerChildText = childSelector.GetComponentInChildren<TMP_Text>(true);
        headerChildText.fontSize = ChatbotUiFactory.U(13f);
        headerChildText.textWrappingMode = TextWrappingModes.NoWrap;
        headerChildText.overflowMode = TextOverflowModes.Ellipsis;
        headerChildText.alignment = TextAlignmentOptions.MidlineLeft;
        childSelectorButton.onClick.AddListener(OpenChildPicker);

        Button history = ChatbotUiFactory.Button("HistoryButton", header.transform, "Lịch sử",
            ChatbotUiFactory.White, ChatbotUiFactory.Navy, 42f);
        historyButton = history;
        TMP_Text historyLabel = history.GetComponentInChildren<TMP_Text>(true);
        historyLabel.fontSize = ChatbotUiFactory.U(14f);
        historyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        historyLabel.overflowMode = TextOverflowModes.Ellipsis;
        history.onClick.AddListener(OpenHistory);
        Button fresh = ChatbotUiFactory.Button("NewConversationButton", header.transform, "Mới",
            ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 42f);
        newConversationButton = fresh;
        TMP_Text freshLabel = fresh.GetComponentInChildren<TMP_Text>(true);
        freshLabel.fontSize = ChatbotUiFactory.U(14f);
        freshLabel.textWrappingMode = TextWrappingModes.NoWrap;
        freshLabel.overflowMode = TextOverflowModes.Ellipsis;
        fresh.onClick.AddListener(StartNewConversation);
        var freshElement = fresh.GetComponent<LayoutElement>();
        freshElement.preferredWidth = ChatbotUiFactory.U(72f);
        freshElement.flexibleWidth = 0f;
        var historyElement = history.GetComponent<LayoutElement>();
        historyElement.preferredWidth = ChatbotUiFactory.U(104f);
        historyElement.flexibleWidth = 0f;
    }

    private void BuildTranscript(Transform parent)
    {
        var body = new GameObject("ConversationBody", typeof(RectTransform));
        body.layer = parent.gameObject.layer;
        body.transform.SetParent(parent, false);
        transcriptBodyRect = (RectTransform)body.transform;
        RectTransform bodyRect = transcriptBodyRect;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        // BuildChatScreen adjusts this after the composer children have been created.
        float composerBottom = GetTaskbarTopInset() + ChatbotUiFactory.U(128f);
        bodyRect.offsetMin = new Vector2(0f, composerBottom);
        bodyRect.offsetMax = new Vector2(0f, -ChatbotUiFactory.U(138f));

        GameObject empty = new GameObject("EmptyState", typeof(RectTransform), typeof(VerticalLayoutGroup));
        empty.layer = body.layer;
        empty.transform.SetParent(body.transform, false);
        RectTransform emptyRect = (RectTransform)empty.transform;
        emptyRect.anchorMin = new Vector2(0.08f, 0.05f);
        emptyRect.anchorMax = new Vector2(0.92f, 0.95f);
        emptyRect.offsetMin = Vector2.zero;
        emptyRect.offsetMax = Vector2.zero;
        var emptyLayout = empty.GetComponent<VerticalLayoutGroup>();
        emptyLayout.childAlignment = TextAnchor.MiddleCenter;
        emptyLayout.spacing = ChatbotUiFactory.U(10f);
        emptyLayout.childControlWidth = true;
        emptyLayout.childControlHeight = true;
        emptyLayout.childForceExpandWidth = true;
        emptyLayout.childForceExpandHeight = false;
        GameObject hero = new GameObject("WelcomeHabi", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        hero.layer = body.layer;
        hero.transform.SetParent(empty.transform, false);
        var heroImage = hero.GetComponent<Image>();
        heroImage.sprite = LoadHabiSprite("habi-happy");
        heroImage.preserveAspect = true;
        var heroLayout = hero.GetComponent<LayoutElement>();
        heroLayout.preferredWidth = ChatbotUiFactory.U(120f);
        heroLayout.preferredHeight = ChatbotUiFactory.U(110f);
        heroLayout.flexibleWidth = 0f;
        TMP_Text welcome = ChatbotUiFactory.Text("WelcomeText", empty.transform,
            "Cùng Habi xây thói quen nhỏ", 20f, ChatbotUiFactory.Navy, TextAlignmentOptions.Center);
        welcome.fontStyle = FontStyles.Bold;
        GameObject suggestions = new GameObject("Suggestions", typeof(RectTransform), typeof(VerticalLayoutGroup));
        suggestions.layer = body.layer;
        suggestions.transform.SetParent(empty.transform, false);
        var suggestionsLayout = suggestions.GetComponent<VerticalLayoutGroup>();
        suggestionsLayout.spacing = ChatbotUiFactory.U(6f);
        suggestionsLayout.childControlWidth = true;
        suggestionsLayout.childControlHeight = true;
        suggestionsLayout.childForceExpandWidth = true;
        suggestionsLayout.childForceExpandHeight = false;
        AddSuggestion(suggestions.transform, "Làm sao giúp bé duy trì thói quen?");
        AddSuggestion(suggestions.transform, "Gợi ý một bước nhỏ để bắt đầu.");
        AddSuggestion(suggestions.transform, "Giúp tôi hiểu tiến độ gần đây của bé.");

        messageScroll = ChatbotUiFactory.ScrollView(body.transform, "MessageScroll", out messageContent);
        // Short conversations should sit near the composer instead of leaving a large empty
        // band below the last message. Longer conversations still grow upward and scroll.
        messageContent.anchorMin = new Vector2(0f, 0f);
        messageContent.anchorMax = new Vector2(1f, 0f);
        messageContent.pivot = new Vector2(0.5f, 0f);
        messageContent.anchoredPosition = Vector2.zero;
        messageContent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
        RectTransform scrollRect = (RectTransform)messageScroll.transform;
        ChatbotUiFactory.Stretch(scrollRect, new Vector2(0f, 0f), new Vector2(0f, 0f));
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        empty.SetActive(true);
        // Empty content is rendered above the scroll viewport, while suggestion buttons still receive input.
        empty.transform.SetAsLastSibling();
    }

    private void BuildComposer(Transform parent)
    {
        GameObject composer = new GameObject("Composer", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        composer.layer = parent.gameObject.layer;
        composer.transform.SetParent(parent, false);
        RectTransform rect = composerRect = (RectTransform)composer.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        float navTop = GetTaskbarTopInset();
        rect.offsetMin = new Vector2(ChatbotUiFactory.U(20f), navTop + ChatbotUiFactory.U(76f));
        rect.offsetMax = new Vector2(-ChatbotUiFactory.U(20f), navTop + ChatbotUiFactory.U(254f));
        composer.GetComponent<Image>().color = Color.clear;
        composer.GetComponent<Image>().raycastTarget = false;
        var group = composer.GetComponent<HorizontalLayoutGroup>();
        group.spacing = ChatbotUiFactory.U(12f);
        group.padding = new RectOffset(0, 0, 0, 0);
        group.childAlignment = TextAnchor.LowerCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        input = ChatbotUiFactory.Input(composer.transform);
        input.onValueChanged.AddListener(_ => UpdateComposerInputHeight());
        UpdateComposerInputHeight();
        sendButton = ChatbotUiFactory.Button("SendButton", composer.transform, "Gửi",
            ChatbotUiFactory.Blue, Color.white, 38f);
        sendButton.GetComponent<LayoutElement>().preferredWidth = ChatbotUiFactory.U(72f);
        sendButton.GetComponent<LayoutElement>().flexibleWidth = 0f;
        SendPointerDownHandler pointerDownHandler = sendButton.gameObject.AddComponent<SendPointerDownHandler>();
        pointerDownHandler.Initialize(sendButton, OnSendClicked);
        input.onSubmit.AddListener(_ => OnSendClicked());

        statusText = ChatbotUiFactory.Text("Status", parent, string.Empty, 17f, ChatbotUiFactory.Muted);
        statusRect = (RectTransform)statusText.transform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.offsetMin = new Vector2(ChatbotUiFactory.U(28f), navTop + ChatbotUiFactory.U(48f));
        statusRect.offsetMax = new Vector2(-ChatbotUiFactory.U(28f), navTop + ChatbotUiFactory.U(74f));

        TMP_Text note = ChatbotUiFactory.Text("ParentDecisionNote", parent,
            "Habi đưa ra gợi ý; bạn quyết định thay đổi trong ứng dụng.", 13f,
            ChatbotUiFactory.Muted, TextAlignmentOptions.Center);
        noteRect = (RectTransform)note.transform;
        noteRect.anchorMin = new Vector2(0f, 0f);
        noteRect.anchorMax = new Vector2(1f, 0f);
        noteRect.pivot = new Vector2(0.5f, 0f);
        noteRect.offsetMin = new Vector2(ChatbotUiFactory.U(20f), navTop + ChatbotUiFactory.U(22f));
        noteRect.offsetMax = new Vector2(-ChatbotUiFactory.U(20f), navTop + ChatbotUiFactory.U(44f));
        UpdateTranscriptViewportBottom(lastLayoutBottom >= 0f ? lastLayoutBottom : navTop);
    }

    private float GetTaskbarTopInset()
    {
        if (!(taskbar is RectTransform taskbarRect) || screen == null || !(screen.transform is RectTransform screenRect))
            return 150f;
        Vector3[] corners = new Vector3[4];
        taskbarRect.GetWorldCorners(corners);
        float bottom = -screenRect.rect.height * screenRect.pivot.y;
        float top = screenRect.InverseTransformPoint(corners[1]).y;
        return Mathf.Clamp(top - bottom + ChatbotUiFactory.U(24f), 0f, screenRect.rect.height * 0.45f);
    }

    private void UpdateSafeArea(bool force = false)
    {
        if (screen == null || !(screen.transform is RectTransform screenRect) ||
            Screen.width <= 0 || Screen.height <= 0)
            return;
        Rect safeArea = Screen.safeArea;
        if (!force && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height &&
            lastSafeArea == safeArea)
            return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = safeArea;
        screenRect.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        screenRect.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        screenRect.offsetMin = Vector2.zero;
        screenRect.offsetMax = Vector2.zero;
        lastLayoutBottom = -1f;
        UpdateComposerInputHeight();
        UpdateKeyboardLayout();
    }

    private void UpdateKeyboardLayout()
    {
        if (composerRect == null || transcriptBodyRect == null || statusRect == null || noteRect == null)
            return;
        float bottomInset = GetTaskbarTopInset();
        bool keyboardVisible = TouchScreenKeyboard.visible || (input != null && input.isFocused);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!keyboardVisible)
            screenHeightWithoutKeyboard = Screen.height;
        bool screenAlreadyResizedForKeyboard = keyboardVisible && screenHeightWithoutKeyboard > 0 &&
            Screen.height < screenHeightWithoutKeyboard * 0.85f;
#endif
        if (keyboardVisible && Screen.height > 0)
        {
            Rect keyboardArea = TouchScreenKeyboard.area;
            Rect safeArea = Screen.safeArea;
            if (keyboardArea.height > 0f && screen.transform is RectTransform screenRect)
            {
                float scale = safeArea.height > 0f ? screenRect.rect.height / safeArea.height : 1f;
                float keyboardInset = Mathf.Max(0f, keyboardArea.yMax - safeArea.yMin) * scale + ChatbotUiFactory.U(12f);
                bottomInset = Mathf.Max(bottomInset, keyboardInset);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            // adjustResize may already have reduced Unity's viewport; in that case the
            // bottom-anchored composer has moved with it, so don't apply the same inset twice.
            int imeInsetPixels = screenAlreadyResizedForKeyboard ? 0 : GetAndroidImeInsetPixels();
            if (imeInsetPixels > 0 && screen.transform is RectTransform androidScreenRect)
            {
                float scale = safeArea.height > 0f
                    ? androidScreenRect.rect.height / safeArea.height
                    : androidScreenRect.rect.height / Screen.height;
                float keyboardInset = imeInsetPixels * scale + ChatbotUiFactory.U(12f);
                bottomInset = Mathf.Max(bottomInset, keyboardInset);
            }
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        else
        {
            cachedKeyboardInsetPixels = 0;
            nextKeyboardInsetPollTime = 0f;
        }
#endif
        if (Mathf.Approximately(lastLayoutBottom, bottomInset))
        {
            UpdateTranscriptViewportBottom(bottomInset);
            return;
        }
        lastLayoutBottom = bottomInset;
        composerRect.offsetMin = new Vector2(ChatbotUiFactory.U(20f), bottomInset + ChatbotUiFactory.U(76f));
        composerRect.offsetMax = new Vector2(-ChatbotUiFactory.U(20f), bottomInset + ChatbotUiFactory.U(254f));
        statusRect.offsetMin = new Vector2(ChatbotUiFactory.U(28f), bottomInset + ChatbotUiFactory.U(48f));
        statusRect.offsetMax = new Vector2(-ChatbotUiFactory.U(28f), bottomInset + ChatbotUiFactory.U(74f));
        noteRect.offsetMin = new Vector2(ChatbotUiFactory.U(20f), bottomInset + ChatbotUiFactory.U(22f));
        noteRect.offsetMax = new Vector2(-ChatbotUiFactory.U(20f), bottomInset + ChatbotUiFactory.U(44f));
        UpdateTranscriptViewportBottom(bottomInset);
    }

    private void UpdateTranscriptViewportBottom(float bottomInset)
    {
        if (transcriptBodyRect == null) return;
        float inputHeight = ChatbotUiFactory.U(38f);
        if (input != null && input.TryGetComponent(out LayoutElement inputLayout))
            inputHeight = Mathf.Max(inputHeight, inputLayout.preferredHeight);
        float sendHeight = sendButton != null && sendButton.TryGetComponent(out LayoutElement sendLayout)
            ? sendLayout.preferredHeight
            : ChatbotUiFactory.U(38f);

        // HorizontalLayoutGroup aligns children to its lower edge. Use the actual top of the
        // input/send controls plus a small gap so the scroll viewport fills down to the composer.
        float visibleComposerTop = bottomInset + ChatbotUiFactory.U(76f) + Mathf.Max(inputHeight, sendHeight);
        transcriptBodyRect.offsetMin = new Vector2(0f, visibleComposerTop + ChatbotUiFactory.U(14f));
    }

    private void ConfigureAndroidKeyboard()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (androidKeyboardConfigured) return;
        try
        {
            using (AndroidJavaClass store = new AndroidJavaClass("com.gsam.habimon.chatbot.ChatbotSecureStore"))
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                store.CallStatic("configureForIme", activity);
            androidKeyboardConfigured = true;
        }
        catch (Exception exception)
        {
            if (!androidKeyboardWarningLogged)
            {
                Debug.LogWarning("[Chatbot] Android keyboard resize setup failed: " + exception.GetType().Name);
                androidKeyboardWarningLogged = true;
            }
        }
#endif
    }

    private int GetAndroidImeInsetPixels()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Time.unscaledTime < nextKeyboardInsetPollTime) return cachedKeyboardInsetPixels;
        nextKeyboardInsetPollTime = Time.unscaledTime + 0.1f;
        try
        {
            using (AndroidJavaClass store = new AndroidJavaClass("com.gsam.habimon.chatbot.ChatbotSecureStore"))
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                cachedKeyboardInsetPixels = Mathf.Max(0,
                    store.CallStatic<int>("getImeBottomInset", activity));
        }
        catch (Exception exception)
        {
            cachedKeyboardInsetPixels = 0;
            if (!androidKeyboardWarningLogged)
            {
                Debug.LogWarning("[Chatbot] Android keyboard inset read failed: " + exception.GetType().Name);
                androidKeyboardWarningLogged = true;
            }
        }
        return cachedKeyboardInsetPixels;
#else
        return 0;
#endif
    }

    private void UpdateComposerInputHeight()
    {
        if (input == null || input.textComponent == null) return;
        LayoutElement element = input.GetComponent<LayoutElement>();
        if (element == null) return;
        float width = input.textViewport != null ? input.textViewport.rect.width : 0f;
        if (width <= 1f) width = ChatbotUiFactory.U(Mathf.Max(200f, Screen.width * 0.62f));
        Vector2 preferred = input.textComponent.GetPreferredValues(input.text ?? string.Empty, width, 320f);
        element.preferredHeight = Mathf.Clamp(preferred.y + ChatbotUiFactory.U(12f),
            ChatbotUiFactory.U(38f), ChatbotUiFactory.U(152f));
        UpdateTranscriptViewportBottom(lastLayoutBottom >= 0f ? lastLayoutBottom : GetTaskbarTopInset());
    }

    private void BuildHistoryPanel(Transform parent)
    {
        historyPanel = new GameObject("HistoryPanel", typeof(RectTransform), typeof(Image));
        historyPanel.layer = parent.gameObject.layer;
        historyPanel.transform.SetParent(parent, false);
        ChatbotUiFactory.Stretch((RectTransform)historyPanel.transform, Vector2.zero, Vector2.zero);
        historyPanel.GetComponent<Image>().color = ChatbotUiFactory.Background;

        GameObject header = new GameObject("HistoryHeader", typeof(RectTransform), typeof(VerticalLayoutGroup));
        header.layer = historyPanel.layer;
        header.transform.SetParent(historyPanel.transform, false);
        var headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(ChatbotUiFactory.U(22f), -ChatbotUiFactory.U(140f));
        headerRect.offsetMax = new Vector2(-ChatbotUiFactory.U(22f), -ChatbotUiFactory.U(18f));
        var headerLayout = header.GetComponent<VerticalLayoutGroup>();
        headerLayout.spacing = ChatbotUiFactory.U(8f);
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;

        GameObject titleRow = new GameObject("HistoryTitleRow", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        titleRow.layer = historyPanel.layer;
        titleRow.transform.SetParent(header.transform, false);
        titleRow.GetComponent<LayoutElement>().preferredHeight = ChatbotUiFactory.U(44f);
        var titleRowLayout = titleRow.GetComponent<HorizontalLayoutGroup>();
        titleRowLayout.spacing = ChatbotUiFactory.U(8f);
        titleRowLayout.childControlWidth = true;
        titleRowLayout.childControlHeight = true;
        titleRowLayout.childForceExpandWidth = false;
        titleRowLayout.childForceExpandHeight = true;
        Button back = ChatbotUiFactory.Button("Back", titleRow.transform, "Quay lại",
            ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 42f);
        back.GetComponent<LayoutElement>().preferredWidth = ChatbotUiFactory.U(72f);
        back.GetComponent<LayoutElement>().flexibleWidth = 0f;
        back.onClick.AddListener(CloseHistory);
        TMP_Text title = ChatbotUiFactory.Text("HistoryTitle", titleRow.transform, "Lịch sử trò chuyện",
            18f, ChatbotUiFactory.Navy);
        title.fontStyle = FontStyles.Bold;
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject actionRow = new GameObject("HistoryActions", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actionRow.layer = historyPanel.layer;
        actionRow.transform.SetParent(header.transform, false);
        actionRow.GetComponent<LayoutElement>().preferredHeight = ChatbotUiFactory.U(44f);
        var actionLayout = actionRow.GetComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = ChatbotUiFactory.U(8f);
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = true;
        historyRetryButton = ChatbotUiFactory.Button("ReloadHistoryButton", actionRow.transform, "Tải lại",
            ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 42f);
        historyRetryButton.GetComponent<LayoutElement>().preferredWidth = ChatbotUiFactory.U(96f);
        historyRetryButton.GetComponent<LayoutElement>().flexibleWidth = 0f;
        historyRetryButton.onClick.AddListener(() => LoadSessions(true));
        Button fresh = ChatbotUiFactory.Button("HistoryNew", actionRow.transform, "Trò chuyện mới",
            ChatbotUiFactory.Blue, Color.white, 42f);
        fresh.GetComponent<LayoutElement>().preferredWidth = ChatbotUiFactory.U(180f);
        fresh.GetComponent<LayoutElement>().flexibleWidth = 0f;
        fresh.onClick.AddListener(StartNewConversation);

        historyStatusText = ChatbotUiFactory.Text("HistoryStatus", historyPanel.transform,
            string.Empty, 18f, ChatbotUiFactory.Muted, TextAlignmentOptions.Center);
        RectTransform historyStatusRect = (RectTransform)historyStatusText.transform;
        historyStatusRect.anchorMin = new Vector2(0.06f, 0.84f);
        historyStatusRect.anchorMax = new Vector2(0.94f, 0.9f);
        historyStatusRect.offsetMin = Vector2.zero;
        historyStatusRect.offsetMax = Vector2.zero;
        ChatbotUiFactory.ScrollView(historyPanel.transform, "HistoryScroll", out sessionContent);
        RectTransform scrollRect = (RectTransform)sessionContent.parent.parent;
        scrollRect.anchorMin = new Vector2(0f, 0.1f);
        scrollRect.anchorMax = new Vector2(1f, 0.82f);
        scrollRect.offsetMin = new Vector2(0f, ChatbotUiFactory.U(12f));
        scrollRect.offsetMax = Vector2.zero;
        historyPanel.SetActive(false);
    }

    private void BuildDeleteDialog(Transform parent)
    {
        deleteDialog = new GameObject("DeleteConversationDialog", typeof(RectTransform), typeof(Image));
        deleteDialog.layer = parent.gameObject.layer;
        deleteDialog.transform.SetParent(parent, false);
        ChatbotUiFactory.Stretch((RectTransform)deleteDialog.transform, Vector2.zero, Vector2.zero);
        deleteDialog.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.36f);

        GameObject card = ChatbotUiFactory.Rect("DialogCard", deleteDialog.transform,
            new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.64f), Vector2.zero, Vector2.zero, ChatbotUiFactory.White);
        TMP_Text title = ChatbotUiFactory.Text("DialogTitle", card.transform,
            "Xóa cuộc trò chuyện?", 28f, ChatbotUiFactory.Navy, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        var titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0.05f, 0.58f);
        titleRect.anchorMax = new Vector2(0.95f, 0.9f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        TMP_Text message = ChatbotUiFactory.Text("DialogMessage", card.transform,
            "Cuộc trò chuyện này sẽ bị xóa vĩnh viễn.", 21f, ChatbotUiFactory.Muted, TextAlignmentOptions.Center);
        var messageRect = (RectTransform)message.transform;
        messageRect.anchorMin = new Vector2(0.05f, 0.34f);
        messageRect.anchorMax = new Vector2(0.95f, 0.62f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;
        GameObject buttons = new GameObject("DialogButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttons.layer = card.layer;
        buttons.transform.SetParent(card.transform, false);
        var buttonRect = (RectTransform)buttons.transform;
        buttonRect.anchorMin = new Vector2(0.04f, 0.06f);
        buttonRect.anchorMax = new Vector2(0.96f, 0.28f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        var group = buttons.GetComponent<HorizontalLayoutGroup>();
        group.spacing = ChatbotUiFactory.U(16f);
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
        ChatbotUiFactory.Button("Keep", buttons.transform, "Giữ lại", ChatbotUiFactory.PaleBlue,
            ChatbotUiFactory.Navy, 78f).onClick.AddListener(CloseDeleteDialog);
        ChatbotUiFactory.Button("ConfirmDelete", buttons.transform, "Xóa", ChatbotUiFactory.Blue,
            Color.white, 78f).onClick.AddListener(ConfirmDeleteSession);
        deleteDialog.SetActive(false);
    }

    private void BuildChildPickerPanel(Transform parent)
    {
        childPickerPanel = new GameObject("ChildPickerPanel", typeof(RectTransform), typeof(Image));
        childPickerPanel.layer = parent.gameObject.layer;
        childPickerPanel.transform.SetParent(parent, false);
        ChatbotUiFactory.Stretch((RectTransform)childPickerPanel.transform, Vector2.zero, Vector2.zero);
        Image backdrop = childPickerPanel.GetComponent<Image>();
        backdrop.color = new Color(0.02f, 0.08f, 0.13f, 0.54f);
        backdrop.raycastTarget = true;

        GameObject card = ChatbotUiFactory.Card(childPickerPanel.transform, "ChildPickerCard",
            ChatbotUiFactory.White, 260f);
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0.06f, 0.18f);
        cardRect.anchorMax = new Vector2(0.94f, 0.82f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        TMP_Text title = ChatbotUiFactory.Text("ChildPickerTitle", card.transform,
            "Chọn bé cần hỗ trợ", 20f, ChatbotUiFactory.Navy);
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0.05f, 0.84f);
        titleRect.anchorMax = new Vector2(0.72f, 0.97f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        Button close = ChatbotUiFactory.Button("CloseChildPicker", card.transform, "Đóng",
            ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 38f);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = new Vector2(0.75f, 0.84f);
        closeRect.anchorMax = new Vector2(0.96f, 0.97f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;
        TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>(true);
        closeLabel.fontSize = ChatbotUiFactory.U(13f);
        closeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        close.onClick.AddListener(CloseChildPicker);

        childPickerStatusText = ChatbotUiFactory.Text("ChildPickerStatus", card.transform,
            string.Empty, 14f, ChatbotUiFactory.Muted);
        RectTransform statusRect = (RectTransform)childPickerStatusText.transform;
        statusRect.anchorMin = new Vector2(0.06f, 0.75f);
        statusRect.anchorMax = new Vector2(0.94f, 0.83f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        ChatbotUiFactory.ScrollView(card.transform, "ChildPickerScroll", out childPickerContent);
        RectTransform scrollRect = (RectTransform)childPickerContent.parent.parent;
        scrollRect.anchorMin = new Vector2(0.04f, 0.04f);
        scrollRect.anchorMax = new Vector2(0.96f, 0.72f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        childPickerPanel.SetActive(false);
    }

    private void AddSuggestion(Transform parent, string suggestion)
    {
        Button button = ChatbotUiFactory.Button("Suggestion", parent, suggestion,
            ChatbotUiFactory.White, ChatbotUiFactory.Navy, 52f);
        button.GetComponent<Image>().color = ChatbotUiFactory.White;
        button.onClick.AddListener(() =>
        {
            if (input == null || sending || !HasActiveScope) return;
            input.text = suggestion;
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
        });
    }

    private void OpenChat()
    {
        if (screen == null) BuildChatScreen();
        if (screen == null) return;
        CloseChildPicker();
        screen.SetActive(true);
        screen.transform.SetAsLastSibling();
        if (taskbar != null) taskbar.SetAsLastSibling();
        ShowHistory(false);
        RefreshScopeFromManager();
        if (HasActiveScope && sessionId == null) LoadSessions(true);
        UpdateEmptyState();
    }

    private void CloseChat()
    {
        CloseChildPicker();
        if (historyPanel != null) historyPanel.SetActive(false);
        if (deleteDialog != null) deleteDialog.SetActive(false);
        pendingDeleteSessionId = null;
        if (screen != null) screen.SetActive(false);
    }

    private void OpenHistory()
    {
        if (sending || loadingMessages || !HasActiveScope) return;
        if (input != null) input.DeactivateInputField();
        ShowHistory(true);
        LoadSessions(true);
    }

    private void OpenChildPicker()
    {
        if (sending || loadingMessages || childPickerPanel == null || manager == null ||
            manager != FirebaseManager.Instance || manager.CurrentUser == null)
            return;

        childPickerPanel.SetActive(true);
        if (screen != null) screen.transform.SetAsLastSibling();
        if (taskbar != null) taskbar.SetAsLastSibling();
        ClearContent(childPickerContent);
        if (childPickerStatusText != null) childPickerStatusText.text = "Đang tải hồ sơ bé…";

        int request = ++childPickerRequestGeneration;
        string requestedParent = manager.GetCurrentParentId();
        int requestedGeneration = manager.SessionGeneration;
        LoadChildOptionsAsync(manager, requestedParent, requestedGeneration, request);
    }

    private async void LoadChildOptionsAsync(FirebaseManager capturedManager, string capturedParent,
        int capturedGeneration, int request)
    {
        try
        {
            List<ChildModel> children = await capturedManager.GetChildrenAsync(capturedParent);
            if (!IsCurrentChildPickerRequest(capturedManager, capturedParent, capturedGeneration, request)) return;

            ClearContent(childPickerContent);
            int optionIndex = 0;
            string activeChildId = capturedManager.GetCurrentChildId();
            bool foundActiveChild = false;
            foreach (ChildModel child in children ?? new List<ChildModel>())
            {
                if (child == null || string.IsNullOrWhiteSpace(child.childId)) continue;
                string childName = string.IsNullOrWhiteSpace(child.name) ? "Bé" : child.name.Trim();
                bool isSelected = string.Equals(child.childId, activeChildId, StringComparison.Ordinal);
                if (isSelected)
                {
                    currentChildDisplayName = childName;
                    foundActiveChild = true;
                }

                string label = isSelected ? "✓  " + childName + " · Đang chọn" : childName;
                Button option = ChatbotUiFactory.Button("ChildOption" + optionIndex++, childPickerContent,
                    label, isSelected ? ChatbotUiFactory.PaleBlue : ChatbotUiFactory.White,
                    ChatbotUiFactory.Navy, 52f);
                TMP_Text optionLabel = option.GetComponentInChildren<TMP_Text>(true);
                optionLabel.fontSize = ChatbotUiFactory.U(14f);
                optionLabel.textWrappingMode = TextWrappingModes.NoWrap;
                optionLabel.overflowMode = TextOverflowModes.Ellipsis;
                option.onClick.AddListener(() => SelectChild(child, capturedParent, capturedGeneration));
            }

            if (childPickerStatusText != null)
                childPickerStatusText.text = optionIndex > 0
                    ? "Chọn hồ sơ bé muốn trò chuyện cùng Habi."
                    : "Chưa có hồ sơ bé. Hãy tạo hồ sơ ở tab Nhiệm vụ.";
            if (foundActiveChild) UpdateHeader();
        }
        catch (Exception exception)
        {
            if (!IsCurrentChildPickerRequest(capturedManager, capturedParent, capturedGeneration, request)) return;
            if (childPickerStatusText != null)
                childPickerStatusText.text = "Không tải được danh sách bé. Hãy đóng rồi thử lại.";
            Debug.LogWarning("[Chatbot] Child list read failed: " + exception.GetType().Name);
        }
    }

    private bool IsCurrentChildPickerRequest(FirebaseManager capturedManager, string capturedParent,
        int capturedGeneration, int request)
    {
        return this != null && isActiveAndEnabled && childPickerPanel != null && childPickerPanel.activeSelf &&
            childPickerRequestGeneration == request && manager == capturedManager &&
            manager == FirebaseManager.Instance && manager.SessionGeneration == capturedGeneration &&
            manager.GetCurrentParentId() == capturedParent;
    }

    private void SelectChild(ChildModel selectedChild, string capturedParent, int capturedGeneration)
    {
        if (selectedChild == null || string.IsNullOrWhiteSpace(selectedChild.childId) || manager == null ||
            manager != FirebaseManager.Instance || manager.GetCurrentParentId() != capturedParent ||
            manager.SessionGeneration != capturedGeneration)
            return;

        string selectedName = string.IsNullOrWhiteSpace(selectedChild.name) ? "Bé" : selectedChild.name.Trim();
        CloseChildPicker();
        currentChildDisplayName = selectedName;
        if (!string.Equals(manager.GetCurrentChildId(), selectedChild.childId, StringComparison.Ordinal))
            manager.SetCurrentChildId(selectedChild.childId);
        UpdateHeader();
        SetStatus(string.Empty);
    }

    private void CloseChildPicker()
    {
        childPickerRequestGeneration++;
        if (childPickerPanel != null) childPickerPanel.SetActive(false);
    }

    private void CloseHistory()
    {
        ShowHistory(false);
        RenderTranscript();
    }

    private void ShowHistory(bool value)
    {
        if (historyPanel != null) historyPanel.SetActive(value);
        if (screen != null) screen.transform.SetAsLastSibling();
        if (taskbar != null) taskbar.SetAsLastSibling();
    }

    private void StartNewConversation()
    {
        if (sending || loadingMessages || !HasActiveScope) return;
        sessionId = null;
        serverMessages.Clear();
        ClearContent(messageContent);
        input.text = string.Empty;
        SetStatus(string.Empty);
        ShowHistory(false);
        UpdateEmptyState();
        if (newConversationButton != null) newConversationButton.interactable = true;
    }

    private void OnSendClicked()
    {
        if (sending || loadingMessages || input == null) return;
        string message = (input.text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetStatus("Hãy nhập câu hỏi trước khi gửi.");
            return;
        }
        if (message.Length > MaximumMessageCharacters)
        {
            SetStatus("Câu hỏi dài quá. Hãy rút gọn còn tối đa 2.000 ký tự.");
            return;
        }
        RefreshScopeFromManager();
        if (!HasActiveScope)
        {
            SetStatus("Đăng nhập và chọn hồ sơ bé để trò chuyện cùng Habi.");
            return;
        }
        SetStatus(string.Empty);
        StartCoroutine(SendMessageRoutine(message, parentId, childId, scopeGeneration, ++operationGeneration));
    }

    private IEnumerator SendMessageRoutine(string message, string capturedParent, string capturedChild,
        int capturedScopeGeneration, int requestGeneration)
    {
        sending = true;
        if (headerAvatarImage != null && thinkingSprite != null) headerAvatarImage.sprite = thinkingSprite;
        SetControlsEnabled(false);
        SetStatus(string.Empty);
        UpdateEmptyState();
        bool accepted = false;
        string errorCode = null;
        string activeSession = sessionId;
        string response = string.Empty;
        int expectedSequence = 1;
        bool gotDone = false;
        bool renderedAssistantText = false;
        float nextAssistantRenderTime = 0f;
        GameObject pendingUserBubble = null;
        GameObject partial = null;

        try
        {
            // Reserve the message order before the typing bubble is added. Keep the pending
            // user row hidden until the server accepts the request.
            pendingUserBubble = CreateMessageBubble(true, message, false);
            if (pendingUserBubble != null) pendingUserBubble.SetActive(false);
            partial = CreateTypingIndicatorBubble();
            UpdateEmptyState();
            ScrollToEnd();

            FirebaseChatbotContextSnapshot snapshot = null;
            Task<FirebaseChatbotContextSnapshot> contextTask = LoadSnapshotAsync(capturedScopeGeneration);
            while (!contextTask.IsCompleted && IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
                yield return null;
            if (!IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
                yield break;
            if (contextTask.IsFaulted)
            {
                SetStatus("Chưa tải được thông tin của bé. Vui lòng thử lại.");
                Debug.LogWarning("[Chatbot] Context read failed: " + contextTask.Exception?.GetBaseException().GetType().Name);
                yield break;
            }
            if (contextTask.IsCanceled || contextTask.Result == null)
            {
                SetStatus("Chưa thể xác nhận hồ sơ đang chọn. Vui lòng thử lại.");
                yield break;
            }
            snapshot = contextTask.Result;
            activeSnapshot = snapshot;
            if (!snapshot.IsCurrent || !IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
            {
                SetStatus("Hồ sơ đã thay đổi. Hãy gửi lại câu hỏi từ hồ sơ đang chọn.");
                yield break;
            }
            if (snapshot.Context != null && snapshot.Context.child != null)
            {
                currentChildDisplayName = snapshot.Context.child.name;
                UpdateHeader();
            }

            if (!EnsureClient()) yield break;

            if (string.IsNullOrEmpty(activeSession))
            {
                ChatbotSessionResponse created = null;
                ChatbotApiError createError = null;
                yield return client.CreateSession(capturedChild, value => created = value, value => createError = value,
                    snapshot.Cancellation);
                if (!IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration)) yield break;
                if (created == null)
                {
                    if (IsAmbiguousCreateFailure(createError))
                        SetStatus("Chưa xác nhận được cuộc trò chuyện mới. Hãy mở Lịch sử để kiểm tra trước khi thử lại.");
                    else
                        SetStatus(CreateErrorMessage(createError));
                    yield break;
                }
                activeSession = created.id;
                sessionId = activeSession;
            }

            ChatbotSendMessageRequest body = new ChatbotSendMessageRequest
            {
                message = message,
                plan = settings != null ? settings.Plan : "free",
                childId = capturedChild,
                context = snapshot.Context
            };
            int httpStatus = 0;
            ChatbotApiError streamError = null;
            Exception consumerException = null;

            yield return client.SendMessage(activeSession, body, value =>
            {
                try
                {
                    if (!IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration)) return;
                    if (value.eventName == "meta")
                    {
                        if (accepted)
                        {
                            errorCode = "duplicate_meta";
                            return;
                        }
                        MetaPayload meta = JsonUtility.FromJson<MetaPayload>(value.data);
                        if (meta == null || meta.session_id != activeSession)
                        {
                            errorCode = "session_mismatch";
                            return;
                        }
                        accepted = true;
                        input.text = string.Empty;
                        if (pendingUserBubble != null) pendingUserBubble.SetActive(true);
                        else CreateMessageBubble(true, message, false);
                        if (partial != null) partial.transform.SetAsLastSibling();
                        UpdateEmptyState();
                        ScrollToEnd();
                    }
                    else if (value.eventName == "delta")
                    {
                        if (gotDone)
                        {
                            errorCode = "data_after_terminal";
                            return;
                        }
                        if (!accepted)
                        {
                            errorCode = "delta_before_meta";
                            return;
                        }
                        DeltaPayload delta = JsonUtility.FromJson<DeltaPayload>(value.data);
                        if (delta == null || delta.seq != expectedSequence || delta.text == null)
                        {
                            errorCode = "invalid_stream_sequence";
                            return;
                        }
                        expectedSequence++;
                        response += delta.text;
                        if (partial != null)
                        {
                            StopTypingIndicator();
                            if (!renderedAssistantText || Time.unscaledTime >= nextAssistantRenderTime)
                            {
                                SetAssistantMessageText(partial, response, false);
                                renderedAssistantText = true;
                                nextAssistantRenderTime = Time.unscaledTime + 0.08f;
                            }
                        }
                        ScrollToEndIfAlreadyNearBottom();
                    }
                    else if (value.eventName == "done")
                    {
                        if (gotDone) errorCode = "duplicate_terminal";
                        gotDone = true;
                    }
                    else if (value.eventName == "error")
                    {
                        ChatbotSseErrorPayload error = JsonUtility.FromJson<ChatbotSseErrorPayload>(value.data);
                        errorCode = error != null ? error.code : "stream_error";
                    }
                }
                catch (Exception exception) { consumerException = exception; }
            }, value =>
            {
                streamError = value;
                if (value != null) httpStatus = (int)value.statusCode;
            }, snapshot.Cancellation, exception => consumerException = exception);

            if (!IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration)) yield break;
            if (consumerException != null)
            {
                Debug.LogError("[Chatbot] Stream callback failed: " + consumerException.GetType().Name);
                errorCode = "client_callback_failed";
            }
            if (string.IsNullOrEmpty(errorCode) && streamError != null) errorCode = streamError.code;

            if (gotDone && string.IsNullOrEmpty(errorCode) && accepted)
            {
                if (partial != null)
                {
                    StopTypingIndicator();
                    partial.name = "AssistantMessage";
                    var bubble = partial.GetComponent<Image>();
                    if (bubble != null) bubble.color = ChatbotUiFactory.White;
                    SetAssistantMessageText(partial, response, true);
                }
                bool historyLoaded = false;
                yield return LoadMessagesRoutine(activeSession, capturedChild, capturedParent,
                    capturedScopeGeneration, requestGeneration, false, value => historyLoaded = value);
                if (IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
                    SetStatus(historyLoaded ? string.Empty : "Câu trả lời đã hoàn tất nhưng chưa tải được lịch sử. Bạn có thể mở Lịch sử để kiểm tra.");
            }
            else if (accepted)
            {
                StopTypingIndicator();
                if (partial != null && partial.TryGetComponent(out Image partialImage))
                    partialImage.color = new Color32(255, 248, 240, 255);
                if (partial != null) SetAssistantMessageText(partial, response, true);
                SetStatus("Kết nối bị gián đoạn. Đang kiểm tra lịch sử đã lưu…");
                yield return LoadMessagesRoutine(activeSession, capturedChild, capturedParent,
                    capturedScopeGeneration, requestGeneration, false, null);
                if (IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
                {
                    string detail = errorCode ?? (httpStatus == 429 ? "quota_exceeded" : null) ??
                        (streamError != null ? streamError.code : null);
                    SetStatus(AfterAcceptedErrorMessage(detail, response));
                }
            }
            else if (IsAmbiguousSendFailure(streamError, errorCode))
            {
                SetStatus("Chưa xác nhận được câu hỏi đã lưu hay chưa. Đang kiểm tra lịch sử; không tự gửi lại để tránh tính lượt trùng.");
                yield return LoadMessagesRoutine(activeSession, capturedChild, capturedParent,
                    capturedScopeGeneration, requestGeneration, false, null);
                if (IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
                    SetStatus("Kết quả gửi chưa xác định. Hãy kiểm tra lịch sử trước khi gửi câu hỏi này lần nữa.");
            }
            else if (IsCurrentRequest(capturedParent, capturedChild, capturedScopeGeneration, requestGeneration))
            {
                string detail = errorCode ?? (streamError != null ? streamError.code : null);
                SetStatus(CreateErrorMessage(streamError, detail));
            }
        }
        finally
        {
            StopTypingIndicator();
            if (!accepted)
            {
                if (pendingUserBubble != null) Destroy(pendingUserBubble);
                if (partial != null) Destroy(partial);
                UpdateEmptyState();
            }
            if (snapshotFromCurrentRequest(requestGeneration)) ReleaseActiveSnapshot();
            if (operationGeneration == requestGeneration)
            {
                sending = false;
                if (headerAvatarImage != null) headerAvatarImage.sprite = happySprite;
                SetControlsEnabled(true);
            }
        }
    }

    private async Task<FirebaseChatbotContextSnapshot> LoadSnapshotAsync(int generation)
    {
        int days = settings != null ? settings.HistoryDays : 30;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset localNow = now.ToOffset(TimeSpan.FromHours(7));
        DateTime localToday = localNow.Date;
        string start = localToday.AddDays(-(days - 1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string end = localToday.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7),
            "Asia/Ho_Chi_Minh", "Asia/Ho_Chi_Minh");
        FirebaseChatbotContextSnapshot snapshot = await FirebaseChatbotContextAdapter.LoadCurrentSnapshotAsync(
            manager, start, end, now, timeZone);
        if (snapshot != null && snapshot.Context != null)
        {
            snapshot.Context.historyRange = new ChatbotHistoryRangeContext
            {
                startDateInclusive = start,
                endDateExclusive = end
            };
        }
        return snapshot;
    }

    private bool IsCurrentRequest(string capturedParent, string capturedChild, int generation, int request)
    {
        return this != null && isActiveAndEnabled && operationGeneration == request &&
            manager == FirebaseManager.Instance && manager != null &&
            manager.SessionGeneration == generation && manager.GetCurrentParentId() == capturedParent &&
            manager.GetCurrentChildId() == capturedChild && parentId == capturedParent && childId == capturedChild;
    }

    private bool snapshotFromCurrentRequest(int request)
    {
        return operationGeneration == request;
    }

    private void ReleaseActiveSnapshot()
    {
        if (activeSnapshot == null) return;
        activeSnapshot.Dispose();
        activeSnapshot = null;
    }

    private void RefreshScopeFromManager()
    {
        if (manager == null || manager != FirebaseManager.Instance)
            BindManager();
        string nextParent = manager != null ? manager.GetCurrentParentId() : string.Empty;
        string nextChild = manager != null ? manager.GetCurrentChildId() : string.Empty;
        int nextGeneration = manager != null ? manager.SessionGeneration : -1;
        if (nextParent == parentId && nextChild == childId && nextGeneration == scopeGeneration)
        {
            UpdateHeader();
            return;
        }

        operationGeneration++;
        scopeCancellation.Cancel();
        scopeCancellation = new ChatbotApiCancellation();
        sessionsRequestGeneration++;
        messagesRequestGeneration++;
        loadingSessions = false;
        loadingMessages = false;
        ReleaseActiveSnapshot();
        sending = false;
        SetControlsEnabled(true);
        parentId = nextParent ?? string.Empty;
        if (!string.Equals(clientOwnerUid, parentId, StringComparison.Ordinal))
        {
            client = null;
            clientOwnerUid = null;
        }
        childId = nextChild ?? string.Empty;
        if (deleteDialog != null) deleteDialog.SetActive(false);
        pendingDeleteSessionId = null;
        currentChildDisplayName = null;
        scopeGeneration = nextGeneration;
        sessionId = null;
        sessions.Clear();
        serverMessages.Clear();
        historyOffset = 0;
        sessionPageAvailable = false;
        ClearContent(messageContent);
        ClearContent(sessionContent);
        if (input != null) input.text = string.Empty;
        UpdateHeader();
        UpdateEmptyState();
        SetControlsEnabled(true);
        if (historyStatusText != null) historyStatusText.text = string.Empty;
        if (historyPanel != null) historyPanel.SetActive(false);
        SetStatus(HasActiveScope ? string.Empty : "Đăng nhập và chọn hồ sơ bé để trò chuyện cùng Habi.");
        if (HasActiveScope && screen != null && screen.activeSelf) LoadSessions(true);
    }

    private void ResetScope()
    {
        operationGeneration++;
        scopeCancellation.Cancel();
        scopeCancellation = new ChatbotApiCancellation();
        sessionsRequestGeneration++;
        messagesRequestGeneration++;
        loadingSessions = false;
        loadingMessages = false;
        ReleaseActiveSnapshot();
        sending = false;
        SetControlsEnabled(true);
        parentId = string.Empty;
        childId = string.Empty;
        currentChildDisplayName = null;
        scopeGeneration = -1;
        client = null;
        clientOwnerUid = null;
        sessionId = null;
        sessions.Clear();
        serverMessages.Clear();
        ClearContent(messageContent);
        ClearContent(sessionContent);
        if (input != null) input.text = string.Empty;
        if (deleteDialog != null) deleteDialog.SetActive(false);
        pendingDeleteSessionId = null;
        if (historyStatusText != null) historyStatusText.text = string.Empty;
        if (historyPanel != null) historyPanel.SetActive(false);
        UpdateHeader();
        UpdateEmptyState();
        SetStatus("Đăng nhập và chọn hồ sơ bé để trò chuyện cùng Habi.");
        SetControlsEnabled(true);
    }

    private void UpdateHeader()
    {
        if (headerChildText == null) return;
        if (input != null && input.placeholder is TMP_Text placeholder)
            placeholder.text = HasActiveScope ? "Bạn muốn hỏi Habi điều gì?" : "Chọn bé trước khi trò chuyện";
        if (manager == null || manager.CurrentUser == null)
        {
            headerChildText.text = "Đăng nhập trước";
            if (childSelectorButton != null) childSelectorButton.interactable = false;
        }
        else if (!HasActiveScope)
        {
            headerChildText.text = "Chọn bé";
            if (childSelectorButton != null) childSelectorButton.interactable = !sending && !loadingMessages;
        }
        else
        {
            headerChildText.text = string.IsNullOrWhiteSpace(currentChildDisplayName)
                ? "Đổi bé"
                : "Bé: " + currentChildDisplayName + " · Đổi";
            if (childSelectorButton != null) childSelectorButton.interactable = !sending && !loadingMessages;
        }
    }

    private void UpdateEmptyState()
    {
        Transform empty = screen != null ? screen.transform.Find("ConversationBody/EmptyState") : null;
        if (empty != null) empty.gameObject.SetActive(serverMessages.Count == 0 && !HasLocalConversationBubble());
    }

    private bool HasLocalConversationBubble()
    {
        if (messageContent == null) return false;
        return messageContent.childCount > 0;
    }

    private void LoadSessions(bool reset)
    {
        if (loadingSessions || !HasActiveScope) return;
        if (!EnsureClient())
        {
            if (historyStatusText != null && statusText != null) historyStatusText.text = statusText.text;
            return;
        }
        if (reset)
        {
            sessions.Clear();
            historyOffset = 0;
            sessionPageAvailable = false;
            ClearContent(sessionContent);
        }
        loadingSessions = true;
        if (historyStatusText != null) historyStatusText.text = reset ? "Đang tải lịch sử…" : "Đang tải thêm…";
        if (historyRetryButton != null) historyRetryButton.interactable = false;
        int loadGeneration = ++sessionsRequestGeneration;
        StartCoroutine(LoadSessionsRoutine(parentId, childId, scopeGeneration, operationGeneration,
            historyOffset, reset, loadGeneration));
    }

    private IEnumerator LoadSessionsRoutine(string capturedParent, string capturedChild, int generation,
        int requestGeneration, int offset, bool replace, int loadGeneration)
    {
        ChatbotSessionListResponse response = null;
        ChatbotApiError error = null;
        yield return client.ListSessions(capturedChild, PageSize, offset,
            value => response = value, value => error = value, scopeCancellation);
        if (loadGeneration != sessionsRequestGeneration) yield break;
        loadingSessions = false;
        if (!IsCurrentRequest(capturedParent, capturedChild, generation, requestGeneration)) yield break;
        if (response == null)
        {
            string errorMessage = CreateErrorMessage(error);
            SetStatus(errorMessage);
            if (historyStatusText != null) historyStatusText.text = errorMessage;
            if (historyRetryButton != null) historyRetryButton.interactable = true;
            yield break;
        }
        if (historyStatusText != null) historyStatusText.text = string.Empty;
        if (historyRetryButton != null) historyRetryButton.interactable = true;
        ChatbotSessionResponse[] page = response.sessions ?? Array.Empty<ChatbotSessionResponse>();
        if (replace)
        {
            sessions.Clear();
            ClearContent(sessionContent);
        }
        sessions.AddRange(page);
        historyOffset = sessions.Count;
        sessionPageAvailable = page.Length == PageSize;
        RenderSessions(capturedParent, capturedChild, generation, requestGeneration);
    }

    private void RenderSessions(string capturedParent, string capturedChild, int generation, int requestGeneration)
    {
        if (sessionContent == null) return;
        ClearContent(sessionContent);
        if (sessions.Count == 0)
        {
            TMP_Text empty = ChatbotUiFactory.Text("HistoryEmpty", sessionContent,
                "Chưa có cuộc trò chuyện nào với Habi.", 16f, ChatbotUiFactory.Muted, TextAlignmentOptions.Center);
            empty.GetComponent<LayoutElement>().minHeight = ChatbotUiFactory.U(100f);
        }
        foreach (ChatbotSessionResponse item in sessions)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
            GameObject row = ChatbotUiFactory.Card(sessionContent, "SessionRow", Color.white, 96f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ChatbotUiFactory.U(10f);
            layout.padding = new RectOffset((int)ChatbotUiFactory.U(18f), (int)ChatbotUiFactory.U(14f),
                (int)ChatbotUiFactory.U(10f), (int)ChatbotUiFactory.U(10f));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            Button open = ChatbotUiFactory.Button("OpenSession", row.transform,
                "Cuộc trò chuyện · " + FormatSessionDate(item.updated_at ?? item.created_at),
                ChatbotUiFactory.White, ChatbotUiFactory.Navy, 70f);
            open.GetComponent<LayoutElement>().flexibleWidth = 1f;
            string id = item.id;
            open.onClick.AddListener(() => OpenSession(id, capturedParent, capturedChild, generation, requestGeneration));
            Button delete = ChatbotUiFactory.Button("DeleteSession", row.transform, "Xóa",
                ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 70f);
            delete.GetComponent<LayoutElement>().preferredWidth = ChatbotUiFactory.U(68f);
            delete.GetComponent<LayoutElement>().flexibleWidth = 0f;
            delete.onClick.AddListener(() => ShowDeleteDialog(id));
        }
        if (sessionPageAvailable)
        {
            Button more = ChatbotUiFactory.Button("LoadMoreSessions", sessionContent, "Tải thêm",
                ChatbotUiFactory.PaleBlue, ChatbotUiFactory.Navy, 68f);
            more.onClick.AddListener(() => LoadSessions(false));
        }
    }

    private void OpenSession(string id, string capturedParent, string capturedChild, int generation, int requestGeneration)
    {
        if (sending || loadingMessages || !IsCurrentRequest(capturedParent, capturedChild, generation, requestGeneration)) return;
        sessionId = id;
        serverMessages.Clear();
        ClearContent(messageContent);
        ShowHistory(false);
        loadingMessages = true;
        SetControlsEnabled(false);
        SetStatus("Đang tải cuộc trò chuyện…");
        StartCoroutine(LoadMessagesRoutine(id, capturedChild, capturedParent, generation,
            requestGeneration, true));
    }

    private IEnumerator LoadMessagesRoutine(string id, string capturedChild, string capturedParent,
        int generation, int requestGeneration, bool showError, Action<bool> completed = null)
    {
        if (!EnsureClient())
        {
            loadingMessages = false;
            SetControlsEnabled(!sending);
            completed?.Invoke(false);
            yield break;
        }
        int loadGeneration = ++messagesRequestGeneration;
        loadingMessages = true;
        SetControlsEnabled(false);
        ChatbotMessageListResponse response = null;
        ChatbotApiError error = null;
        yield return client.GetMessages(id, capturedChild, value => response = value, value => error = value,
            scopeCancellation);
        if (loadGeneration != messagesRequestGeneration)
        {
            completed?.Invoke(false);
            yield break;
        }
        loadingMessages = false;
        SetControlsEnabled(!sending);
        if (!IsCurrentRequest(capturedParent, capturedChild, generation, requestGeneration) || sessionId != id)
        {
            completed?.Invoke(false);
            yield break;
        }
        if (response == null)
        {
            if (showError) SetStatus(CreateErrorMessage(error));
            completed?.Invoke(false);
            yield break;
        }
        serverMessages.Clear();
        serverMessages.AddRange(response.messages ?? Array.Empty<ChatbotMessageResponse>());
        RenderTranscript();
        if (showError) SetStatus(string.Empty);
        completed?.Invoke(true);
    }

    private void RenderTranscript()
    {
        if (messageContent == null) return;
        ClearContent(messageContent);
        foreach (ChatbotMessageResponse message in serverMessages)
        {
            if (message == null || (message.role != "user" && message.role != "assistant")) continue;
            CreateMessageBubble(message.role == "user", message.content, false);
        }
        UpdateEmptyState();
        ScrollToEnd();
    }

    private GameObject CreateMessageBubble(bool fromUser, string value, bool partial)
    {
        if (messageContent == null) return null;
        GameObject row = new GameObject(fromUser ? "UserMessageRow" : "AssistantMessageRow",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.layer = messageContent.gameObject.layer;
        row.transform.SetParent(messageContent, false);
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = fromUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        // Apply the bubble's preferred width; otherwise the RectTransform keeps its 100-unit default
        // and long Vietnamese messages wrap almost one character per line on device.
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        var rowElement = row.GetComponent<LayoutElement>();
        rowElement.flexibleWidth = 1f;
        rowElement.minHeight = ChatbotUiFactory.U(56f);

        Color bubbleColor = fromUser ? ChatbotUiFactory.PaleBlue : ChatbotUiFactory.White;
        GameObject bubble = ChatbotUiFactory.Card(row.transform, fromUser ? "UserBubble" : "AssistantBubble", bubbleColor, 56f);
        var bubbleRect = (RectTransform)bubble.transform;
        var bubbleElement = bubble.GetComponent<LayoutElement>();
        float available = messageScroll != null ? messageScroll.viewport.rect.width : ChatbotUiFactory.U(355f);
        bubbleElement.preferredWidth = Mathf.Clamp(available * 0.82f,
            ChatbotUiFactory.U(240f), ChatbotUiFactory.U(355f));
        bubbleElement.flexibleWidth = 0f;
        var bubbleLayout = bubble.AddComponent<VerticalLayoutGroup>();
        bubbleLayout.padding = new RectOffset((int)ChatbotUiFactory.U(20f), (int)ChatbotUiFactory.U(20f),
            (int)ChatbotUiFactory.U(14f), (int)ChatbotUiFactory.U(14f));
        bubbleLayout.childControlWidth = true;
        bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;
        var fitter = bubble.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text text = ChatbotUiFactory.Text("MessageText", bubble.transform,
            value ?? string.Empty, 18f, ChatbotUiFactory.Navy);
        text.richText = !fromUser;
        if (!fromUser)
            text.text = ChatbotMarkdownRenderer.RenderToTmp(value ?? string.Empty, !partial);
        text.GetComponent<LayoutElement>().flexibleWidth = 1f;
        if (partial) text.name = "PartialAssistantText";
        bubbleRect.localScale = Vector3.one;
        return bubble;
    }

    private GameObject CreateTypingIndicatorBubble()
    {
        StopTypingIndicator();
        GameObject bubble = CreateMessageBubble(false, string.Empty, true);
        typingIndicatorText = bubble != null ? bubble.GetComponentInChildren<TMP_Text>(true) : null;
        if (typingIndicatorText != null)
        {
            typingIndicatorText.richText = false;
            typingIndicatorText.fontStyle = FontStyles.Bold;
            typingIndicatorText.text = "·  •  ·";
            typingIndicatorRoutine = StartCoroutine(AnimateTypingIndicator(typingIndicatorText));
        }
        return bubble;
    }

    private IEnumerator AnimateTypingIndicator(TMP_Text indicator)
    {
        string[] frames = { "·  •  ·", "•  ·  ·", "·  ·  •" };
        int frame = 0;
        while (indicator != null && sending)
        {
            indicator.text = frames[frame];
            frame = (frame + 1) % frames.Length;
            yield return new WaitForSecondsRealtime(0.32f);
        }
        typingIndicatorRoutine = null;
    }

    private void StopTypingIndicator()
    {
        if (typingIndicatorRoutine != null)
            StopCoroutine(typingIndicatorRoutine);
        typingIndicatorRoutine = null;
        typingIndicatorText = null;
    }

    private static void SetAssistantMessageText(GameObject bubble, string markdown, bool isFinal)
    {
        if (bubble == null) return;
        TMP_Text text = bubble.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        text.richText = true;
        text.text = ChatbotMarkdownRenderer.RenderToTmp(markdown ?? string.Empty, isFinal);
    }

    private void ShowDeleteDialog(string id)
    {
        if (sending || loadingMessages || deleteDialog == null) return;
        pendingDeleteSessionId = id;
        deleteDialog.SetActive(true);
        screen.transform.SetAsLastSibling();
        if (taskbar != null) taskbar.SetAsLastSibling();
    }

    private void CloseDeleteDialog()
    {
        pendingDeleteSessionId = null;
        if (deleteDialog != null) deleteDialog.SetActive(false);
    }

    private void ConfirmDeleteSession()
    {
        if (sending || string.IsNullOrWhiteSpace(pendingDeleteSessionId) || !HasActiveScope) return;
        StartCoroutine(DeleteSessionRoutine(pendingDeleteSessionId, parentId, childId, scopeGeneration,
            operationGeneration));
    }

    private IEnumerator DeleteSessionRoutine(string id, string capturedParent, string capturedChild,
        int generation, int requestGeneration)
    {
        if (!EnsureClient()) yield break;
        sending = true;
        SetControlsEnabled(false);
        SetStatus("Đang xóa cuộc trò chuyện…");
        bool deleted = false;
        ChatbotApiError error = null;
        yield return client.DeleteSession(id, capturedChild, () => deleted = true, value => error = value,
            scopeCancellation);
        if (!IsCurrentRequest(capturedParent, capturedChild, generation, requestGeneration)) yield break;
        if (deleteDialog != null) deleteDialog.SetActive(false);
        pendingDeleteSessionId = null;
        if (IsCurrentRequest(capturedParent, capturedChild, generation, requestGeneration))
        {
            sending = false;
            SetControlsEnabled(true);
            if (deleted)
            {
                sessions.RemoveAll(item => item != null && item.id == id);
                if (sessionId == id)
                {
                    sessionId = null;
                    serverMessages.Clear();
                    ClearContent(messageContent);
                    ShowHistory(false);
                    UpdateEmptyState();
                }
                RenderSessions(capturedParent, capturedChild, generation, requestGeneration);
                SetStatus(string.Empty);
            }
            else
            {
                SetStatus("Chưa xác nhận được kết quả xóa. Đang tải lại lịch sử…");
                LoadSessions(true);
                if (error != null) Debug.LogWarning("[Chatbot] Delete result: " + error.code);
            }
        }
    }

    private bool EnsureClient()
    {
        if (client != null && string.Equals(clientOwnerUid, parentId, StringComparison.Ordinal)) return true;
        if (settings == null) settings = Resources.Load<ChatbotRuntimeSettings>("HABIMONChatbotSettings");
        if (settings == null || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            SetStatus("Trợ lý chưa được cấu hình trên ứng dụng này.");
            return false;
        }
        if (manager == null || manager.CurrentUser == null || !HasActiveScope)
        {
            SetStatus("Đăng nhập và chọn hồ sơ bé để trò chuyện cùng Habi.");
            return false;
        }
        try
        {
            string token = ChatbotCredentialStore.GetOrCreate(parentId);
            client = new ChatbotApiClient(settings.BaseUrl, token, settings.AllowInsecureLocalhostForDevelopment);
            clientOwnerUid = parentId;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[Chatbot] Secure credential/API configuration unavailable: " + exception.GetType().Name);
            SetStatus("Trợ lý chưa sẵn sàng trên thiết bị này. Vui lòng thử lại sau.");
            return false;
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        bool chatReady = manager != null && manager.CurrentUser != null && HasActiveScope;
        if (sendButton != null) sendButton.interactable = enabled && !loadingMessages && chatReady;
        if (input != null) input.interactable = enabled && chatReady;
        if (childSelectorButton != null)
            childSelectorButton.interactable = enabled && !sending && !loadingMessages &&
                manager != null && manager.CurrentUser != null;
        bool canChangeConversation = enabled && !sending && !loadingMessages && HasActiveScope;
        if (historyButton != null) historyButton.interactable = canChangeConversation;
        if (newConversationButton != null) newConversationButton.interactable = canChangeConversation;
        if (historyRetryButton != null) historyRetryButton.interactable = enabled && HasActiveScope && !loadingSessions;
    }

    private void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value ?? string.Empty;
    }

    private void ScrollToEnd()
    {
        if (messageScroll == null) return;
        Canvas.ForceUpdateCanvases();
        messageScroll.verticalNormalizedPosition = 0f;
    }

    private void ScrollToEndIfAlreadyNearBottom()
    {
        if (messageScroll != null && messageScroll.verticalNormalizedPosition <= 0.12f)
            ScrollToEnd();
    }

    private static void ClearContent(RectTransform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static string FormatSessionDate(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
            return date.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        return "Ngày chưa rõ";
    }

    private static string CreateErrorMessage(ChatbotApiError error, string overrideCode = null)
    {
        string code = overrideCode ?? (error != null ? error.code : null);
        long status = error != null ? error.statusCode : 0;
        if (status == 429 || code == "http_error" && status == 429) return "Bạn đã đạt giới hạn trò chuyện hôm nay.";
        if (status == 401 || status == 403) return "Phiên trò chuyện không hợp lệ. Vui lòng đăng nhập lại.";
        if (status == 404) return "Cuộc trò chuyện không còn khả dụng. Hãy tải lại lịch sử để chọn cuộc trò chuyện khác.";
        if (status == 422) return "Câu hỏi hoặc thông tin gửi lên chưa hợp lệ. Hãy kiểm tra nội dung rồi thử lại.";
        if (code == "timeout") return "Kết nối chậm. Câu hỏi vẫn ở ô nhập; hãy thử lại khi mạng ổn định.";
        if (code == "cancelled") return "Hồ sơ đã đổi nên lượt gửi cũ được đóng lại.";
        if (code == "connection_failed") return "Không kết nối được với Habi. Kiểm tra mạng rồi thử lại.";
        if (code == "incomplete_stream" || code == "invalid_sse_payload" || code == "unsupported_sse_event")
            return "Phản hồi bị gián đoạn. Hãy tải lại lịch sử để kiểm tra trước khi gửi tiếp.";
        if (status >= 500) return "Máy chủ đang bận. Vui lòng thử lại sau.";
        return "Chưa gửi được câu hỏi. Nội dung vẫn được giữ để bạn thử lại.";
    }

    private static string AfterAcceptedErrorMessage(string code, string partialResponse)
    {
        if (code == "generation_failed")
            return "Habi chưa trả lời xong. Câu hỏi đã được gửi và tính vào giới hạn hôm nay.";
        if (code == "storage_failed")
            return "Chưa xác nhận được câu trả lời đã lưu. Lịch sử đã được tải lại nếu có thể.";
        if (code == "quota_exceeded")
            return "Bạn đã đạt giới hạn trò chuyện hôm nay.";
        if (string.IsNullOrEmpty(partialResponse))
            return "Kết nối bị gián đoạn. Lịch sử đã được kiểm tra; không tự gửi lại để tránh tính lượt trùng.";
        return "Câu trả lời bị ngắt giữa chừng. Lịch sử đã được kiểm tra; không tự gửi lại để tránh tính lượt trùng.";
    }

    private static bool IsAmbiguousCreateFailure(ChatbotApiError error)
    {
        if (error == null) return true;
        if (error.statusCode >= 400 && error.statusCode < 500) return false;
        return true;
    }

    private static bool IsAmbiguousSendFailure(ChatbotApiError error, string code)
    {
        if (code == "cancelled") return false;
        if (error != null && error.statusCode >= 400 && error.statusCode < 500) return false;
        return true;
    }

    private static bool IsValidPlan(string value)
    {
        return value == "free" || value == "plus" || value == "premium";
    }

    private Sprite LoadHabiSprite(string fileName)
    {
        return Resources.Load<Sprite>("Chatbot/" + fileName);
    }

    private static void DestroyNavigationSprite()
    {
        // The texture is owned by the runtime host and released by Unity when the host object dies.
    }
}

internal sealed class SendPointerDownHandler : MonoBehaviour, IPointerDownHandler
{
    private Button sendButton;
    private Action sendAction;

    public void Initialize(Button owner, Action action)
    {
        sendButton = owner;
        sendAction = action;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            sendButton == null || !sendButton.interactable)
            return;
        sendAction?.Invoke();
    }
}

 [Serializable] internal sealed class ChatbotSseErrorPayload { public string code; }
#pragma warning restore 0649
