using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class ChatbotUiFactory
{
    private const float ReferenceResolutionWidth = 1080f;
    private const float DesignReferenceWidth = 390f;
    internal static readonly Color Background = new Color32(248, 249, 254, 255);
    internal static readonly Color Navy = new Color32(0, 65, 101, 255);
    internal static readonly Color Blue = new Color32(5, 89, 136, 255);
    internal static readonly Color PaleBlue = new Color32(226, 241, 250, 255);
    internal static readonly Color Muted = new Color32(75, 88, 99, 255);
    internal static readonly Color Amber = new Color32(254, 182, 76, 255);
    internal static readonly Color White = Color.white;
    private static Sprite roundedSprite;
    private static TMP_FontAsset chatbotFontAsset;

    private static TMP_FontAsset ChatbotFont
    {
        get
        {
            if (chatbotFontAsset == null)
            {
                chatbotFontAsset = Resources.Load<TMP_FontAsset>("HABIMONChatbotVietnameseFont");
                if (chatbotFontAsset == null)
                {
                    Debug.LogError("The static Vietnamese chatbot font asset is missing from Resources.");
                    chatbotFontAsset = TMP_Settings.defaultFontAsset;
                }
            }

            return chatbotFontAsset;
        }
    }

    internal static float U(float designPixels)
    {
        return designPixels * (ReferenceResolutionWidth / DesignReferenceWidth);
    }

    internal static Sprite CreateChatIcon(out Texture2D texture)
    {
        const int size = 64;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HABIMON Chat Tab Icon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[size * size];
        Color fill = PaleBlue;
        Color stroke = Blue;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // A rounded speech bubble and tail drawn as a vector-like raster icon. It scales without
            // introducing another vendor font or image-generation asset.
            float dx = Mathf.Max(Mathf.Abs(x - 32f) - 21f, 0f);
            float dy = Mathf.Max(Mathf.Abs(y - 35f) - 16f, 0f);
            bool roundedBody = dx * dx + dy * dy <= 12f * 12f;
            bool tail = PointInTriangle(x, y, new Vector2(23f, 20f), new Vector2(15f, 6f), new Vector2(37f, 19f));
            if (!roundedBody && !tail) continue;

            float edge = DistanceToRectEdge(x, y, 11f, 14f, 53f, 56f, 10f);
            if (tail && !roundedBody) edge = DistanceToSegment(x, y, 23f, 20f, 15f, 6f);
            pixels[y * size + x] = edge <= 3.3f ? stroke : fill;
        }
        DrawDot(pixels, size, 25, 35, 2.6f, Navy);
        DrawDot(pixels, size, 32, 35, 2.6f, Navy);
        DrawDot(pixels, size, 39, 35, 2.6f, Navy);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "HABIMON Chat Tab Icon";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static bool PointInTriangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = (x - c.x) * (a.y - c.y) - (a.x - c.x) * (y - c.y);
        float s2 = (x - a.x) * (b.y - a.y) - (b.x - a.x) * (y - a.y);
        float s3 = (x - b.x) * (c.y - b.y) - (c.x - b.x) * (y - b.y);
        return !((s1 < 0 || s2 < 0 || s3 < 0) && (s1 > 0 || s2 > 0 || s3 > 0));
    }

    private static float DistanceToRectEdge(float x, float y, float left, float bottom,
        float right, float top, float radius)
    {
        float cx = Mathf.Clamp(x, left + radius, right - radius);
        float cy = Mathf.Clamp(y, bottom + radius, top - radius);
        float outside = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        float signed = outside + Mathf.Min(Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(y - cy)) - radius, 0f);
        return Mathf.Abs(signed);
    }

    private static float DistanceToSegment(float x, float y, float x1, float y1, float x2, float y2)
    {
        Vector2 p = new Vector2(x, y), a = new Vector2(x1, y1), b = new Vector2(x2, y2);
        Vector2 delta = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, delta) / Vector2.Dot(delta, delta));
        return Vector2.Distance(p, a + delta * t);
    }

    private static void DrawDot(Color[] pixels, int size, int cx, int cy, float radius, Color color)
    {
        for (int y = Mathf.Max(0, (int)(cy - radius)); y <= Mathf.Min(size - 1, (int)(cy + radius)); y++)
        for (int x = Mathf.Max(0, (int)(cx - radius)); x <= Mathf.Min(size - 1, (int)(cx + radius)); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                pixels[y * size + x] = color;
    }

    internal static GameObject Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = obj.GetComponent<Image>();
        image.color = color ?? Color.clear;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = color.HasValue && (color.Value.a > 0f);
        return obj;
    }

    internal static TMP_Text Text(string name, Transform parent, string value, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.font = ChatbotFont;
        text.text = value ?? string.Empty;
        text.fontSize = U(fontSize);
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        obj.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return text;
    }

    internal static Button Button(string name, Transform parent, string label, Color background,
        Color foreground, float height = 88f)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = background;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        var button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = U(height);
        element.preferredHeight = U(height);
        element.flexibleWidth = 1f;
        var labelText = Text("Label", obj.transform, label, 17f, foreground, TextAlignmentOptions.Center);
        Stretch((RectTransform)labelText.transform, new Vector2(U(12f), U(4f)), new Vector2(-U(12f), -U(4f)));
        AddSoftShadow(obj);
        return button;
    }

    internal static TMP_InputField Input(Transform parent)
    {
        var obj = new GameObject("MessageInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = White;
        image.raycastTarget = true;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.layer = obj.layer;
        viewport.transform.SetParent(obj.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect, new Vector2(U(18f), U(6f)), new Vector2(-U(18f), -U(6f)));

        var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.layer = viewport.layer;
        textObj.transform.SetParent(viewport.transform, false);
        var text = textObj.GetComponent<TextMeshProUGUI>();
        text.font = ChatbotFont;
        text.fontSize = U(17f);
        text.color = Navy;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        Stretch((RectTransform)text.transform, Vector2.zero, Vector2.zero);

        var placeholderObj = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObj.layer = viewport.layer;
        placeholderObj.transform.SetParent(viewport.transform, false);
        var placeholder = placeholderObj.GetComponent<TextMeshProUGUI>();
        placeholder.font = ChatbotFont;
        placeholder.fontSize = U(16f);
        placeholder.color = new Color32(128, 142, 151, 255);
        placeholder.text = "Bạn muốn hỏi Habi điều gì?";
        placeholder.textWrappingMode = TextWrappingModes.Normal;
        placeholder.richText = false;
        placeholder.raycastTarget = false;
        Stretch((RectTransform)placeholder.transform, Vector2.zero, Vector2.zero);

        var input = obj.GetComponent<TMP_InputField>();
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 2000;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.richText = false;
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = U(38f);
        element.preferredHeight = U(38f);
        element.flexibleHeight = 0f;
        element.minWidth = U(180f);
        element.preferredWidth = U(240f);
        element.flexibleWidth = 1f;
        return input;
    }

    internal static ScrollRect ScrollView(Transform parent, string name, out RectTransform content)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform, Vector2.zero, Vector2.zero);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.layer = root.layer;
        viewport.transform.SetParent(root.transform, false);
        var viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect, Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = new Color32(248, 249, 254, 255);
        viewport.GetComponent<Image>().raycastTarget = true;

        var body = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        body.layer = root.layer;
        body.transform.SetParent(viewport.transform, false);
        content = (RectTransform)body.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = body.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)U(22f), (int)U(22f), (int)U(20f), (int)U(20f));
        layout.spacing = U(12f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = body.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = U(40f);
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return scroll;
    }

    internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    internal static GameObject Card(Transform parent, string name, Color color, float minHeight = 80f)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        obj.GetComponent<Image>().sprite = RoundedSprite;
        obj.GetComponent<Image>().type = Image.Type.Sliced;
        var element = obj.GetComponent<LayoutElement>();
        element.minHeight = U(minHeight);
        element.flexibleWidth = 1f;
        AddSoftShadow(obj);
        return obj;
    }

    private static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HABIMON Rounded UI Surface",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            const int samples = 4;
            const float radius = 15.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float px = x + (sx + 0.5f) / samples;
                    float py = y + (sy + 0.5f) / samples;
                    float dx = Mathf.Max(Mathf.Abs(px - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(py - size * 0.5f) - (size * 0.5f - radius), 0f);
                    if (dx * dx + dy * dy <= radius * radius) inside++;
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, inside / (float)(samples * samples));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect,
                new Vector4(20f, 20f, 20f, 20f));
            roundedSprite.name = "HABIMON Rounded UI Surface";
            roundedSprite.hideFlags = HideFlags.DontSave;
            return roundedSprite;
        }
    }

    private static void AddSoftShadow(GameObject target)
    {
        var shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.0f, 0.255f, 0.396f, 0.12f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;
    }
}
