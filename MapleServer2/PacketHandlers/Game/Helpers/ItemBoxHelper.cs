using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types.Metadata;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game.Helpers;

internal static class ItemBoxHelper
{
    public static List<Item> GetItemsFromDropGroup(DropGroupContent dropContent, Gender playerGender, Job job)
    {
        List<Item> items = new();
        var rng = RandomProvider.Get();
        var amount = rng.Next((int) dropContent.MinAmount, (int) dropContent.MaxAmount);
        foreach (var id in dropContent.ItemIds)
        {
            if (dropContent.SmartGender)
            {
                var itemGender = ItemMetadataStorage.GetGender(id);
                if (itemGender != playerGender && itemGender is not Gender.Neutral)
                {
                    continue;
                }
            }

            var recommendJobs = ItemMetadataStorage.GetRecommendJobs(id);
            if (recommendJobs.Contains(job) || recommendJobs.Contains(Job.None))
            {
                Item newItem = new(id)
                {
                    Enchants = dropContent.EnchantLevel,
                    Amount = amount,
                    Rarity = dropContent.Rarity
                };
                items.Add(newItem);
            }
        }
        return items;
    }

    public static void GiveItemFromSelectBox(GameSession session, Item sourceItem, int index)
    {
        var box = sourceItem.Function.SelectItemBox;
        var metadata = ItemDropMetadataStorage.GetItemDropMetadata(box.BoxId);
        if (metadata == null)
        {
            session.Send(NoticePacket.Notice("No items found", NoticeType.Chat));
            return;
        }

        var inventory = session.Player.Inventory;
        inventory.ConsumeItem(session, sourceItem.Uid, 1);

        // Select boxes disregards group ID. Adding these all to a filtered list
        List<DropGroupContent> dropContentsList = new();
        foreach (var group in metadata.DropGroups)
        {
            foreach (var dropGroupContent in group.Contents)
            {
                if (dropGroupContent.SmartDropRate == 100)
                {
                    var recommendJobs = ItemMetadataStorage.GetRecommendJobs(dropGroupContent.ItemIds.First());
                    if (recommendJobs.Contains(session.Player.Job) || recommendJobs.Contains(Job.None))
                    {
                        dropContentsList.Add(dropGroupContent);
                    }
                    continue;
                }
                dropContentsList.Add(dropGroupContent);
            }
        }

        var dropContents = dropContentsList[index];

        var rng = RandomProvider.Get();
        var amount = rng.Next((int) dropContents.MinAmount, (int) dropContents.MaxAmount);
        foreach (var id in dropContents.ItemIds)
        {
            Item newItem = new(id)
            {
                Enchants = dropContents.EnchantLevel,
                Amount = amount,
                Rarity = dropContents.Rarity

            };
            inventory.AddItem(session, newItem, true);
        }
    }

    public static void GiveItemFromOpenBox(GameSession session, Item item)
    {
        var box = item.Function.OpenItemBox;
        var metadata = ItemDropMetadataStorage.GetItemDropMetadata(box.BoxId);
        if (metadata == null)
        {
            session.Send(NoticePacket.Notice("No items found", NoticeType.Chat));
            return;
        }

        if (box.AmountRequired > item.Amount)
        {
            return;
        }

        var inventory = session.Player.Inventory;
        if (box.RequiredItemId > 0)
        {
            var requiredItem = inventory.GetItemByItemId(box.RequiredItemId);
            if (requiredItem == null)
            {
                return;
            }

            inventory.ConsumeItem(session, requiredItem.Uid, 1);
        }

        inventory.ConsumeItem(session, item.Uid, box.AmountRequired);

        var rng = RandomProvider.Get();

        // Receive one item from each drop group
        if (box.ReceiveOneItem)
        {
            foreach (var group in metadata.DropGroups)
            {
                //randomize the contents
                var contentList = group.Contents.OrderBy(x => rng.Next()).ToList();
                foreach (var dropContent in contentList)
                {
                    var items = GetItemsFromDropGroup(dropContent, session.Player.Gender, session.Player.Job);
                    foreach (var newItem in items)
                    {
                        inventory.AddItem(session, newItem, true);
                    }
                }
            }
            return;
        }

        // receive all items from each drop group
        foreach (var group in metadata.DropGroups)
        {
            foreach (var dropContent in group.Contents)
            {
                var items = GetItemsFromDropGroup(dropContent, session.Player.Gender, session.Player.Job);
                foreach (var newItem in items)
                {
                    inventory.AddItem(session, newItem, true);
                }
            }
        }
    }
}
