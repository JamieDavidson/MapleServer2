using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class NewsNotificationHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.NEWS_NOTIFICATION;

    private static class NewsNotificationOperations
    {
        public const byte OpenBrowser = 0x0;
        public const byte OpenSidebar = 0x2;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        short unk = packet.ReadShort();
        var mode = packet.ReadByte();

        switch (mode)
        {
            case NewsNotificationOperations.OpenBrowser:
                HandleOpenBrowser(session);
                break;
            case NewsNotificationOperations.OpenSidebar:
                HandleOpenSidebar(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleOpenBrowser(GameSession session)
    {
        session.Send(NewsNotificationPacket.OpenBrowser());
    }

    private static void HandleOpenSidebar(GameSession session)
    {
        session.Send(NewsNotificationPacket.OpenSidebar());
    }
}
