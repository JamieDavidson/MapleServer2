﻿using Maple2Storage.Enums;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ShopHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.SHOP;

    private static class ShopOperations
    {
        public const byte Buy = 0x4;
        public const byte Sell = 0x5;
        public const byte Close = 0x6;
        public const byte OpenViaItem = 0x0A;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ShopOperations.Close:
                HandleClose(session);
                break;
            case ShopOperations.Buy:
                HandleBuy(session, packet);
                break;
            case ShopOperations.Sell:
                HandleSell(session, packet);
                break;
            case ShopOperations.OpenViaItem:
                HandleOpenViaItem(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    public static void HandleOpen(GameSession session, IFieldObject<NpcMetadata> npcFieldObject)
    {
        var metadata = npcFieldObject.Value;

        var shop = DatabaseManager.Shops.FindById(metadata.ShopId);
        if (shop == null)
        {
            Logger.Warn($"Unknown shop ID: {metadata.ShopId}");
            return;
        }

        session.Send(ShopPacket.Open(shop));
        foreach (var shopItem in shop.Items)
        {
            session.Send(ShopPacket.LoadProducts(shopItem));
        }
        session.Send(ShopPacket.Reload());
        session.Send(NpcTalkPacket.Respond(npcFieldObject, NpcType.Default, DialogType.None, 0));
        session.Player.ShopId = shop.Id;
    }

    private static void HandleClose(GameSession session)
    {
        session.Send(ShopPacket.Close());
        session.Player.ShopId = 0;
    }

    private static void HandleSell(GameSession session, IPacketReader packet)
    {
        // sell to shop
        var itemUid = packet.ReadLong();
        var quantity = packet.ReadInt();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByUid(itemUid);
        if (item == null)
        {
            return;
        }

        var price = ItemMetadataStorage.GetCustomSellPrice(item.Id);
        session.Player.Wallet.Meso.Modify(price * quantity);

        session.Player.Inventory.ConsumeItem(session, item.Uid, quantity);

        session.Send(ShopPacket.Sell(item, quantity));
    }

    private static void HandleBuy(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadInt();
        var quantity = packet.ReadInt();

        var shopItem = DatabaseManager.ShopItems.FindByUid(itemUid);

        switch (shopItem.TokenType)
        {
            case ShopCurrencyType.Meso:
                session.Player.Wallet.Meso.Modify(-(shopItem.Price * quantity));
                break;
            case ShopCurrencyType.ValorToken:
                session.Player.Wallet.ValorToken.Modify(-(shopItem.Price * quantity));
                break;
            case ShopCurrencyType.Treva:
                session.Player.Wallet.Treva.Modify(-(shopItem.Price * quantity));
                break;
            case ShopCurrencyType.Rue:
                session.Player.Wallet.Rue.Modify(-(shopItem.Price * quantity));
                break;
            case ShopCurrencyType.HaviFruit:
                session.Player.Wallet.HaviFruit.Modify(-(shopItem.Price * quantity));
                break;
            case ShopCurrencyType.Meret:
            case ShopCurrencyType.GameMeret:
            case ShopCurrencyType.EventMeret:
                session.Player.Account.RemoveMerets(shopItem.Price * quantity);
                break;
            case ShopCurrencyType.Item:
                var inventory = session.Player.Inventory;
                var itemCost = inventory.GetItemByItemId(shopItem.RequiredItemId);
                if (itemCost.Amount < shopItem.Price)
                {
                    return;
                }
                session.Player.Inventory.ConsumeItem(session, itemCost.Uid, shopItem.Price);
                break;
            default:
                session.SendNotice($"Unknown currency: {shopItem.TokenType}");
                return;
        }

        // add item to inventory
        Item item = new(shopItem.ItemId)
        {
            Amount = quantity * shopItem.Quantity,
            Rarity = shopItem.ItemRank
        };
        session.Player.Inventory.AddItem(session, item, true);

        // complete purchase
        session.Send(ShopPacket.Buy(shopItem.ItemId, quantity, shopItem.Price, shopItem.TokenType));
    }

    private static void HandleOpenViaItem(GameSession session, IPacketReader packet)
    {
        var unk = packet.ReadByte();
        var itemId = packet.ReadInt();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByItemId(itemId);
        if (item == null)
        {
            return;
        }

        var shop = DatabaseManager.Shops.FindById(item.ShopID);
        if (shop == null)
        {
            Logger.Warn($"Unknown shop ID: {item.ShopID}");
            return;
        }

        session.Send(ShopPacket.Open(shop));
        foreach (var shopItem in shop.Items)
        {
            session.Send(ShopPacket.LoadProducts(shopItem));
        }
        session.Send(ShopPacket.Reload());
    }
}
