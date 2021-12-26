using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class PlayerHostHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.PLAYER_HOST;

    private static class PlayerHostOperations
    {
        public const byte Claim = 0x1;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case PlayerHostOperations.Claim:
                HandleClaim(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleClaim(GameSession session, IPacketReader packet)
    {
        var hongBaoId = packet.ReadInt();

        var hongBao = GameServer.HongBaoManager.GetHongBaoById(hongBaoId);
        if (hongBao == null)
        {
            return;
        }

        if (hongBao.Active == false)
        {
            session.Send(PlayerHostPacket.HongbaoGiftNotice(session.Player, hongBao, 0));
            return;
        }

        hongBao.AddReceiver(session.Player);
    }
}
