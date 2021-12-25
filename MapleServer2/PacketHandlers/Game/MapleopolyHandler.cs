using Maple2Storage.Tools;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MapleopolyHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.MAPLEOPOLY;

    private static class MapleopolyOperations
    {
        public const byte Open = 0x0;
        public const byte Roll = 0x1;
        public const byte ProcessTile = 0x3;
    }

    private static class MapleopolyNotices
    {
        public const byte NotEnoughTokens = 0x1;
        public const byte DiceAlreadyRolled = 0x4;
        public const byte YouCannotRollRightNow = 0x5;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case MapleopolyOperations.Open:
                HandleOpen(session);
                break;
            case MapleopolyOperations.Roll:
                HandleRoll(session);
                break;
            case MapleopolyOperations.ProcessTile:
                HandleProcessTile(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        List<MapleopolyTile> tiles = DatabaseManager.Mapleopoly.FindAllTiles();

        int tokenAmount = 0;
        var inventory = session.Player.Inventory;
        var token = inventory.GetItemByItemId(Mapleopoly.TOKEN_ITEM_ID);
        if (token != null)
        {
            tokenAmount = token.Amount;
        }
        session.Send(MapleopolyPacket.Open(session.Player.Mapleopoly, tiles, tokenAmount));
    }

    private static void HandleRoll(GameSession session)
    {
        // Check if player can roll
        var inventory = session.Player.Inventory;
        var token = inventory.GetItemByItemId(Mapleopoly.TOKEN_ITEM_ID);

        var mapleopoly = session.Player.Mapleopoly;
        if (mapleopoly.FreeRollAmount > 0)
        {
            mapleopoly.FreeRollAmount--;
        }
        else if (token != null && token.Amount >= Mapleopoly.TOKEN_COST)
        {
            inventory.ConsumeItem(session, token.Uid, Mapleopoly.TOKEN_COST);
        }
        else
        {
            session.Send(MapleopolyPacket.Notice(MapleopolyNotices.NotEnoughTokens));
            return;
        }

        Random rnd = RandomProvider.Get();

        // roll two dice
        int roll1 = rnd.Next(1, 6);
        int roll2 = rnd.Next(1, 6);
        int totalRoll = roll1 + roll2;

        mapleopoly.TotalTileCount += totalRoll;
        if (roll1 == roll2)
        {
            mapleopoly.FreeRollAmount++;
        }
        session.Send(MapleopolyPacket.Roll(mapleopoly.TotalTileCount, roll1, roll2));
    }

    private static void HandleProcessTile(GameSession session)
    {
        int currentTilePosition = session.Player.Mapleopoly.TotalTileCount % Mapleopoly.TILE_AMOUNT;

        MapleopolyTile currentTile = DatabaseManager.Mapleopoly.FindTileByPosition(currentTilePosition + 1);

        switch (currentTile.Type)
        {
            case MapleopolyTileType.Item:
            case MapleopolyTileType.TreasureTrove:
                Item item = new(currentTile.ItemId)
                {
                    Amount = currentTile.ItemAmount,
                    Rarity = currentTile.ItemRarity
                };
                session.Player.Inventory.AddItem(session, item, true);
                break;
            case MapleopolyTileType.Backtrack:
                session.Player.Mapleopoly.TotalTileCount -= currentTile.TileParameter;
                break;
            case MapleopolyTileType.MoveForward:
                session.Player.Mapleopoly.TotalTileCount += currentTile.TileParameter;
                break;
            case MapleopolyTileType.RoundTrip:
                session.Player.Mapleopoly.TotalTileCount += Mapleopoly.TILE_AMOUNT;
                break;
            case MapleopolyTileType.GoToStart:
                int tileToStart = Mapleopoly.TILE_AMOUNT - currentTilePosition;
                session.Player.Mapleopoly.TotalTileCount += tileToStart;
                break;
            case MapleopolyTileType.Start:
                break;
            default:
                Logger.Warn("Unsupported tile");
                break;
        }

        ProcessTrip(session); // Check if player passed Start
        session.Send(MapleopolyPacket.ProcessTile(session.Player.Mapleopoly, currentTile));
    }

    private static void ProcessTrip(GameSession session)
    {
        int newTotalTrips = session.Player.Mapleopoly.TotalTileCount / Mapleopoly.TILE_AMOUNT;
        if (newTotalTrips <= session.Player.Mapleopoly.TotalTrips)
        {
            return;
        }

        int difference = newTotalTrips - session.Player.Mapleopoly.TotalTrips;

        List<MapleopolyEvent> items = DatabaseManager.Events.FindAllMapleopolyEvents();
        for (int i = 0; i < difference; i++)
        {
            session.Player.Mapleopoly.TotalTrips++;

            // Check if there's any item to give for every 1 trip
            MapleopolyEvent mapleopolyItem1 = items.FirstOrDefault(x => x.TripAmount == 0);
            if (mapleopolyItem1 != null)
            {
                Item item1 = new(mapleopolyItem1.ItemId)
                {
                    Amount = mapleopolyItem1.ItemAmount,
                    Rarity = mapleopolyItem1.ItemRarity
                };
                session.Player.Inventory.AddItem(session, item1, true);
            }

            // Check if there's any other item to give for hitting a specific number of trips
            MapleopolyEvent mapleopolyItem2 = items.FirstOrDefault(x => x.TripAmount == session.Player.Mapleopoly.TotalTrips);
            if (mapleopolyItem2 == null)
            {
                continue;
            }
            Item item2 = new(mapleopolyItem2.ItemId)
            {
                Amount = mapleopolyItem2.ItemAmount,
                Rarity = mapleopolyItem2.ItemRarity
            };
            session.Player.Inventory.AddItem(session, item2, true);
        }
    }
}
