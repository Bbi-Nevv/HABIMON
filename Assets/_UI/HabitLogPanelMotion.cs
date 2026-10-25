using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Portrait layout and short, unscaled transitions for the garden report.</summary>
public sealed class HabitLogPanelMotion : MonoBehaviour
{
    [SerializeField] private RectTransform layoutRoot;
    [SerializeField] private RectTransform[] animatedCards;
    [SerializeField] private RectTransform mascot;
    [SerializeField] private bool respectSafeArea = true;

    private Vector2[] positions;
    private CanvasGroup[] groups;
    private Sequence entrance;
    private Vector2 mascotPosition;
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;

    private void Awake()
    {
        positions = new Vector2[animatedCards.Length];
        groups = new CanvasGroup[animatedCards.Length];
        for (int i = 0; i < animatedCards.Length; i++)
        {
            positions[i] = animatedCards[i].anchoredPosition;
            groups[i] = animatedCards[i].GetComponent<CanvasGroup>();
            if (groups[i] == null)
                groups[i] = animatedCards[i].gameObject.AddComponent<CanvasGroup>();
        }
        if (mascot != null)
            mascotPosition = mascot.anchoredPosition;
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (button.GetComponent<HabitLogButtonFeedback>() == null)
                button.gameObject.AddComponent<HabitLogButtonFeedback>();
    }

    private void OnEnable()
    {
        FitPortrait();
        entrance?.Kill();
        entrance = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < animatedCards.Length; i++)
        {
            RectTransform card = animatedCards[i];
            card.anchoredPosition = positions[i] + new Vector2(0f, -24f);
            groups[i].alpha = 0f;
            entrance.Insert(i * 0.045f, card.DOAnchorPos(positions[i], 0.4f).SetEase(Ease.OutCubic));
            entrance.Insert(i * 0.045f, groups[i].DOFade(1f, 0.3f));
        }
        if (mascot != null)
            mascot.DOAnchorPosY(mascotPosition.y + 8f, 1.8f).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void Update()
    {
        if (lastScreenSize != new Vector2(Screen.width, Screen.height) || lastSafeArea != Screen.safeArea)
            FitPortrait();
    }

    private void FitPortrait()
    {
        if (layoutRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        float scale = Mathf.Max(0.001f, canvas.scaleFactor);
        Rect area = respectSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
        float fit = Mathf.Min(area.width / scale / 1080f, area.height / scale / 1920f);
        layoutRoot.localScale = Vector3.one * fit;
        layoutRoot.anchoredPosition = (area.center - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2(Screen.width, Screen.height);
    }

    private void OnDisable()
    {
        entrance?.Kill();
        if (positions != null)
            for (int i = 0; i < animatedCards.Length; i++)
            {
                if (animatedCards[i] == null)
                    continue;
                animatedCards[i].anchoredPosition = positions[i];
                groups[i].alpha = 1f;
            }
        if (mascot != null)
        {
            mascot.DOKill();
            mascot.anchoredPosition = mascotPosition;
        }
    }
}
