using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemExtractionHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_EXTRACTION;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var anvilItemUid = packet.ReadLong();
        var sourceItemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(sourceItemUid) || !inventory.HasItemWithUid(anvilItemUid))
        {
            return;
        }

        var sourceItem = inventory.GetItemByUid(sourceItemUid);

        var metadata = ItemExtractionMetadataStorage.GetMetadata(sourceItem.Id);
        if (metadata == null)
        {
            return;
        }

        var anvils = inventory.GetItemsByTag("ItemExtraction").ToArray();
        var anvilAmount = anvils.Sum(a => a.Value.Amount);

        if (anvilAmount < metadata.ScrollCount)
        {
            session.Send(ItemExtractionPacket.InsufficientAnvils());
            return;
        }

        Item resultItem = new(metadata.ResultItemId)
        {
            Color = sourceItem.Color
        };

        session.Player.Inventory.ConsumeItem(session, anvilItemUid, metadata.ScrollCount);
        session.Player.Inventory.AddItem(session, resultItem, true);
        sourceItem.RemainingGlamorForges -= 1;

        session.Send(ItemExtractionPacket.Extract(sourceItem));
    }
}
