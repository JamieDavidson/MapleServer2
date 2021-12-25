using Maple2Storage.Enums;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class UgcHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.UGC;

    private static class UgcOperations
    {
        public const byte CreateUgcItem = 0x01;
        public const byte AddUgcItem = 0x03;
        public const byte ProfilePicture = 0x0B;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case UgcOperations.CreateUgcItem:
                HandleCreateUGCItem(session, packet);
                break;
            case UgcOperations.AddUgcItem:
                HandleAddUgcItem(session, packet);
                break;
            case UgcOperations.ProfilePicture:
                HandleProfilePicture(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleCreateUGCItem(GameSession session, PacketReader packet)
    {
        packet.ReadLong();
        packet.ReadByte();
        packet.ReadByte();
        packet.ReadByte();
        packet.ReadInt();
        long accountId = packet.ReadLong();
        long characterId = packet.ReadLong();
        packet.ReadLong();
        packet.ReadInt();
        packet.ReadShort();
        packet.ReadShort();
        long unk = packet.ReadLong(); // some kind of UID
        int itemId = packet.ReadInt();
        int amount = packet.ReadInt();
        string itemName = packet.ReadUnicodeString();
        packet.ReadByte();
        long cost = packet.ReadLong();
        bool useVoucher = packet.ReadBool();

        UgcDesignMetadata metadata = UgcDesignMetadataStorage.GetItem(itemId);
        if (metadata is null)
        {
            return;
        }

        if (useVoucher)
        {
            var inventory = session.Player.Inventory;
            var voucher = inventory.GetItemByTag("FreeDesignCoupon");
            if (voucher is null)
            {
                return;
            }
            session.Player.Inventory.ConsumeItem(session, voucher.Uid, 1);
        }
        else
        {
            switch (metadata.CurrencyType)
            {
                case CurrencyType.Meso when !session.Player.Wallet.Meso.Modify(-cost):
                    session.SendNotice("You don't have enough mesos.");
                    return;
                case CurrencyType.Meret when !session.Player.Account.Meret.Modify(-cost):
                    session.SendNotice("You don't have enough merets.");
                    return;
            }
        }

        Item item = new(itemId, 1)
        {
            Rarity = metadata.Rarity,
            UGC = new(itemName, characterId, session.Player.Name, accountId, metadata.SalePrice)
        };
        DatabaseManager.Items.Update(item);

        session.Send(UgcPacket.CreateUGC(true, item.UGC));
    }

    private static void HandleAddUgcItem(GameSession session, PacketReader packet)
    {
        packet.ReadByte();
        packet.ReadByte();
        packet.ReadByte();
        packet.ReadInt();
        long accountId = packet.ReadLong();
        long characterId = packet.ReadLong();
        packet.ReadInt();
        long ugcUid = packet.ReadLong();
        string ugcGuid = packet.ReadUnicodeString();

        if (accountId != session.Player.Account.Id || characterId != session.Player.CharacterId || ugcUid == 0)
        {
            return;
        }

        Item item = DatabaseManager.Items.FindByUGCUid(ugcUid);
        if (item is null)
        {
            return;
        }
        item.SetMetadataValues();

        session.Player.Inventory.AddItem(session, item, true);
        session.Send(UgcPacket.UpdateUGCItem(session.Player.FieldPlayer, item));
        session.Send(UgcPacket.SetItemUrl(item.UGC));
    }

    private static void HandleProfilePicture(GameSession session, PacketReader packet)
    {
        string path = packet.ReadUnicodeString();
        session.Player.ProfileUrl = path;
        DatabaseManager.Characters.UpdateProfileUrl(session.Player.CharacterId, path);

        session.FieldManager.BroadcastPacket(UgcPacket.SetProfilePictureURL(session.Player.FieldPlayer.ObjectId, session.Player.CharacterId, path));
    }
}
