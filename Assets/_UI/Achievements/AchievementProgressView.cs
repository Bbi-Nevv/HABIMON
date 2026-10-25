using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class AchievementProgressView : MonoBehaviour
{
    [SerializeField] private Sprite mascotSprite;
    [SerializeField] private Sprite waterSprite;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField, Range(6, 32)] private int dotCount = 19;

    private static readonly Color Blue = new Color(0.02f, 0.36f, 0.53f);
    private static readonly Color PaleBlue = new Color(0.82f, 0.89f, 0.93f);
    private Image[] dots;
    private RectTransform marker;
    private RectTransform rewardAnchor;
    private TMP_Text counter;
    private TMP_Text status;
    private TMP_Text endLabel;
    private Image rewardIcon;
    private GameObject lockVisual;
    private Tween progressTween;
    private Tween completionTween;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private Sprite roundedSprite;
    private float displayedProgress;
    private float targetProgress;
    private bool built;

    public RectTransform RewardOrigin { get { EnsureBuilt(); return rewardAnchor; } }

    private void Awake() => EnsureBuilt();

    private void OnEnable()
    {
        if (built)
            ApplyProgress(targetProgress);
    }

    private void EnsureBuilt()
    {
        if (built)
            return;
        built = true;
        CreateSprites();

        Image card = GetComponent<Image>();
        if (card == null)
            card = gameObject.AddComponent<Image>();
        card.sprite = roundedSprite;
        card.type = Image.Type.Sliced;
        card.color = Color.white;
        card.raycastTarget = false;

        Text("Title", transform, "Hành trình của cây", new Vector2(0f, 1f), new Vector2(35f, -28f), new Vector2(520f, 40f), 28f, new Vector2(0f, 1f));
        Image pill = Picture("WaterCounter", transform, roundedSprite, new Color(0.91f, 0.96f, 1f),
            new Vector2(1f, 1f), new Vector2(-35f, -25f), new Vector2(215f, 44f));
        pill.rectTransform.pivot = new Vector2(1f, 1f);
        pill.type = Image.Type.Sliced;
        if (waterSprite != null)
            Picture("Drop", pill.transform, waterSprite, Color.white, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(25f, 32f));
        counter = Text("CounterText", pill.transform, "0/20 giọt", new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(170f, 40f), 25f);

        RectTransform track = Rectangle("ProgressTrack", transform, new Vector2(0.12f, 0.43f), Vector2.zero, Vector2.zero);
        track.anchorMax = new Vector2(0.88f, 0.61f);
        track.offsetMin = Vector2.zero;
        track.offsetMax = Vector2.zero;
        int count = Mathf.Clamp(dotCount, 6, 32);
        dots = new Image[count];
        for (int i = 0; i < count; i++)
            dots[i] = Picture("Dot_" + i, track, circleSprite, PaleBlue,
                new Vector2((float)i / (count - 1), 0.5f), Vector2.zero, Vector2.one * 13f);

        Image start = Picture("Start", transform, circleSprite, Blue,
            new Vector2(0.07f, 0.52f), Vector2.zero, Vector2.one * 65f);
        if (waterSprite != null)
            Picture("Drop", start.transform, waterSprite, Color.white, Vector2.one * 0.5f, Vector2.zero, new Vector2(28f, 38f));
        Image end = Picture("Unlock", transform, circleSprite, Blue,
            new Vector2(0.93f, 0.52f), Vector2.zero, Vector2.one * 65f);
        rewardAnchor = end.rectTransform;
        rewardIcon = Picture("Reward", end.transform, null, Color.white, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 48f);
        lockVisual = Rectangle("Lock", end.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 32f).gameObject;
        Picture("Shackle", lockVisual.transform, circleSprite, Color.white, Vector2.one * 0.5f, new Vector2(0f, 5f), Vector2.one * 21f);
        Picture("Hole", lockVisual.transform, circleSprite, Blue, Vector2.one * 0.5f, new Vector2(0f, 5f), Vector2.one * 12f);
        Picture("Body", lockVisual.transform, roundedSprite, Color.white, Vector2.one * 0.5f, new Vector2(0f, -6f), new Vector2(24f, 19f));
        Picture("Keyhole", lockVisual.transform, circleSprite, Blue, Vector2.one * 0.5f, new Vector2(0f, -5f), Vector2.one * 5f);

        Image markerImage = Picture("MascotMarker", track, circleSprite, Color.white,
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.one * 82f);
        marker = markerImage.rectTransform;
        Picture("Mascot", marker, mascotSprite != null ? mascotSprite : waterSprite, Color.white,
            Vector2.one * 0.5f, Vector2.zero, Vector2.one * 72f);

        Text("StartLabel", transform, "Bắt đầu", new Vector2(0.09f, 0f), new Vector2(0f, 28f), new Vector2(150f, 40f), 24f);
        status = Text("Status", transform, "Cây đang lớn", new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(450f, 40f), 26f);
        endLabel = Text("EndLabel", transform, "Mở khóa", new Vector2(0.9f, 0f), new Vector2(0f, 28f), new Vector2(180f, 40f), 24f);
        ApplyProgress(0f);
    }

    public void SetProgress(int total, int target, Item reward, bool completed)
    {
        EnsureBuilt();
        counter.text = target > 0 ? $"{Mathf.Min(total, target)}/{target} giọt" : "0/0 giọt";
        status.text = target <= 0 ? "Chưa có thành tựu" : completed ? "Đã mở khóa" : "Cây đang lớn";
        endLabel.text = completed ? "Đã mở khóa" : "Mở khóa";
        lockVisual.SetActive(!completed);
        rewardIcon.sprite = reward != null ? reward.ItemIcon : null;
        rewardIcon.enabled = completed && rewardIcon.sprite != null;

        float progress = target > 0 ? Mathf.Clamp01((float)total / target) : 0f;
        targetProgress = progress;
        if (progressTween != null)
            progressTween.Kill();
        if (!isActiveAndEnabled)
        {
            ApplyProgress(progress);
            return;
        }
        progressTween = DOTween.To(() => displayedProgress, ApplyProgress, progress, 0.35f)
            .SetUpdate(true).SetEase(Ease.OutQuad).OnKill(() => progressTween = null);
    }

    private void ApplyProgress(float progress)
    {
        displayedProgress = progress;
        marker.anchorMin = marker.anchorMax = new Vector2(progress, 0.5f);
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = progress > 0f && (float)i / (dots.Length - 1) <= progress ? Blue : PaleBlue;
    }

    public void PlayCompletion()
    {
        if (!isActiveAndEnabled)
            return;
        if (completionTween != null)
            completionTween.Kill();
        transform.localScale = Vector3.one;
        completionTween = transform.DOPunchScale(Vector3.one * 0.045f, 0.45f, 4, 0.5f)
            .SetUpdate(true).OnKill(() => completionTween = null);
    }

    private void OnDisable()
    {
        if (progressTween != null)
            progressTween.Kill();
        if (completionTween != null)
            completionTween.Kill();
        if (built)
            ApplyProgress(targetProgress);
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (circleSprite != null) Destroy(circleSprite);
        if (roundedSprite != null) Destroy(roundedSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }

    private void CreateSprites()
    {
        const int size = 64;
        circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTexture.name = "AchievementUIRoundShape";
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f,
                    Mathf.Clamp01(32f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f))));
        circleTexture.SetPixels(pixels);
        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        roundedSprite = Sprite.Create(circleTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
            100f, 0, SpriteMeshType.FullRect, Vector4.one * 31f);
    }

    private static RectTransform Rectangle(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Picture(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Rectangle(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private TMP_Text Text(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Vector2? pivot = null)
    {
        RectTransform rect = Rectangle(name, parent, anchor, position, size);
        if (pivot.HasValue)
            rect.pivot = pivot.Value;
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = pivot.HasValue ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        label.color = Blue;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}
