using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestTutorialItemHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_TUTORIAL_ITEM;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var metadata = JobMetadataStorage.GetTutorialItems((int) session.Player.Job);

        var inventory = session.Player.Inventory; 
            
        foreach (var tutorialItem in metadata)
        {
            var tutorialItemsCount = inventory.GetItemCount(tutorialItem.ItemId);
            tutorialItemsCount += inventory.Cosmetics.Where(x => x.Value.Id == tutorialItem.ItemId).Count();
            tutorialItemsCount += inventory.Equips.Where(x => x.Value.Id == tutorialItem.ItemId).Count();

            if (tutorialItemsCount >= tutorialItem.Amount)
            {
                continue;
            }

            var amountRemaining = tutorialItem.Amount - tutorialItemsCount;

            Item item = new(tutorialItem.ItemId)
            {
                Rarity = tutorialItem.Rarity,
                Amount = amountRemaining
            };
            session.Player.Inventory.AddItem(session, item, true);
        }
    }
}
