using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class SkillHandler : GamePacketHandler
{
    private static readonly Random Rand = RandomProvider.Get();

    public override RecvOp OpCode => RecvOp.SKILL;

    private static class SkillHandlerOperations
    {
        public const byte Cast = 0x0;
        public const byte Damage = 0x1;
        public const byte Sync = 0x2;
        public const byte SyncTick = 0x3;
        public const byte Cancel = 0x4;
    }

    private static class DamagingOperation
    {
        public const byte SyncDamage = 0x0;
        public const byte Damage = 0x1;
        public const byte RegionSkill = 0x2;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case SkillHandlerOperations.Cast:
                HandleCast(session, packet);
                break;
            case SkillHandlerOperations.Damage:
                HandleDamageMode(session, packet);
                break;
            case SkillHandlerOperations.Sync:
                HandleSyncSkills(session, packet);
                break;
            case SkillHandlerOperations.SyncTick:
                HandleSyncTick(packet);
                break;
            case SkillHandlerOperations.Cancel:
                HandleCancelSkill(packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private void HandleDamageMode(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case DamagingOperation.SyncDamage:
                HandleSyncDamage(session, packet);
                break;
            case DamagingOperation.Damage:
                HandleDamage(session, packet);
                break;
            case DamagingOperation.RegionSkill:
                HandleRegionSkills(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleCast(GameSession session, IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var serverTick = packet.ReadInt();
        var skillId = packet.ReadInt();
        var skillLevel = packet.ReadShort();
        var attackPoint = packet.ReadByte();
        var position = packet.Read<CoordF>();
        var direction = packet.Read<CoordF>();
        var rotation = packet.Read<CoordF>();
        packet.ReadFloat();
        var clientTick = packet.ReadInt();
        packet.ReadBool();
        packet.ReadLong();
        var flag = packet.ReadBool();
        if (flag)
        {
            packet.ReadInt();
            var unkString = packet.ReadUnicodeString();
        }

        SkillCast skillCast = new(skillId, skillLevel, skillSN, serverTick, session.Player.FieldPlayer.ObjectId, clientTick, attackPoint);
        session.Player.FieldPlayer.Cast(skillCast);

        // TODO: Move to FieldActor.Cast()
        if (skillCast != null)
        {
            session.FieldManager.BroadcastPacket(SkillUsePacket.SkillUse(skillCast, position, direction, rotation));
            session.Send(StatPacket.SetStats(session.Player.FieldPlayer));
        }
    }

    private static void HandleSyncSkills(GameSession session, IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var skillId = packet.ReadInt();
        var skillLevel = packet.ReadShort();
        var motionPoint = packet.ReadByte();
        var position = packet.Read<CoordF>();
        var unkCoords = packet.Read<CoordF>();
        var rotation = packet.Read<CoordF>();
        var unknown = packet.Read<CoordF>();
        var toggle = packet.ReadBool();
        packet.ReadInt();
        packet.ReadByte();

        session.FieldManager.BroadcastPacket(SkillSyncPacket.Sync(skillSN, session.Player.FieldPlayer, position, rotation, toggle), session);
    }

    private static void HandleSyncTick(IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var serverTick = packet.ReadInt();
    }

    private static void HandleCancelSkill(IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
    }

    private static void HandleSyncDamage(GameSession session, IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var attackPoint = packet.ReadByte();
        var position = packet.Read<CoordF>();
        var rotation = packet.Read<CoordF>();
        var count = packet.ReadByte();
        packet.ReadInt();

        List<int> atkCount = new();
        List<int> sourceId = new();
        List<int> targetId = new();
        List<short> animation = new();
        // TODO: Handle multiple projectiles
        for (var i = 0; i < count; i++)
        {
            atkCount.Add(packet.ReadInt());
            sourceId.Add(packet.ReadInt());
            targetId.Add(packet.ReadInt());
            animation.Add(packet.ReadShort());
        }

        session.FieldManager.BroadcastPacket(SkillDamagePacket.SyncDamage(skillSN, position, rotation, session.Player.FieldPlayer, sourceId, count, atkCount, targetId, animation));
    }

    private static void HandleDamage(GameSession session, IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var attackCounter = packet.ReadInt();
        var playerObjectId = packet.ReadInt();
        var position = packet.Read<CoordF>();
        var impactPos = packet.Read<CoordF>();
        var rotation = packet.Read<CoordF>();
        int attackPoint = packet.ReadByte();
        var count = packet.ReadByte();
        packet.ReadInt();

        var fieldPlayer = session.Player.FieldPlayer;

        var isCrit = DamageHandler.RollCrit(session.Player.Stats[StatId.CritRate].Total);

        // TODO: Check if skillSN matches server's current skill for the player
        // TODO: Verify if its the player or an ally
        if (fieldPlayer.SkillCast.IsHeal())
        {
            Status status = new(fieldPlayer.SkillCast, fieldPlayer.ObjectId, fieldPlayer.ObjectId, 1);
            StatusHandler.Handle(session, status);

            // TODO: Heal based on stats
            session.FieldManager.BroadcastPacket(SkillDamagePacket.Heal(status, 50));
            fieldPlayer.Stats[StatId.Hp].Increase(50);
            session.Send(StatPacket.UpdateStats(fieldPlayer, StatId.Hp));
        }
        else
        {
            List<DamageHandler> damages = new();
            for (var i = 0; i < count; i++)
            {
                var entityId = packet.ReadInt();
                packet.ReadByte();

                var mob = session.FieldManager.State.Mobs.GetValueOrDefault(entityId);
                if (mob == null)
                {
                    continue;
                }

                var damage = DamageHandler.CalculateDamage(fieldPlayer.SkillCast, fieldPlayer, mob, isCrit);

                mob.Damage(damage);
                // TODO: Move logic to Damage()
                session.FieldManager.BroadcastPacket(StatPacket.UpdateMobStats(mob));
                if (mob.IsDead)
                {
                    HandleMobKill(session, mob);
                }

                damages.Add(damage);

                // TODO: Check if the skill is a debuff for an entity
                var skillCast = fieldPlayer.SkillCast;
                if (skillCast.IsDebuffElement() || skillCast.IsDebuffToEntity() || skillCast.IsDebuffElement())
                {
                    Status status = new(fieldPlayer.SkillCast, mob.ObjectId, fieldPlayer.ObjectId, 1);
                    StatusHandler.Handle(session, status);
                }
            }

            session.FieldManager.BroadcastPacket(SkillDamagePacket.Damage(skillSN, attackCounter, position, rotation, fieldPlayer, damages));
        }
    }

    private static void HandleRegionSkills(GameSession session, IPacketReader packet)
    {
        var skillSN = packet.ReadLong();
        var mode = packet.ReadByte();
        var unknown = packet.ReadInt();
        var unknown2 = packet.ReadInt();
        var position = packet.Read<CoordF>();
        var rotation = packet.Read<CoordF>();

        // TODO: Verify rest of skills to proc correctly.
        // Send status correctly when Region attacks are proc.
        var parentSkill = SkillUsePacket.SkillCastMap[skillSN];

        if (parentSkill.GetConditionSkill() == null)
        {
            return;
        }

        foreach (var conditionSkill in parentSkill.GetConditionSkill())
        {
            if (!conditionSkill.Splash)
            {
                continue;
            }

            SkillCast skillCast = new(conditionSkill.Id, conditionSkill.Level, GuidGenerator.Long(), session.ServerTick, parentSkill);
            RegionSkillHandler.Handle(session, GuidGenerator.Int(), session.Player.FieldPlayer.Coord, skillCast);
        }
    }

    private static void HandleMobKill(GameSession session, IFieldObject<NpcMetadata> mob)
    {
        // TODO: Add trophy + item drops
        // Drop Money
        var dropMeso = Rand.Next(2) == 0;
        if (dropMeso)
        {
            // TODO: Calculate meso drop rate
            Item meso = new(90000001, Rand.Next(2, 800));
            session.FieldManager.AddResource(meso, mob, session.Player.FieldPlayer);
        }
        // Drop Meret
        var dropMeret = Rand.Next(40) == 0;
        if (dropMeret)
        {
            Item meret = new(90000004, 20);
            session.FieldManager.AddResource(meret, mob, session.Player.FieldPlayer);
        }
        // Drop SP
        var dropSP = Rand.Next(6) == 0;
        if (dropSP)
        {
            Item spBall = new(90000009, 20);
            session.FieldManager.AddResource(spBall, mob, session.Player.FieldPlayer);
        }
        // Drop EP
        var dropEP = Rand.Next(10) == 0;
        if (dropEP)
        {
            Item epBall = new(90000010, 20);
            session.FieldManager.AddResource(epBall, mob, session.Player.FieldPlayer);
        }
        // Drop Items
        // Send achieves (?)
        // Gain Mob EXP
        session.Player.Levels.GainExp(mob.Value.Experience);
        // Send achieves (2)

        var mapId = session.Player.MapId.ToString();
        // Prepend zero if map id is equal to 7 digits
        if (mapId.Length == 7)
        {
            mapId = $"0{mapId}";
        }

        // Quest Check
        QuestHelper.UpdateQuest(session, mob.Value.Id.ToString(), "npc", mapId);
    }
}
