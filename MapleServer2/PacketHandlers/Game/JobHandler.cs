using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class JobHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.JOB;

    private static class JobOperations
    {
        public const byte Close = 0x08;
        public const byte Save = 0x09;
        public const byte Reset = 0x0A;
        public const byte Preset = 0x0B;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case JobOperations.Close:
                HandleCloseSkillTree(session);
                break;
            case JobOperations.Save:
                HandleSaveSkillTree(session, packet);
                break;
            case JobOperations.Reset:
                HandleResetSkillTree(session, packet);
                break;
            case JobOperations.Preset:
                HandlePresetSkillTree(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleCloseSkillTree(GameSession session)
    {
        session.Send(JobPacket.Close());
    }

    private static void HandleSaveSkillTree(GameSession session, IPacketReader packet)
    {
        // Get skill tab to update
        SkillTab skillTab = session.Player.SkillTabs.FirstOrDefault(x => x.TabId == session.Player.ActiveSkillTabId);

        // Read skills
        int count = packet.ReadInt(); // Number of skills
        for (int i = 0; i < count; i++)
        {
            // Read skill info
            int id = packet.ReadInt(); // Skill id
            short level = packet.ReadShort(); // Skill level
            byte learned = packet.ReadByte(); // 00 if unlearned 01 if learned

            // Update current character skill tree data with new skill
            skillTab.AddOrUpdate(id, level, learned > 0);
        }

        // Send JOB packet that contains all skills then send KEY_TABLE packet to update hotbars
        session.Send(JobPacket.Save(session.Player, session.Player.FieldPlayer.ObjectId));
        session.Send(KeyTablePacket.SendHotbars(session.Player.GameOptions));
        DatabaseManager.SkillTabs.Update(skillTab);
    }

    private static void HandleResetSkillTree(GameSession session, IPacketReader packet)
    {
        int unknown = packet.ReadInt();

        SkillTab skillTab = session.Player.SkillTabs.FirstOrDefault(x => x.TabId == session.Player.ActiveSkillTabId);
        skillTab.ResetSkillTree(session.Player.Job);
        session.Send(JobPacket.Save(session.Player, session.Player.FieldPlayer.ObjectId));
        DatabaseManager.SkillTabs.Update(skillTab);
    }

    private static void HandlePresetSkillTree(GameSession session, IPacketReader packet)
    {
        SkillTab skillTab = session.Player.SkillTabs.FirstOrDefault(x => x.TabId == session.Player.ActiveSkillTabId);
        int skillCount = packet.ReadInt();
        for (int i = 0; i < skillCount; i++)
        {
            int skillId = packet.ReadInt();
            short skillLevel = packet.ReadShort();
            bool learned = packet.ReadBool();
            skillTab.AddOrUpdate(skillId, skillLevel, learned);
        }

        session.Send(JobPacket.Save(session.Player, session.Player.FieldPlayer.ObjectId));
        session.Send(KeyTablePacket.SendHotbars(session.Player.GameOptions));
        DatabaseManager.SkillTabs.Update(skillTab);
    }
}
