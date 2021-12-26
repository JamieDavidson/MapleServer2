using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemRepackageHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_REPACKAGE;

    private static class ItemRepackageOperations
    {
        public const byte Repackage = 0x1;
    }

    private static class ItemRepackageErrors
    {
        public const byte CannotBePackaged = 0x1;
        public const byte ItemInvalid = 0x2;
        public const byte CannotRepackageRightNow = 0x3;
        public const byte InvalidRarity = 0x4;
        public const byte InvalidLevel = 0x5;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ItemRepackageOperations.Repackage:
                HandleRepackage(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleRepackage(GameSession session, IPacketReader packet)
    {
        var ribbonUid = packet.ReadLong();
        var repackingItemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        var ribbon = inventory.GetItemByUid(ribbonUid);
        var repackingItem = inventory.GetItemByUid(repackingItemUid);
        if (repackingItem == null || ribbon == null)
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.ItemInvalid));
            return;
        }

        if (repackingItem.RemainingTrades != 0)
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.CannotBePackaged));
        }

        var ribbonRequirementAmount = ItemMetadataStorage.GetRepackageConsumeCount(ribbon.Id);
        if (ribbonRequirementAmount > ribbon.Amount)
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.CannotBePackaged));
            return;
        }

        if (!ItemRepackageMetadataStorage.ItemCanRepackage(ribbon.Function.Id, repackingItem.Level, repackingItem.Rarity))
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.ItemInvalid));
            return;
        }

        repackingItem.RepackageCount -= 1;
        repackingItem.RemainingTrades++;

        session.Player.Inventory.ConsumeItem(session, ribbon.Uid, ribbonRequirementAmount);

        session.Send(ItemRepackagePacket.Repackage(repackingItem));
    }
}
