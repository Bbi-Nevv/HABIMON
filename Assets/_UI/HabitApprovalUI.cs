using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HabitApprovalUI : MonoBehaviour
{
    private GameObject panelRoot;
    private Button openButton;
    private Button closeButton;
    private TMP_Text statusText;
    private RectTransform listContent;
    private FirebaseManager subscribedManager;
    private int refreshVersion;

    private void Awake()
    {
        BuildUi();
    }

    private void OnEnable()
    {
        SubscribeToSession();
    }

    private void Start()
    {
        SubscribeToSession();
        _ = RefreshAsync();
    }

    private void OnDisable()
    {
        refreshVersion++;
        UnsubscribeFromSession();
    }

    private void OnDestroy()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(OpenPanel);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);
    }

    private void BuildUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            enabled = false;
            return;
        }

        openButton = CreateButton("HabitApprovalOpenButton", canvas.transform, "Duyệt nhiệm vụ (0)");
        RectTransform openRect = (RectTransform)openButton.transform;
        openRect.anchorMin = new Vector2(1f, 0f);
        openRect.anchorMax = new Vector2(1f, 0f);
        openRect.pivot = new Vector2(1f, 0f);
        openRect.sizeDelta = new Vector2(280f, 56f);
        openRect.anchoredPosition = new Vector2(-28f, 28f);
        openButton.onClick.AddListener(OpenPanel);

        panelRoot = new GameObject("HabitApprovalPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = (RectTransform)panelRoot.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 620f);
        panelRoot.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.94f, 0.99f);

        TMP_Text title = CreateText("Title", "Nhiệm vụ chờ phụ huynh duyệt", panelRoot.transform, 30f);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -62f);
        titleRect.offsetMax = new Vector2(-78f, -12f);

        closeButton = CreateButton("CloseButton", panelRoot.transform, "Đóng");
        RectTransform closeRect = (RectTransform)closeButton.transform;
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(80f, 44f);
        closeRect.anchoredPosition = new Vector2(-16f, -14f);
        closeButton.onClick.AddListener(ClosePanel);

        statusText = CreateText("Status", string.Empty, panelRoot.transform, 20f);
        RectTransform statusRect = (RectTransform)statusText.transform;
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.offsetMin = new Vector2(24f, -104f);
        statusRect.offsetMax = new Vector2(-24f, -68f);
        statusText.color = new Color(0.18f, 0.2f, 0.22f);

        GameObject scrollObject = new GameObject("ApprovalScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(panelRoot.transform, false);
        RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(20f, 20f);
        scrollRectTransform.offsetMax = new Vector2(-20f, -118f);
        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 36f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Image>().raycastTarget = true;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        listContent = (RectTransform)content.transform;
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRect;
        scroll.content = listContent;

        panelRoot.SetActive(false);
    }

    private void SubscribeToSession()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || subscribedManager == manager)
            return;

        UnsubscribeFromSession();
        subscribedManager = manager;
        subscribedManager.CurrentChildChanged += OnCurrentChildChanged;
        subscribedManager.SessionChanged += OnSessionChanged;
    }

    private void UnsubscribeFromSession()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.CurrentChildChanged -= OnCurrentChildChanged;
        subscribedManager.SessionChanged -= OnSessionChanged;
        subscribedManager = null;
    }

    private void OnCurrentChildChanged()
    {
        _ = RefreshAsync();
    }

    private void OnSessionChanged()
    {
        _ = RefreshAsync();
    }

    private void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        _ = RefreshAsync();
    }

    private void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private async Task RefreshAsync()
    {
        int requestVersion = ++refreshVersion;
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
        {
            if (openButton != null)
                openButton.gameObject.SetActive(false);
            ClearRows();
            SetOpenButtonText("Duyệt nhiệm vụ (0)");
            SetStatus("Chưa kết nối Firebase.");
            return;
        }

        if (!await manager.EnsureInitializedAsync())
        {
            if (this != null && requestVersion == refreshVersion)
                SetStatus($"Firebase chưa sẵn sàng: {manager.InitializationError}");
            return;
        }

        if (this == null || !isActiveAndEnabled || requestVersion != refreshVersion ||
            manager != FirebaseManager.Instance)
            return;

        if (manager.CurrentUser == null)
        {
            if (openButton != null)
                openButton.gameObject.SetActive(false);
            ClearRows();
            SetOpenButtonText("Duyệt nhiệm vụ (0)");
            SetStatus("Vui lòng đăng nhập để xem nhiệm vụ chờ duyệt.");
            return;
        }

        if (openButton != null)
            openButton.gameObject.SetActive(true);

        string parentId = manager.GetCurrentParentId();
        string childId = manager.GetCurrentChildId();
        int sessionGeneration = manager.SessionGeneration;
        if (string.IsNullOrEmpty(childId))
        {
            ClearRows();
            SetOpenButtonText("Duyệt nhiệm vụ (0)");
            SetStatus("Chọn một trẻ để xem nhiệm vụ chờ duyệt.");
            return;
        }

        try
        {
            List<PendingHabitApproval> pending = await HabitRewardService.GetPendingApprovalsAsync(parentId, childId);
            if (this == null || !isActiveAndEnabled || requestVersion != refreshVersion ||
                !IsCurrentScope(manager, parentId, childId, sessionGeneration))
                return;

            ClearRows();
            SetOpenButtonText($"Duyệt nhiệm vụ ({pending.Count})");
            if (pending.Count == 0)
                SetStatus("Không có nhiệm vụ nào đang chờ duyệt.");
            else
                SetStatus($"Có {pending.Count} nhiệm vụ của trẻ đang chờ phụ huynh xác nhận.");

            foreach (PendingHabitApproval item in pending)
                CreateApprovalRow(item, parentId, childId, sessionGeneration);
        }
        catch (Exception ex)
        {
            if (this == null || requestVersion != refreshVersion)
                return;

            Debug.LogError($"[HabitApprovalUI] Không thể tải danh sách duyệt: {ex.Message}");
            SetStatus("Không thể tải danh sách. Vui lòng thử lại.");
        }
    }

    private void CreateApprovalRow(
        PendingHabitApproval item, string parentId, string childId, int sessionGeneration)
    {
        GameObject row = new GameObject("ApprovalRow", typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(listContent, false);
        row.GetComponent<Image>().color = new Color(0.91f, 0.94f, 0.91f);
        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 86f;
        HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 10f;
        rowGroup.padding = new RectOffset(12, 12, 8, 8);
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;

        HabitLogModel log = item.Log;
        HabitModel habit = item.Habit;
        string title = habit != null && !string.IsNullOrWhiteSpace(habit.title)
            ? habit.title
            : "Không tìm thấy thói quen";
        string reward = habit != null
            ? $"Thưởng: {log.rewardGoldAtCompletion ?? habit.rewardGold} vàng, {log.rewardExpAtCompletion ?? habit.rewardExp} EXP"
            : "Không thể duyệt vì dữ liệu thói quen không còn.";
        string rowText = $"{title}\nNgày {log.dateKey} · {reward}";
        TMP_Text label = CreateText("ApprovalDetails", rowText, row.transform, 18f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(0.14f, 0.17f, 0.15f);
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;

        Button approveButton = CreateButton("ApproveButton", row.transform, "Duyệt");
        LayoutElement buttonLayout = approveButton.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 120f;
        buttonLayout.preferredHeight = 52f;
        approveButton.interactable = habit != null;
        approveButton.onClick.AddListener(() => _ = ApproveAsync(item, parentId, childId, sessionGeneration, approveButton));
    }

    private async Task ApproveAsync(
        PendingHabitApproval item, string parentId, string childId, int sessionGeneration, Button button)
    {
        if (button == null || item?.Log == null)
            return;

        FirebaseManager manager = FirebaseManager.Instance;
        if (!IsCurrentScope(manager, parentId, childId, sessionGeneration))
            return;

        button.interactable = false;
        bool approved;
        try
        {
            approved = await HabitRewardService.ApprovePendingLogAsync(parentId, childId, item.Log.logId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HabitApprovalUI] Không thể duyệt nhiệm vụ: {ex.Message}");
            if (this != null && button != null)
            {
                SetStatus("Không thể duyệt nhiệm vụ này. Hãy tải lại danh sách rồi thử lại.");
                button.interactable = true;
            }
            return;
        }

        if (this == null || !isActiveAndEnabled ||
            !IsCurrentScope(manager, parentId, childId, sessionGeneration))
            return;

        if (!approved)
        {
            SetStatus("Không thể duyệt nhiệm vụ này. Hãy tải lại danh sách rồi thử lại.");
            if (button != null)
                button.interactable = true;
            return;
        }

        await RefreshAsync();
    }

    private void ClearRows()
    {
        if (listContent == null)
            return;

        for (int index = listContent.childCount - 1; index >= 0; index--)
            Destroy(listContent.GetChild(index).gameObject);
    }

    private void SetOpenButtonText(string value)
    {
        TMP_Text text = openButton != null ? openButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null)
            text.text = value;
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value;
    }

    private static bool IsCurrentScope(FirebaseManager manager, string parentId, string childId, int generation)
    {
        return manager != null && manager == FirebaseManager.Instance &&
            manager.SessionGeneration == generation &&
            manager.GetCurrentParentId() == parentId && manager.GetCurrentChildId() == childId;
    }

    private static Button CreateButton(string name, Transform parent, string caption)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.25f, 0.48f, 0.34f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", caption, buttonObject.transform, 20f);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        RectTransform textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private static TMP_Text CreateText(string name, string value, Transform parent, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.black;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
