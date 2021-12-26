using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class PremiumClubHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.PREMIUM_CLUB;

    private static class PremiumClubOperations
    {
        public const byte Open = 0x1;
        public const byte ClaimItems = 0x2;
        public const byte OpenPurchaseWindow = 0x3;
        public const byte PurchaseMembership = 0x4;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case PremiumClubOperations.Open:
                HandleOpen(session);
                break;
            case PremiumClubOperations.ClaimItems:
                HandleClaimItems(session, packet);
                break;
            case PremiumClubOperations.OpenPurchaseWindow:
                HandleOpenPurchaseWindow(session);
                break;
            case PremiumClubOperations.PurchaseMembership:
                HandlePurchaseMembership(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        session.Send(PremiumClubPacket.Open());
    }

    private static void HandleClaimItems(GameSession session, IPacketReader packet)
    {
        var benefitId = packet.ReadInt();
        session.Send(PremiumClubPacket.ClaimItem(benefitId));

        if (!PremiumClubDailyBenefitMetadataStorage.IsValid(benefitId))
        {
            return;
        }

        var benefit = PremiumClubDailyBenefitMetadataStorage.GetMetadata(benefitId);

        Item benefitRewardItem = new(benefit.ItemId)
        {
            Rarity = benefit.ItemRarity,
            Amount = benefit.ItemAmount
        };

        session.Player.Inventory.AddItem(session, benefitRewardItem, true);

        // TODO only claim once a day
    }

    private static void HandleOpenPurchaseWindow(GameSession session)
    {
        session.Send(PremiumClubPacket.OpenPurchaseWindow());
    }

    private static void HandlePurchaseMembership(GameSession session, IPacketReader packet)
    {
        var vipId = packet.ReadInt();

        if (!PremiumClubPackageMetadataStorage.IsValid(vipId))
        {
            return;
        }

        var vipPackage = PremiumClubPackageMetadataStorage.GetMetadata(vipId);

        if (!session.Player.Account.RemoveMerets(vipPackage.Price))
        {
            return;
        }

        session.Send(PremiumClubPacket.PurchaseMembership(vipId));

        foreach (var item in vipPackage.BonusItem)
        {
            Item bonusItem = new(item.Id)
            {
                Rarity = item.Rarity,
                Amount = item.Amount
            };
            session.Player.Inventory.AddItem(session, bonusItem, true);
        }

        var vipTime = vipPackage.VipPeriod * 3600; // Convert to seconds, as vipPeriod is given as hours

        ActivatePremium(session, vipTime);
    }

    public static void ActivatePremium(GameSession session, long vipTime)
    {
        var expiration = TimeInfo.Now() + vipTime;

        var account = session.Player.Account;
        if (!account.IsVip())
        {
            account.VIPExpiration = expiration;
            session.Send(NoticePacket.Notice(SystemNotice.PremiumActivated, NoticeType.ChatAndFastText));
        }
        else
        {
            account.VIPExpiration += vipTime;
            session.Send(NoticePacket.Notice(SystemNotice.PremiumExtended, NoticeType.ChatAndFastText));
        }
        session.Send(BuffPacket.SendBuff(0, new(100000014, session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId, 1, (int) vipTime, 1)));
        session.Send(PremiumClubPacket.ActivatePremium(session.Player.FieldPlayer, account.VIPExpiration));
    }
}
