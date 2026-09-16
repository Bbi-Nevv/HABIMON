using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform contentTransform;

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

        if (texts.Length > 0)
            texts[0].text = habit.title;

        if (texts.Length > 1)       
            texts[1].text = habit.description;

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
