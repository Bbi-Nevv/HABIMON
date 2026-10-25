using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class EquipmentSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ItemType itemType;
    [SerializeField] private Image itemIcon;
    // [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private Image background;
    [SerializeField] private Color validDropColor = new Color(0.65f, 0.9f, 0.65f);
    [SerializeField] private Color invalidDropColor = new Color(1f, 0.65f, 0.65f);

    private Color normalColor;
    private bool referencesCached;

    public Inventory Inventory { get; private set; }
    public ItemType ItemType => itemType;
    public Item EquippedItem { get; private set; }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (referencesCached)
            return;

        referencesCached = true;
        if (background == null)
            background = GetComponent<Image>();

        if (background != null)
        {
            normalColor = background.color;
            background.raycastTarget = true;
        }

        if (itemIcon != null && itemIcon != background)
            itemIcon.raycastTarget = false;
        // if (itemNameText != null)
        //     itemNameText.raycastTarget = false;
    }

    public void Initialize(Inventory inventory)
    {
        CacheReferences();
        Inventory = inventory;
        ResetHighlight();
    }

    public void OnDrop(PointerEventData eventData)
    {
        ResetHighlight();
        InventoryItemView view = GetDraggedItem(eventData);
        if (view != null && Inventory != null && view.Inventory == Inventory)
            Inventory.TryEquipItem(view.Item, this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        InventoryItemView view = GetDraggedItem(eventData);
        if (view == null || background == null)
            return;

        bool valid = Inventory != null && view.Inventory == Inventory && Inventory.CanEquipItem(view.Item, this);
        background.color = valid ? validDropColor : invalidDropColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHighlight();
    }

    private void OnDisable()
    {
        ResetHighlight();
    }

    public void ResetHighlight()
    {
        if (background != null)
            background.color = normalColor;
    }

    public void ShowItem(Item item)
    {
        EquippedItem = item != null && item.ItemType == itemType ? item : null;
        if (itemIcon != null)
        {
            itemIcon.sprite = EquippedItem != null ? EquippedItem.ItemIcon : null;
            itemIcon.enabled = itemIcon.sprite != null;
            itemIcon.preserveAspect = true;
        }

        // if (itemNameText != null)
        //     itemNameText.text = EquippedItem != null ? EquippedItem.ItemName : string.Empty;
    }

    // Optional unequip button: connect its OnClick to EquipmentSlot.Unequip.
    public void Unequip()
    {
        if (Inventory != null)
            Inventory.Unequip(this);
    }

    private static InventoryItemView GetDraggedItem(PointerEventData eventData)
    {
        return eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventoryItemView>() : null;
    }
}
