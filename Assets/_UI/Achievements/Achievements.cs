using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Achievements : MonoBehaviour
{
    public const string TotalWaterKey = "Habimon.Achievements.TotalWaterUsed";
    private const string CompletionPrefix = "Habimon.Achievements.Completed.";

    [Serializable]
    public class WaterMilestone
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField, Min(1)] private int requiredWater = 2;
        [SerializeField] private Item rewardItem;

        public string Id => !string.IsNullOrEmpty(id) ? id : requiredWater + "." + (rewardItem != null ? rewardItem.ItemId : "missing");
        public int RequiredWater => requiredWater;
        public Item RewardItem => rewardItem;

        internal void Validate(HashSet<string> ids)
        {
            requiredWater = Mathf.Max(1, requiredWater);
            if (string.IsNullOrEmpty(id) || !ids.Add(id))
            {
                id = Guid.NewGuid().ToString("N");
                ids.Add(id);
            }
        }
    }

    [Header("Water milestones (cumulative drops spent watering)")]
    [SerializeField] private List<WaterMilestone> milestones = new List<WaterMilestone>();
    [SerializeField] private Inventory inventory;
    [SerializeField] private AchievementProgressView progressView;

    [Header("Reward flight")]
    [SerializeField] private RectTransform inventoryTarget;
    [Tooltip("Optional. Defaults to the reward end of the progress card.")]
    [SerializeField] private RectTransform rewardOrigin;
    [Tooltip("An overlay on the target's Canvas. Defaults to its root Canvas.")]
    [SerializeField] private RectTransform animationRoot;
    [SerializeField, Min(0.1f)] private float flightDuration = 0.8f;
    [SerializeField, Min(20f)] private float rewardIconSize = 90f;

    private static event Action ProgressChanged;
    private readonly Queue<Item> rewardQueue = new Queue<Item>();
    private Sequence flightSequence;
    private GameObject flyingReward;
    private bool processing;

    public static int TotalWaterUsed => Mathf.Max(0, PlayerPrefs.GetInt(TotalWaterKey, 0));

    // Called only after Watering has actually paid for a new watering cycle.
    // Recording does not depend on the achievement UI being open or enabled.
    public static void RecordWatering(int amount)
    {
        if (amount <= 0)
            return;

        int total = (int)Math.Min(int.MaxValue, (long)TotalWaterUsed + amount);
        PlayerPrefs.SetInt(TotalWaterKey, total);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (progressView == null)
            progressView = GetComponentInChildren<AchievementProgressView>(true);

        RegisterRewards();
    }

    private void OnEnable()
    {
        ProgressChanged += RefreshAchievements;
        RefreshAchievements();
    }

    private void OnDisable()
    {
        ProgressChanged -= RefreshAchievements;
        // Rewards were already saved. Closing UI cancels cosmetics only.
        rewardQueue.Clear();
        if (flightSequence != null)
        {
            flightSequence.Kill();
            flightSequence = null;
        }
        if (flyingReward != null)
            Destroy(flyingReward);
        flyingReward = null;
    }

    private void RegisterRewards()
    {
        if (inventory == null)
            return;

        foreach (WaterMilestone milestone in milestones)
        {
            if (milestone != null)
                inventory.RegisterUnlockableItem(milestone.RewardItem);
        }
    }

    public bool IsCompleted(WaterMilestone milestone)
    {
        return milestone != null && PlayerPrefs.GetInt(CompletionPrefix + milestone.Id, 0) == 1;
    }

    public void RefreshAchievements()
    {
        if (processing)
            return;

        processing = true;
        try
        {
            RegisterRewards();
            foreach (WaterMilestone milestone in milestones)
            {
                if (milestone == null || milestone.RequiredWater <= 0 || milestone.RewardItem == null ||
                    string.IsNullOrEmpty(milestone.RewardItem.ItemId) || inventory == null)
                    continue;

                if (IsCompleted(milestone))
                {
                    inventory.AddItem(milestone.RewardItem);
                    continue;
                }
                if (TotalWaterUsed < milestone.RequiredWater)
                    continue;

                bool added = inventory.AddItem(milestone.RewardItem);
                if (!inventory.ContainsItem(milestone.RewardItem))
                    continue;

                // Save ownership before the completion receipt and before any tween.
                // Re-entering/restarting cannot grant a duplicate Item.
                PlayerPrefs.SetInt(CompletionPrefix + milestone.Id, 1);
                PlayerPrefs.Save();
                if (added)
                    rewardQueue.Enqueue(milestone.RewardItem);
            }

            RefreshProgressView();
        }
        finally
        {
            processing = false;
        }

        PlayNextReward();
    }

    private void RefreshProgressView()
    {
        if (progressView == null)
            return;

        WaterMilestone next = null;
        WaterMilestone last = null;
        foreach (WaterMilestone milestone in milestones)
        {
            if (milestone == null || milestone.RequiredWater <= 0 || milestone.RewardItem == null)
                continue;
            if (last == null || milestone.RequiredWater > last.RequiredWater)
                last = milestone;
            if (!IsCompleted(milestone) && (next == null || milestone.RequiredWater < next.RequiredWater))
                next = milestone;
        }

        WaterMilestone shown = next ?? last;
        progressView.SetProgress(TotalWaterUsed, shown != null ? shown.RequiredWater : 0,
            shown != null ? shown.RewardItem : null, shown != null && next == null);
    }

    private void PlayNextReward()
    {
        if (!isActiveAndEnabled || flightSequence != null)
            return;

        while (rewardQueue.Count > 0)
        {
            Item reward = rewardQueue.Dequeue();
            RectTransform origin = rewardOrigin != null ? rewardOrigin
                : progressView != null ? progressView.RewardOrigin : null;
            Canvas canvas = inventoryTarget != null ? inventoryTarget.GetComponentInParent<Canvas>() : null;
            RectTransform root = animationRoot != null ? animationRoot
                : canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (reward == null || reward.ItemIcon == null || origin == null || inventoryTarget == null ||
                root == null || !root.gameObject.activeInHierarchy || !origin.gameObject.activeInHierarchy)
                continue;

            Canvas.ForceUpdateCanvases();
            if (!TryGetPosition(origin, root, out Vector2 start) || !TryGetPosition(inventoryTarget, root, out Vector2 end))
                continue;

            flyingReward = new GameObject("AchievementRewardFlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flyingReward.layer = root.gameObject.layer;
            RectTransform rect = (RectTransform)flyingReward.transform;
            rect.SetParent(root, false);
            rect.SetAsLastSibling();
            rect.localPosition = start;
            rect.sizeDelta = Vector2.one * Mathf.Max(20f, rewardIconSize);
            Image image = flyingReward.GetComponent<Image>();
            image.sprite = reward.ItemIcon;
            image.preserveAspect = true;
            image.raycastTarget = false;

            float duration = Mathf.Max(0.1f, flightDuration);
            flightSequence = DOTween.Sequence().SetUpdate(true).Pause();
            if (progressView != null)
                progressView.PlayCompletion();
            flightSequence.Append(rect.DOScale(1.25f, 0.18f).SetEase(Ease.OutBack));
            flightSequence.Append(rect.DOLocalJump(end, 80f, 1, duration).SetEase(Ease.InOutQuad));
            flightSequence.Join(rect.DOScale(0.35f, duration).SetEase(Ease.InQuad));
            flightSequence.OnComplete(() =>
            {
                flightSequence = null;
                if (flyingReward != null)
                    Destroy(flyingReward);
                flyingReward = null;
                PlayNextReward();
            });
            flightSequence.Play();
            return;
        }
    }

    private static bool TryGetPosition(RectTransform source, RectTransform root, out Vector2 position)
    {
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Canvas targetCanvas = root.GetComponentInParent<Canvas>();
        if (sourceCanvas == null || targetCanvas == null)
        {
            position = default;
            return false;
        }

        sourceCanvas = sourceCanvas.rootCanvas;
        targetCanvas = targetCanvas.rootCanvas;
        Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, source.TransformPoint(source.rect.center));
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, targetCamera, out position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var ids = new HashSet<string>();
        foreach (WaterMilestone milestone in milestones)
        {
            if (milestone == null)
                continue;
            milestone.Validate(ids);
        }
    }
#endif
}
