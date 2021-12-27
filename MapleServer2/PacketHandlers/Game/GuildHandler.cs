using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class GuildHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.GUILD;

    private static class GuildOperations
    {
        public const byte Create = 0x1;
        public const byte Disband = 0x2;
        public const byte Invite = 0x3;
        public const byte InviteResponse = 0x5;
        public const byte Leave = 0x7;
        public const byte Kick = 0x8;
        public const byte RankChange = 0xA;
        public const byte PlayerMessage = 0xD;
        public const byte CheckIn = 0xF;
        public const byte TransferLeader = 0x3D;
        public const byte GuildNotice = 0x3E;
        public const byte UpdateRank = 0x41;
        public const byte ListGuild = 0x42;
        public const byte GuildMail = 0x45;
        public const byte SubmitApplication = 0x50;
        public const byte WithdrawApplication = 0x51;
        public const byte ApplicationResponse = 0x52;
        public const byte LoadApplications = 0x54;
        public const byte LoadGuildList = 0x55;
        public const byte SearchGuildByName = 0x56;
        public const byte UseBuff = 0x59;
        public const byte UpgradeBuff = 0x5A;
        public const byte UpgradeHome = 0x62;
        public const byte ChangeHomeTheme = 0x63;
        public const byte EnterHouse = 0x64;
        public const byte GuildDonate = 0x6E;
        public const byte Services = 0x6F;
    }

    private static class GuildErrorNotice
    {
        public const byte GuildNotFound = 0x3;
        public const byte CharacterIsAlreadyInAGuild = 0x4;
        public const byte UnableToSendInvite = 0x5;
        public const byte InviteFailed = 0x6;
        public const byte UserAlreadyJoinedAGuild = 0x7;
        public const byte GuildNoLongerValid = 0x8;
        public const byte UnableToInvitePlayer = 0xA;
        public const byte GuildWithSameNameExists = 0xB;
        public const byte NameContainsForbiddenWord = 0xC;
        public const byte GuildMemberNotFound = 0xD;
        public const byte CannotDisbandWithMembers = 0xE;
        public const byte GuildIsAtCapacity = 0xF;
        public const byte GuildMemberHasNotJoined = 0x10;
        public const byte LeaderCannotLeaveGuild = 0x11;
        public const byte CannotKickLeader = 0x12;
        public const byte NotEnoughMesos = 0x14;
        public const byte InsufficientPermissions = 0x15;
        public const byte OnlyLeaderCanDoThis = 0x16;
        public const byte RankCannotBeUsed = 0x17;
        public const byte CannotChangeMaxCapacityToValue = 0x18;
        public const byte IncorrectRank = 0x19;
        public const byte RankCannotBeGranted = 0x1B;
        public const byte RankSettingFailed = 0x1C;
        public const byte CannotDoDuringGuildBattle = 0x21;
        public const byte ApplicationNotFound = 0x27;
        public const byte TargetIsInAnUninvitableLocation = 0x29;
        public const byte GuildLevelNotHighEnough = 0x2A;
        public const byte InsufficientGuildFunds = 0x2B;
        public const byte CannotUseGuildSkillsRightNow = 0x2C;
        public const byte YouAreAlreadyAtGloriousArena = 0x2E;
        public const byte ApplicationsAreNotAccepted = 0x2F;
        public const byte YouNeedAtLeastXPlayersOnline = 0x30;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case GuildOperations.Create:
                HandleCreate(session, packet);
                break;
            case GuildOperations.Disband:
                HandleDisband(session);
                break;
            case GuildOperations.Invite:
                HandleInvite(session, packet);
                break;
            case GuildOperations.InviteResponse:
                HandleInviteResponse(session, packet);
                break;
            case GuildOperations.Leave:
                HandleLeave(session);
                break;
            case GuildOperations.Kick:
                HandleKick(session, packet);
                break;
            case GuildOperations.RankChange:
                HandleRankChange(session, packet);
                break;
            case GuildOperations.PlayerMessage:
                HandlePlayerMessage(session, packet);
                break;
            case GuildOperations.CheckIn:
                HandleCheckIn(session);
                break;
            case GuildOperations.TransferLeader:
                HandleTransferLeader(session, packet);
                break;
            case GuildOperations.GuildNotice:
                HandleGuildNotice(session, packet);
                break;
            case GuildOperations.UpdateRank:
                HandleUpdateRank(session, packet);
                break;
            case GuildOperations.ListGuild:
                HandleListGuild(session, packet);
                break;
            case GuildOperations.GuildMail:
                HandleGuildMail(session, packet);
                break;
            case GuildOperations.SubmitApplication:
                HandleSubmitApplication(session, packet);
                break;
            case GuildOperations.WithdrawApplication:
                HandleWithdrawApplication(session, packet);
                break;
            case GuildOperations.ApplicationResponse:
                HandleApplicationResponse(session, packet);
                break;
            case GuildOperations.LoadApplications:
                HandleLoadApplications(session);
                break;
            case GuildOperations.LoadGuildList:
                HandleLoadGuildList(session, packet);
                break;
            case GuildOperations.SearchGuildByName:
                HandleSearchGuildByName(session, packet);
                break;
            case GuildOperations.UseBuff:
                HandleUseBuff(session, packet);
                break;
            case GuildOperations.UpgradeBuff:
                HandleUpgradeBuff(session, packet);
                break;
            case GuildOperations.UpgradeHome:
                HandleUpgradeHome(session, packet);
                break;
            case GuildOperations.ChangeHomeTheme:
                HandleChangeHomeTheme(session, packet);
                break;
            case GuildOperations.EnterHouse:
                HandleEnterHouse(session);
                break;
            case GuildOperations.GuildDonate:
                HandleGuildDonate(session, packet);
                break;
            case GuildOperations.Services:
                HandleServices(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleGuildMail(GameSession session, IPacketReader packet)
    {
        var title = packet.ReadUnicodeString();
        var body = packet.ReadUnicodeString();

        var sender = session.Player;
        var guild = session.Player.Guild;

        var guildMemberCharacterIds = guild.Members
            .Select(m => m.Player.CharacterId)
            .Where(i => i != sender.CharacterId);
        
        foreach (var characterId in guildMemberCharacterIds)
        {
            MailHelper.SendMail(MailType.Player, characterId, sender.CharacterId, sender.Name, title, body, "", "", new List<Item>(), 0, 0, out var mail);

            session.Send(MailPacket.Send(mail));
        }
    }

    private static void HandleCreate(GameSession session, IPacketReader packet)
    {
        var guildName = packet.ReadUnicodeString();

        if (session.Player.Guild != null)
        {
            return;
        }

        if (!session.Player.Wallet.Meso.Modify(-2000))
        {
            session.Send(GuildPacket.ErrorNotice(GuildErrorNotice.NotEnoughMesos));
            return;
        }

        if (DatabaseManager.Guilds.NameExists(guildName))
        {
            session.Send(GuildPacket.ErrorNotice(GuildErrorNotice.GuildWithSameNameExists));
            return;
        }
        Guild newGuild = new(guildName, session.Player);

        GameServer.GuildManager.AddGuild(newGuild);

        session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag2(session.Player, guildName));
        session.Send(GuildPacket.Create(guildName));

        var inviter = ""; // nobody because nobody invited the guild leader

        var member = newGuild.Members.FirstOrDefault(x => x.Player == session.Player);
        session.Send(GuildPacket.UpdateGuild(newGuild));
        session.Send(GuildPacket.MemberBroadcastJoinNotice(member, inviter, false));
        session.Send(GuildPacket.MemberJoin(session.Player));

        // Remove any applications
        foreach (var application in session.Player.GuildApplications)
        {
            var guild = GameServer.GuildManager.GetGuildById(application.GuildId);
            application.Remove(session.Player, guild);
        }
        DatabaseManager.Characters.Update(session.Player);
    }

    private static void HandleDisband(GameSession session)
    {
        var guild = GameServer.GuildManager.GetGuildByLeader(session.Player);
        if (guild == null)
        {
            return;
        }

        // Remove any applications
        if (guild.Applications.Count > 0)
        {
            foreach (var application in guild.Applications)
            {
                var player = GameServer.PlayerManager.GetPlayerById(application.CharacterId);
                if (player == null)
                {
                    continue;
                }
                application.Remove(player, guild);
                // TODO: Send mail to player as rejected auto message
            }
        }
        session.Send(GuildPacket.DisbandConfirm());
        session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag(session.Player));
        guild.RemoveMember(session.Player);
        GameServer.GuildManager.RemoveGuild(guild);
        DatabaseManager.Guilds.Delete(guild.Id);
    }

    private static void HandleInvite(GameSession session, IPacketReader packet)
    {
        var targetPlayer = packet.ReadUnicodeString();

        var guild = GameServer.GuildManager.GetGuildByLeader(session.Player);
        if (guild == null)
        {
            return;
        }

        var playerInvited = GameServer.PlayerManager.GetPlayerByName(targetPlayer);
        if (playerInvited == null)
        {
            session.Send(GuildPacket.ErrorNotice(GuildErrorNotice.UnableToSendInvite));
        }

        if (playerInvited.Guild != null)
        {
            session.Send(GuildPacket.ErrorNotice(GuildErrorNotice.CharacterIsAlreadyInAGuild));
            return;
        }

        if (guild.Members.Count >= guild.Capacity)
        {
            //TODO Plug in 'full guild' error packets
            return;
        }

        session.Send(GuildPacket.InviteConfirm(playerInvited));
        playerInvited.Session.Send(GuildPacket.SendInvite(session.Player, playerInvited, guild));

    }

    private static void HandleInviteResponse(GameSession session, IPacketReader packet)
    {
        var guildId = packet.ReadLong();
        var guildName = packet.ReadUnicodeString();
        packet.ReadShort();
        var inviterName = packet.ReadUnicodeString();
        var inviteeName = packet.ReadUnicodeString();
        var response = packet.ReadByte(); // 01 accept 

        var guild = GameServer.GuildManager.GetGuildById(guildId);
        if (guild == null)
        {
            return;
        }

        var inviter = GameServer.PlayerManager.GetPlayerByName(inviterName);
        if (inviter == null)
        {
            return;
        }

        if (response == 00)
        {
            inviter.Session.Send(GuildPacket.InviteNotification(inviteeName, 256));
            session.Send(GuildPacket.InviteResponseConfirm(inviter, session.Player, guild, response));
            return;
        }

        guild.AddMember(session.Player);
        var member = guild.Members.FirstOrDefault(x => x.Player == session.Player);
        if (member == null)
        {
            return;
        }

        inviter.Session.Send(GuildPacket.InviteNotification(inviteeName, response));
        session.Send(GuildPacket.InviteResponseConfirm(inviter, session.Player, guild, response));
        session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag2(session.Player, guildName));
        guild.BroadcastPacketGuild(GuildPacket.MemberBroadcastJoinNotice(member, inviterName, true));
        guild.BroadcastPacketGuild(GuildPacket.MemberJoin(session.Player), session);
        session.Send(GuildPacket.UpdateGuild(guild));
    }

    private static void HandleLeave(GameSession session)
    {
        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        session.Send(GuildPacket.LeaveConfirm());
        session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag(session.Player));
        guild.BroadcastPacketGuild(GuildPacket.MemberLeaveNotice(session.Player));
        guild.RemoveMember(session.Player);
    }

    private static void HandleKick(GameSession session, IPacketReader packet)
    {
        var target = packet.ReadUnicodeString();

        var targetPlayer = GameServer.PlayerManager.GetPlayerByName(target);
        if (targetPlayer == null)
        {
            return;
        }

        var guild = GameServer.GuildManager.GetGuildByLeader(session.Player);
        if (guild == null)
        {
            return;
        }

        if (targetPlayer.CharacterId == guild.LeaderCharacterId)
        {
            //TODO: Error packets
            return;
        }

        var selfPlayer = guild.Members.FirstOrDefault(x => x.Player == session.Player);
        if (selfPlayer == null)
        {
            return;
        }

        if (!((GuildRights) guild.Ranks[selfPlayer.Rank].Rights).HasFlag(GuildRights.CanInvite))
        {
            return;
        }

        session.Send(GuildPacket.KickConfirm(targetPlayer));
        if (targetPlayer.Session != null)
        {
            targetPlayer.Session.Send(GuildPacket.KickNotification(session.Player));
            targetPlayer.Session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag(targetPlayer));
        }
        guild.RemoveMember(targetPlayer);
        guild.BroadcastPacketGuild(GuildPacket.KickMember(targetPlayer, session.Player));
    }

    private static void HandleRankChange(GameSession session, IPacketReader packet)
    {
        var memberName = packet.ReadUnicodeString();
        var rank = packet.ReadByte();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null || session.Player.CharacterId != guild.LeaderCharacterId)
        {
            return;
        }

        var member = guild.Members.First(x => x.Player.Name == memberName);
        if (member == null || member.Rank == rank)
        {
            return;
        }

        member.Rank = rank;
        session.Send(GuildPacket.RankChangeConfirm(memberName, rank));
        guild.BroadcastPacketGuild(GuildPacket.RankChangeNotice(session.Player.Name, memberName, rank));
    }

    private static void HandlePlayerMessage(GameSession session, IPacketReader packet)
    {
        var message = packet.ReadUnicodeString();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var member = guild.Members.FirstOrDefault(x => x.Player == session.Player);
        if (member == null)
        {
            return;
        }

        member.Motto = message;
        guild.BroadcastPacketGuild(GuildPacket.UpdatePlayerMessage(session.Player, message));
    }

    private static void HandleCheckIn(GameSession session)
    {
        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }
        var member = guild.Members.First(x => x.Player == session.Player);

        // Check if attendance timestamp is today
        var date = DateTimeOffset.FromUnixTimeSeconds(member.AttendanceTimestamp);
        if (date == DateTime.Today)
        {
            return;
        }

        var contributionAmount = GuildContributionMetadataStorage.GetContributionAmount("attend");
        var property = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        member.AddContribution(contributionAmount);
        member.AttendanceTimestamp = TimeInfo.Now() + Environment.TickCount;
        session.Send(GuildPacket.CheckInBegin());

        Item guildCoins = new(30000861)
        {
            Rarity = 4,
            Amount = property.AttendGuildCoin
        };

        session.Player.Inventory.AddItem(session, guildCoins, true);
        guild.AddExp(session, property.AttendExp);
        guild.ModifyFunds(session, property, property.AttendFunds);
        guild.BroadcastPacketGuild(GuildPacket.UpdatePlayerContribution(member, contributionAmount));
        session.Send(GuildPacket.FinishCheckIn(member));
    }

    private static void HandleTransferLeader(GameSession session, IPacketReader packet)
    {
        var target = packet.ReadUnicodeString();

        var newLeader = GameServer.PlayerManager.GetPlayerByName(target);
        if (newLeader == null)
        {
            return;
        }

        var oldLeader = session.Player;

        var guild = GameServer.GuildManager.GetGuildByLeader(oldLeader);
        if (guild == null || guild.LeaderCharacterId != oldLeader.CharacterId)
        {
            return;
        }
        var newLeaderMember = guild.Members.FirstOrDefault(x => x.Player.CharacterId == newLeader.CharacterId);
        var oldLeaderMember = guild.Members.FirstOrDefault(x => x.Player.CharacterId == oldLeader.CharacterId);
        newLeaderMember.Rank = 0;
        oldLeaderMember.Rank = 1;
        guild.LeaderCharacterId = newLeader.CharacterId;
        guild.LeaderAccountId = newLeader.AccountId;
        guild.LeaderName = newLeader.Name;

        session.Send(GuildPacket.TransferLeaderConfirm(newLeader));
        guild.BroadcastPacketGuild(GuildPacket.AssignNewLeader(newLeader, oldLeader));
        guild.AssignNewLeader(oldLeader, newLeader);
    }

    private static void HandleGuildNotice(GameSession session, IPacketReader packet)
    {
        packet.ReadByte();
        var notice = packet.ReadUnicodeString();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var member = guild.Members.FirstOrDefault(x => x.Player == session.Player);
        if (member == null)
        {
            return;
        }

        if (!((GuildRights) guild.Ranks[member.Rank].Rights).HasFlag(GuildRights.CanGuildNotice))
        {
            return;
        }

        session.Send(GuildPacket.GuildNoticeConfirm(notice));
        guild.BroadcastPacketGuild(GuildPacket.GuildNoticeChange(session.Player, notice));
    }

    private static void HandleUpdateRank(GameSession session, IPacketReader packet)
    {
        var rankIndex = packet.ReadByte();
        var rankIndex2 = packet.ReadByte(); // repeat
        var rankName = packet.ReadUnicodeString();
        var rights = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildByLeader(session.Player);
        if (guild == null || guild.LeaderCharacterId != session.Player.CharacterId)
        {
            return;
        }

        guild.Ranks[rankIndex].Name = rankName;
        guild.Ranks[rankIndex].Rights = rights;
        session.Send(GuildPacket.UpdateRankConfirm(guild, rankIndex));
        guild.BroadcastPacketGuild(GuildPacket.UpdateRankNotice(guild, rankIndex));
    }

    private static void HandleListGuild(GameSession session, IPacketReader packet)
    {
        var toggle = packet.ReadBool();

        var guild = GameServer.GuildManager.GetGuildByLeader(session.Player);
        if (guild == null)
        {
            return;
        }

        guild.Searchable = toggle;
        session.Send(GuildPacket.ListGuildConfirm(toggle));
        session.Send(GuildPacket.ListGuildUpdate(session.Player, toggle));
    }

    private static void HandleSubmitApplication(GameSession session, IPacketReader packet)
    {
        var guildId = packet.ReadLong();

        if (session.Player.GuildApplications.Count >= 10)
        {
            return;
        }

        var guild = GameServer.GuildManager.GetGuildById(guildId);
        if (guild == null)
        {
            return;
        }

        GuildApplication application = new(session.Player.CharacterId, guild.Id);
        application.Add(session.Player, guild);

        session.Send(GuildPacket.SubmitApplication(application, guild.Name));
        foreach (var member in guild.Members)
        {
            if (((GuildRights) guild.Ranks[member.Rank].Rights).HasFlag(GuildRights.CanInvite))
            {
                member.Player.Session.Send(GuildPacket.SendApplication(application, session.Player));
            }
        }
    }

    private static void HandleWithdrawApplication(GameSession session, IPacketReader packet)
    {
        var guildApplicationId = packet.ReadLong();

        var application = session.Player.GuildApplications.FirstOrDefault(x => x.Id == guildApplicationId);
        if (application == null)
        {
            return;
        }

        var guild = GameServer.GuildManager.GetGuildById(application.GuildId);
        if (guild == null)
        {
            return;
        }

        application.Remove(session.Player, guild);

        session.Send(GuildPacket.WithdrawApplicationPlayerUpdate(application, guild.Name));
        foreach (var member in guild.Members)
        {
            if (((GuildRights) guild.Ranks[member.Rank].Rights).HasFlag(GuildRights.CanInvite))
            {
                member.Player.Session.Send(GuildPacket.WithdrawApplicationGuildUpdate(application.Id));
            }
        }
    }

    private static void HandleApplicationResponse(GameSession session, IPacketReader packet)
    {
        var guildApplicationId = packet.ReadLong();
        var response = packet.ReadByte();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var application = guild.Applications.FirstOrDefault(x => x.Id == guildApplicationId);
        if (application == null)
        {
            return;
        }

        var applier = GameServer.PlayerManager.GetPlayerById(application.CharacterId);

        session.Send(GuildPacket.ApplicationResponse(guildApplicationId, applier.Name, response));
        if (response == 1)
        {
            session.Send(GuildPacket.InviteNotification(applier.Name, response));
        }
        guild.BroadcastPacketGuild(GuildPacket.ApplicationResponseBroadcastNotice(session.Player.Name, applier.Name, response, guildApplicationId));
        application.Remove(applier, guild);

        if (applier.Session != null)
        {
            applier.Session.Send(GuildPacket.ApplicationResponseToApplier(guild.Name, guildApplicationId, response));
        }

        if (response == 0)
        {
            if (applier.Session != null)
            {
                // TODO: Send System mail for rejection
            }
            return;
        }

        guild.AddMember(applier);
        if (applier.Session != null)
        {
            applier.Session.Send(GuildPacket.InviteResponseConfirm(session.Player, applier, guild, response));
            applier.Session.FieldManager.BroadcastPacket(GuildPacket.UpdateGuildTag2(applier, guild.Name));
        }

        var member = guild.Members.FirstOrDefault(x => x.Player == applier);
        guild.BroadcastPacketGuild(GuildPacket.MemberBroadcastJoinNotice(member, session.Player.Name, false));
        guild.BroadcastPacketGuild(GuildPacket.MemberJoin(applier));
        guild.BroadcastPacketGuild(GuildPacket.UpdateGuild(guild));
    }

    private static void HandleLoadApplications(GameSession session)
    {
        session.Send(GuildPacket.LoadApplications(session.Player));
    }

    private static void HandleLoadGuildList(GameSession session, IPacketReader packet)
    {
        var focusAttributes = packet.ReadInt();

        var guildList = GameServer.GuildManager.GetGuildList();

        if (guildList.Count == 0)
        {
            return;
        }

        if (focusAttributes == -1)
        {
            session.Send(GuildPacket.DisplayGuildList(guildList));
            return;
        }

        // TODO: Filter guilds with focusAttributes
        session.Send(GuildPacket.DisplayGuildList(guildList));
    }

    private static void HandleSearchGuildByName(GameSession session, IPacketReader packet)
    {
        var name = packet.ReadUnicodeString();

        var guildList = GameServer.GuildManager.GetGuildListByName(name);
        session.Send(GuildPacket.DisplayGuildList(guildList));
    }

    private static void HandleUseBuff(GameSession session, IPacketReader packet)
    {
        var buffId = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var buffLevel = guild.Buffs.FirstOrDefault(x => x.Id == buffId).Level;

        var buff = GuildBuffMetadataStorage.GetGuildBuffLevelData(buffId, buffLevel);
        if (buff == null)
        {
            return;
        }

        if (buffId > 1000)
        {
            if (!session.Player.Wallet.Meso.Modify(-buff.Cost))
            {
                return;
            }
        }
        else
        {
            if (buff.Cost > guild.Funds)
            {
                return;
            }
            guild.Funds -= buff.Cost;
        }
        session.Send(GuildPacket.ActivateBuff(buffId));
        session.Send(GuildPacket.UseBuffNotice(buffId));
    }

    private static void HandleUpgradeBuff(GameSession session, IPacketReader packet)
    {
        var buffId = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var buff = guild.Buffs.First(x => x.Id == buffId);

        // get next level's data
        var metadata = GuildBuffMetadataStorage.GetGuildBuffLevelData(buffId, buff.Level + 1);

        var guildProperty = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        if (guildProperty.Level < metadata.LevelRequirement)
        {
            return;
        }

        if (guild.Funds < metadata.UpgradeCost)
        {
            return;
        }

        guild.ModifyFunds(session, guildProperty, -metadata.UpgradeCost);
        buff.Level++;
        guild.BroadcastPacketGuild(GuildPacket.UpgradeBuff(buffId, buff.Level, session.Player.Name));
    }

    private static void HandleUpgradeHome(GameSession session, IPacketReader packet)
    {
        var themeId = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null || guild.LeaderCharacterId != session.Player.CharacterId)
        {
            return;
        }

        var houseMetadata = GuildHouseMetadataStorage.GetMetadataByThemeId(guild.HouseRank + 1, themeId);
        if (houseMetadata == null)
        {
            return;
        }

        var guildProperty = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        if (guildProperty.Level < houseMetadata.RequiredLevel ||
            guild.Funds < houseMetadata.UpgradeCost)
        {
            return;
        }

        guild.ModifyFunds(session, guildProperty, -houseMetadata.UpgradeCost);
        guild.HouseRank = houseMetadata.Level;
        guild.HouseTheme = houseMetadata.Theme;
        guild.BroadcastPacketGuild(GuildPacket.ChangeHouse(session.Player.Name, guild.HouseRank, guild.HouseTheme)); // need to confirm if this is the packet used when upgrading
    }

    private static void HandleChangeHomeTheme(GameSession session, IPacketReader packet)
    {
        var themeId = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null || guild.LeaderCharacterId != session.Player.CharacterId)
        {
            return;
        }

        var houseMetadata = GuildHouseMetadataStorage.GetMetadataByThemeId(guild.HouseRank, themeId);
        if (houseMetadata == null)
        {
            return;
        }

        var guildProperty = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        if (guild.Funds < houseMetadata.UpgradeCost)
        {
            return;
        }

        guild.ModifyFunds(session, guildProperty, -houseMetadata.RethemeCost);
        guild.HouseTheme = houseMetadata.Theme;
        guild.BroadcastPacketGuild(GuildPacket.ChangeHouse(session.Player.Name, guild.HouseRank, guild.HouseTheme));
    }

    private static void HandleEnterHouse(GameSession session)
    {
        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var mapid = GuildHouseMetadataStorage.GetFieldId(guild.HouseRank, guild.HouseTheme);
        if (mapid == 0)
        {
            return;
        }

        session.Player.Warp(mapid, instanceId: guild.Id);
    }

    private static void HandleGuildDonate(GameSession session, IPacketReader packet)
    {
        var donateQuantity = packet.ReadInt();
        var donationAmount = donateQuantity * 10000;

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var guildProperty = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        var member = guild.Members.First(x => x.Player == session.Player);
        if (member.DailyDonationCount >= guildProperty.DonationMax)
        {
            return;
        }

        if (!session.Player.Wallet.Meso.Modify(-donationAmount))
        {
            session.Send(GuildPacket.ErrorNotice(GuildErrorNotice.NotEnoughMesos));
            return;
        }

        Item coins = new(30000861)
        {
            Rarity = 4,
            Amount = guildProperty.DonateGuildCoin * donateQuantity
        };

        session.Player.Inventory.AddItem(session, coins, true);

        var contribution = GuildContributionMetadataStorage.GetContributionAmount("donation");

        member.DailyDonationCount += (byte) donateQuantity;
        member.AddContribution(contribution * donateQuantity);
        guild.ModifyFunds(session, guildProperty, donationAmount);
        session.Send(GuildPacket.UpdatePlayerDonation());
        guild.BroadcastPacketGuild(GuildPacket.UpdatePlayerContribution(member, donateQuantity));
    }

    private static void HandleServices(GameSession session, IPacketReader packet)
    {
        var serviceId = packet.ReadInt();

        var guild = GameServer.GuildManager.GetGuildById(session.Player.Guild.Id);
        if (guild == null)
        {
            return;
        }

        var currentLevel = 0;
        var service = guild.Services.FirstOrDefault(x => x.Id == serviceId);
        if (service != null)
        {
            service.Level = currentLevel;
        }

        var serviceMetadata = GuildServiceMetadataStorage.GetMetadata(serviceId, currentLevel);
        if (serviceMetadata == null)
        {
            return;
        }

        var propertyMetadata = GuildPropertyMetadataStorage.GetMetadata(guild.Exp);

        if (guild.HouseRank < serviceMetadata.HouseLevelRequirement ||
            propertyMetadata.Level < serviceMetadata.LevelRequirement ||
            guild.Funds < serviceMetadata.UpgradeCost)
        {
            return;
        }

        guild.ModifyFunds(session, propertyMetadata, -serviceMetadata.UpgradeCost);
        guild.BroadcastPacketGuild(GuildPacket.UpgradeService(session.Player, serviceId, serviceMetadata.Level));
    }
}
