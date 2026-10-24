using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentTransform;
    [SerializeField] private List<Sprite> iconSprites;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Sprite bookIcon;

    private Dictionary<string, Sprite> iconLookup;

    private void Awake()
    {
        BuildIconLookup();
    }

    private void BuildIconLookup()
    {
        iconLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        if (iconSprites == null)
            return;

        foreach (var sprite in iconSprites)
        {
            if (sprite == null)
                continue;

            if (!iconLookup.ContainsKey(sprite.name))
                iconLookup[sprite.name] = sprite;
        }
    }

    private Sprite GetSpriteByIconKey(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey))
            return null;

        if (string.Equals(iconKey, "icon_book", StringComparison.OrdinalIgnoreCase))
            return bookIcon != null ? bookIcon : fallbackIcon;
        if (string.Equals(iconKey, "icon_default", StringComparison.OrdinalIgnoreCase))
            return fallbackIcon;

        if (iconLookup == null)
            BuildIconLookup();

        if (iconLookup != null && iconLookup.TryGetValue(iconKey, out var sprite))
            return sprite;

        return null;
    }

    public void ClearHabitItems()
    {
        if (contentTransform == null)
            return;

        for (int i = contentTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(contentTransform.GetChild(i).gameObject);
        }
    }

    public void SpawnHabitItem(
        HabitModel habit,
        string habitId,
        Action<string> onDelete = null,
        Func<string, bool, Task<bool>> onActiveChanged = null)
    {
        if (contentTransform == null || habit == null)
        {
            Debug.LogWarning("[MissionUI] Chưa gán contentTransform hoặc habit null.");
            return;
        }

        GameObject newItem = Instantiate(itemPrefab, contentTransform);
        TMP_Text[] texts = newItem.GetComponentsInChildren<TMP_Text>(true);

        Image iconImage = newItem.transform.Find("icon")?.GetComponent<Image>();
        if (iconImage == null)
            iconImage = newItem.GetComponentInChildren<Image>(true);

        if (texts.Length > 0)
            texts[0].text = habit.title;

        if (texts.Length > 1)
            texts[1].text = habit.description;
        TMP_Text descriptionText = texts.Length > 1 ? texts[1] : null;

        if (iconImage != null)
        {
            Sprite resolvedSprite = GetSpriteByIconKey(habit.icon);
            if (resolvedSprite != null)
            {
                iconImage.sprite = resolvedSprite;
            }
            else
            {
                iconImage.sprite = GetSpriteByIconKey("icon_default");
                if (iconImage.sprite == null && iconSprites != null && iconSprites.Count > 0)
                    iconImage.sprite = iconSprites[0];
                if (iconImage.sprite == null)
                    Debug.LogWarning($"[MissionUI] Không tìm thấy sprite cho iconKey='{habit.icon}' trong iconSprites.");
            }
        }
        else
        {
            Debug.LogWarning($"[MissionUI] Không tìm thấy Image cho habit: {habit.title}");
        }

        Toggle activeToggle = newItem.GetComponentInChildren<Toggle>(true);
        if (activeToggle != null)
        {
            activeToggle.SetIsOnWithoutNotify(habit.isActive);
            if (onActiveChanged == null)
                activeToggle.interactable = false;
            else
                activeToggle.onValueChanged.AddListener(isActive =>
                    _ = SaveActiveStateAsync(newItem, activeToggle, descriptionText, habit, habitId, isActive, onActiveChanged));
        }

        Button deleteButton = newItem.transform.Find("Button")?.GetComponent<Button>();
        if (deleteButton != null && !string.IsNullOrEmpty(habitId))
        {
            deleteButton.onClick.AddListener(() => onDelete?.Invoke(habitId));
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform as RectTransform);
    }

    private static async Task SaveActiveStateAsync(
        GameObject item,
        Toggle toggle,
        TMP_Text descriptionText,
        HabitModel habit,
        string habitId,
        bool isActive,
        Func<string, bool, Task<bool>> save)
    {
        bool previousValue = !isActive;
        toggle.interactable = false;
        bool saved = false;
        try
        {
            saved = await save(habitId, isActive);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MissionUI] Không thể lưu trạng thái thói quen: {ex.Message}");
        }

        if (item == null || toggle == null)
            return;

        toggle.interactable = true;
        if (!saved)
        {
            toggle.SetIsOnWithoutNotify(previousValue);
            if (descriptionText != null)
                descriptionText.text = $"{habit.description}\nKhông thể lưu trạng thái. Hãy thử lại.";
            return;
        }

        habit.isActive = isActive;
        if (descriptionText != null)
            descriptionText.text = habit.description;
    }

}
