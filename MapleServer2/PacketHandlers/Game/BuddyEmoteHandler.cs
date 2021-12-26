using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BuddyEmoteHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BUDDY_EMOTE;

    private static class BuddyEmoteOperations
    {
        public const byte InviteBuddyEmote = 0x0;
        public const byte InviteBuddyEmoteConfirm = 0x1;
        public const byte LearnEmote = 0x2;
        public const byte AcceptEmote = 0x3;
        public const byte DeclineEmote = 0x4;
        public const byte StopEmote = 0x6;
    }
    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case BuddyEmoteOperations.InviteBuddyEmote:
                HandleInviteBuddyEmote(session, packet);
                break;
            case BuddyEmoteOperations.InviteBuddyEmoteConfirm:
                HandleInviteBuddyEmoteConfirm(session, packet);
                break;
            case BuddyEmoteOperations.LearnEmote:
                HandleLearnEmote(session, packet);
                break;
            case BuddyEmoteOperations.AcceptEmote:
                HandleAcceptEmote(session, packet);
                break;
            case BuddyEmoteOperations.DeclineEmote:
                HandleDeclineEmote(session, packet);
                break;
            case BuddyEmoteOperations.StopEmote:
                HandleStopEmote(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleInviteBuddyEmote(GameSession session, IPacketReader packet)
    {
        var buddyEmoteId = packet.ReadInt();
        var characterId = packet.ReadLong();

        var buddy = GameServer.PlayerManager.GetPlayerById(characterId);
        if (buddy == null)
        {
            return;
        }

        buddy.Session.Send(BuddyEmotePacket.SendRequest(buddyEmoteId, session.Player));
    }

    private static void HandleInviteBuddyEmoteConfirm(GameSession session, IPacketReader packet)
    {
        var senderCharacterId = packet.ReadLong();

        var buddy = GameServer.PlayerManager.GetPlayerById(senderCharacterId);
        if (buddy == null)
        {
            return;
        }

        buddy.Session.Send(BuddyEmotePacket.ConfirmSendRequest(session.Player));
    }

    private static void HandleLearnEmote(GameSession session, IPacketReader packet)
    {
        var emoteItemUid = packet.ReadLong();
        // TODO grab emoteId from emoteItemUid
        session.Send(BuddyEmotePacket.LearnEmote());
    }

    private static void HandleAcceptEmote(GameSession session, IPacketReader packet)
    {
        var buddyEmoteId = packet.ReadInt();
        var senderCharacterId = packet.ReadLong();
        var senderCoords = packet.Read<CoordF>();
        var selfCoords = packet.Read<CoordF>();
        var rotation = packet.ReadInt();

        var buddy = GameServer.PlayerManager.GetPlayerById(senderCharacterId);
        if (buddy == null)
        {
            return;
        }

        buddy.Session.Send(BuddyEmotePacket.SendAccept(buddyEmoteId, session.Player));
        session.Send(BuddyEmotePacket.StartEmote(buddyEmoteId, buddy.Session.Player, session.Player, selfCoords, rotation));
        buddy.Session.Send(BuddyEmotePacket.StartEmote(buddyEmoteId, buddy.Session.Player, session.Player, selfCoords, rotation));
    }

    private static void HandleDeclineEmote(GameSession session, IPacketReader packet)
    {
        var buddyEmoteId = packet.ReadInt();
        var senderCharacterId = packet.ReadLong();

        var other = GameServer.PlayerManager.GetPlayerById(senderCharacterId);
        if (other == null)
        {
            return;
        }

        other.Session.Send(BuddyEmotePacket.DeclineEmote(buddyEmoteId, session.Player));
    }

    private static void HandleStopEmote(GameSession session, IPacketReader packet)
    {
        var buddyEmoteId = packet.ReadInt();
        var target = packet.ReadLong();

        var buddy = GameServer.PlayerManager.GetPlayerById(target);
        if (buddy == null)
        {
            return;
        }

        buddy.Session.Send(BuddyEmotePacket.StopEmote(buddyEmoteId, session.Player));
    }
}
