﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class SkillBookTreeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_SKILL_BOOK_TREE;

    private static class SkillBookOperations
    {
        public const byte Open = 0x00;
        public const byte Save = 0x01;
        public const byte Rename = 0x02;
        public const byte AddTab = 0x04;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case SkillBookOperations.Open:
                HandleOpen(session);
                break;
            case SkillBookOperations.Save:
                HandleSave(session, packet);
                break;
            case SkillBookOperations.Rename:
                HandleRename(session, packet);
                break;
            case SkillBookOperations.AddTab:
                HandleAddTab(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        session.Send(SkillBookTreePacket.Open(session.Player));
    }

    private static void HandleSave(GameSession session, PacketReader packet)
    {
        long activeTabId = packet.ReadLong();
        long selectedTab = packet.ReadLong(); // if 0 player used activate tab
        int unknown = packet.ReadInt();
        int tabCount = packet.ReadInt();
        for (int i = 0; i < tabCount; i++)
        {
            long tabId = packet.ReadLong();
            string tabName = packet.ReadUnicodeString();

            SkillTab skillTab = session.Player.SkillTabs.FirstOrDefault(x => x.TabId == tabId);
            if (skillTab == default)
            {
                skillTab = new(session.Player.CharacterId, session.Player.Job, tabId, tabName);
                session.Player.SkillTabs.Add(skillTab);
            }
            else
            {
                skillTab = session.Player.SkillTabs[i];
                skillTab.TabId = tabId;
                skillTab.Name = tabName;
            }

            skillTab.ResetSkillTree(session.Player.Job);
            int skillCount = packet.ReadInt();
            for (int j = 0; j < skillCount; j++)
            {
                int skillId = packet.ReadInt();
                int skillLevel = packet.ReadInt();
                skillTab.AddOrUpdate(skillId, (short) skillLevel, skillLevel > 0);
            }
        }

        session.Player.ActiveSkillTabId = activeTabId;
        session.Send(SkillBookTreePacket.Save(session.Player, selectedTab));
        foreach (SkillTab skillTab in session.Player.SkillTabs)
        {
            DatabaseManager.SkillTabs.Update(skillTab);
        }
    }

    private static void HandleRename(GameSession session, PacketReader packet)
    {
        long id = packet.ReadLong();
        string newName = packet.ReadUnicodeString();

        SkillTab skillTab = session.Player.SkillTabs.FirstOrDefault(x => x.TabId == id);
        skillTab.Name = newName;
        session.Send(SkillBookTreePacket.Rename(id, newName));
    }

    private static void HandleAddTab(GameSession session)
    {
        if (!session.Player.Account.RemoveMerets(990))
        {
            return;
        }

        session.Send(SkillBookTreePacket.AddTab(session.Player));
    }
}
