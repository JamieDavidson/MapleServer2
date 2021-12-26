using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestItemLockHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_LOCK;

    private static class ItemLockOperations
    {
        public const byte Open = 0x00;
        public const byte Add = 0x01;
        public const byte Remove = 0x02;
        public const byte Update = 0x03;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case ItemLockOperations.Open:
                session.Player.LockInventory = new();
                break;
            case ItemLockOperations.Add:
                HandleAdd(session, packet);
                break;
            case ItemLockOperations.Remove:
                HandleRemove(session, packet);
                break;
            case ItemLockOperations.Update:
                HandleUpdateItem(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleAdd(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();
        var uid = packet.ReadLong();

        session.Player.LockInventory.Add(session, mode, uid);
    }

    private static void HandleRemove(GameSession session, IPacketReader packet)
    {
        var uid = packet.ReadLong();

        session.Player.LockInventory.Remove(session, uid);
    }

    private static void HandleUpdateItem(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        session.Player.LockInventory.Update(session, operation);
    }
}
