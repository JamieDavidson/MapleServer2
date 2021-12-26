using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class VibrateHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.VIBRATE;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var entityId = packet.ReadString();
        var skillSN = packet.ReadLong();
        var skillId = packet.ReadInt();
        var skillLevel = packet.ReadShort();
        var unkShort = packet.ReadShort();
        var unkInt = packet.ReadInt();
        var playerCoords = packet.Read<CoordF>();

        if (!MapEntityStorage.IsVibrateObject(session.Player.MapId, entityId))
        {
            return;
        }

        SkillCast skillCast = new(skillId, skillLevel, skillSN, session.ServerTick);
        session.FieldManager.BroadcastPacket(VibratePacket.Vibrate(entityId, skillCast, session.Player.FieldPlayer));
    }
}
