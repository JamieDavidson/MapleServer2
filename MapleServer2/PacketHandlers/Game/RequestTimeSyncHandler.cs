using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestTimeSyncHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_TIME_SYNC;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var key = packet.ReadInt();

        session.Send(TimeSyncPacket.SetSessionServerTick(key));
    }
}
