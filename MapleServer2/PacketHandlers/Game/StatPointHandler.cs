using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class StatPointHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.STAT_POINT;

    private static class StatPointOperations
    {
        public const byte Increment = 0x2;
        public const byte Reset = 0x3;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case StatPointOperations.Increment:
                HandleStatIncrement(session, packet);
                break;
            case StatPointOperations.Reset:
                HandleResetStatDistribution(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleStatIncrement(GameSession session, IPacketReader packet)
    {
        var statTypeIndex = packet.ReadByte();

        session.Player.StatPointDistribution.AddPoint(statTypeIndex); // Deprecate?
        session.Player.Stats.Allocate((StatId) statTypeIndex);
        session.Send(StatPointPacket.WriteStatPointDistribution(session.Player));
        session.Send(StatPacket.SetStats(session.Player.FieldPlayer));
    }

    private static void HandleResetStatDistribution(GameSession session)
    {
        session.Player.Stats.ResetAllocations(session.Player.StatPointDistribution);
        session.Send(StatPointPacket.WriteStatPointDistribution(session.Player)); // Deprecate?
        session.Send(StatPacket.SetStats(session.Player.FieldPlayer));
    }
}
