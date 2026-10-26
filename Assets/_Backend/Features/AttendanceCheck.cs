using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AttendanceCheck : MonoBehaviour
{
    public const string WaterDropsKey = "Habimon.Attendance.WaterDrops";
    public const string LastCheckInDateKey = "Habimon.Attendance.LastCheckInDate";
    private const string DateFormat = "yyyy-MM-dd";

    [Header("Daily reward")]
    [SerializeField, Min(1)] private int dailyWaterReward = 10;

    [Header("UI")]
    [SerializeField] private Button attendanceButton;
    [SerializeField] private TMP_Text waterCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Water drop animation")]
    [Tooltip("The existing water icon. Its sprite is used for the flying drops.")]
    [SerializeField] private Image waterDropIcon;
    [Tooltip("Optional. Defaults to the attendance button's position.")]
    [SerializeField] private RectTransform rewardOrigin;
    [Tooltip("Optional. Defaults to the water icon, then the water count text.")]
    [SerializeField] private RectTransform waterTarget;
    [Tooltip("An overlay RectTransform on the same root Canvas as the target. Defaults to that root Canvas.")]
    [SerializeField] private RectTransform animationRoot;
    [SerializeField, Range(1, 20)] private int maxAnimatedDrops = 10;
    [SerializeField, Min(0.1f)] private float flightDuration = 0.65f;
    [SerializeField, Min(0f)] private float dropInterval = 0.06f;
    [SerializeField, Min(1f)] private float dropSize = 40f;

    private readonly List<GameObject> flyingDrops = new List<GameObject>();
    private Sequence rewardSequence;
    private float refreshTimer;
    private bool isAnimating;

    private static event Action WaterBalanceChanged;

    // Local device balance, shared across scenes and independent of Firebase accounts.
    public int WaterDrops => Mathf.Max(0, PlayerPrefs.GetInt(WaterDropsKey, 0));
    public static bool CanCheckInToday => CanClaimOn(DateTime.Today);
    public bool CanCheckIn => CanCheckInToday;

    public static bool TrySpendWater(int amount)
    {
        int balance = Mathf.Max(0, PlayerPrefs.GetInt(WaterDropsKey, 0));
        if (amount <= 0 || balance < amount)
            return false;

        PlayerPrefs.SetInt(WaterDropsKey, balance - amount);
        PlayerPrefs.Save();
        WaterBalanceChanged?.Invoke();
        return true;
    }

    private void OnEnable()
    {
        WaterBalanceChanged += HandleWaterBalanceChanged;
        if (attendanceButton != null)
            attendanceButton.onClick.AddListener(CheckIn);

        RefreshUI();
    }

    private void OnDisable()
    {
        WaterBalanceChanged -= HandleWaterBalanceChanged;
        if (attendanceButton != null)
            attendanceButton.onClick.RemoveListener(CheckIn);

        StopRewardAnimation();
        SetWaterCount(WaterDrops);
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = 1f;
        RefreshUI();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            RefreshUI();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            RefreshUI();
    }

    /// <summary>Can also be called from a Unity Button OnClick event.</summary>
    public void CheckIn()
    {
        if (!isActiveAndEnabled || isAnimating)
            return;

        DateTime today = DateTime.Today;
        // Read prefs on every claim so other AttendanceCheck instances cannot award twice.
        if (!CanClaimOn(today))
        {
            RefreshUI();
            return;
        }

        int previousBalance = WaterDrops;
        int reward = (int)Math.Min(Math.Max(1, dailyWaterReward), (long)int.MaxValue - previousBalance);

        // Persist before playing cosmetics: closing the panel/app cannot lose the reward.
        PlayerPrefs.SetInt(WaterDropsKey, previousBalance + reward);
        PlayerPrefs.SetString(LastCheckInDateKey, today.ToString(DateFormat, CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        WaterBalanceChanged?.Invoke();

        PlayRewardAnimation(previousBalance, reward);
        RefreshUI();
    }

    private void HandleWaterBalanceChanged()
    {
        // Spending during a reward flight must not let its old count overwrite the balance.
        StopRewardAnimation();
        RefreshUI();
    }

    private static bool CanClaimOn(DateTime today)
    {
        string savedDate = PlayerPrefs.GetString(LastCheckInDateKey, string.Empty);
        if (string.IsNullOrEmpty(savedDate))
            return true;

        if (!DateTime.TryParseExact(savedDate, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime lastDate))
            return true;

        // Moving the clock backwards must not reopen the most recently claimed day.
        return today > lastDate;
    }

    private void RefreshUI()
    {
        DateTime now = DateTime.Now;
        bool canClaim = CanClaimOn(now.Date);

        if (attendanceButton != null)
            attendanceButton.interactable = canClaim && !isAnimating;

        if (statusText != null)
            statusText.text = canClaim
                ? $"Điểm danh nhận {Math.Max(1, dailyWaterReward)} giọt nước"
                : "Đã điểm danh hôm nay";

        if (countdownText != null)
        {
            TimeSpan remaining = now.Date.AddDays(1) - now;
            countdownText.text = canClaim
                ? "Có thể điểm danh"
                : $"Ngày mới sau {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        if (!isAnimating)
            SetWaterCount(WaterDrops);
    }

    private void SetWaterCount(int amount)
    {       
        if (waterCountText != null)
            waterCountText.text = amount.ToString(CultureInfo.InvariantCulture);
    }

    private void PlayRewardAnimation(int previousBalance, int reward)
    {
        RectTransform target = waterTarget != null ? waterTarget
            : waterDropIcon != null ? waterDropIcon.rectTransform
            : waterCountText != null ? waterCountText.rectTransform : null;
        RectTransform origin = rewardOrigin != null ? rewardOrigin
            : attendanceButton != null ? attendanceButton.transform as RectTransform : null;
        Canvas targetCanvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        RectTransform root = animationRoot != null ? animationRoot
            : targetCanvas != null ? targetCanvas.rootCanvas.transform as RectTransform : null;     

        if (reward <= 0 || waterDropIcon == null || waterDropIcon.sprite == null ||
            origin == null || target == null || root == null || !root.gameObject.activeInHierarchy)
        {
            SetWaterCount(WaterDrops);
            return;
        }

        Canvas.ForceUpdateCanvases();
        if (!TryGetPositionInRoot(origin, root, out Vector2 start) ||
            !TryGetPositionInRoot(target, root, out Vector2 end))
        {
            SetWaterCount(WaterDrops);
            return;
        }

        isAnimating = true;
        SetWaterCount(previousBalance);
        int count = Mathf.Clamp(maxAnimatedDrops, 1, Math.Min(reward, 20));
        float duration = Mathf.Max(0.1f, flightDuration);
        rewardSequence = DOTween.Sequence().SetUpdate(true).Pause();

        for (int i = 0; i < count; i++)
        {
            GameObject drop = new GameObject("AttendanceWaterDrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            drop.layer = root.gameObject.layer;
            RectTransform rect = (RectTransform)drop.transform;
            rect.SetParent(root, false);
            rect.sizeDelta = Vector2.one * Mathf.Max(1f, dropSize);
            rect.localPosition = start + UnityEngine.Random.insideUnitCircle * 25f;
            Image image = drop.GetComponent<Image>();
            image.sprite = waterDropIcon.sprite;
            image.color = waterDropIcon.color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            flyingDrops.Add(drop);

            float delay = i * Mathf.Max(0f, dropInterval);
            // Core Transform shortcuts work without installing DOTween's optional UI module.
            rewardSequence.Insert(delay, rect.DOLocalJump(end, 45f, 1, duration).SetEase(Ease.InQuad));
            rewardSequence.Insert(delay, rect.DOScale(0.45f, duration).SetEase(Ease.InQuad));
            int arrivedBalance = previousBalance + (int)((long)reward * (i + 1) / count);
            rewardSequence.InsertCallback(delay + duration, () =>
            {
                SetWaterCount(arrivedBalance);
                if (drop != null)
                    Destroy(drop);
            });
        }

        rewardSequence.OnComplete(() =>
        {
            rewardSequence = null;
            isAnimating = false;
            flyingDrops.Clear();
            RefreshUI();
        });
        rewardSequence.Play();
    }

    // Convert via screen space so CanvasScaler, camera canvases and different UI parents work.
    private static bool TryGetPositionInRoot(RectTransform source, RectTransform root, out Vector2 position)
    {
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Canvas rootCanvas = root.GetComponentInParent<Canvas>();
        if (sourceCanvas == null || rootCanvas == null)
        {
            position = default;
            return false;
        }

        sourceCanvas = sourceCanvas.rootCanvas;
        rootCanvas = rootCanvas.rootCanvas;
        Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        Camera rootCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(sourceCamera, source.TransformPoint(source.rect.center));
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, rootCamera, out position);
    }

    private void StopRewardAnimation()
    {
        if (rewardSequence != null)
        {
            rewardSequence.Kill();
            rewardSequence = null;
        }

        foreach (GameObject drop in flyingDrops)
        {
            if (drop != null)
                Destroy(drop);
        }

        flyingDrops.Clear();
        isAnimating = false;
    }
}
