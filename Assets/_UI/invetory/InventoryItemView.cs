using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private GameObject equippedMarker;

    private RectTransform dragVisual;
    private int dragPointerId;

    public Inventory Inventory { get; private set; }
    public Item Item { get; private set; }

    public void Initialize(Inventory inventory, Item item)
    {
        CancelDrag();
        Inventory = inventory;
        Item = item;
        if (icon == null)
            icon = GetComponent<Image>();

        if (icon != null)
        {
            icon.sprite = item != null ? item.ItemIcon : null;
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            icon.raycastTarget = true;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item != null ? item.ItemName : string.Empty;
            itemNameText.raycastTarget = false;
        }

        SetEquipped(false);
    }

    public void SetEquipped(bool equipped)
    {
        if (equippedMarker != null)
        {
            equippedMarker.SetActive(equipped);
            foreach (Graphic graphic in equippedMarker.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragVisual != null || eventData.button != PointerEventData.InputButton.Left ||
            Inventory == null || !Inventory.isActiveAndEnabled || Item == null ||
            Item.ItemIcon == null || Inventory.DragRoot == null)
            return;

        GameObject visual = new GameObject("DraggedInventoryItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visual.layer = Inventory.DragRoot.gameObject.layer;
        dragVisual = (RectTransform)visual.transform;
        dragVisual.SetParent(Inventory.DragRoot, false);
        dragVisual.SetAsLastSibling();
        dragVisual.sizeDelta = icon != null ? icon.rectTransform.rect.size : new Vector2(80f, 80f);
        Image image = visual.GetComponent<Image>();
        image.sprite = Item.ItemIcon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.85f);
        dragPointerId = eventData.pointerId;
        UpdateDragPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragVisual != null && eventData.pointerId == dragPointerId)
            UpdateDragPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == dragPointerId)
            CancelDrag();
    }

    private void OnDisable()
    {
        CancelDrag();
    }

    public void CancelDrag()
    {
        if (dragVisual == null)
            return;

        Destroy(dragVisual.gameObject);
        dragVisual = null;
    }

    private void UpdateDragPosition(PointerEventData eventData)
    {
        RectTransform root = Inventory.DragRoot;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        canvas = canvas.rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position, camera, out Vector2 position))
            dragVisual.localPosition = position;
    }
}
