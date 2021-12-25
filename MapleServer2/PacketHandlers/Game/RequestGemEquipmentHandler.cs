﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestGemEquipmentHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_GEM_EQUIPMENT;

    private static class RequestGemEquipmentOperations
    {
        public const byte EquipItem = 0x00;
        public const byte UnequipItem = 0x01;
        public const byte Transprency = 0x03;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case RequestGemEquipmentOperations.EquipItem:
                HandleEquipItem(session, packet);
                break;
            case RequestGemEquipmentOperations.UnequipItem:
                HandleUnequipItem(session, packet);
                break;
            case RequestGemEquipmentOperations.Transprency:
                HandleTransparency(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleEquipItem(GameSession session, PacketReader packet)
    {
        long itemUid = packet.ReadLong();

        // Remove from inventory
        bool success = session.Player.Inventory.RemoveItem(session, itemUid, out Item item);

        if (!success)
        {
            return;
        }

        // Unequip existing item in slot
        Item[] badges = session.Player.Inventory.Badges;
        int index = Array.FindIndex(badges, x => x != null && x.GemSlot == item.GemSlot);
        if (index >= 0)
        {
            // Add to inventory
            badges[index].IsEquipped = false;
            session.Player.Inventory.AddItem(session, badges[index], false);

            // Unequip
            badges[index] = default;
            session.FieldManager.BroadcastPacket(GemPacket.UnequipItem(session, item.GemSlot));
        }

        // Equip
        item.IsEquipped = true;
        int emptyIndex = Array.FindIndex(badges, x => x == default);
        if (emptyIndex == -1)
        {
            return;
        }
        badges[emptyIndex] = item;
        session.FieldManager.BroadcastPacket(GemPacket.EquipItem(session, item, emptyIndex));
    }

    private static void HandleUnequipItem(GameSession session, PacketReader packet)
    {
        byte index = packet.ReadByte();

        Item[] badges = session.Player.Inventory.Badges;

        Item item = badges[index];
        if (item == null)
        {
            return;
        }
        // Add to inventory
        item.IsEquipped = false;
        session.Player.Inventory.AddItem(session, item, false);

        // Unequip
        badges[index] = default;
        session.FieldManager.BroadcastPacket(GemPacket.UnequipItem(session, item.GemSlot));
    }

    private static void HandleTransparency(GameSession session, PacketReader packet)
    {
        byte index = packet.ReadByte();
        byte[] transparencyBools = packet.ReadBytes(10);

        Item item = session.Player.Inventory.Badges[index];

        item.TransparencyBadgeBools = transparencyBools;

        session.FieldManager.BroadcastPacket(GemPacket.EquipItem(session, item, index));
    }
}
