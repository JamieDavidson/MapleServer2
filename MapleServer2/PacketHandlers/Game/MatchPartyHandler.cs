using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

public class MatchPartyHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.MATCH_PARTY;

    private static class MatchPartyOperations
    {
        public const byte CreateListing = 0x0;
        public const byte RemoveListing = 0x1;
        public const byte Refresh = 0x2;
    }

    private static class SearchFilter
    {
        public const byte MostMembers = 0xC;
        public const byte LeastMembers = 0xB;
        public const byte OldestFirst = 0x16;
        public const byte NewestFirst = 0x15;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case MatchPartyOperations.CreateListing:
                HandleCreateListing(session, packet);
                break;
            case MatchPartyOperations.RemoveListing:
                HandleRemoveListing(session);
                break;
            case MatchPartyOperations.Refresh:
                HandleRefresh(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    public static void HandleCreateListing(GameSession session, PacketReader packet)
    {
        string partyName = packet.ReadUnicodeString();
        bool approval = packet.ReadBool();
        int memberCountRecruit = packet.ReadInt();

        Party party = GameServer.PartyManager.GetPartyByLeader(session.Player);

        if (party == null)
        {
            Party newParty = new(partyName, approval, session.Player, memberCountRecruit);
            GameServer.PartyManager.AddParty(newParty);

            session.Send(PartyPacket.Create(newParty, false));
            session.Send(PartyPacket.UpdateHitpoints(session.Player));

            session.Player.Party = newParty;
            party = newParty;
        }
        else
        {
            if (party.Members.Count >= memberCountRecruit)
            {
                return;
            }
            party.PartyFinderId = GuidGenerator.Long();
            party.Name = partyName;
            party.Approval = approval;
            party.RecruitMemberCount = memberCountRecruit;
        }

        party.BroadcastPacketParty(MatchPartyPacket.CreateListing(party));
        party.BroadcastPacketParty(PartyPacket.MatchParty(party, true));
    }

    public static void HandleRemoveListing(GameSession session)
    {
        Party party = session.Player.Party;
        if (party == null)
        {
            return;
        }

        party.BroadcastPacketParty(MatchPartyPacket.RemoveListing(party));

        if (party.Members.Count == 1)
        {
            party.RemoveMember(session.Player);
            return;
        }

        party.PartyFinderId = 0;
        party.BroadcastPacketParty(PartyPacket.MatchParty(null, false));
    }

    public static void HandleRefresh(GameSession session, PacketReader packet)
    {
        //Get search terms:
        long unk = packet.ReadLong();
        var filterMode = packet.ReadByte();
        string searchText = packet.ReadUnicodeString().ToLower();
        long unk2 = packet.ReadLong();

        List<Party> partyList = GameServer.PartyManager.GetPartyFinderList();

        //Filter
        switch (filterMode)
        {
            case SearchFilter.MostMembers:
                partyList = partyList.OrderByDescending(p => p.Members.Count).ToList();
                break;
            case SearchFilter.LeastMembers:
                partyList = partyList.OrderBy(p => p.Members.Count).ToList();
                break;
            case SearchFilter.OldestFirst:
                partyList = partyList.OrderBy(p => p.CreationTimestamp).ToList();
                break;
            case SearchFilter.NewestFirst:
                partyList = partyList.OrderByDescending(p => p.CreationTimestamp).ToList();
                break;
        }

        //Filter text
        if (searchText.Length > 0)
        {
            partyList = partyList.Where(o => o.Name.ToLower().Contains(searchText)).ToList();
        }

        session.Send(MatchPartyPacket.SendListings(partyList));
    }
}
