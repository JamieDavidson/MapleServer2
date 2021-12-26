using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestUserEnvHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_USER_ENV;

    private static class UserEnvMode
    {
        public const byte Change = 0x1;
        public const byte Trophy = 0x3;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case UserEnvMode.Change:
                HandleTitleChange(session, packet);
                break;
            case UserEnvMode.Trophy:
                HandleTrophy(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleTitleChange(GameSession session, IPacketReader packet)
    {
        int titleID = packet.ReadInt();

        if (titleID < 0)
        {
            return;
        }

        session.Player.TitleId = titleID;
        session.FieldManager.BroadcastPacket(UserEnvPacket.UpdateTitle(session, titleID));
    }

    private static void HandleTrophy(GameSession session)
    {
        session.Send(UserEnvPacket.UpdateTrophy());
    }
}
