using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Owned items")]
    [SerializeField] private List<Item> items = new List<Item>();
    [Tooltip("Reward items available to restore after they have been unlocked.")]
    [SerializeField] private List<Item> unlockableItems = new List<Item>();

    [Header("Inventory UI")]
    [SerializeField] private InventoryItemView itemPrefab;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional overlay on the inventory's Canvas. Defaults to its root Canvas.")]
    [SerializeField] private RectTransform dragRoot;

    [Header("Monster equipment")]
    [SerializeField] private Accessory monsterAccessories;
    [SerializeField] private List<EquipmentSlot> equipmentSlots = new List<EquipmentSlot>();

    private readonly List<InventoryItemView> itemViews = new List<InventoryItemView>();
    private bool initialized;

    public IReadOnlyList<Item> Items
    {
        get { Initialize(); return items; }
    }
    public RectTransform DragRoot => dragRoot;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;
        initialized = true;
        if (monsterAccessories == null)
            monsterAccessories = FindFirstObjectByType<Accessory>();

        if (dragRoot == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                dragRoot = canvas.rootCanvas.transform as RectTransform;
        }

        // Allow an inactive scene object to serve as the item template.
        if (itemPrefab != null && contentRoot != null && itemPrefab.transform.IsChildOf(contentRoot))
            itemPrefab.gameObject.SetActive(false);

        foreach (Item item in unlockableItems)
        {
            if (item != null && !string.IsNullOrEmpty(item.ItemId) &&
                PlayerPrefs.GetInt(GetUnlockKey(item), 0) == 1 && !ContainsItem(item))
                items.Add(item);
        }
    }

    public bool ContainsItem(Item item)
    {
        Initialize();
        if (item == null)
            return false;

        foreach (Item owned in items)
        {
            if (owned == item || (owned != null && !string.IsNullOrEmpty(item.ItemId) && owned.ItemId == item.ItemId))
                return true;
        }
        return false;
    }

    public void RegisterUnlockableItem(Item item)
    {
        Initialize();
        if (item == null || string.IsNullOrEmpty(item.ItemId))
            return;

        if (!unlockableItems.Contains(item))
            unlockableItems.Add(item);

        if (PlayerPrefs.GetInt(GetUnlockKey(item), 0) == 1 && !ContainsItem(item))
        {
            items.Add(item);
            RefreshAfterAddingItem();
        }
    }

    public bool AddItem(Item item)
    {
        Initialize();
        if (item == null || string.IsNullOrEmpty(item.ItemId) || ContainsItem(item))
            return false;

        if (!unlockableItems.Contains(item))
            unlockableItems.Add(item);

        items.Add(item);
        PlayerPrefs.SetInt(GetUnlockKey(item), 1);
        PlayerPrefs.Save();
        RefreshAfterAddingItem();
        return true;
    }

    private static string GetUnlockKey(Item item) => "Habimon.Inventory.Unlocked." + item.ItemId;

    private void RefreshAfterAddingItem()
    {
        if (isActiveAndEnabled && itemPrefab != null && contentRoot != null)
            Rebuild();
    }

    private void OnEnable()
    {
        Initialize();
        if (monsterAccessories != null)
            monsterAccessories.EquipmentChanged += HandleEquipmentChanged;

        foreach (EquipmentSlot slot in equipmentSlots)
        {
            if (slot != null)
                slot.Initialize(this);
        }

        Rebuild();
    }

    private void OnDisable()
    {
        if (monsterAccessories != null)
            monsterAccessories.EquipmentChanged -= HandleEquipmentChanged;

        foreach (InventoryItemView view in itemViews)
        {
            if (view != null)
                view.CancelDrag();
        }

        foreach (EquipmentSlot slot in equipmentSlots)
        {
            if (slot != null)
                slot.ResetHighlight();
        }
    }

    public void Rebuild()
    {
        Initialize();
        foreach (InventoryItemView view in itemViews)
        {
            if (view == null)
                continue;

            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }
        itemViews.Clear();

        if (itemPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("Assign Inventory Item Prefab and Content Root to generate inventory items.", this);
            RefreshEquipmentUI();
            return;
        }

        foreach (Item item in items)
        {
            if (item == null)
                continue;

            InventoryItemView view = Instantiate(itemPrefab, contentRoot);
            view.Initialize(this, item);
            view.gameObject.SetActive(true);
            itemViews.Add(view);
        }

        RefreshEquipmentUI();
    }

    public bool CanEquipItem(Item item, EquipmentSlot slot)
    {
        return isActiveAndEnabled && item != null && items.Contains(item) &&
               slot != null && slot.Inventory == this && equipmentSlots.Contains(slot) &&
               monsterAccessories != null && monsterAccessories.CanEquip(item, slot.ItemType);
    }

    public bool TryEquipItem(Item item, EquipmentSlot slot)
    {
        return CanEquipItem(item, slot) && monsterAccessories.TryEquip(item, slot.ItemType);
    }

    public bool Unequip(EquipmentSlot slot)
    {
        return isActiveAndEnabled && slot != null && slot.Inventory == this &&
               equipmentSlots.Contains(slot) && monsterAccessories != null &&
               monsterAccessories.Unequip(slot.ItemType);
    }

    private void HandleEquipmentChanged(ItemType itemType, Item item)
    {
        RefreshEquipmentUI();
    }

    private void RefreshEquipmentUI()
    {
        foreach (EquipmentSlot slot in equipmentSlots)
        {
            if (slot != null)
                slot.ShowItem(monsterAccessories != null ? monsterAccessories.GetEquippedItem(slot.ItemType) : null);
        }

        foreach (InventoryItemView view in itemViews)
        {
            if (view != null && view.Item != null)
                view.SetEquipped(monsterAccessories != null &&
                    monsterAccessories.GetEquippedItem(view.Item.ItemType) == view.Item);
        }
    }
}
