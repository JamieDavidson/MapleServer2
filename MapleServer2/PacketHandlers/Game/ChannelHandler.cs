
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ChannelHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CHANNEL;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var channelId = packet.ReadShort();

        var player = session.Player;
        player.InstanceId = channelId;
        player.ChannelId = channelId;

        player.WarpGameToGame(player.MapId, channelId);
    }
}
