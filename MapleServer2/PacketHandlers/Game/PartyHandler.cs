using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class PartyHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.PARTY;

    private static class PartyOperations
    {
        public const byte Invite = 0x1;
        public const byte Join = 0x2;
        public const byte Leave = 0x3;
        public const byte Kick = 0x4;
        public const byte SetLeader = 0x11;
        public const byte FinderJoin = 0x17;
        public const byte SummonParty = 0x1D;
        public const byte VoteKick = 0x2D;
        public const byte ReadyCheck = 0x2E;
        public const byte FindDungeonParty = 0x21;
        public const byte CancelFindDungeonParty = 0x22;
        public const byte ReadyCheckUpdate = 0x30;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte(); //Mode

        switch (operation)
        {
            case PartyOperations.Invite:
                HandleInvite(session, packet);
                break;
            case PartyOperations.Join:
                HandleJoin(session, packet);
                break;
            case PartyOperations.Leave:
                HandleLeave(session);
                break;
            case PartyOperations.Kick:
                HandleKick(session, packet);
                break;
            case PartyOperations.SetLeader:
                HandleSetLeader(session, packet);
                break;
            case PartyOperations.FinderJoin:
                HandleFinderJoin(session, packet);
                break;
            case PartyOperations.SummonParty:
                HandleSummonParty();
                break;
            case PartyOperations.VoteKick:
                HandleVoteKick(session, packet);
                break;
            case PartyOperations.ReadyCheck:
                HandleStartReadyCheck(session);
                break;
            case PartyOperations.FindDungeonParty:
                HandleFindDungeonParty(session, packet);
                break;
            case PartyOperations.ReadyCheckUpdate:
                HandleReadyCheckUpdate(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleInvite(GameSession session, PacketReader packet)
    {
        string target = packet.ReadUnicodeString();

        Player other = GameServer.PlayerManager.GetPlayerByName(target);
        if (other == null)
        {
            return;
        }

        if (session.Player.Party != null)
        {
            Party party = session.Player.Party;

            if (party.Leader != session.Player)
            {
                session.Send(PartyPacket.Notice(session.Player, PartyNotice.NotLeader));
                return;
            }

            if (other == session.Player)
            {
                session.Send(PartyPacket.Notice(session.Player, PartyNotice.InviteSelf));
                return;
            }

            if (other.Party != null)
            {
                Party otherParty = other.Party;

                if (otherParty.Members.Count > 1)
                {
                    session.Send(PartyPacket.Notice(session.Player, PartyNotice.UnableToInvite));
                    return;
                }
            }

            other.Session.Send(PartyPacket.SendInvite(session.Player, party));
        }
        else
        {
            if (other.Party != null)
            {
                Party otherParty = other.Party;

                if (otherParty.Members.Count == 1)
                {
                    Party newParty = new(session.Player);
                    GameServer.PartyManager.AddParty(newParty);

                    session.Send(PartyPacket.Create(newParty, true));
                    other.Session.Send(PartyPacket.SendInvite(session.Player, newParty));
                    return;
                }

                session.Send(PartyPacket.Notice(other, PartyNotice.RequestToJoin));
                otherParty.Leader.Session.Send(PartyPacket.JoinRequest(session.Player));
                return;
            }

            {
                // create party
                Party newParty = new(session.Player);
                GameServer.PartyManager.AddParty(newParty);
                session.Send(PartyPacket.Create(newParty, true));
                other.Session.Send(PartyPacket.SendInvite(session.Player, newParty));
            }
        }
    }

    private static void HandleJoin(GameSession session, PacketReader packet)
    {
        string target = packet.ReadUnicodeString();
        PartyNotice response = (PartyNotice) packet.ReadByte();
        int partyId = packet.ReadInt();

        JoinParty(session, response, partyId);
    }

    private static void JoinParty(GameSession session, PartyNotice response, int partyId)
    {
        Party party = GameServer.PartyManager.GetPartyById(partyId);
        if (party == null)
        {
            session.Send(PartyPacket.Notice(session.Player, PartyNotice.PartyNotFound));
            return;
        }

        if (party.Members.Contains(session.Player) || party.Leader == session.Player)
        {
            return;
        }

        if (response != PartyNotice.AcceptedInvite)
        {
            party.Leader.Session.Send(PartyPacket.Notice(session.Player, response));
            return;
        }

        if (party.Members.Count >= 10)
        {
            session.Send(PartyPacket.Notice(session.Player, PartyNotice.FullParty));
            return;
        }

        if (session.Player.Party != null)
        {
            Party currentParty = session.Player.Party;
            if (currentParty.Members.Count == 1)
            {
                currentParty.RemoveMember(session.Player);
            }
        }

        if (party.Members.Count == 1)
        {
            //establish party.
            party.BroadcastPacketParty(PartyPacket.Join(session.Player));
            party.AddMember(session.Player);
            session.Send(PartyPacket.Create(party, true));
            party.BroadcastPacketParty(PartyPacket.UpdateHitpoints(party.Leader));
            party.BroadcastPacketParty(PartyPacket.UpdatePlayer(session.Player));
            return;
        }

        party.BroadcastPacketParty(PartyPacket.Join(session.Player));
        party.AddMember(session.Player);
        session.Send(PartyPacket.Create(party, true));
        party.BroadcastPacketParty(PartyPacket.UpdatePlayer(session.Player));

        foreach (Player member in party.Members)
        {
            if (member != session.Player)
            {
                party.BroadcastPacketParty(PartyPacket.UpdateHitpoints(member));
            }
        }
    }

    private static void HandleLeave(GameSession session)
    {
        Party party = session.Player.Party;

        session.Send(PartyPacket.Leave(session.Player, 1)); //1 = You're the player leaving
        party?.RemoveMember(session.Player);

        if (party != null)
        {
            party.BroadcastPacketParty(PartyPacket.Leave(session.Player, 0));
        }
    }

    private static void HandleSetLeader(GameSession session, PacketReader packet)
    {
        string target = packet.ReadUnicodeString();

        Player newLeader = GameServer.PlayerManager.GetPlayerByName(target);
        if (newLeader == null)
        {
            return;
        }

        Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
        if (party == null)
        {
            return;
        }

        party.BroadcastPacketParty(PartyPacket.SetLeader(newLeader));
        party.Leader = newLeader;
        party.Members.Remove(newLeader);
        party.Members.Insert(0, newLeader);
    }

    private static void HandleFinderJoin(GameSession session, PacketReader packet)
    {
        int partyId = packet.ReadInt();
        string leaderName = packet.ReadUnicodeString();

        Party party = GameServer.PartyManager.GetPartyById(partyId);
        if (party == null)
        {
            session.Send(PartyPacket.Notice(session.Player, PartyNotice.OutdatedRecruitmentListing));
            return;
        }

        if (party.PartyFinderId == 0)
        {
            session.Send(PartyPacket.Notice(session.Player, PartyNotice.RecruitmentListingDeleted));
            return;
        }

        if (session.Player.Party == null)
        {
            return;
        }

        //Join party
        JoinParty(session, PartyNotice.AcceptedInvite, partyId);
    }

    private static void HandleKick(GameSession session, PacketReader packet)
    {
        long charId = packet.ReadLong();

        Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
        if (party == null)
        {
            return;
        }

        Player kickedPlayer = GameServer.PlayerManager.GetPlayerById(charId);
        if (kickedPlayer == null)
        {
            return;
        }

        party.BroadcastPacketParty(PartyPacket.Kick(kickedPlayer));
        party.RemoveMember(kickedPlayer);
    }

    private static void HandleVoteKick(GameSession session, PacketReader packet)
    {
        long charId = packet.ReadLong();

        Party party = session.Player.Party;
        if (party == null)
        {
            return;
        }

        Player kickedPlayer = GameServer.PlayerManager.GetPlayerById(charId);
        if (kickedPlayer == null)
        {
            return;
        }

        if (party.Members.Count < 4)
        {
            session.Send(PartyPacket.Notice(session.Player, PartyNotice.InsufficientMemberCountForKickVote));
        }

        //TODO: Keep a counter of vote kicks for a player?
    }

    public static void HandleSummonParty()
    {
        //TODO: implement Summon Party Button
    }
    private static void HandleStartReadyCheck(GameSession session)
    {
        Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
        if (party == null)
        {
            return;
        }

        if (party.ReadyCheck.Count > 0) // a ready check is already in progress
        {
            return;
        }

        party.StartReadyCheck();
    }

    private static void HandleFindDungeonParty(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();

        if (session.Player.Party == null)
        {
            Party newParty = new(session.Player);
            GameServer.PartyManager.AddParty(newParty);
            session.Send(PartyPacket.Create(newParty, true));
        }

        Party party = session.Player.Party;

        // TODO: Party pairing system

        session.Send(PartyPacket.DungeonFindParty());
    }

    private static void CancelFindDungeonParty(GameSession session)
    {
        Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);
        if (party == null)
        {
            return;
        }

        if (party.Members.Count <= 1)
        {
            party.RemoveMember(session.Player);
        }

        // TODO: Remove party from pairing system

        session.Send(PartyPacket.DungeonFindParty());
    }

    private static void HandleReadyCheckUpdate(GameSession session, PacketReader packet)
    {
        int checkNum = packet.ReadInt() + 1; //+ 1 is because the ReadyChecks variable is always 1 ahead
        byte response = packet.ReadByte();

        Party party = session.Player.Party;
        if (party == null)
        {
            return;
        }

        party.BroadcastPacketParty(PartyPacket.ReadyCheck(session.Player, response));

        party.ReadyCheck.Add(session.Player);
        if (party.ReadyCheck.Count == party.Members.Count)
        {
            party.BroadcastPacketParty(PartyPacket.EndReadyCheck());
            party.ReadyCheck.Clear();
        }
    }
}
