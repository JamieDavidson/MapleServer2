using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class StateHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.STATE;

    private static class StateHandlerOperations
    {
        public const byte Jump = 0x0;
        public const byte Land = 0x1;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case StateHandlerOperations.Jump:
                HandleJump(session);
                break;
            case StateHandlerOperations.Land:
                break;
        }
    }

    private static void HandleJump(GameSession session)
    {
        session.Player.TrophyUpdate("jump", addAmount: 1);
    }
}
