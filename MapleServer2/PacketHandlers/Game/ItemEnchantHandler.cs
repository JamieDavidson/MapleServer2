﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemEnchantHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_ENCHANT;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        byte operation = packet.ReadByte();

        switch (operation)
        {
            case 0: // Sent when opening up enchant ui
                break;
            case 1:
                HandleBeginEnchant(session, packet);
                break;
            case 4:
                HandleOpheliaEnchant(session, packet);
                break;
            case 6:
                HandlePeachyEnchant(session, packet);
                break;
        }
    }

    private static void HandleBeginEnchant(GameSession session, IPacketReader packet)
    {
        byte type = packet.ReadByte();
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByUid(itemUid);
        if (item != null)
        {
            session.Send(ItemEnchantPacket.BeginEnchant(type, item));
        }
    }

    private static void HandleOpheliaEnchant(GameSession session, IPacketReader packet)
    {
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByUid(itemUid);
        if (item != null)
        {
            item.Enchants += 5;
            item.Charges += 10;
            session.Send(ItemEnchantPacket.EnchantResult(item));
        }
    }

    private static void HandlePeachyEnchant(GameSession session, IPacketReader packet)
    {
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByUid(itemUid);
        if (item != null)
        {
            item.EnchantExp += 5000;
            if (item.EnchantExp >= 10000)
            {
                item.EnchantExp %= 10000;
                item.Enchants++;
            }
            session.Send(ItemEnchantPacket.EnchantResult(item));
            session.Send(ItemEnchantPacket.UpdateCharges(item));
        }
    }
}
