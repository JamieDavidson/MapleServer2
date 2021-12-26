using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestHomeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_HOME;

    private static class RequestHomeOperations
    {
        public const byte InviteToHome = 0x01;
        public const byte MoveToHome = 0x03;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case RequestHomeOperations.InviteToHome:
                HandleInviteToHome(session, packet);
                break;
            case RequestHomeOperations.MoveToHome:
                HandleMoveToHome(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleInviteToHome(GameSession session, IPacketReader packet)
    {
        var characterName = packet.ReadUnicodeString();
        var target = GameServer.PlayerManager.GetPlayerByName(characterName);
        if (target == null)
        {
            return;
        }
        if (session.Player.Account.Home == null)
        {
            return;
        }

        target.Session.Send(InviteToHomePacket.InviteToHome(session.Player));
    }

    // The same mode also handles creation of new homes.
    private static void HandleMoveToHome(GameSession session, IPacketReader packet)
    {
        var homeTemplate = packet.ReadInt();
        var player = session.Player;
        if (player.Account.Home == null)
        {
            player.Account.Home = new(player.Account.Id, player.Name, homeTemplate);
            GameServer.HomeManager.AddHome(player.Account.Home);

            // Send inventories
            session.Send(WarehouseInventoryPacket.StartList());
            var counter = 0;
            foreach (var kvp in player.Account.Home.WarehouseInventory)
            {
                session.Send(WarehouseInventoryPacket.Load(kvp.Value, ++counter));
            }
            session.Send(WarehouseInventoryPacket.EndList());

            session.Send(FurnishingInventoryPacket.StartList());
            foreach (var cube in player.Account.Home.FurnishingInventory.Values.Where(x => x.Item != null))
            {
                session.Send(FurnishingInventoryPacket.Load(cube));
            }
            session.Send(FurnishingInventoryPacket.EndList());
        }
        var home = GameServer.HomeManager.GetHomeById(player.Account.Home.Id);

        player.VisitingHomeId = player.Account.Home.Id;
        player.Guide = null;

        player.WarpGameToGame(home.MapId, home.InstanceId);
    }
}
