﻿using System.Collections.Immutable;
using System.Diagnostics;
using Maple2Storage.Enums;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

// TODO: make this class thread safe?
namespace MapleServer2.Types;

public class Inventory
{
    public readonly long Id;

    // This contains ALL inventory Items regardless of tab
    private readonly Dictionary<long, Item> Items;
    private readonly Dictionary<ItemSlot, Item> Equips;
    public readonly Dictionary<ItemSlot, Item> Cosmetics;
    public readonly Item[] Badges;
    public readonly Item[] LapenshardStorage;

    // Map of Slot to Uid for each inventory
    private readonly Dictionary<short, long>[] SlotMaps;

    private readonly Dictionary<InventoryTab, short> DefaultSize = new()
    {
        { InventoryTab.Gear, 48 },
        { InventoryTab.Outfit, 150 },
        { InventoryTab.Mount, 48 },
        { InventoryTab.Catalyst, 48 },
        { InventoryTab.FishingMusic, 48 },
        { InventoryTab.Quest, 48 },
        { InventoryTab.Gemstone, 48 },
        { InventoryTab.Misc, 84 },
        { InventoryTab.LifeSkill, 126 },
        { InventoryTab.Pets, 60 },
        { InventoryTab.Consumable, 84 },
        { InventoryTab.Currency, 48 },
        { InventoryTab.Badge, 60 },
        { InventoryTab.Lapenshard, 48 },
        { InventoryTab.Fragment, 48 }
    };

    public readonly Dictionary<InventoryTab, short> ExtraSize = new()
    {
        { InventoryTab.Gear, 0 },
        { InventoryTab.Outfit, 0 },
        { InventoryTab.Mount, 0 },
        { InventoryTab.Catalyst, 0 },
        { InventoryTab.FishingMusic, 0 },
        { InventoryTab.Quest, 0 },
        { InventoryTab.Gemstone, 0 },
        { InventoryTab.Misc, 0 },
        { InventoryTab.LifeSkill, 0 },
        { InventoryTab.Pets, 0 },
        { InventoryTab.Consumable, 0 },
        { InventoryTab.Currency, 0 },
        { InventoryTab.Badge, 0 },
        { InventoryTab.Lapenshard, 0 },
        { InventoryTab.Fragment, 0 }
    };

    // Only use to share information between handler functions. Should always be empty
    public readonly Dictionary<long, Item> TemporaryStorage = new();

    public Inventory(bool addToDatabase)
    {
        Equips = new Dictionary<ItemSlot, Item>();
        Cosmetics = new Dictionary<ItemSlot, Item>();
        Badges = new Item[12];
        Items = new Dictionary<long, Item>();
        LapenshardStorage = new Item[6];

        var maxTabs = Enum.GetValues(typeof(InventoryTab)).Cast<byte>().Max();
        SlotMaps = new Dictionary<short, long>[maxTabs + 1];

        for (byte i = 0; i <= maxTabs; i++)
        {
            SlotMaps[i] = new Dictionary<short, long>();
        }

        if (addToDatabase)
        {
            Id = DatabaseManager.Inventories.Insert(this);
        }
    }

    public Inventory(long id, Dictionary<InventoryTab, short> extraSize, List<Item> items) : this(false)
    {
        Id = id;
        ExtraSize = extraSize;
        var badgeIndex = 0;

        foreach (var item in items)
        {
            item.SetMetadataValues();
            if (item.IsEquipped)
            {
                switch (item.InventoryTab)
                {
                    case InventoryTab.Outfit:
                        Cosmetics.Add(item.ItemSlot, item);
                        continue;
                    case InventoryTab.Badge:
                        Badges[badgeIndex++] = item;
                        continue;
                    case InventoryTab.Lapenshard:
                        LapenshardStorage[item.Slot] = item;
                        continue;
                    default:
                        Equips.Add(item.ItemSlot, item);
                        continue;
                }
            }

            Add(item);
        }
    }

