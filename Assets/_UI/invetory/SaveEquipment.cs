using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SaveEquipment : MonoBehaviour
{
    private const string DefaultPrefix = "Habimon.Equipment";

    [SerializeField] private Inventory inventory;
    [Tooltip("The main monster and any inventory previews sharing its equipment.")]
    [SerializeField] private List<Accessory> accessoryDisplays = new List<Accessory>();
    [Tooltip("Items that can be restored in addition to Inventory.Items.")]
    [SerializeField] private List<Item> additionalItems = new List<Item>();
    [Tooltip("Use a different prefix for each independent monster/save profile.")]
    [SerializeField] private string playerPrefsPrefix = DefaultPrefix;

    private readonly Dictionary<string, Item> itemLookup = new Dictionary<string, Item>();
    private readonly List<Accessory> subscribedDisplays = new List<Accessory>();
    private bool started;
    private bool isApplying;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (accessoryDisplays.Count == 0)
        {
            Accessory localAccessory = GetComponent<Accessory>();
            if (localAccessory == null)
                localAccessory = FindFirstObjectByType<Accessory>(FindObjectsInactive.Include);
            if (localAccessory != null)
                accessoryDisplays.Add(localAccessory);
        }
    }

    private void Start()
    {
        // All active Accessory.Awake calls have completed before loading saved data.
        LoadEquipment();
        Subscribe();
        started = true;
    }

    private void OnEnable()
    {
        if (!started)
            return;

        LoadEquipment();
        Subscribe();
    }

    private void OnDisable()
    {
        foreach (Accessory display in subscribedDisplays)
        {
            if (display != null)
                display.EquipmentChanged -= HandleEquipmentChanged;
        }
        subscribedDisplays.Clear();
    }

    private void Subscribe()
    {
        foreach (Accessory display in accessoryDisplays)
        {
            if (display == null || subscribedDisplays.Contains(display))
                continue;

            display.EquipmentChanged += HandleEquipmentChanged;
            subscribedDisplays.Add(display);
        }
    }

    public void LoadEquipment()
    {
        if (isApplying)
            return;

        isApplying = true;
        try
        {
            BuildItemLookup();
            foreach (ItemType slotType in Enum.GetValues(typeof(ItemType)))
            {
                string key = GetKey(slotType);
                if (!PlayerPrefs.HasKey(key))
                    continue; // First launch: keep Starting Equipment.

                string itemId = PlayerPrefs.GetString(key, string.Empty);
                Item item = null;
                if (!string.IsNullOrEmpty(itemId) &&
                    (!itemLookup.TryGetValue(itemId, out item) || item.ItemType != slotType))
                {
                    Debug.LogWarning($"Cannot restore equipment for {slotType}: Item ID '{itemId}' is missing or has a different type.", this);
                    continue;
                }

                ApplyToDisplays(slotType, item);
            }
        }
        finally
        {
            isApplying = false;
        }
    }

    private void BuildItemLookup()
    {
        itemLookup.Clear();
        if (inventory != null)
        {
            foreach (Item item in inventory.Items)
                AddItem(item);
        }
        foreach (Item item in additionalItems)
            AddItem(item);

        foreach (Accessory display in accessoryDisplays)
        {
            if (display == null)
                continue;

            display.InitializeEquipment();
            foreach (ItemType slotType in Enum.GetValues(typeof(ItemType)))
                AddItem(display.GetEquippedItem(slotType));
        }
    }

    private void AddItem(Item item)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemId))
            return;

        if (itemLookup.TryGetValue(item.ItemId, out Item existing) && existing != item)
        {
            Debug.LogWarning($"Duplicate equipment Item ID: {item.ItemId}.", this);
            return;
        }
        itemLookup[item.ItemId] = item;
    }

    private void HandleEquipmentChanged(ItemType slotType, Item item)
    {
        if (isApplying)
            return;

        if (item != null && (item.ItemType != slotType || string.IsNullOrEmpty(item.ItemId)))
        {
            Debug.LogWarning("Equipment needs a matching ItemType and a persistent Item ID before it can be saved.", this);
            return;
        }

        // Empty string explicitly records an unequipped slot; deleting the key would
        // incorrectly restore Starting Equipment on the next launch.
        PlayerPrefs.SetString(GetKey(slotType), item != null ? item.ItemId : string.Empty);
        PlayerPrefs.Save();

        isApplying = true;
        try
        {
            ApplyToDisplays(slotType, item);
        }
        finally
        {
            isApplying = false;
        }
    }

    private void ApplyToDisplays(ItemType slotType, Item item)
    {
        foreach (Accessory display in accessoryDisplays)
        {
            if (display == null || display.GetEquippedItem(slotType) == item)
                continue;

            if (item == null)
                display.Unequip(slotType);
            else
                display.TryEquip(item, slotType);
        }
    }

    private string GetKey(ItemType slotType)
    {
        string prefix = string.IsNullOrWhiteSpace(playerPrefsPrefix) ? DefaultPrefix : playerPrefsPrefix;
        return $"{prefix}.{(int)slotType}";
    }
}
