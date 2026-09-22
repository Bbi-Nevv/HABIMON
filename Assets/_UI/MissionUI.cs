using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentTransform;
    [SerializeField] private List<Sprite> iconSprites;

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

    public void SpawnHabitItem(HabitModel habit, string habitId, Action<string> onDelete = null, Action<string, bool> onActiveChanged = null)
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
            activeToggle.onValueChanged.RemoveAllListeners();
            activeToggle.onValueChanged.AddListener(isActive =>
            {
                habit.isActive = isActive;
                onActiveChanged?.Invoke(habitId, isActive);
            });
        }

        Button deleteButton = newItem.transform.Find("Button")?.GetComponent<Button>();
        if (deleteButton != null && !string.IsNullOrEmpty(habitId))
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke(habitId));
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform as RectTransform);
    }

}
