using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

public class ItemRepackageHandler : GamePacketHandler
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

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case ItemRepackageOperations.Repackage:
                HandleRepackage(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleRepackage(GameSession session, PacketReader packet)
    {
        long ribbonUid = packet.ReadLong();
        long repackingItemUid = packet.ReadLong();

        Item ribbon = session.Player.Inventory.Items.Values.FirstOrDefault(x => x.Uid == ribbonUid);
        Item repackingItem = session.Player.Inventory.Items.Values.FirstOrDefault(x => x.Uid == repackingItemUid);
        if (repackingItem == null || ribbon == null)
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.ItemInvalid));
            return;
        }

        if (repackingItem.RemainingTrades != 0)
        {
            session.Send(ItemRepackagePacket.Notice(ItemRepackageErrors.CannotBePackaged));
        }

        int ribbonRequirementAmount = ItemMetadataStorage.GetRepackageConsumeCount(ribbon.Id);
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