    public void AddItem(GameSession session, Item item, bool isNew)
    {
        switch (item.Type)
        {
            case ItemType.Currency:
                AddMoney(session, item);
                return;
            case ItemType.Furnishing:
                AddToWarehouse(session, item);
                return;
        }

        // Checks if item is stackable or not
        if (item.StackLimit > 1)
        {
            // If item has slot defined, try to add to that slot
            if (item.Slot != -1 && !SlotTaken(item, item.Slot))
            {
                AddNewItem(session, item, isNew);
                return;
            }

            // If slot is occupied
            // Finds item in inventory with same id, rarity and a stack not full
            var existingItem = Items.Values.FirstOrDefault(x => x.Id == item.Id && x.Amount < x.StackLimit && x.Rarity == item.Rarity);
            if (existingItem is not null)
            {
                // Updates item amount
                if (existingItem.Amount + item.Amount <= existingItem.StackLimit)
                {
                    existingItem.Amount += item.Amount;

                    DatabaseManager.Items.Delete(item.Uid);

                    session.Send(ItemInventoryPacket.Update(existingItem.Uid, existingItem.Amount));
                    session.Send(ItemInventoryPacket.MarkItemNew(existingItem, item.Amount));
                    return;
                }

                // Updates inventory for item amount overflow
                var added = existingItem.StackLimit - existingItem.Amount;
                item.Amount -= added;
                existingItem.Amount = existingItem.StackLimit;

                session.Send(ItemInventoryPacket.Update(existingItem.Uid, existingItem.Amount));
                session.Send(ItemInventoryPacket.MarkItemNew(existingItem, added));
            }

            // Add item to first free slot
            AddNewItem(session, item, isNew);
            return;
        }

        // If item is not stackable and amount is 1, add to inventory to next free slot
        if (item.Amount == 1)
        {
            AddNewItem(session, item, isNew);
            return;
        }

        // If item is not stackable and amount is greater than 1, add multiple times
        for (var i = 0; i < item.Amount; i++)
        {
            Item newItem = new(item)
            {
                Amount = 1,
                Uid = 0
            };
            newItem.Uid = DatabaseManager.Items.Insert(newItem);

            AddNewItem(session, newItem, isNew);
        }
    }

    public bool CanHold(Item item, int amount = -1)
    {
        var remaining = amount > 0 ? amount : item.Amount;
        return CanHold(item.Id, remaining, item.InventoryTab);
    }

    public bool CanHold(int itemId, int amount)
    {
        return CanHold(itemId, amount, ItemMetadataStorage.GetTab(itemId));
    }

    public void ConsumeItem(GameSession session, long uid, int amount)
    {
        if (!Items.TryGetValue(uid, out var item) || amount > item.Amount)
        {
            return;
        }

        if (amount == item.Amount || item.Amount - amount <= 0)
        {
            RemoveItem(session, uid, out var _);
            return;
        }

        item.Amount -= amount;
        session.Send(ItemInventoryPacket.Update(uid, item.Amount));
    }

    public void DropItem(GameSession session, long uid, int amount, bool isBound)
    {
        // Drops item not bound
        if (!isBound)
        {
            var remaining = Remove(uid, out var droppedItem, amount); // Returns remaining amount of item
            switch (remaining)
            {
                case < 0:
                    return; // Removal failed
                case > 0: // Updates item amount
                    session.Send(ItemInventoryPacket.Update(uid, remaining));
                    DatabaseManager.Items.Update(Items[uid]);
                    break;
                default: // Removes item
                    session.Send(ItemInventoryPacket.Remove(uid));
                    break;
            }

            session.FieldManager.AddItem(session, droppedItem); // Drops item onto floor
            return;
        }

        // Drops bound item
        if (session.Player.Inventory.Remove(uid, out var removedItem) != 0)
        {
            return; // Removal from inventory failed
        }

        session.Send(ItemInventoryPacket.Remove(uid));
        DatabaseManager.Items.Delete(removedItem.Uid);
    }

    public static void EquipItem(GameSession session, long itemUid, ItemSlot equipSlot)
    {
        // Remove the item from the users inventory
        var inventory = session.Player.Inventory;
        inventory.RemoveItem(session, itemUid, out var item);
        if (item == null)
        {
            return;
        }

        // Get correct equipped inventory
        var equippedInventory = session.Player.Inventory.GetEquippedInventory(item.InventoryTab);

        // Move previously equipped item back to inventory
        if (equippedInventory.Remove(equipSlot, out var prevItem))
        {
            prevItem.Slot = item.Slot;
            prevItem.IsEquipped = false;
            inventory.AddItem(session, prevItem, false);
            session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, prevItem));

