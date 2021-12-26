using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Enums;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BlackMarketHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BLACK_MARKET;

    private static class BlackMarketOperations
    {
        public const byte Open = 0x1;
        public const byte CreateListing = 0x2;
        public const byte CancelListing = 0x3;
        public const byte Search = 0x4;
        public const byte Purchase = 0x5;
        public const byte PrepareListing = 0x8;
    }

    private static class BlackMarketErrors
    {
        public const byte FailedToListItem = 0x05;
        public const byte ItemNotInInventory = 0x0E;
        public const byte ItemCannotBeListed = 0x20;
        public const byte OneMinuteRestriction = 0x25;
        public const byte Fatigue = 0x26;
        public const byte CannotUseBlackMarket = 0x27;
        public const byte QuantityNotAvailable = 0x29;
        public const byte CannotPurchaseOwnItems = 0x2A;
        public const byte RequiredLevelToList = 0x2B;
        public const byte RequiredLevelToBuy = 0x2C;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case BlackMarketOperations.Open:
                HandleOpen(session);
                break;
            case BlackMarketOperations.CreateListing:
                HandleCreateListing(session, packet);
                break;
            case BlackMarketOperations.CancelListing:
                HandleCancelListing(session, packet);
                break;
            case BlackMarketOperations.Search:
                HandleSearch(session, packet);
                break;
            case BlackMarketOperations.Purchase:
                HandlePurchase(session, packet);
                break;
            case BlackMarketOperations.PrepareListing:
                HandlePrepareListing(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        var listings = GameServer.BlackMarketManager.GetListingsByCharacterId(session.Player.CharacterId);
        session.Send(BlackMarketPacket.Open(listings));
    }

    private static void HandlePrepareListing(GameSession session, IPacketReader packet)
    {
        var itemId = packet.ReadInt();
        var rarity = packet.ReadInt();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithRarity(itemId, rarity))
        {
            return;
        }

        var npcShopPrice = 0;

        var shopItem = DatabaseManager.ShopItems.FindByItemId(itemId);
        if (shopItem != null)
        {
            npcShopPrice = shopItem.Price;
        }

        session.Send(BlackMarketPacket.PrepareListing(itemId, rarity, npcShopPrice));
    }

    private static void HandleCreateListing(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var price = packet.ReadLong();
        var quantity = packet.ReadInt();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            session.Send(BlackMarketPacket.Error(BlackMarketErrors.ItemNotInInventory));
            return;
        }

        var depositRate = 0.01; // 1% deposit rate
        var maxDeposit = 100000;

        var calculatedDeposit = (int) (depositRate * (price * quantity));
        var deposit = Math.Min(calculatedDeposit, maxDeposit);

        if (!session.Player.Wallet.Meso.Modify(-deposit))
        {
            return;
        }

        var item = inventory.GetItemByUid(itemUid);
        if (item.Amount < quantity)
        {
            return;
        }

        if (item.Amount > quantity)
        {
            item.TrySplit(quantity, out var newStack);
            session.Send(ItemInventoryPacket.Update(item.Uid, item.Amount));
            item = newStack;
        }
        else
        {
            session.Player.Inventory.ConsumeItem(session, item.Uid, quantity);
        }

        BlackMarketListing listing = new(session.Player, item, quantity, price, deposit);
        session.Send(BlackMarketPacket.CreateListing(listing));
    }

    private static void HandleCancelListing(GameSession session, IPacketReader packet)
    {
        var listingId = packet.ReadLong();

        var listing = GameServer.BlackMarketManager.GetListingById(listingId);
        if (listing == null)
        {
            return;
        }

        if (listing.OwnerCharacterId != session.Player.CharacterId)
        {
            return;
        }

        DatabaseManager.BlackMarketListings.Delete(listingId);
        GameServer.BlackMarketManager.RemoveListing(listing);
        session.Send(BlackMarketPacket.CancelListing(listing, false));
        MailHelper.BlackMarketCancellation(listing);
    }

    private static void HandleSearch(GameSession session, IPacketReader packet)
    {
        var minCategoryId = packet.ReadInt();
        var maxCategoryId = packet.ReadInt();
        var minLevel = packet.ReadInt();
        var maxLevel = packet.ReadInt();
        var job = (JobFlag) packet.ReadInt();
        var rarity = packet.ReadInt();
        var minEnchantLevel = packet.ReadInt();
        var maxEnchantLevel = packet.ReadInt();
        var minSockets = packet.ReadByte();
        var maxSockets = packet.ReadByte();
        var name = packet.ReadUnicodeString();
        var startPage = packet.ReadInt();
        var sort = packet.ReadLong();
        packet.ReadShort();
        var additionalOptionsEnabled = packet.ReadBool();

        List<ItemStat> stats = new();
        if (additionalOptionsEnabled)
        {
            packet.ReadByte(); // always 1
            for (var i = 0; i < 3; i++)
            {
                var statId = packet.ReadInt();
                var value = packet.ReadInt();
                if (value == 0)
                {
                    continue;
                }

                var stat = ReadStat(statId, value);
                if (stat == null)
                {
                    continue;
                }
                stats.Add(stat);
            }
        }

        var itemCategories = BlackMarketTableMetadataStorage.GetItemCategories(minCategoryId, maxCategoryId);
        var searchResults = GameServer.BlackMarketManager.GetSearchedListings(itemCategories, minLevel, maxLevel, rarity, name, job,
            minEnchantLevel, maxEnchantLevel, minSockets, maxSockets, startPage, sort, additionalOptionsEnabled, stats);

        session.Send(BlackMarketPacket.SearchResults(searchResults));
    }

    private static void HandlePurchase(GameSession session, IPacketReader packet)
    {
        var listingId = packet.ReadLong();
        var amount = packet.ReadInt();

        var listing = GameServer.BlackMarketManager.GetListingById(listingId);
        if (listing == null)
        {
            return;
        }

        if (listing.OwnerAccountId == session.Player.AccountId)
        {
            session.Send(BlackMarketPacket.Error(BlackMarketErrors.CannotPurchaseOwnItems));
            return;
        }

        if (listing.Item.Amount < amount)
        {
            session.Send(BlackMarketPacket.Error(BlackMarketErrors.QuantityNotAvailable));
            return;
        }

        if (!session.Player.Wallet.Meso.Modify(-listing.Price * amount))
        {
            return;
        }

        Item purchasedItem;
        var removeListing = false;
        if (listing.Item.Amount == amount)
        {
            purchasedItem = listing.Item;
            GameServer.BlackMarketManager.RemoveListing(listing);
            DatabaseManager.BlackMarketListings.Delete(listing.Id);
            removeListing = true;
        }
        else
        {
            listing.Item.Amount -= amount;
            Item newItem = new(listing.Item)
            {
                Amount = amount
            };
            newItem.Uid = DatabaseManager.Items.Insert(newItem);
            purchasedItem = newItem;
        }

        MailHelper.BlackMarketTransaction(purchasedItem, listing, session.Player.CharacterId, listing.Price, removeListing);
        session.Send(BlackMarketPacket.Purchase(listingId, amount));
    }

    private static ItemStat ReadStat(int statId, int value)
    {
        // Normal Stat with percent value
        if (statId >= 1000 && statId < 11000)
        {
            var percent = (float) (value + 5) / 10000;
            var attribute = (StatId) (statId - 1000);
            return new NormalStat(attribute, 0, percent);
        }

        // Special Stat with percent value
        if (statId >= 11000)
        {
            var percent = (float) (value + 5) / 10000;
            var attribute = (SpecialStatId) (statId - 11000);
            return new SpecialStat(attribute, 0, percent);
        }

        // Normal Stat with flat value
        return new NormalStat((StatId) statId, value, 0);
    }
}
