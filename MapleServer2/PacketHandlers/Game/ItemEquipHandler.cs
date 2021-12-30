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

        Inventory.EquipItem(session, itemUid, equipSlot);
    }

    private static void HandleUnequipItem(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var inventory = session.Player.Inventory;

        // Unequip gear
        inventory.UnequipItem(session, itemUid);
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
