﻿using System.Xml;
using GameDataParser.Files;
using GameDataParser.Tools;
using Maple2.File.IO.Crypto.Common;
using Maple2Storage.Enums;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;

namespace GameDataParser.Parsers;

public class ItemSocketScrollParser : Exporter<List<ItemSocketScrollMetadata>>
{
    public ItemSocketScrollParser(MetadataResources resources) : base(resources, MetadataName.ItemSocketScroll) { }

    protected override List<ItemSocketScrollMetadata> Parse()
    {
        List<ItemSocketScrollMetadata> items = new();

        PackFileEntry entry = Resources.XmlReader.Files.FirstOrDefault(x => x.Name.StartsWith("table/itemsocketscroll"));
        if (entry is null)
        {
            return items;
        }

        // Parse XML
        XmlDocument document = Resources.XmlReader.GetXmlDocument(entry);
        XmlNodeList nodes = document.SelectNodes("/ms2/scroll");

        foreach (XmlNode node in nodes)
        {
            List<int> intItemSlots = node.Attributes["slot"].Value.SplitAndParseToInt(',').ToList();
            List<ItemType> itemSlots = intItemSlots.Select(itemSlot => (ItemType) itemSlot).ToList();

            ItemSocketScrollMetadata metadata = new()
            {
                Id = int.Parse(node.Attributes["id"].Value),
                MinLevel = int.Parse(node.Attributes["minLv"].Value),
                MaxLevel = int.Parse(node.Attributes["maxLv"].Value),
                ItemTypes = itemSlots,
                Rarity = int.Parse(node.Attributes["rank"].Value),
                MakeUntradeable = node.Attributes["tradableCountDeduction"]?.Value == "1",
            };

            items.Add(metadata);
        }
        return items;
    }
}
