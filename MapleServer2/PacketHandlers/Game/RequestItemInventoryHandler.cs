using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestItemInventoryHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_INVENTORY;

    private static class RequestItemInventoryOperations
    {
        public const byte Move = 0x3;
        public const byte Drop = 0x4;
        public const byte DropBound = 0x5;
        public const byte Sort = 0xA;
        public const byte Expand = 0xB;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case RequestItemInventoryOperations.Move:
                HandleMove(session, packet);
                break;
            case RequestItemInventoryOperations.Drop:
                HandleDrop(session, packet);
                break;
            case RequestItemInventoryOperations.DropBound:
                HandleDropBound(session, packet);
                break;
            case RequestItemInventoryOperations.Sort:
                HandleSort(session, packet);
                break;
            case RequestItemInventoryOperations.Expand:
                HandleExpand(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleMove(GameSession session, IPacketReader packet)
    {
        var uid = packet.ReadLong(); // Grabs incoming item packet uid
        var dstSlot = packet.ReadShort(); // Grabs incoming item packet slot
        session.Player.Inventory.MoveItem(session, uid, dstSlot);
    }

    private static void HandleDrop(GameSession session, IPacketReader packet)
    {
        // TODO: Make sure items are tradable?
        var uid = packet.ReadLong();
        var amount = packet.ReadInt(); // Grabs incoming item packet amount
        session.Player.Inventory.DropItem(session, uid, amount, false);
    }

    private static void HandleDropBound(GameSession session, IPacketReader packet)
    {
        var uid = packet.ReadLong();
        session.Player.Inventory.DropItem(session, uid, 0, true);
    }

    private static void HandleSort(GameSession session, IPacketReader packet)
    {
        var tab = (InventoryTab) packet.ReadShort();
        session.Player.Inventory.SortInventory(session, tab);
    }

    private static void HandleExpand(GameSession session, IPacketReader packet)
    {
        var tab = (InventoryTab) packet.ReadByte();
        session.Player.Inventory.ExpandInventory(session, tab);
    }
}
