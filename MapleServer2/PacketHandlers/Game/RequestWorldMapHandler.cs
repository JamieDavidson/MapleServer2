using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestWorldMapHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_WORLD_MAP;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case 0: // open
                HandleOpen(session);
                break;
        }
        packet.ReadByte(); // always 0?
        var tab = packet.ReadInt();
    }

    private static void HandleOpen(GameSession session)
    {
        session.Send(WorldMapPacket.Open());
    }
}
