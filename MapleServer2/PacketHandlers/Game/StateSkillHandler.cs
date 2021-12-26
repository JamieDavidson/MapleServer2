﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class StateSkillHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.STATE_SKILL;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        if (operation == 0)
        {
            // This count seems to increase for each skill used
            var counter = packet.ReadInt();
            // objectId for climb, 13641 (0x3549 for swim dash)
            var objectId = packet.ReadInt();
            var clientTime = packet.ReadInt();
            var skillId = packet.ReadInt();
            packet.ReadShort(); // 1
            session.Player.FieldPlayer.Animation = (byte) packet.ReadInt(); // Animation
            var clientTick = packet.ReadInt();
            packet.ReadLong(); // 0

            if (SkillMetadataStorage.GetSkill(skillId).State == "gosGlide")
            {
                session.Player.OnAirMount = true;
            }

            // TODO: Broadcast this to all field players
        }
    }
}
