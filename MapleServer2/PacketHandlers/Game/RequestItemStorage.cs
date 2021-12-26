﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestItemStorage : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_STORAGE;

    private static class ItemStorageMode
    {
        public const byte Add = 0x00;
        public const byte Remove = 0x01;
        public const byte Move = 0x02;
        public const byte Mesos = 0x03;
        public const byte Expand = 0x06;
        public const byte Sort = 0x08;
        public const byte LoadBank = 0x0C;
        public const byte Close = 0x0F;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ItemStorageMode.Add:
                HandleAdd(session, packet);
                break;
            case ItemStorageMode.Remove:
                HandleRemove(session, packet);
                break;
            case ItemStorageMode.Move:
                HandleMove(session, packet);
                break;
            case ItemStorageMode.Mesos:
                HandleMesos(session, packet);
                break;
            case ItemStorageMode.Expand:
                HandleExpand(session);
                break;
            case ItemStorageMode.Sort:
                HandleSort(session);
                break;
            case ItemStorageMode.LoadBank:
                HandleLoadBank(session);
                break;
            case ItemStorageMode.Close:
                HandleClose(session.Player.Account.BankInventory);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleAdd(GameSession session, IPacketReader packet)
    {
        packet.ReadLong();
        var uid = packet.ReadLong();
        var slot = packet.ReadShort();
        var amount = packet.ReadInt();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(uid))
        {
            return;
        }

        session.Player.Account.BankInventory.Add(session, uid, amount, slot);
    }

    private static void HandleRemove(GameSession session, IPacketReader packet)
    {
        packet.ReadLong();
        var uid = packet.ReadLong();
        var destinationSlot = packet.ReadShort();
        var amount = packet.ReadInt();

        if (!session.Player.Account.BankInventory.Remove(session, uid, amount, out var item))
        {
            return;
        }
        item.Slot = destinationSlot;

        session.Player.Inventory.AddItem(session, item, false);
    }

    private static void HandleMove(GameSession session, IPacketReader packet)
    {
        packet.ReadLong();
        var destinationUid = packet.ReadLong();
        var destinationSlot = packet.ReadShort();

        session.Player.Account.BankInventory.Move(session, destinationUid, destinationSlot);
    }

    private static void HandleMesos(GameSession session, IPacketReader packet)
    {
        packet.ReadLong();
        var mode = packet.ReadByte();
        var amount = packet.ReadLong();
        var wallet = session.Player.Wallet;
        var bankInventory = session.Player.Account.BankInventory;

        if (mode == 1) // add mesos
        {
            if (wallet.Meso.Modify(-amount))
            {
                bankInventory.Mesos.Modify(amount);
            }
            return;
        }

        if (mode == 0) // remove mesos
        {
            if (bankInventory.Mesos.Modify(-amount))
            {
                wallet.Meso.Modify(amount);
            }
        }
    }

    private static void HandleExpand(GameSession session)
    {
        session.Player.Account.BankInventory.Expand(session);
    }

    private static void HandleSort(GameSession session)
    {
        session.Player.Account.BankInventory.Sort(session);
    }

    private static void HandleLoadBank(GameSession session)
    {
        session.Player.Account.BankInventory.LoadBank(session);
    }

    private static void HandleClose(BankInventory bankInventory)
    {
        DatabaseManager.BankInventories.Update(bankInventory);
    }
}
