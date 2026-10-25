using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "HABIMON/Inventory/Item")]
public class Item : ScriptableObject
{
    [SerializeField, HideInInspector] private string itemId;
    [SerializeField] private string itemName;
    [SerializeField] private Sprite itemIcon;
    [SerializeField] private ItemType itemType;
    [Tooltip("Optional sprite worn by the monster. Defaults to Item Icon.")]
    [SerializeField] private Sprite equipmentSprite;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite ItemIcon => itemIcon;
    public ItemType ItemType => itemType;
    public Sprite EquipmentSprite => equipmentSprite != null ? equipmentSprite : itemIcon;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Asset GUID survives renames/reordering and is different for duplicated assets.
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(assetPath))
            itemId = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
    }
#endif

    public void use()
    {
        // Implement the logic for using the item here
        Debug.Log($"Using item: {itemName}");
    }
}
