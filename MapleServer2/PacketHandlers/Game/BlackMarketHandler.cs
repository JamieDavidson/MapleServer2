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

    public override void Handle(GameSession session, PacketReader packet)
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
        List<BlackMarketListing> listings = GameServer.BlackMarketManager.GetListingsByCharacterId(session.Player.CharacterId);
        session.Send(BlackMarketPacket.Open(listings));
    }

    private static void HandlePrepareListing(GameSession session, PacketReader packet)
    {
        int itemId = packet.ReadInt();
        int rarity = packet.ReadInt();

        if (!session.Player.Inventory.Items.Any(x => x.Value.Id == itemId && x.Value.Rarity == rarity))
        {
            return;
        }

        int npcShopPrice = 0;

        ShopItem shopItem = DatabaseManager.ShopItems.FindByItemId(itemId);
        if (shopItem != null)
        {
            npcShopPrice = shopItem.Price;
        }

        session.Send(BlackMarketPacket.PrepareListing(itemId, rarity, npcShopPrice));
    }

    private static void HandleCreateListing(GameSession session, PacketReader packet)
    {
        long itemUid = packet.ReadLong();
        long price = packet.ReadLong();
        int quantity = packet.ReadInt();

        if (!session.Player.Inventory.Items.ContainsKey(itemUid))
        {
            session.Send(BlackMarketPacket.Error(BlackMarketErrors.ItemNotInInventory));
            return;
        }

        double depositRate = 0.01; // 1% deposit rate
        int maxDeposit = 100000;

        int calculatedDeposit = (int) (depositRate * (price * quantity));
        int deposit = Math.Min(calculatedDeposit, maxDeposit);

        if (!session.Player.Wallet.Meso.Modify(-deposit))
        {
            return;
        }

        Item item = session.Player.Inventory.Items[itemUid];
        if (item.Amount < quantity)
        {
            return;
        }

        if (item.Amount > quantity)
        {
            item.TrySplit(quantity, out Item newStack);
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

    private static void HandleCancelListing(GameSession session, PacketReader packet)
    {
        long listingId = packet.ReadLong();

        BlackMarketListing listing = GameServer.BlackMarketManager.GetListingById(listingId);
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

    private static void HandleSearch(GameSession session, PacketReader packet)
    {
        int minCategoryId = packet.ReadInt();
        int maxCategoryId = packet.ReadInt();
        int minLevel = packet.ReadInt();
        int maxLevel = packet.ReadInt();
        JobFlag job = (JobFlag) packet.ReadInt();
        int rarity = packet.ReadInt();
        int minEnchantLevel = packet.ReadInt();
        int maxEnchantLevel = packet.ReadInt();
        byte minSockets = packet.ReadByte();
        byte maxSockets = packet.ReadByte();
        string name = packet.ReadUnicodeString();
        int startPage = packet.ReadInt();
        long sort = packet.ReadLong();
        packet.ReadShort();
        bool additionalOptionsEnabled = packet.ReadBool();

        List<ItemStat> stats = new();
        if (additionalOptionsEnabled)
        {
            packet.ReadByte(); // always 1
            for (int i = 0; i < 3; i++)
            {
                int statId = packet.ReadInt();
                int value = packet.ReadInt();
                if (value == 0)
                {
                    continue;
                }

                ItemStat stat = ReadStat(statId, value);
                if (stat == null)
                {
                    continue;
                }
                stats.Add(stat);
            }
        }

        List<string> itemCategories = BlackMarketTableMetadataStorage.GetItemCategories(minCategoryId, maxCategoryId);
        List<BlackMarketListing> searchResults = GameServer.BlackMarketManager.GetSearchedListings(itemCategories, minLevel, maxLevel, rarity, name, job,
            minEnchantLevel, maxEnchantLevel, minSockets, maxSockets, startPage, sort, additionalOptionsEnabled, stats);

        session.Send(BlackMarketPacket.SearchResults(searchResults));
    }

    private static void HandlePurchase(GameSession session, PacketReader packet)
    {
        long listingId = packet.ReadLong();
        int amount = packet.ReadInt();

        BlackMarketListing listing = GameServer.BlackMarketManager.GetListingById(listingId);
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
        bool removeListing = false;
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
            float percent = (float) (value + 5) / 10000;
            StatId attribute = (StatId) (statId - 1000);
            return new NormalStat(attribute, 0, percent);
        }

        // Special Stat with percent value
        if (statId >= 11000)
        {
            float percent = (float) (value + 5) / 10000;
            SpecialStatId attribute = (SpecialStatId) (statId - 11000);
            return new SpecialStat(attribute, 0, percent);
        }

        // Normal Stat with flat value
        return new NormalStat((StatId) statId, value, 0);
    }
}
