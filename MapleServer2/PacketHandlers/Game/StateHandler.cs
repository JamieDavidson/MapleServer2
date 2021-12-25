using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

public class StateHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.STATE;

    private static class StateHandlerMode
    {
        public const byte Jump = 0x0;
        public const byte Land = 0x1;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case StateHandlerMode.Jump:
                HandleJump(session);
                break;
            case StateHandlerMode.Land:
                break;
        }
    }

    private static void HandleJump(GameSession session)
    {
        session.Player.TrophyUpdate("jump", addAmount: 1);
    }
}
