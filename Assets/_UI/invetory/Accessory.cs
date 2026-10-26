using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Accessory : MonoBehaviour
{
    [Header("Accessory Setup")]
    [SerializeField] private List<Sprite> accessoryImages = new List<Sprite>();
    
    [SerializeField] private Image eyeglassesImage;
    [SerializeField] private Image neckImage;
    [SerializeField] private Image handImage;
    [SerializeField] private List<Item> startingEquipment = new List<Item>();

    private readonly Dictionary<ItemType, Item> equippedItems = new Dictionary<ItemType, Item>();
    private bool initialized;

    public event Action<ItemType, Item> EquipmentChanged;

    private void Awake()
    {
        InitializeEquipment();
    }

    public void InitializeEquipment()
    {
        if (initialized)
            return;

        // An inactive inventory preview can be restored before its Awake runs.
        initialized = true;
        // Legacy accessoryImages is kept as a sprite catalogue for existing scenes.
        // Only typed Item data may equip a sprite on the monster.
        ClearImage(eyeglassesImage);
        ClearImage(neckImage);
        ClearImage(handImage);

        foreach (Item item in startingEquipment)
        {
            if (item != null)
                TryEquip(item, item.ItemType);
        }
    }

    public Item GetEquippedItem(ItemType itemType)
    {
        InitializeEquipment();
        equippedItems.TryGetValue(itemType, out Item item);
        return item;
    }

    public bool CanEquip(Item item, ItemType slotType)
    {
        InitializeEquipment();
        return item != null && item.ItemType == slotType &&
               item.EquipmentSprite != null && GetTargetImage(slotType) != null;
    }

    public bool TryEquip(Item item, ItemType slotType)
    {
        // Validate here too, even if callers bypass the drag/drop UI.
        if (!CanEquip(item, slotType))
            return false;

        Image target = GetTargetImage(slotType);
        target.sprite = item.EquipmentSprite;
        target.preserveAspect = true;
        target.raycastTarget = false;
        target.enabled = true;
        equippedItems[slotType] = item;
        EquipmentChanged?.Invoke(slotType, item);
        return true;
    }

    public bool Unequip(ItemType slotType)
    {
        InitializeEquipment();
        if (!equippedItems.Remove(slotType))
            return false;

        ClearImage(GetTargetImage(slotType));
        EquipmentChanged?.Invoke(slotType, null);
        return true;
    }

    private Image GetTargetImage(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.eyeglasses: return eyeglassesImage;
            case ItemType.neck: return neckImage;
            case ItemType.hand: return handImage;
            default: return null;
        }
    }

    private static void ClearImage(Image image)
    {
        if (image == null)
            return;

        image.sprite = null;
        image.raycastTarget = false;
        image.enabled = false;
    }
}
