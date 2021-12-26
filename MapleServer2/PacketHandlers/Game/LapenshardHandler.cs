using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class LapenshardHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_LAPENSHARD;

    private static class LapenshardOperations
    {
        public const byte Equip = 0x1;
        public const byte Unequip = 0x2;
        public const byte AddFusion = 0x3;
        public const byte AddCatalyst = 0x4;
        public const byte Fusion = 0x5;
    }

    private static class LapenshardColor
    {
        public const byte Red = 41;
        public const byte Blue = 42;
        public const byte Green = 43;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case LapenshardOperations.Equip:
                HandleEquip(session, packet);
                break;
            case LapenshardOperations.Unequip:
                HandleUnequip(session, packet);
                break;
            case LapenshardOperations.AddFusion:
                HandleAddFusion(session, packet);
                break;
            case LapenshardOperations.AddCatalyst:
                HandleAddCatalyst(session, packet);
                break;
            case LapenshardOperations.Fusion:
                HandleFusion(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleEquip(GameSession session, IPacketReader packet)
    {
        var slotId = packet.ReadInt();
        var itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByUid(itemUid);
        if (item is null)
        {
            return;
        }

        if (item.Type != ItemType.Lapenshard)
        {
            return;
        }

        if (inventory.LapenshardStorage[slotId] is not null)
        {
            return;
        }

        Item newLapenshard = new(item)
        {
            Amount = 1,
            IsEquipped = true,
            Slot = (short) slotId
        };

        newLapenshard.Uid = DatabaseManager.Items.Insert(newLapenshard);

        inventory.LapenshardStorage[slotId] = newLapenshard;
        inventory.ConsumeItem(session, item.Uid, 1);
        session.Send(LapenshardPacket.Equip(slotId, item.Id));
    }

    private static void HandleUnequip(GameSession session, IPacketReader packet)
    {
        var slotId = packet.ReadInt();

        var lapenshard = session.Player.Inventory.LapenshardStorage[slotId];
        if (lapenshard is null)
        {
            return;
        }

        session.Player.Inventory.LapenshardStorage[slotId] = null;
        lapenshard.Slot = -1;
        lapenshard.IsEquipped = false;
        session.Player.Inventory.AddItem(session, lapenshard, true);
        session.Send(LapenshardPacket.Unequip(slotId));
    }

    private static void HandleAddFusion(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var itemId = packet.ReadInt();
        packet.ReadInt();
        var inventory = session.Player.Inventory;

        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }
        // GMS2 Always 100% success rate
        session.Send(LapenshardPacket.Select(10000));
    }

    private static void HandleAddCatalyst(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var itemId = packet.ReadInt();
        packet.ReadInt();
        var amount = packet.ReadInt();
        var inventory = session.Player.Inventory;

        var item = inventory.GetItemByUid(itemUid);
        if (item is null || item.Amount < amount)
        {
            return;
        }

        // GMS2 Always 100% success rate
        session.Send(LapenshardPacket.Select(10000));
    }

    private static void HandleFusion(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var itemId = packet.ReadInt();
        packet.ReadInt();
        var catalystCount = packet.ReadInt();
        var inventory = session.Player.Inventory;

        List<(long Uid, int Amount)> items = new();
        for (var i = 0; i < catalystCount; i++)
        {
            var uid = packet.ReadLong();
            var amount = packet.ReadInt();
            (long, int) item = new(uid, amount);
            items.Add(item);
        }

        // Check if items are in inventory
        foreach ((var uid, var amount) in items)
        {
            var item = inventory.GetItemByUid(uid);
            if (item is null || item.Amount < amount)
            {
                return;
            }
        }

        var itemType = itemId / 1000000;
        var crystal = "";
        switch (itemType)
        {
            case LapenshardColor.Red:
                crystal = "RedCrystal";
                break;
            case LapenshardColor.Blue:
                crystal = "BlueCrystal";
                break;
            case LapenshardColor.Green:
                crystal = "GreenCrystal";
                break;
        }

        //Tier, Copies, Crystals, Mesos
        Dictionary<byte, (byte Copies, short CrystalsAmount, int Mesos)> costs = new()
        {
            { 1, new(4, 34, 600000) },
            { 2, new(5, 41, 800000) },
            { 3, new(6, 51, 1000000) },
            { 4, new(7, 63, 1200000) },
            { 5, new(8, 78, 1500000) },
            { 6, new(14, 102, 2000000) },
            { 7, new(20, 135, 2700000) },
            { 8, new(30, 190, 3800000) },
            { 9, new(50, 305, 6100000) },
        };

        // There are multiple ids for each type of material
        // Count all items with the same tag in inventory
        var crystals = inventory.GetItemsByTag(crystal).ToList();
        var crystalsTotalAmount = crystals.Sum(i => i.Value.Amount);
        var tier = (byte) (itemId % 10);

        if (costs[tier].CrystalsAmount > crystalsTotalAmount || !session.Player.Wallet.Meso.Modify(-costs[tier].Mesos))
        {
            return;
        }

        int crystalCost = costs[tier].CrystalsAmount;

        // Consume all Crystals
        foreach ((var key, var value) in crystals)
        {
            if (value.Amount >= crystalCost)
            {
                session.Player.Inventory.ConsumeItem(session, key, crystalCost);
                break;
            }
            crystalCost -= value.Amount;
            session.Player.Inventory.ConsumeItem(session, key, value.Amount);
        }

        // Consume all Lapenshards
        foreach ((var uid, var amount) in items)
        {
            session.Player.Inventory.ConsumeItem(session, uid, amount);
        }

        session.Player.Inventory.ConsumeItem(session, itemUid, 1);
        session.Player.Inventory.AddItem(session, new(itemId + 1) { Rarity = 3 }, true);
        session.Send(LapenshardPacket.Upgrade(itemId, true));
    }
}
