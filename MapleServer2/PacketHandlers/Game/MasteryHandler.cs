using Maple2Storage.Enums;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MasteryHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CONSTRUCT_RECIPE;

    private static class MasteryOperations
    {
        public const byte RewardBox = 0x01;
        public const byte CraftItem = 0x02;
    }

    private static class MasteryNotices
    {
        public const byte NotEnoughMastery = 0x01;
        public const byte NotEnoughMesos = 0x02;
        public const byte RequiredQuestIsNotCompleted = 0x03;
        public const byte NotEnoughItems = 0x04;
        public const byte InsufficientLevel = 0x07;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case MasteryOperations.RewardBox:
                HandleRewardBox(session, packet);
                break;
            case MasteryOperations.CraftItem:
                HandleCraftItem(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleRewardBox(GameSession session, PacketReader packet)
    {
        int rewardBoxDetails = packet.ReadInt();
        int type = rewardBoxDetails / 1000;
        int grade = rewardBoxDetails % 100;

        // get the reward box item ID
        MasteryMetadata mastery = MasteryMetadataStorage.GetMastery(type);
        if (mastery == null)
        {
            Logger.Error($"Unknown mastery type {type} from user: {session.Player.Name}");
            return;
        }

        int rewardBoxItemId = mastery.Grades[grade - 1].RewardJobItemID;
        Item rewardBox = new(rewardBoxItemId)
        {
            Amount = 1
        };

        // give player the reward box item
        session.Player.Inventory.AddItem(session, rewardBox, true);

        // mark reward box as claimed
        session.Send(MasteryPacket.ClaimReward(rewardBoxDetails, rewardBox));
    }

    private static void HandleCraftItem(GameSession session, PacketReader packet)
    {
        int recipeId = packet.ReadInt();

        // attempt to oad the recipe metadata
        RecipeMetadata recipe = RecipeMetadataStorage.GetRecipe(recipeId);
        if (recipe == null)
        {
            Logger.Error($"Unknown recipe ID {recipeId} from user: {session.Player.Name}");
            return;
        }

        if (recipe.RequireMastery > 0)
        {
            if (session.Player.Levels.MasteryExp.First(x => x.Type == (MasteryType) recipe.MasteryType).CurrentExp < recipe.RequireMastery)
            {
                session.Send(MasteryPacket.MasteryNotice(MasteryNotices.NotEnoughMastery));
                return;
            }
        }

        if (recipe.RequireQuest.Count > 0)
        {
            foreach (int questId in recipe.RequireQuest)
            {
                if (session.Player.QuestData.TryGetValue(questId, out QuestStatus quest) && quest.State is not QuestState.Finished)
                {
                    session.Send(MasteryPacket.MasteryNotice(MasteryNotices.RequiredQuestIsNotCompleted));
                    return;
                }
            }
        }

        // does the play have enough mesos for this recipe?
        if (!session.Player.Wallet.Meso.Modify(-recipe.RequireMeso))
        {
            session.Send(MasteryPacket.MasteryNotice(MasteryNotices.NotEnoughMesos));
            return;
        }

        // does the player have all the required ingredients for this recipe?
        if (!PlayerHasAllIngredients(session, recipe))
        {
            session.Send(MasteryPacket.MasteryNotice(MasteryNotices.NotEnoughItems));
            return;
        }

        // only add reward items once all required items & mesos have been removed from player
        if (RemoveRequiredItemsFromInventory(session, recipe))
        {
            AddRewardItemsToInventory(session, recipe);
        }
    }

    private static bool RemoveRequiredItemsFromInventory(GameSession session, RecipeMetadata recipe)
    {
        List<RecipeItem> ingredients = recipe.RequiredItems;

        var inventory = session.Player.Inventory;
        foreach (RecipeItem ingredient in ingredients)
        {
            Item item = inventory.GetItemByItemIdAndRarity(ingredient.ItemId, ingredient.Rarity);
            if (item == null || item.Amount < ingredient.Amount)
            {
                return false;
            }

            inventory.ConsumeItem(session, item.Uid, ingredient.Amount);
        }

        return true;
    }
    
    private static void AddRewardItemsToInventory(GameSession session, RecipeMetadata recipe)
    {
        // award items
        List<RecipeItem> resultItems = recipe.RewardItems;
        foreach (RecipeItem resultItem in resultItems)
        {
            Item rewardItem = new(resultItem.ItemId)
            {
                Rarity = resultItem.Rarity,
                Amount = resultItem.Amount
            };
            session.Player.Inventory.AddItem(session, rewardItem, true);
            session.Send(MasteryPacket.GetCraftedItem((MasteryType) recipe.MasteryType, rewardItem));
        }

        // add mastery exp
        session.Player.Levels.GainMasteryExp((MasteryType) recipe.MasteryType, recipe.RewardMastery);

        // add player exp
        if (recipe.ExceptRewardExp)
        {
            // TODO: add metadata for common exp tables to be able to look up exp amount for masteries etc.
        }
    }

    private static bool PlayerHasEnoughMesos(GameSession session, RecipeMetadata recipe)
    {
        long mesoBalance = session.Player.Wallet.Meso.Amount;
        if (mesoBalance == 0)
        {
            return false;
        }

        return mesoBalance >= recipe.RequireMeso;
    }

    private static bool PlayerHasAllIngredients(GameSession session, RecipeMetadata recipe)
    {
        List<RecipeItem> ingredients = recipe.RequiredItems;

        var inventory = session.Player.Inventory;
        foreach (RecipeItem ingredient in ingredients)
        {
            Item item = inventory.GetItemByItemIdAndRarity(ingredient.ItemId, ingredient.Rarity);
            if (item == null || item.Amount < ingredient.Amount)
            {
                return false;
            }
        }

        return true;
    }
}
