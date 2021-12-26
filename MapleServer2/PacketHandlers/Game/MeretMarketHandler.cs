using Maple2Storage.Enums;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MeretMarketHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.MERET_MARKET;

    private static class MeretMarketOperations
    {
        public const byte LoadPersonalListings = 0xB;
        public const byte LoadSales = 0xC;
        public const byte ListItem = 0xD;
        public const byte RemoveListing = 0xE;
        public const byte UnlistItem = 0xF;
        public const byte RelistItem = 0x12;
        public const byte CollectProfit = 0x14;
        public const byte Initialize = 0x16;
        public const byte OpenShop = 0x1B;
        public const byte SendMarketRequest = 0x1D;
        public const byte Purchase = 0x1E;
        public const byte Home = 0x65;
        public const byte OpenDesignShop = 0x66;
        public const byte LoadCart = 0x6B;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case MeretMarketOperations.LoadPersonalListings:
                HandleLoadPersonalListings(session);
                break;
            case MeretMarketOperations.LoadSales:
                HandleLoadSales(session);
                break;
            case MeretMarketOperations.ListItem:
                HandleListItem(session, packet);
                break;
            case MeretMarketOperations.RemoveListing:
                HandleRemoveListing(session, packet);
                break;
            case MeretMarketOperations.UnlistItem:
                HandleUnlistItem(session, packet);
                break;
            case MeretMarketOperations.RelistItem:
                HandleRelistItem(session, packet);
                break;
            case MeretMarketOperations.CollectProfit:
                HandleCollectProfit(session, packet);
                break;
            case MeretMarketOperations.Initialize:
                HandleInitialize(session);
                break;
            case MeretMarketOperations.OpenShop:
                HandleOpenShop(session, packet);
                break;
            case MeretMarketOperations.Purchase:
                HandlePurchase(session, packet);
                break;
            case MeretMarketOperations.Home:
                HandleHome(session);
                break;
            case MeretMarketOperations.OpenDesignShop:
                HandleOpenDesignShop(session);
                break;
            case MeretMarketOperations.LoadCart:
                HandleLoadCart(session);
                break;
            case MeretMarketOperations.SendMarketRequest:
                HandleSendMarketRequest(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleLoadPersonalListings(GameSession session)
    {
        var items = GameServer.UGCMarketManager.GetItemsByCharacterId(session.Player.CharacterId);

        // TODO: Possibly a better way to implement updating item status?
        foreach (var item in items)
        {
            if (item.ListingExpirationTimestamp < TimeInfo.Now() && item.Status == UGCMarketListingStatus.Active)
            {
                item.Status = UGCMarketListingStatus.Expired;
                DatabaseManager.UGCMarketItems.Update(item);
            }
        }
        session.Send(MeretMarketPacket.LoadPersonalListings(items));
    }

    private static void HandleLoadSales(GameSession session)
    {
        var sales = GameServer.UGCMarketManager.GetSalesByCharacterId(session.Player.CharacterId);
        session.Send(MeretMarketPacket.LoadSales(sales));
    }

    private static void HandleListItem(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var salePrice = packet.ReadLong();
        var promote = packet.ReadBool();
        var tags = packet.ReadUnicodeString().Split(",").ToList();
        var description = packet.ReadUnicodeString();
        var listingFee = packet.ReadLong();

        // TODO: Check if item is a ugc block and not an item. Find item from their block inventory
        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }

        var item = inventory.GetItemByUid(itemUid);

        if (item.UGC is null || item.UGC.CharacterId != session.Player.CharacterId)
        {
            return;
        }

        if (salePrice < item.UGC.SalePrice || salePrice > long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopSellMaxPrice")))
        {
            return;
        }

        var totalFee = GetListingFee(session.Player.CharacterId, promote);
        if (!HandleMarketItemPay(session, totalFee, MeretMarketCurrencyType.Meret))
        {
            return;
        }

        UGCMarketItem marketItem = new(item, salePrice, session.Player, tags, description, promote);

        session.Send(MeretMarketPacket.ListItem(marketItem));
        session.Send(MeretMarketPacket.UpdateExpiration(marketItem));
    }

    private static long GetListingFee(long characterId, bool promote)
    {
        var activeListingsCount = GameServer.UGCMarketManager.GetItemsByCharacterId(characterId).Count;
        var baseFee = long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopBaseListFee"));
        var fee = baseFee + activeListingsCount * 100;

        // Max fee being 390
        fee = Math.Min(fee, baseFee + 200);
        if (promote)
        {
            fee += long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopAdFeeMerat"));
        }
        return fee;
    }

    private static void HandleRemoveListing(GameSession session, IPacketReader packet)
    {
        packet.ReadInt(); // 0
        var ugcMarketItemId = packet.ReadLong();
        packet.ReadLong(); // duplicate id read?

        var item = GameServer.UGCMarketManager.FindItemById(ugcMarketItemId);
        if (item is null || item.SellerCharacterId != session.Player.CharacterId)
        {
            return;
        }

        session.Send(MeretMarketPacket.RemoveListing(item.Id));
        DatabaseManager.UGCMarketItems.Delete(item.Id);
        GameServer.UGCMarketManager.RemoveListing(item);
    }

    private static void HandleUnlistItem(GameSession session, IPacketReader packet)
    {
        packet.ReadInt(); // 0
        var ugcMarketItemId = packet.ReadLong();
        packet.ReadLong(); // duplicate id read?

        var item = GameServer.UGCMarketManager.FindItemById(ugcMarketItemId);
        if (item is null || item.SellerCharacterId != session.Player.CharacterId)
        {
            return;
        }

        item.ListingExpirationTimestamp = long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopExpiredListingRemovalHour")) * 3600 + TimeInfo.Now();
        item.PromotionExpirationTimestamp = 0;
        item.Status = UGCMarketListingStatus.Expired;
        DatabaseManager.UGCMarketItems.Update(item);
        session.Send(MeretMarketPacket.UpdateExpiration(item));
    }

    private static void HandleRelistItem(GameSession session, IPacketReader packet)
    {
        var ugcMarketItemId = packet.ReadLong();
        var price = packet.ReadLong();
        var promote = packet.ReadBool();
        var tags = packet.ReadUnicodeString().Split(",").ToList();
        var description = packet.ReadUnicodeString();
        var listingFee = packet.ReadLong();

        var item = GameServer.UGCMarketManager.FindItemById(ugcMarketItemId);
        if (item is null || item.SellerCharacterId != session.Player.CharacterId || item.ListingExpirationTimestamp < TimeInfo.Now())
        {
            return;
        }

        var totalFee = GetListingFee(session.Player.CharacterId, promote);
        if (!HandleMarketItemPay(session, totalFee, MeretMarketCurrencyType.Meret))
        {
            return;
        }

        item.Price = price;
        item.ListingExpirationTimestamp = long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopSaleDay")) * 86400 + TimeInfo.Now();
        if (promote)
        {
            item.PromotionExpirationTimestamp = long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopAdHour")) * 3600 + item.ListingExpirationTimestamp;
        }
        item.Status = UGCMarketListingStatus.Active;
        item.Description = description;
        item.Tags = tags;
        DatabaseManager.UGCMarketItems.Update(item);
        session.Send(MeretMarketPacket.RelistItem(item));
    }

    private static void HandleCollectProfit(GameSession session, IPacketReader packet)
    {
        var saleId = packet.ReadLong();

        var sales = GameServer.UGCMarketManager.GetSalesByCharacterId(session.Player.CharacterId);
        var profitDelayTime = long.Parse(ConstantsMetadataStorage.GetConstant("UGCShopProfitDelayInDays"));
        long totalProfit = 0;
        foreach (var sale in sales)
        {
            if (!(sale.SoldTimestamp + profitDelayTime < TimeInfo.Now()))
            {
                continue;
            }
            totalProfit += sale.Profit;
            GameServer.UGCMarketManager.RemoveSale(sale);
            DatabaseManager.UGCMarketSales.Delete(saleId);
        }

        session.Player.Account.GameMeret.Modify(totalProfit);
        session.Send(MeretsPacket.UpdateMerets(session.Player.Account, totalProfit));
        session.Send(MeretMarketPacket.UpdateProfit(saleId));
    }

    private static void HandleInitialize(GameSession session)
    {
        session.Send(MeretMarketPacket.Initialize());
    }

    private static void HandleOpenShop(GameSession session, IPacketReader packet)
    {
        var category = (MeretMarketCategory) packet.ReadInt();

        var metadata = MeretMarketCategoryMetadataStorage.GetMetadata((int) category);
        if (metadata is null)
        {
            return;
        }

        switch (metadata.Section)
        {
            case MeretMarketSection.PremiumMarket:
                HandleOpenPremiumMarket(session, category);
                break;
            case MeretMarketSection.RedMeretMarket:
                HandleOpenRedMeretMarket();
                break;
            case MeretMarketSection.UGCMarket:
                HandleOpenUGCMarket(session, packet, metadata);
                break;
        }
    }

    private static void HandleOpenPremiumMarket(GameSession session, MeretMarketCategory category)
    {
        var marketItems = DatabaseManager.MeretMarket.FindAllByCategoryId((category));
        if (marketItems is null)
        {
            return;
        }
        session.Send(MeretMarketPacket.LoadPremiumShopCategory(marketItems));
    }

    private static void HandleOpenUGCMarket(GameSession session, IPacketReader packet, MeretMarketCategoryMetadata metadata)
    {
        var gender = (GenderFlag) packet.ReadByte();
        var job = (JobFlag) packet.ReadInt();
        short sortBy = packet.ReadByte();

        var items = GameServer.UGCMarketManager.FindItemsByCategory(metadata.ItemCategories, gender, job, sortBy);
        session.Send(MeretMarketPacket.LoadUGCShopCategory(items));
    }

    private static void HandleOpenRedMeretMarket()
    {
        // TODO: Red Meret Market
    }

    private static void HandlePurchase(GameSession session, IPacketReader packet)
    {
        var quantity = packet.ReadByte();
        var marketItemId = packet.ReadInt();
        var ugcItemId = packet.ReadLong();
        if (ugcItemId != 0)
        {
            PurchaseUGCItem(session, ugcItemId);
            return;
        }

        PurchasePremiumItem(session, packet, marketItemId);
    }

    private static void PurchaseUGCItem(GameSession session, long ugcMarketItemId)
    {
        var marketItem = GameServer.UGCMarketManager.FindItemById(ugcMarketItemId);
        if (marketItem is null || marketItem.ListingExpirationTimestamp < TimeInfo.Now())
        {
            return;
        }

        if (!HandleMarketItemPay(session, marketItem.Price, MeretMarketCurrencyType.Meret))
        {
            return;
        }

        marketItem.SalesCount++;
        DatabaseManager.UGCMarketItems.Update(marketItem);
        _ = new UGCMarketSale(marketItem.Price, marketItem.Item.UGC.Name, marketItem.SellerCharacterId);

        Item item = new(marketItem.Item)
        {
            CreationTime = TimeInfo.Now(),
        };
        item.Uid = DatabaseManager.Items.Insert(item);

        session.Player.Inventory.AddItem(session, item, true);
        session.Send(MeretMarketPacket.Purchase(0, marketItem.Id, marketItem.Price, 1));
    }

    private static void PurchasePremiumItem(GameSession session, IPacketReader packet, int marketItemId)
    {
        packet.ReadInt();
        var childMarketItemId = packet.ReadInt();
        var unk2 = packet.ReadLong();
        var itemIndex = packet.ReadInt();
        var totalQuantity = packet.ReadInt();
        var unk3 = packet.ReadInt();
        var unk4 = packet.ReadByte();
        var unk5 = packet.ReadUnicodeString();
        var unk6 = packet.ReadUnicodeString();
        var price = packet.ReadLong();

        var marketItem = DatabaseManager.MeretMarket.FindById(marketItemId);
        if (marketItem is null)
        {
            return;
        }

        if (childMarketItemId != 0)
        {
            marketItem = marketItem.AdditionalQuantities.FirstOrDefault(x => x.MarketId == childMarketItemId);
            if (marketItem is null)
            {
                return;
            }
        }

        if (!HandleMarketItemPay(session, marketItem.Price, marketItem.TokenType))
        {
            return;
        }

        Item item = new(marketItem.ItemId)
        {
            Amount = marketItem.Quantity + marketItem.BonusQuantity,
            Rarity = marketItem.Rarity
        };
        if (marketItem.Duration != 0)
        {
            item.ExpiryTime = TimeInfo.Now() + Environment.TickCount + marketItem.Duration * 24 * 60 * 60;
        }
        session.Player.Inventory.AddItem(session, item, true);
        session.Send(MeretMarketPacket.Purchase(marketItem.MarketId, 0, marketItem.Price, totalQuantity, itemIndex));
    }

    private static bool HandleMarketItemPay(GameSession session, long price, MeretMarketCurrencyType currencyType)
    {
        return currencyType switch
        {
            MeretMarketCurrencyType.Meret => session.Player.Account.RemoveMerets(price),
            MeretMarketCurrencyType.Meso => session.Player.Wallet.Meso.Modify(price),
            _ => false,
        };
    }

    private static void HandleHome(GameSession session)
    {
        var marketItems = DatabaseManager.MeretMarket.FindAllByCategoryId(MeretMarketCategory.Promo);
        if (marketItems is null)
        {
            return;
        }
        session.Send(MeretMarketPacket.Promos(marketItems));
    }

    private static void HandleOpenDesignShop(GameSession session)
    {
        var promoItems = GameServer.UGCMarketManager.GetPromoItems();
        var newestItems = GameServer.UGCMarketManager.GetNewestItems();
        session.Send(MeretMarketPacket.OpenDesignShop(promoItems, newestItems));
    }

    private static void HandleLoadCart(GameSession session)
    {
        session.Send(MeretMarketPacket.LoadCart());
    }

    private static void HandleSendMarketRequest(GameSession session, IPacketReader packet)
    {
        packet.ReadByte(); //constant 1
        var meretMarketItemUid = packet.ReadInt();
        List<MeretMarketItem> meretMarketItems = new()
        {
            DatabaseManager.MeretMarket.FindById(meretMarketItemUid)
        };
        session.Send(MeretMarketPacket.LoadPremiumShopCategory(meretMarketItems));
    }
}
