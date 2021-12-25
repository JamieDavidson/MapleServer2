using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class SystemShopHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.SYSTEM_SHOP;

    private static class ShopOperations
    {
        public const byte Arena = 0x03;
        public const byte Fishing = 0x04;
        public const byte ViaItem = 0x0A;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case ShopOperations.ViaItem:
                HandleViaItem(session, packet);
                break;
            case ShopOperations.Fishing:
                HandleFishingShop(session, packet);
                break;
            case ShopOperations.Arena:
                HandleMapleArenaShop(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleViaItem(GameSession session, PacketReader packet)
    {
        bool openShop = packet.ReadBool();

        if (!openShop)
        {
            return;
        }

        int itemId = packet.ReadInt();

        var inventory = session.Player.Inventory;
        var item = inventory.GetItemByItemId(itemId);
        if (item == null)
        {
            return;
        }

        Shop shop = DatabaseManager.Shops.FindById(item.ShopID);
        if (shop == null)
        {
            Logger.Warn($"Unknown shop ID: {item.ShopID}");
            return;
        }

        session.Send(ShopPacket.Open(shop));
        foreach (ShopItem shopItem in shop.Items)
        {
            session.Send(ShopPacket.LoadProducts(shopItem));
        }
        session.Send(ShopPacket.Reload());
        session.Send(SystemShopPacket.Open());
    }
    private static void HandleFishingShop(GameSession session, PacketReader packet)
    {
        bool openShop = packet.ReadBool();

        if (!openShop)
        {
            return;
        }

        OpenSystemShop(session, 161);
    }

    private static void HandleMapleArenaShop(GameSession session, PacketReader packet)
    {
        bool openShop = packet.ReadBool();

        if (!openShop)
        {
            return;
        }

        OpenSystemShop(session, 168);
    }

    private static void OpenSystemShop(GameSession session, int shopId)
    {
        Shop shop = DatabaseManager.Shops.FindById(shopId);

        session.Send(ShopPacket.Open(shop));
        foreach (ShopItem shopItem in shop.Items)
        {
            session.Send(ShopPacket.LoadProducts(shopItem));
        }
        session.Send(ShopPacket.Reload());
        session.Send(SystemShopPacket.Open());
    }
}
