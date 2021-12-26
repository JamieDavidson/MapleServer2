using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestItemUseMultipleHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_ITEM_USE_MULTIPLE;

    private static class BoxType
    {
        public const byte Open = 0x00;
        public const byte Select = 0x01;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        int itemId = packet.ReadInt();
        packet.ReadShort(); // Unknown
        int amount = packet.ReadInt();
        var boxType = packet.ReadShort();

        string functionName = ItemMetadataStorage.GetFunction(itemId).Name;
        if (functionName != "SelectItemBox" && functionName != "OpenItemBox")
        {
            return;
        }

        var inventory = session.Player.Inventory;
        var items = inventory.GetItemsByItemId(itemId).ToArray();
        if (items.Length == 0)
        {
            return;
        }

        int index = 0;
        if (boxType == BoxType.Select)
        {
            index = packet.ReadShort() - 0x30; // Starts at 0x30 for some reason
            if (index < 0)
            {
                return;
            }
            SelectItemBox selectBox = ItemMetadataStorage.GetFunction(itemId).SelectItemBox;
            HandleSelectBox(session, items, selectBox, index, amount);
            return;
        }

        OpenItemBox openBox = ItemMetadataStorage.GetFunction(itemId).OpenItemBox;
        HandleOpenBox(session, items, /*openBox,*/ amount);
    }

    private static void HandleSelectBox(GameSession session, IEnumerable<Item> items, SelectItemBox box, int index, int amount)
    {
        ItemDropMetadata metadata = ItemDropMetadataStorage.GetItemDropMetadata(box.BoxId);
        int opened = 0;
        foreach (var item in items)
        {
            for (int i = opened; i < amount; i++)
            {
                if (item.Amount <= 0)
                {
                    break;
                }

                opened++;
                ItemBoxHelper.GiveItemFromSelectBox(session, item, index);
            }
        }

        session.Send(ItemUsePacket.Use(items.FirstOrDefault().Id, amount));
    }

    private static void HandleOpenBox(GameSession session, IEnumerable<Item> items, /*OpenItemBox box,*/ int amount)
    {
        int opened = 0;
        foreach (var item in items)
        {
            for (int i = opened; i < amount; i++)
            {
                if (item.Amount <= 0)
                {
                    break;
                }

                opened++;
                ItemBoxHelper.GiveItemFromOpenBox(session, item);
            }
        }

        session.Send(ItemUsePacket.Use(items.FirstOrDefault().Id, amount));
    }
}
