using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Managers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BuddyHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BUDDY;

    private static class BuddyOperations
    {
        public const byte SendRequest = 0x2;
        public const byte Accept = 0x3;
        public const byte Decline = 0x4;
        public const byte Block = 0x5;
        public const byte Unblock = 0x6;
        public const byte RemoveFriend = 0x7;
        public const byte EditBlockReason = 0xA;
        public const byte CancelRequest = 0x11;
    }

    private static class BuddyNotices
    {
        public const byte RequestSent = 0x0;
        public const byte CharacterNotFound = 0x1;
        public const byte RequestAlreadySent = 0x2;
        public const byte AlreadyFriends = 0x3;
        public const byte CannotAddSelf = 0x4;
        public const byte CannotSendRequest = 0x5;
        public const byte CannotBlock = 0x6;
        public const byte CannotAddFriends = 0x7;
        public const byte OtherUserCannotAddFriends = 0x8;
        public const byte DeclinedRequest = 0x9;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case BuddyOperations.SendRequest:
                HandleSendRequest(session, packet);
                break;
            case BuddyOperations.Accept:
                HandleAccept(session, packet);
                break;
            case BuddyOperations.Decline:
                HandleDecline(session, packet);
                break;
            case BuddyOperations.Block:
                HandleBlock(session, packet);
                break;
            case BuddyOperations.Unblock:
                HandleUnblock(session, packet);
                break;
            case BuddyOperations.RemoveFriend:
                HandleRemoveFriend(session, packet);
                break;
            case BuddyOperations.EditBlockReason:
                HandleEditBlockReason(session, packet);
                break;
            case BuddyOperations.CancelRequest:
                HandleCancelRequest(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleSendRequest(GameSession session, IPacketReader packet)
    {
        string otherPlayerName = packet.ReadUnicodeString();
        string message = packet.ReadUnicodeString();

        if (!DatabaseManager.Characters.NameExists(otherPlayerName))
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.CharacterNotFound, otherPlayerName));
            return;
        }

        Player targetPlayer = GameServer.PlayerManager.GetPlayerByName(otherPlayerName);
        if (targetPlayer == null) // If the player is not online, get player data from database
        {
            targetPlayer = DatabaseManager.Characters.FindPartialPlayerByName(otherPlayerName);
            targetPlayer.BuddyList = GameServer.BuddyManager.GetBuddies(targetPlayer.CharacterId);
        }

        if (targetPlayer.CharacterId == session.Player.CharacterId)
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.CannotAddSelf, targetPlayer.Name));
            return;
        }

        if (session.Player.BuddyList.Count(b => !b.Blocked) >= 100) // 100 is friend limit
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.CannotAddFriends, targetPlayer.Name));
            return;
        }

        if (targetPlayer.BuddyList.Count(b => !b.Blocked) >= 100)
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.OtherUserCannotAddFriends, targetPlayer.Name));
            return;
        }

        if (BuddyManager.IsBlocked(session.Player, targetPlayer))
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.DeclinedRequest, targetPlayer.Name));
            return;
        }

        if (BuddyManager.IsFriend(session.Player, targetPlayer))
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.AlreadyFriends, targetPlayer.Name));
            return;
        }

        long id = GuidGenerator.Long();
        Buddy buddy = new(id, session.Player.CharacterId, targetPlayer, message, true, false);
        Buddy buddyTargetPlayer = new(id, targetPlayer.CharacterId, session.Player, message, false, true);
        GameServer.BuddyManager.AddBuddy(buddy);
        GameServer.BuddyManager.AddBuddy(buddyTargetPlayer);
        session.Player.BuddyList.Add(buddy);

        session.Send(BuddyPacket.Notice(BuddyNotices.RequestSent, targetPlayer.Name));
        session.Send(BuddyPacket.AddToList(buddy));

        if (targetPlayer.Session != null && targetPlayer.Session.Connected())
        {
            targetPlayer.BuddyList.Add(buddyTargetPlayer);
            targetPlayer.Session.Send(BuddyPacket.AddToList(buddyTargetPlayer));
        }
    }

    private static void HandleRemoveFriend(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
        Buddy buddyFriend = GameServer.BuddyManager.GetBuddyByPlayerAndId(buddy.Friend, buddyId);

        session.Send(BuddyPacket.RemoveFromList(buddy));

        Player otherPlayer = GameServer.PlayerManager.GetPlayerByName(buddy.Friend.Name);
        if (otherPlayer != null)
        {
            otherPlayer.Session.Send(BuddyPacket.RemoveFromList(buddyFriend));
            otherPlayer.BuddyList.Remove(buddyFriend);
        }

        GameServer.BuddyManager.RemoveBuddy(buddy);
        GameServer.BuddyManager.RemoveBuddy(buddyFriend);
        session.Player.BuddyList.Remove(buddy);
        DatabaseManager.Buddies.Delete(buddy.Id);
        DatabaseManager.Buddies.Delete(buddyFriend.Id);
    }

    private static void HandleEditBlockReason(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();
        string otherPlayerName = packet.ReadUnicodeString();
        string newBlockReason = packet.ReadUnicodeString();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
        if (buddy == null || otherPlayerName != buddy.Friend.Name)
        {
            return;
        }

        buddy.Message = newBlockReason;
        session.Send(BuddyPacket.EditBlockReason(buddy));
        DatabaseManager.Buddies.Update(buddy);
    }

    private static void HandleAccept(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
        Buddy buddyFriend = GameServer.BuddyManager.GetBuddyByPlayerAndId(buddy.Friend, buddyId);

        buddy.IsFriendRequest = false;
        buddyFriend.IsPending = false;

        session.Send(BuddyPacket.AcceptRequest(buddy));
        session.Send(BuddyPacket.UpdateBuddy(buddy));
        session.Send(BuddyPacket.LoginLogoutNotification(buddy));
        DatabaseManager.Buddies.Update(buddy);
        DatabaseManager.Buddies.Update(buddyFriend);

        Player otherPlayer = GameServer.PlayerManager.GetPlayerByName(buddy.Friend.Name);
        if (otherPlayer != null)
        {
            otherPlayer.Session.Send(BuddyPacket.UpdateBuddy(buddyFriend));
            otherPlayer.Session.Send(BuddyPacket.AcceptNotification(buddyFriend));
        }
    }

    private static void HandleDecline(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
        Buddy buddyFriend = GameServer.BuddyManager.GetBuddyByPlayerAndId(buddy.Friend, buddyId);

        session.Send(BuddyPacket.DeclineRequest(buddy));

        Player otherPlayer = GameServer.PlayerManager.GetPlayerByName(buddy.Friend.Name);
        if (otherPlayer != null)
        {
            otherPlayer.Session.Send(BuddyPacket.RemoveFromList(buddyFriend));
            otherPlayer.BuddyList.Remove(buddyFriend);
        }

        GameServer.BuddyManager.RemoveBuddy(buddy);
        GameServer.BuddyManager.RemoveBuddy(buddyFriend);
        session.Player.BuddyList.Remove(buddy);
        DatabaseManager.Buddies.Delete(buddy.Id);
        DatabaseManager.Buddies.Delete(buddyFriend.Id);
    }

    private static void HandleBlock(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();
        string targetName = packet.ReadUnicodeString();
        string message = packet.ReadUnicodeString();

        if (session.Player.BuddyList.Count(b => b.Blocked) >= 100) // 100 is block limit
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.CannotBlock, targetName));
            return;
        }

        if (!DatabaseManager.Characters.NameExists(targetName))
        {
            session.Send(BuddyPacket.Notice(BuddyNotices.CharacterNotFound, targetName));
            return;
        }

        Player targetPlayer = GameServer.PlayerManager.GetPlayerByName(targetName);
        if (targetPlayer == null) // If the player is not online, get player data from database
        {
            targetPlayer = DatabaseManager.Characters.FindPartialPlayerByName(targetName);
            targetPlayer.BuddyList = GameServer.BuddyManager.GetBuddies(targetPlayer.CharacterId);
        }

        if (buddyId == 0) // if buddy doesn't exist, create Buddy
        {
            long id = GuidGenerator.Long();
            Buddy buddy = new(id, session.Player.CharacterId, targetPlayer, message, false, false, true);
            GameServer.BuddyManager.AddBuddy(buddy);
            session.Player.BuddyList.Add(buddy);

            session.Send(BuddyPacket.AddToList(buddy));
            session.Send(BuddyPacket.Block(buddy));
        }
        else
        {
            Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
            Buddy buddyFriend = GameServer.BuddyManager.GetBuddyByPlayerAndId(buddy.Friend, buddyId);

            if (targetPlayer.Session != null && targetPlayer.Session.Connected())
            {
                targetPlayer.BuddyList.Remove(buddyFriend);
                targetPlayer.Session.Send(BuddyPacket.RemoveFromList(buddyFriend));
            }

            GameServer.BuddyManager.RemoveBuddy(buddyFriend);

            buddy.BlockReason = message;
            buddy.Blocked = true;
            session.Send(BuddyPacket.UpdateBuddy(buddy));
            session.Send(BuddyPacket.Block(buddy));
            DatabaseManager.Buddies.Update(buddy);
            DatabaseManager.Buddies.Delete(buddyFriend.Id);
        }
    }

    private static void HandleUnblock(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);

        session.Send(BuddyPacket.Unblock(buddy));
        session.Send(BuddyPacket.RemoveFromList(buddy));

        GameServer.BuddyManager.RemoveBuddy(buddy);
        session.Player.BuddyList.Remove(buddy);
        DatabaseManager.Buddies.Delete(buddy.Id);
    }

    private static void HandleCancelRequest(GameSession session, IPacketReader packet)
    {
        long buddyId = packet.ReadLong();

        Buddy buddy = GameServer.BuddyManager.GetBuddyByPlayerAndId(session.Player, buddyId);
        Buddy buddyFriend = GameServer.BuddyManager.GetBuddyByPlayerAndId(buddy.Friend, buddyId);

        session.Send(BuddyPacket.CancelRequest(buddy));

        Player targetPlayer = GameServer.PlayerManager.GetPlayerByName(buddy.Friend.Name);

        if (targetPlayer != null)
        {
            targetPlayer.Session.Send(BuddyPacket.RemoveFromList(buddyFriend));
            targetPlayer.BuddyList.Remove(buddyFriend);
        }

        GameServer.BuddyManager.RemoveBuddy(buddy);
        GameServer.BuddyManager.RemoveBuddy(buddyFriend);
        session.Player.BuddyList.Remove(buddy);
        DatabaseManager.Buddies.Delete(buddy.Id);
        DatabaseManager.Buddies.Delete(buddyFriend.Id);
    }
}
