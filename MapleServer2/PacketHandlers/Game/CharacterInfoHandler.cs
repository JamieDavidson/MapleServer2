using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class CharacterInfoHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CHARACTER_INFO;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var characterId = packet.ReadLong();

        session.Send(CharacterInfoPacket.WriteCharacterInfo(characterId, GameServer.PlayerManager.GetPlayerById(characterId)));
    }
}
