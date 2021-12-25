using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemExchangeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_EXCHANGE;

    private static class ItemExchangeOperations
    {
        public const byte Use = 0x1;
    }
    
    private static class ItemExchangeNotices
    {
        public const short Sucess = 0x0;
        public const short Invalid = 0x1;
        public const short CannotFuse = 0x2;
        public const short InsufficientMeso = 0x3;
        public const short InsufficientItems = 0x4;
        public const short EnchantLevelTooHigh = 0x5;
        public const short ItemIsLocked = 0x6;
        public const short CheckFusionAmount = 0x7;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case ItemExchangeOperations.Use:
                HandleUse(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleUse(GameSession session, PacketReader packet)
    {
        long itemUid = packet.ReadLong();
        long unk = packet.ReadLong();
        int quantity = packet.ReadInt();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }

        Item item = inventory.GetItemByUid(itemUid);

        ItemExchangeScrollMetadata exchange = ItemExchangeScrollMetadataStorage.GetMetadata(item.Function.Id);

        if (!session.Player.Wallet.Meso.Modify(-exchange.MesoCost * quantity))
        {
            session.Send(ItemExchangePacket.Notice(ItemExchangeNotices.InsufficientMeso));
            return;
        }

        if (exchange.ItemCost.Count != 0 && !PlayerHasAllIngredients(session, exchange, quantity))
        {
            session.Send(ItemExchangePacket.Notice(ItemExchangeNotices.InsufficientItems));
            return;
        }

        if (!RemoveRequiredItemsFromInventory(session, exchange, item, quantity))
        {
            return;
        }

        Item exchangeRewardItem = new(exchange.RewardId)
        {
            Rarity = exchange.RewardRarity,
            Amount = exchange.RewardAmount * quantity
        };

        session.Player.Inventory.AddItem(session, exchangeRewardItem, true);
        session.Send(ItemExchangePacket.Notice(ItemExchangeNotices.Sucess));

    }

    // TODO: This is surely a bug right? This isn't how checks for item presence work! Test once I can be bothered to run the server lol
    private static bool PlayerHasAllIngredients(GameSession session, ItemExchangeScrollMetadata exchange, int quantity)
    {
        // TODO: Check if rarity matches

        var inventory = session.Player.Inventory;
        for (int i = 0; i < exchange.ItemCost.Count; i++)
        {
            ItemRequirementMetadata exchangeItem = exchange.ItemCost.ElementAt(i);
            Item item = inventory.GetItemByItemId(exchangeItem.Id);

            if (item == null)
            {
                continue;
            }

            return item.Amount >= exchangeItem.Amount * quantity;
        }
        return false;
    }

    private static bool RemoveRequiredItemsFromInventory(GameSession session, ItemExchangeScrollMetadata exchange, Item originItem, int quantity)
    {
        var inventory = session.Player.Inventory;
        if (exchange.ItemCost.Count != 0)
        {
            for (int i = 0; i < exchange.ItemCost.Count; i++)
            {
                ItemRequirementMetadata exchangeItem = exchange.ItemCost.ElementAt(i);
                Item item = inventory.GetItemByItemId(exchangeItem.Id);
                if (item == null)
                {
                    continue;
                }
                session.Player.Inventory.ConsumeItem(session, item.Uid, exchangeItem.Amount * quantity);
            }
        }

        inventory.ConsumeItem(session, originItem.Uid, exchange.RecipeAmount * quantity);

        return true;
    }
}
