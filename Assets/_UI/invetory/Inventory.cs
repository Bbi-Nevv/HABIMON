using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Owned items")]
    [SerializeField] private List<Item> items = new List<Item>();

    [Header("Inventory UI")]
    [SerializeField] private InventoryItemView itemPrefab;
    [SerializeField] private Transform contentRoot;
    [Tooltip("Optional overlay on the inventory's Canvas. Defaults to its root Canvas.")]
    [SerializeField] private RectTransform dragRoot;

    [Header("Monster equipment")]
    [SerializeField] private Accessory monsterAccessories;
    [SerializeField] private List<EquipmentSlot> equipmentSlots = new List<EquipmentSlot>();

    private readonly List<InventoryItemView> itemViews = new List<InventoryItemView>();

    public IReadOnlyList<Item> Items => items;
    public RectTransform DragRoot => dragRoot;

    private void Awake()
    {
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
    }

    private void OnEnable()
    {
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
