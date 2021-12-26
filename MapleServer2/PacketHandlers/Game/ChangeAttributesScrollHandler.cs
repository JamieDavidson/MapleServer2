﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ChangeAttributesScrollHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CHANGE_ATTRIBUTES_SCROLL;

    private static class ChangeAttributeMode
    {
        public const byte ChangeAttributes = 1;
        public const byte SelectNewAttributes = 3;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();
        switch (mode)
        {
            case ChangeAttributeMode.ChangeAttributes:
                HandleChangeAttributes(session, packet);
                break;
            case ChangeAttributeMode.SelectNewAttributes:
                HandleSelectNewAttributes(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleChangeAttributes(GameSession session, IPacketReader packet)
    {
        short lockStatId = -1;
        bool isSpecialStat = false;
        long scrollUid = packet.ReadLong();
        long gearUid = packet.ReadLong();
        packet.Skip(9);
        bool useLock = packet.ReadBool();
        if (useLock)
        {
            isSpecialStat = packet.ReadBool();
            lockStatId = packet.ReadShort();
        }

        var inventory = session.Player.Inventory;
        Item scroll = inventory.GetItemByUid(scrollUid);
        Item gear = inventory.GetItemByUid(gearUid);
        Item scrollLock = null;

        // Check if gear and scroll exists in inventory
        if (scroll == null || gear == null)
        {
            return;
        }

        string tag = "";
        if (Item.IsAccessory(gear.ItemSlot))
        {
            tag = "LockItemOptionAccessory";
        }
        else if (Item.IsArmor(gear.ItemSlot))
        {
            tag = "LockItemOptionArmor";
        }
        else if (Item.IsWeapon(gear.ItemSlot))
        {
            tag = "LockItemOptionWeapon";
        }
        else if (Item.IsPet(gear.Id))
        {
            tag = "LockItemOptionPet";
        }

        if (useLock)
        {
            scrollLock = inventory.GetItemByTagAndRarity(tag, gear.Rarity);
            // Check if scroll lock exists in inventory
            if (scrollLock == null)
            {
                return;
            }
        }

        Item newItem = new(gear);

        // Set new values for attributes
        newItem.Stats.BonusStats = ItemStats.RollNewBonusValues(newItem, lockStatId, isSpecialStat);

        inventory.TemporaryStorage[newItem.Uid] = newItem;

        session.Player.Inventory.ConsumeItem(session, scroll.Uid, 1);
        if (useLock)
        {
            session.Player.Inventory.ConsumeItem(session, scrollLock.Uid, 1);
        }

        session.Send(ChangeAttributesScrollPacket.PreviewNewItem(newItem));
    }

    private static void HandleSelectNewAttributes(GameSession session, IPacketReader packet)
    {
        long gearUid = packet.ReadLong();

        Inventory inventory = session.Player.Inventory;
        Item gear = inventory.TemporaryStorage.FirstOrDefault(x => x.Key == gearUid).Value;
        if (gear == null)
        {
            return;
        }

        inventory.TemporaryStorage.Remove(gear.Uid);
        inventory.Replace(gear);
        session.Send(ChangeAttributesScrollPacket.AddNewItem(gear));
    }
}
