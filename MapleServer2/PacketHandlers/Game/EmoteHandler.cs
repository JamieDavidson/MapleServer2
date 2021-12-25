using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class EmoteHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.EMOTION;

    private static class EmoteOperations
    {
        public const byte LearnEmote = 0x1;
        public const byte UseEmote = 0x2;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case EmoteOperations.LearnEmote:
                HandleLearnEmote(session, packet);
                break;
            case EmoteOperations.UseEmote:
                HandleUseEmote(packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleLearnEmote(GameSession session, PacketReader packet)
    {
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }

        Item item = inventory.GetItemByUid(itemUid);

        if (session.Player.Emotes.Contains(item.SkillId))
        {
            return;
        }

        session.Player.Emotes.Add(item.SkillId);

        session.Send(EmotePacket.LearnEmote(item.SkillId));

        session.Player.Inventory.ConsumeItem(session, item.Uid, 1);
    }

    private static void HandleUseEmote(PacketReader packet)
    {
        int emoteId = packet.ReadInt();
        string animationName = packet.ReadUnicodeString();
        // animationName is the name in /Xml/anikeytext.xml
    }
}
