using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemEquipHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_EQUIP;

    private static class ItemEquipOperations
    {
        public const byte Equip = 0;
        public const byte Unequip = 1;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ItemEquipOperations.Equip:
                HandleEquipItem(session, packet);
                break;
            case ItemEquipOperations.Unequip:
                HandleUnequipItem(session, packet);
                break;
        }
    }

    private static void HandleEquipItem(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var equipSlotStr = packet.ReadUnicodeString();
        if (!Enum.TryParse(equipSlotStr, out ItemSlot equipSlot))
        {
            Logger.Warn($"Unknown equip slot: {equipSlotStr}");
            return;
        }

        // Remove the item from the users inventory
        var inventory = session.Player.Inventory;
        inventory.RemoveItem(session, itemUid, out var item);
        if (item == null)
        {
            return;
        }

        // Get correct equipped inventory
        var equippedInventory = session.Player.GetEquippedInventory(item.InventoryTab);
        if (equippedInventory == null)
        {
            Logger.Warn($"equippedInventory was null: {item.InventoryTab}");
            return;
        }

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
                        session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, prevItem2));
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

    private static void HandleUnequipItem(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var inventory = session.Player.Inventory;

        // Unequip gear
        var kvpEquips = inventory.Equips.FirstOrDefault(x => x.Value.Uid == itemUid);
        if (kvpEquips.Value != null)
        {
            if (inventory.Equips.Remove(kvpEquips.Key, out var unequipItem))
            {
                unequipItem.Slot = -1;
                unequipItem.IsEquipped = false;
                inventory.AddItem(session, unequipItem, false);
                session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, unequipItem));

                DecreaseStats(session, unequipItem);
            }

            return;
        }

        // Unequip cosmetics
        var kvpCosmetics = inventory.Cosmetics.FirstOrDefault(x => x.Value.Uid == itemUid);
        if (kvpCosmetics.Value != null)
        {
            if (inventory.Cosmetics.Remove(kvpCosmetics.Key, out var unequipItem))
            {
                unequipItem.Slot = -1;
                unequipItem.IsEquipped = false;
                inventory.AddItem(session, unequipItem, false);
                session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, unequipItem));
            }
        }
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