            if (prevItem.InventoryTab == InventoryTab.Gear)
            {
                DecreaseStats(session, prevItem);
            }
        }

        // Handle unequipping pants when equipping dresses
        // Handle unequipping off-hand when equipping two-handed weapons
        if (item.IsDress || item.IsTwoHand)
        {
            if (equippedInventory.Remove(item.IsDress ? ItemSlot.PA : ItemSlot.LH, out var prevItem2))
            {
                prevItem2.Slot = -1;
                if (prevItem == null)
                {
                    prevItem2.Slot = item.Slot;
                }

                prevItem2.IsEquipped = false;
                inventory.AddItem(session, prevItem2, false);
                session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, prevItem2));
            }
        }

        // Handle unequipping dresses when equipping pants
        // Handle unequipping two-handed main-hands when equipping off-hand weapons
        if (item.ItemSlot == ItemSlot.PA || item.ItemSlot == ItemSlot.LH)
        {
            var prevItemSlot = item.ItemSlot == ItemSlot.PA ? ItemSlot.CL : ItemSlot.RH;
            if (equippedInventory.ContainsKey(prevItemSlot))
            {
                if (equippedInventory[prevItemSlot] != null && equippedInventory[prevItemSlot].IsDress)
                {
                    if (equippedInventory.Remove(prevItemSlot, out var prevItem2))
                    {
                        prevItem2.Slot = item.Slot;
                        prevItem2.IsEquipped = false;
                        inventory.AddItem(session, prevItem2, false);
                        session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer,
                            prevItem2));
                    }
                }
            }
        }

        // Equip new item
        item.IsEquipped = true;
        item.ItemSlot = equipSlot;
        equippedInventory[equipSlot] = item;
        session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, item, equipSlot));

        // Add stats if gear
        if (item.InventoryTab == InventoryTab.Gear)
        {
            IncreaseStats(session, item);
        }
    }

    public void ExpandInventory(GameSession session, InventoryTab tab)
    {
        var meretPrice = long.Parse(ConstantsMetadataStorage.GetConstant("InventoryExpandPrice1Row"));
        const short ExpansionAmount = 6;

        if (!session.Player.Account.RemoveMerets(meretPrice))
        {
            return;
        }

        ExtraSize[tab] += ExpansionAmount;
        session.Send(ItemInventoryPacket.LoadTab(tab, ExtraSize[tab]));
        session.Send(ItemInventoryPacket.Expand());
    }

    private Dictionary<ItemSlot, Item> GetEquippedInventory(InventoryTab tab)
    {
        return tab switch
        {
            InventoryTab.Gear => Equips,
            InventoryTab.Outfit => Cosmetics,
            _ => null
        };
    }

    public IReadOnlyDictionary<ItemSlot, Item> GetEquipment()
    {
        return Equips.ToImmutableDictionary(k => k.Key, k => k.Value);
    }

    public Item GetEquippedItem(long itemUid)
    {
        var gearItem = Equips.FirstOrDefault(x => x.Value.Uid == itemUid).Value;

        return gearItem ?? Cosmetics.FirstOrDefault(x => x.Value.Uid == itemUid).Value;
    }

    public int GetFreeSlots(InventoryTab tab)
    {
        return DefaultSize[tab] + ExtraSize[tab] - GetSlots(tab).Count;
    }

    public int GetItemAmount(int itemId)
    {
        return Items.Values.Where(x => x.Id == itemId).Sum(x => x.Amount);
    }

    public bool HasEquipmentWithEnchantmentCount(int enchantmentCount)
    {
        return Equips.Values.Any(e => e.Enchants >= enchantmentCount);
    }

    public Item GetItemByFunctionId(int functionId)
    {
        return Items.Values
            .FirstOrDefault(i => i.Function.Id == functionId);
    }

    public Item GetItemByItemId(int itemId)
    {
        return Items.Values.FirstOrDefault(i => i.Id == itemId);
    }

    public Item GetItemByItemIdAndRarity(int itemId, int rarity)
    {
        return Items.Values.FirstOrDefault(i => i.Id == itemId && i.Rarity == rarity);
    }

    public Item GetItemByTag(string itemTag)
    {
        return Items.Values.FirstOrDefault(i => i.Tag == itemTag);
    }

    public Item GetItemByTagAndRarity(string tag, int rarity)
    {
        return Items.Values.FirstOrDefault(i => i.Tag == tag && i.Rarity == rarity);
    }

    public Item GetItemByUid(long uid)
    {
        return Items.ContainsKey(uid) ? Items[uid] : null;
    }

    public int GetItemCount(int itemId)
    {
        return Items.Values.Count(i => i.Id == itemId);
    }

    public IEnumerable<Item> GetItemsByItemId(int itemId)
    {
        return Items.Values.Where(i => i.Id == itemId);
    }

    public IEnumerable<KeyValuePair<long, Item>> GetItemsByTag(string itemTag)
    {
        return Items.Where(i => i.Value.Tag == itemTag);
    }

    public IEnumerable<Item> GetItemsNotNull()
    {
        return Items.Values.Where(i => i != null);
    }

    public IEnumerable<Item> GetItemsToDismantle(InventoryTab inventoryTab, int rarity)
    {
        return Items.Values
            .Where(i => i.EnableBreak && i.Rarity <= rarity && i.InventoryTab == inventoryTab)
            .ToList();
    }

    public bool HasItemWithRarity(int itemId, int rarity)
    {
        return Items.Values.Any(i => i.Id == itemId && i.Rarity == rarity);
    }

    public bool HasItemWithUid(long uid)
    {
        return Items.ContainsKey(uid);
    }

    public void LoadInventoryTab(GameSession session, InventoryTab tab)
    {
        session.Send(ItemInventoryPacket.ResetTab(tab));
        session.Send(ItemInventoryPacket.LoadTab(tab, ExtraSize[tab]));
        session.Send(ItemInventoryPacket.LoadItem(GetItems(tab)));
    }

    public IEnumerable<Item> LockItems(IEnumerable<long> itemIdsToUpdate, byte operation)
    {
        var items = new List<Item>();

        foreach (var uid in itemIdsToUpdate)
        {
            if (!Items.ContainsKey(uid))
            {
                continue;
            }

            Items[uid].IsLocked = operation == 0;
            Items[uid].UnlockTime = operation == 1 ? TimeInfo.AddDays(3) : 0;
            items.Add(Items[uid]);
        }

        return items;
    }

    public void MoveItem(GameSession session, long uid, short dstSlot)
    {
        if (!RemoveInternal(uid, out var srcItem))
        {
            return;
        }

        var srcSlot = srcItem.Slot;

        if (SlotMaps[(int) srcItem.InventoryTab].TryGetValue(dstSlot, out var dstUid))
        {
            var item = Items[dstUid];
            // If item is stackable and same id and rarity, try to increase the item amount instead of swapping slots
            if (item.Id == srcItem.Id && item.Amount < item.StackLimit && item.Rarity == srcItem.Rarity && item.StackLimit > 1)
            {
                // Updates item amount
                if (item.Amount + srcItem.Amount <= item.StackLimit)
                {
                    item.Amount += srcItem.Amount;

                    DatabaseManager.Items.Delete(srcItem.Uid);

                    session.Send(ItemInventoryPacket.Update(item.Uid, item.Amount));
                    session.Send(ItemInventoryPacket.Remove(srcItem.Uid));
                    return;
                }

                // Updates inventory for item amount overflow
                var added = item.StackLimit - item.Amount;
                srcItem.Amount -= added;
                item.Amount = item.StackLimit;

                session.Send(ItemInventoryPacket.Update(srcItem.Uid, srcItem.Amount));
                session.Send(ItemInventoryPacket.Update(item.Uid, item.Amount));
                return;
            }
        }

        // Move dstItem to srcSlot if removed
        if (RemoveInternal(dstUid, out var dstItem))
        {
            dstItem.Slot = srcSlot;
            AddInternal(dstItem);
        }

        // Move srcItem to dstSlot
        srcItem.Slot = dstSlot;
        AddInternal(srcItem);

        session.Send(ItemInventoryPacket.Move(dstUid, srcSlot, uid, dstSlot));
    }

    public bool RemoveItem(GameSession session, long uid, out Item item)
    {
        if (Remove(uid, out item) == -1)
        {
            return false;
        }

        session.Send(ItemInventoryPacket.Remove(uid));
        return true;
    }

    // Replaces an existing item with an updated copy of itself


    public bool Replace(Item item)
    {
        if (!Items.ContainsKey(item.Uid))
        {
            return false;
        }

        RemoveInternal(item.Uid, out var replacedItem);
        item.Slot = replacedItem.Slot;
        AddInternal(item);

        return true;
    }

    public void SortInventory(GameSession session, InventoryTab tab)
    {
        // Get all items in tab
        var slots = GetSlots(tab);
        var tabItems = slots.Select(kvp => Items[kvp.Value]).ToList();

        // group items by item id and sum the amount, return a new list of items with updated amount (ty gh copilot)
        var groupedItems = tabItems.Where(x => x.StackLimit > 1).GroupBy(x => x.Id).Select(x => new Item(x.First())
        {
            Amount = x.Sum(y => y.Amount)
        }).ToList();

        // Add items that can't be grouped
        groupedItems.AddRange(tabItems.Where(x => x.StackLimit == 1));

        // Sort by item id
        groupedItems.Sort((x, y) => x.Id.CompareTo(y.Id));

        // Update the slot mapping
        slots.Clear();
        short slotId = 0;

        // Items that overflow stack limit
        List<Item> itemList = new();

        // Delete items that got grouped
        foreach (var oldItem in tabItems)
        {
            var newItem = groupedItems.FirstOrDefault(x => x.Uid == oldItem.Uid);
            if (newItem is null)
            {
                Items.Remove(oldItem.Uid);
                DatabaseManager.Items.Delete(oldItem.Uid);
                continue;
            }

            if (newItem.Amount > newItem.StackLimit)
            {
                // how many times can we split the item?
                var splitAmount = newItem.Amount / newItem.StackLimit;
                // how many items are left over?
                var remainder = newItem.Amount % newItem.StackLimit;

                // split the item
                for (var i = 0; i < splitAmount; i++)
                {
                    if (!newItem.TrySplit(newItem.StackLimit, out var splitItem))
                    {
                        continue;
                    }

                    itemList.Add(splitItem);
                }

                // Delete the original item if remainder is 0
                if (remainder <= 0)
                {
                    Items.Remove(oldItem.Uid);
                    DatabaseManager.Items.Delete(oldItem.Uid);
                    continue;
                }

                // set the remainder
                newItem.Amount = remainder;
            }

            // Update the slot mapping
            slots.Add(slotId++, newItem.Uid);
            newItem.Slot = slotId;

            // Update the item
            Items[newItem.Uid] = newItem;
            DatabaseManager.Items.Update(newItem);
        }

        foreach (var item in itemList)
        {
            AddNewItem(session, item, false);
        }

        session.Send(ItemInventoryPacket.ResetTab(tab));
        session.Send(ItemInventoryPacket.LoadItemsToTab(tab, GetItems(tab)));
    }

    public void UnequipItem(GameSession session, long itemUid)
    {
        // Unequip gear
        var (itemSlot, item) = Equips.FirstOrDefault(x => x.Value.Uid == itemUid);
        if (item != null)
        {
            if (!Equips.Remove(itemSlot, out var unequipItem))
            {
                return;
            }

            unequipItem.Slot = -1;
            unequipItem.IsEquipped = false;
            AddItem(session, unequipItem, false);

            session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, unequipItem));

            DecreaseStats(session, unequipItem);

            return;
        }

        // Unequip cosmetics
        var (key, value) = Cosmetics.FirstOrDefault(x => x.Value.Uid == itemUid);
        if (value != null)
        {
            if (!Cosmetics.Remove(key, out var unequipItem))
            {
                return;
            }

            unequipItem.Slot = -1;
            unequipItem.IsEquipped = false;
            AddItem(session, unequipItem, false);
            session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, unequipItem));
        }
    }

    private bool Add(Item item)
    {
        // Item has a slot set, try to use that slot
        if (item.Slot >= 0)
        {
            if (!SlotTaken(item, item.Slot))
            {
                AddInternal(item);
                return true;
            }

            item.Slot = -1; // Reset slot
        }

        var tabSize = (short) (DefaultSize[item.InventoryTab] + ExtraSize[item.InventoryTab]);
        for (short i = 0; i < tabSize; i++)
        {
            if (SlotTaken(item, i))
            {
                continue;
            }

            item.Slot = i;

            AddInternal(item);
            return true;
        }

        return false;
    }

    private void AddInternal(Item item)
    {
        Debug.Assert(!Items.ContainsKey(item.Uid), "Error adding an item that already exists");
        Items[item.Uid] = item;

        Debug.Assert(!GetSlots(item.InventoryTab).ContainsKey(item.Slot), "Error adding item to slot that is already taken.");
        GetSlots(item.InventoryTab)[item.Slot] = item.Uid;
    }

    private static void AddMoney(GameSession session, Item item)
    {
        switch (item.Id)
        {
            case 90000001: // Meso
                session.Player.Wallet.Meso.Modify(item.Amount);
                return;
            case 90000006: // Valor Token
                session.Player.Wallet.ValorToken.Modify(item.Amount);
                return;
            case 90000004: // Meret
            case 90000011: // Meret
            case 90000015: // Meret
            case 90000016: // Meret
                session.Player.Account.Meret.Modify(item.Amount);
                return;
            case 90000013: // Rue
                session.Player.Wallet.Rue.Modify(item.Amount);
                return;
            case 90000014: // Havi
                session.Player.Wallet.HaviFruit.Modify(item.Amount);
                return;
            case 90000017: // Treva
                session.Player.Wallet.Treva.Modify(item.Amount);
                return;
            case 90000021: // Guild Funds
                if (session.Player.Guild == null)
                {
                    return;
                }

                session.Player.Guild.Funds += item.Amount;
                session.Player.Guild.BroadcastPacketGuild(GuildPacket.UpdateGuildFunds(session.Player.Guild.Funds));
                DatabaseManager.Guilds.Update(session.Player.Guild);
                return;
        }
    }

    private void AddNewItem(GameSession session, Item item, bool isNew)
    {
        if (!Add(item)) // Adds item into internal database
        {
            return;
        }

        session.Send(ItemInventoryPacket.Add(item)); // Sends packet to add item clientside
        if (isNew)
        {
            session.Send(ItemInventoryPacket.MarkItemNew(item, item.Amount)); // Marks Item as New
        }
    }

    private static void AddToWarehouse(GameSession session, Item item)
    {
        if (session.Player.Account.Home == null)
        {
            return;
        }

        var home = GameServer.HomeManager.GetHomeById(session.Player.Account.Home.Id);
        if (home == null)
        {
            return;
        }

        _ = home.AddWarehouseItem(session, item.Id, item.Amount, item);
        session.Send(WarehouseInventoryPacket.GainItemMessage(item, item.Amount));
    }

    private bool CanHold(int itemId, int amount, InventoryTab tab)
    {
        if (GetFreeSlots(tab) > 0)
        {
            return true;
        }

        foreach (var i in Items.Values.Where(x => x.InventoryTab == tab && x.Id == itemId && x.StackLimit > 0))
        {
            var available = i.StackLimit - i.Amount;
            amount -= available;
            if (amount <= 0)
            {
                return true;
            }
        }

        return false;
    }

    private ICollection<Item> GetItems(InventoryTab tab)
    {
        return GetSlots(tab).Select(kvp => Items[kvp.Value]).ToImmutableList();
    }

    private Dictionary<short, long> GetSlots(InventoryTab tab)
    {
        return SlotMaps[(int) tab];
    }

    private int Remove(long uid, out Item removedItem, int amount = -1)
    {
        // Removing more than available
        if (!Items.TryGetValue(uid, out var item) || item.Amount < amount)
        {
            removedItem = null;
            return -1;
        }

        if (amount >= 0 && item.Amount != amount)
        {
            return item.TrySplit(amount, out removedItem) ? item.Amount : -1;
        }

        // Remove All
        if (!RemoveInternal(uid, out removedItem))
        {
            return -1;
        }

        return 0;
    }

    private bool RemoveInternal(long uid, out Item item)
    {
        return Items.Remove(uid, out item) && GetSlots(item.InventoryTab).Remove(item.Slot);
    }

    private bool SlotTaken(Item item, short slot = -1)
    {
        return GetSlots(item.InventoryTab).ContainsKey(slot < 0 ? item.Slot : slot);
    }

    private static void DecreaseStats(GameSession session, Item item)
    {
        if (item.Stats.BasicStats.Count != 0)
        {
            foreach (var stat in item.Stats.BasicStats.OfType<NormalStat>())
            {
                session.Player.Stats[stat.ItemAttribute].DecreaseBonus(stat.Flat);
            }
        }

        if (item.Stats.BonusStats.Count != 0)
        {
            foreach (var stat in item.Stats.BonusStats.OfType<NormalStat>())
            {
                session.Player.Stats[stat.ItemAttribute].DecreaseBonus(stat.Flat);
            }
        }

        session.Send(StatPacket.SetStats(session.Player.FieldPlayer));
    }

    private static void IncreaseStats(GameSession session, Item item)
    {
        if (item.Stats.BasicStats.Count != 0)
        {
            foreach (var stat in item.Stats.BasicStats.OfType<NormalStat>())
            {
                session.Player.Stats[stat.ItemAttribute].IncreaseBonus(stat.Flat);
            }
        }

        if (item.Stats.BonusStats.Count != 0)
        {
            foreach (var stat in item.Stats.BonusStats.OfType<NormalStat>())
            {
                session.Player.Stats[stat.ItemAttribute].IncreaseBonus(stat.Flat);
            }
        }

        session.Send(StatPacket.SetStats(session.Player.FieldPlayer));
    }
}
