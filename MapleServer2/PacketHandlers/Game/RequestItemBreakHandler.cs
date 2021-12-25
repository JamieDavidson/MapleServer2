using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

public class RequestItemBreakHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_BREAK;

    private static class ItemBreakMode
    {
        public const byte Open = 0x00;
        public const byte Add = 0x01;
        public const byte Remove = 0x02;
        public const byte Dismantle = 0x03;
        public const byte AutoAdd = 0x06;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();
        switch (mode)
        {
            case ItemBreakMode.Open:
                session.Player.DismantleInventory.Slots = new Tuple<long, int>[100];
                session.Player.DismantleInventory.Rewards = new();
                break;
            case ItemBreakMode.Add:
                HandleAdd(session, packet);
                break;
            case ItemBreakMode.Remove:
                HandleRemove(session, packet);
                break;
            case ItemBreakMode.Dismantle:
                HandleDismantle(session);
                break;
            case ItemBreakMode.AutoAdd:
                HandleAutoAdd(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleAdd(GameSession session, PacketReader packet)
    {
        short slot = (short) packet.ReadInt();
        long uid = packet.ReadLong();
        int amount = packet.ReadInt();

        session.Player.DismantleInventory.DismantleAdd(session, slot, uid, amount);
    }

    private static void HandleRemove(GameSession session, PacketReader packet)
    {
        long uid = packet.ReadLong();
        session.Player.DismantleInventory.Remove(session, uid);
    }

    private static void HandleDismantle(GameSession session)
    {
        session.Player.DismantleInventory.Dismantle(session);
    }

    private static void HandleAutoAdd(GameSession session, PacketReader packet)
    {
        InventoryTab inventoryTab = (InventoryTab) packet.ReadByte();
        byte rarityType = packet.ReadByte();

        session.Player.DismantleInventory.AutoAdd(session, inventoryTab, rarityType);
    }
}
