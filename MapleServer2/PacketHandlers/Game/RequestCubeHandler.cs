using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Enums;
using MapleServer2.Managers;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestCubeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_CUBE;

    private static class RequestCubeOperations
    {
        public const byte LoadFurnishingItem = 0x1;
        public const byte BuyPlot = 0x2;
        public const byte ForfeitPlot = 0x6;
        public const byte HandleAddFurnishing = 0xA;
        public const byte RemoveCube = 0xC;
        public const byte RotateCube = 0xE;
        public const byte ReplaceCube = 0xF;
        public const byte Pickup = 0x11;
        public const byte Drop = 0x12;
        public const byte HomeName = 0x15;
        public const byte HomePassword = 0x18;
        public const byte NominateHouse = 0x19;
        public const byte HomeDescription = 0x1D;
        public const byte ClearInterior = 0x1F;
        public const byte RequestLayout = 0x23;
        public const byte IncreaseSize = 0x25;
        public const byte DecreaseSize = 0x26;
        public const byte DecorationReward = 0x28;
        public const byte InteriorDesingReward = 0x29;
        public const byte EnablePermission = 0x2A;
        public const byte SetPermission = 0x2B;
        public const byte IncreaseHeight = 0x2C;
        public const byte DecreaseHeight = 0x2D;
        public const byte SaveLayout = 0x2E;
        public const byte DecorPlannerLoadLayout = 0x2F;
        public const byte LoadLayout = 0x30;
        public const byte KickEveryone = 0x31;
        public const byte ChangeBackground = 0x33;
        public const byte ChangeLighting = 0x34;
        public const byte ChangeCamera = 0x36;
        public const byte UpdateBudget = 0x38;
        public const byte GiveBuildingPermission = 0x39;
        public const byte RemoveBuildingPermission = 0x3A;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case RequestCubeOperations.LoadFurnishingItem:
                HandleLoadFurnishingItem(session, packet);
                break;
            case RequestCubeOperations.BuyPlot:
                HandleBuyPlot(session, packet);
                break;
            case RequestCubeOperations.ForfeitPlot:
                HandleForfeitPlot(session);
                break;
            case RequestCubeOperations.HandleAddFurnishing:
                HandleAddFurnishing(session, packet);
                break;
            case RequestCubeOperations.RemoveCube:
                HandleRemoveCube(session, packet);
                break;
            case RequestCubeOperations.RotateCube:
                HandleRotateCube(session, packet);
                break;
            case RequestCubeOperations.ReplaceCube:
                HandleReplaceCube(session, packet);
                break;
            case RequestCubeOperations.Pickup:
                HandlePickup(session, packet);
                break;
            case RequestCubeOperations.Drop:
                HandleDrop(session);
                break;
            case RequestCubeOperations.HomeName:
                HandleHomeName(session, packet);
                break;
            case RequestCubeOperations.HomePassword:
                HandleHomePassword(session, packet);
                break;
            case RequestCubeOperations.NominateHouse:
                HandleNominateHouse(session);
                break;
            case RequestCubeOperations.HomeDescription:
                HandleHomeDescription(session, packet);
                break;
            case RequestCubeOperations.ClearInterior:
                HandleClearInterior(session);
                break;
            case RequestCubeOperations.RequestLayout:
                HandleRequestLayout(session, packet);
                break;
            case RequestCubeOperations.IncreaseSize:
            case RequestCubeOperations.DecreaseSize:
            case RequestCubeOperations.IncreaseHeight:
            case RequestCubeOperations.DecreaseHeight:
                HandleModifySize(session, operation);
                break;
            case RequestCubeOperations.DecorationReward:
                HandleDecorationReward(session);
                break;
            case RequestCubeOperations.InteriorDesingReward:
                HandleInteriorDesingReward(session, packet);
                break;
            case RequestCubeOperations.SaveLayout:
                HandleSaveLayout(session, packet);
                break;
            case RequestCubeOperations.DecorPlannerLoadLayout:
                HandleDecorPlannerLoadLayout(session, packet);
                break;
            case RequestCubeOperations.LoadLayout:
                HandleLoadLayout(session, packet);
                break;
            case RequestCubeOperations.KickEveryone:
                HandleKickEveryone(session);
                break;
            case RequestCubeOperations.ChangeLighting:
            case RequestCubeOperations.ChangeBackground:
            case RequestCubeOperations.ChangeCamera:
                HandleModifyInteriorSettings(session, operation, packet);
                break;
            case RequestCubeOperations.EnablePermission:
                HandleEnablePermission(session, packet);
                break;
            case RequestCubeOperations.SetPermission:
                HandleSetPermission(session, packet);
                break;
            case RequestCubeOperations.UpdateBudget:
                HandleUpdateBudget(session, packet);
                break;
            case RequestCubeOperations.GiveBuildingPermission:
                HandleGiveBuildingPermission(session, packet);
                break;
            case RequestCubeOperations.RemoveBuildingPermission:
                HandleRemoveBuildingPermission(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleLoadFurnishingItem(GameSession session, IPacketReader packet)
    {
        var itemId = packet.ReadInt();
        var itemUid = packet.ReadLong();
        session.FieldManager.BroadcastPacket(ResponseCubePacket.LoadFurnishingItem(session.Player.FieldPlayer, itemId, itemUid));
    }

    private static void HandleBuyPlot(GameSession session, IPacketReader packet)
    {
        var groupId = packet.ReadInt();
        var homeTemplate = packet.ReadInt();
        var player = session.Player;

        if (player.Account.Home != null && player.Account.Home.PlotMapId != 0)
        {
            return;
        }

        var land = UGCMapMetadataStorage.GetGroupMetadata(player.MapId, (byte) groupId);
        if (land == null)
        {
            return;
        }

        //Check if sale event is active
        var price = land.Price;
        var ugcMapContractSale = DatabaseManager.Events.FindUGCMapContractSaleEvent();
        if (ugcMapContractSale != null)
        {
            var markdown = land.Price * (ugcMapContractSale.DiscountAmount / 100 / 100);
            price = land.Price - markdown;
        }

        if (!HandlePlotPayment(session, land.PriceItemCode, price))
        {
            session.SendNotice("You don't have enough mesos!");
            return;
        }

        if (player.Account.Home == null)
        {
            player.Account.Home = new(player.Account.Id, player.Name, homeTemplate)
            {
                PlotMapId = player.MapId,
                PlotNumber = land.Id,
                Expiration = TimeInfo.Now() + Environment.TickCount + land.ContractDate * 24 * 60 * 60,
                Name = player.Name
            };
            GameServer.HomeManager.AddHome(player.Account.Home);
        }
        else
        {
            player.Account.Home.PlotMapId = player.MapId;
            player.Account.Home.PlotNumber = land.Id;
            player.Account.Home.Expiration = TimeInfo.Now() + Environment.TickCount + land.ContractDate * 24 * 60 * 60;

            var home = GameServer.HomeManager.GetHomeById(player.Account.Home.Id);
            home.PlotMapId = player.Account.Home.PlotMapId;
            home.PlotNumber = player.Account.Home.PlotNumber;
            home.Expiration = player.Account.Home.Expiration;
        }

        session.FieldManager.BroadcastPacket(ResponseCubePacket.PurchasePlot(player.Account.Home.PlotNumber, 0, player.Account.Home.Expiration));
        session.FieldManager.BroadcastPacket(ResponseCubePacket.EnablePlotFurnishing(player));
        session.Send(ResponseCubePacket.LoadHome(session.Player.FieldPlayer.ObjectId, session.Player.Account.Home));
        session.FieldManager.BroadcastPacket(ResponseCubePacket.HomeName(player), session);
        session.Send(ResponseCubePacket.CompletePurchase());
    }

    private static void HandleForfeitPlot(GameSession session)
    {
        var player = session.Player;
        if (player.Account.Home == null || player.Account.Home.PlotMapId == 0)
        {
            return;
        }

        var plotMapId = player.Account.Home.PlotMapId;
        var plotNumber = player.Account.Home.PlotNumber;
        var apartmentNumber = player.Account.Home.ApartmentNumber;

        player.Account.Home.PlotMapId = 0;
        player.Account.Home.PlotNumber = 0;
        player.Account.Home.ApartmentNumber = 0;
        player.Account.Home.Expiration = 32503561200; // year 2999

        var home = GameServer.HomeManager.GetHomeById(player.Account.Home.Id);
        home.PlotMapId = 0;
        home.PlotNumber = 0;
        home.ApartmentNumber = 0;
        home.Expiration = 32503561200; // year 2999

        var cubes = session.FieldManager.State.Cubes.Values.Where(x => x.Value.PlotNumber == plotNumber);
        foreach (var cube in cubes)
        {
            RemoveCube(session, session.Player.FieldPlayer, cube, home);
        }

        session.Send(ResponseCubePacket.ForfeitPlot(plotNumber, apartmentNumber, TimeInfo.Now()));
        session.Send(ResponseCubePacket.RemovePlot(plotNumber, apartmentNumber));
        session.Send(ResponseCubePacket.LoadHome(session.Player.FieldPlayer.ObjectId, session.Player.Account.Home));
        session.Send(ResponseCubePacket.RemovePlot2(plotMapId, plotNumber));
        // 54 00 0E 01 00 00 00 01 01 00 00 00, send mail
    }

    private static void HandleAddFurnishing(GameSession session, IPacketReader packet)
    {
        var coord = packet.Read<CoordB>();
        var padding = packet.ReadByte();
        var itemId = packet.ReadInt();
        var itemUid = packet.ReadLong();
        var padding2 = packet.ReadByte();
        var rotation = packet.Read<CoordF>();

        var coordF = coord.ToFloat();
        var player = session.Player;

        var liftable = session.FieldManager.State.LiftableObjects.Values.FirstOrDefault(x => x.ItemId == itemId);
        if (liftable is not null)
        {
            HandleAddLiftable(player, session.FieldManager, liftable, coord, rotation);
            return;
        }

        var mapIsHome = player.MapId == (int) Map.PrivateResidence;
        var home = mapIsHome ? GameServer.HomeManager.GetHomeById(player.VisitingHomeId) : GameServer.HomeManager.GetHomeById(player.Account.Home.Id);

        var plotNumber = MapMetadataStorage.GetPlotNumber(player.MapId, coord);
        if (plotNumber <= 0)
        {
            session.Send(ResponseCubePacket.CantPlaceHere(session.Player.FieldPlayer.ObjectId));
            return;
        }

        var plot = UGCMapMetadataStorage.GetGroupMetadata(player.MapId, (byte) plotNumber);
        var height = mapIsHome ? home.Height : plot.HeightLimit;
        var size = mapIsHome ? home.Size : plot.Area / 2;
        if (IsCoordOutsideHeightLimit(coord.ToShort(), player.MapId, height) || mapIsHome && IsCoordOutsideSizeLimit(coord, size))
        {
            session.Send(ResponseCubePacket.CantPlaceHere(session.Player.FieldPlayer.ObjectId));
            return;
        }

        var shopMetadata = FurnishingShopMetadataStorage.GetMetadata(itemId);
        if (shopMetadata == null || !shopMetadata.Buyable)
        {
            return;
        }

        if (session.FieldManager.State.Cubes.Values.Any(x => x.Coord == coord.ToFloat()))
        {
            return;
        }

        IFieldObject<Cube> fieldCube;
        if (player.IsInDecorPlanner)
        {
            Cube cube = new(new(itemId), plotNumber, coordF, rotation);

            fieldCube = session.FieldManager.RequestFieldObject(cube);
            fieldCube.Coord = coordF;
            fieldCube.Rotation = rotation;
            home.DecorPlannerInventory.Add(cube.Uid, cube);

            session.FieldManager.AddCube(fieldCube, session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId);
            return;
        }

        IFieldObject<Player> homeOwner = session.FieldManager.State.Players.Values.FirstOrDefault(x => x.Value.AccountId == home.AccountId);
        if (player.Account.Id != home.AccountId)
        {
            if (homeOwner == default)
            {
                session.SendNotice("You cannot do that unless the home owner is present."); // TODO: use notice packet
                return;
            }

            if (!home.BuildingPermissions.Contains(player.Account.Id))
            {
                session.SendNotice("You don't have building rights.");
                return;
            }
        }

        var warehouseItems = home.WarehouseInventory;
        var furnishingInventory = home.FurnishingInventory;
        Item item;
        if (player.Account.Id != home.AccountId)
        {
            if ((!warehouseItems.TryGetValue(itemUid, out item) || warehouseItems[itemUid].Amount <= 0) && !PurchaseFurnishingItemFromBudget(session.FieldManager, homeOwner.Value, home, shopMetadata))
            {
                NotEnoughMoneyInBudget(session, shopMetadata);
                return;
            }
        }
        else
        {
            if ((!warehouseItems.TryGetValue(itemUid, out item) || warehouseItems[itemUid].Amount <= 0) && !PurchaseFurnishingItem(session, shopMetadata))
            {
                NotEnoughMoney(session, shopMetadata);
                return;
            }
        }

        fieldCube = AddCube(session, item, itemId, rotation, coordF, plotNumber, homeOwner, home);

        homeOwner.Value.Session.Send(FurnishingInventoryPacket.Load(fieldCube.Value));
        session.FieldManager.AddCube(fieldCube, homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId);

        AddFunctionCube(session, coord, fieldCube);
    }

    private static void HandleAddLiftable(Player player, FieldManager fieldManager, LiftableObject liftable, CoordB coord, CoordF rotation)
    {
        liftable.Position = coord.ToFloat();
        liftable.Rotation = rotation;

        liftable.State = LiftableState.Active;
        liftable.Enabled = true;

        var target = MapEntityStorage.GetLiftablesTargets(player.MapId)?.FirstOrDefault(x => x.Position == liftable.Position);
        if (target is not null)
        {
            liftable.State = LiftableState.Disabled;
            QuestHelper.UpdateQuest(player.Session, liftable.ItemId.ToString(), "item_move", target.Target.ToString());
        }

        fieldManager.BroadcastPacket(LiftablePacket.Drop(liftable));
        fieldManager.BroadcastPacket(ResponseCubePacket.PlaceLiftable(liftable, player.FieldPlayer.ObjectId));
        fieldManager.BroadcastPacket(BuildModePacket.Use(player.FieldPlayer, BuildModeHandler.BuildModeTypes.Stop));
        fieldManager.BroadcastPacket(LiftablePacket.UpdateEntityByCoord(liftable));
    }

    private static void HandleRemoveCube(GameSession session, IPacketReader packet)
    {
        var coord = packet.Read<CoordB>();
        var player = session.Player;

        var mapIsHome = player.MapId == (int) Map.PrivateResidence;
        var home = mapIsHome ? GameServer.HomeManager.GetHomeById(player.VisitingHomeId) : GameServer.HomeManager.GetHomeById(player.Account.Home.Id);

        IFieldObject<Player> homeOwner = session.FieldManager.State.Players.Values.FirstOrDefault(x => x.Value.AccountId == home.AccountId);
        if (player.Account.Id != home.AccountId)
        {
            if (homeOwner == default)
            {
                session.SendNotice("You cannot do that unless the home owner is present."); // TODO: use notice packet
                return;
            }

            if (!home.BuildingPermissions.Contains(player.Account.Id))
            {
                session.SendNotice("You don't have building rights.");
                return;
            }
        }

        var cube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord.ToFloat());
        if (cube == default || cube.Value.Item == null)
        {
            return;
        }

        RemoveCube(session, homeOwner, cube, home);
    }

    private static void HandleRotateCube(GameSession session, IPacketReader packet)
    {
        var coord = packet.Read<CoordB>();
        var player = session.Player;

        var mapIsHome = player.MapId == (int) Map.PrivateResidence;
        var home = mapIsHome ? GameServer.HomeManager.GetHomeById(player.VisitingHomeId) : GameServer.HomeManager.GetHomeById(player.Account.Home.Id);
        if (player.Account.Id != home.AccountId && !home.BuildingPermissions.Contains(player.Account.Id))
        {
            return;
        }

        var cube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord.ToFloat());
        if (cube == default)
        {
            return;
        }

        cube.Rotation -= CoordF.From(0, 0, 90);
        var inventory = player.IsInDecorPlanner ? home.DecorPlannerInventory : home.FurnishingInventory;
        inventory[cube.Value.Uid].Rotation = cube.Rotation;

        session.Send(ResponseCubePacket.RotateCube(session.Player.FieldPlayer, cube));
    }

    private static void HandleReplaceCube(GameSession session, IPacketReader packet)
    {
        var coord = packet.Read<CoordB>();
        packet.Skip(1);
        var replacementItemId = packet.ReadInt();
        var replacementItemUid = packet.ReadLong();
        var unk = packet.ReadByte();
        var unk2 = packet.ReadLong(); // maybe part of rotation?
        var zRotation = packet.ReadFloat();
        var rotation = CoordF.From(0, 0, zRotation);

        var player = session.Player;

        var mapIsHome = player.MapId == (int) Map.PrivateResidence;
        var home = mapIsHome ? GameServer.HomeManager.GetHomeById(player.VisitingHomeId) : GameServer.HomeManager.GetHomeById(player.Account.Home.Id);

        var plotNumber = MapMetadataStorage.GetPlotNumber(player.MapId, coord);
        if (plotNumber <= 0)
        {
            session.Send(ResponseCubePacket.CantPlaceHere(session.Player.FieldPlayer.ObjectId));
            return;
        }

        var plot = UGCMapMetadataStorage.GetGroupMetadata(player.MapId, (byte) plotNumber);
        var height = mapIsHome ? home.Height : plot.HeightLimit;
        var size = mapIsHome ? home.Size : plot.Area / 2;
        var groundHeight = GetGroundCoord(coord, player.MapId, height);
        if (groundHeight == null)
        {
            return;
        }

        var isCubeSolid = ItemMetadataStorage.GetIsCubeSolid(replacementItemId);
        if (!isCubeSolid && coord.Z == groundHeight?.Z)
        {
            session.Send(ResponseCubePacket.CantPlaceHere(session.Player.FieldPlayer.ObjectId));
            return;
        }

        if (IsCoordOutsideHeightLimit(coord.ToShort(), player.MapId, height) || mapIsHome && IsCoordOutsideSizeLimit(coord, size))
        {
            session.Send(ResponseCubePacket.CantPlaceHere(session.Player.FieldPlayer.ObjectId));
            return;
        }

        var shopMetadata = FurnishingShopMetadataStorage.GetMetadata(replacementItemId);
        if (shopMetadata == null || !shopMetadata.Buyable)
        {
            return;
        }

        // Not checking if oldFieldCube is null on ground height because default blocks don't have IFieldObjects.
        var oldFieldCube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord.ToFloat());
        if (oldFieldCube == default && coord.Z != groundHeight?.Z)
        {
            return;
        }

        IFieldObject<Cube> newFieldCube;
        if (player.IsInDecorPlanner)
        {
            Cube cube = new(new(replacementItemId), plotNumber, coord.ToFloat(), rotation);

            newFieldCube = session.FieldManager.RequestFieldObject(cube);
            newFieldCube.Coord = coord.ToFloat();
            newFieldCube.Rotation = rotation;

            home.DecorPlannerInventory.Remove(oldFieldCube.Value.Uid);
            session.FieldManager.State.RemoveCube(oldFieldCube.ObjectId);

            home.DecorPlannerInventory.Add(cube.Uid, cube);
            session.FieldManager.BroadcastPacket(ResponseCubePacket.ReplaceCube(session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId, newFieldCube, false));
            session.FieldManager.State.AddCube(newFieldCube);
            return;
        }

        IFieldObject<Player> homeOwner = session.FieldManager.State.Players.Values.FirstOrDefault(x => x.Value.AccountId == home.AccountId);
        if (player.Account.Id != home.AccountId)
        {
            if (homeOwner == default)
            {
                session.SendNotice("You cannot do that unless the home owner is present."); // TODO: use notice packet
                return;
            }

            if (!home.BuildingPermissions.Contains(player.Account.Id))
            {
                session.SendNotice("You don't have building rights.");
                return;
            }
        }

        var warehouseItems = home.WarehouseInventory;
        var furnishingInventory = home.FurnishingInventory;
        Item item;
        if (player.Account.Id != home.AccountId)
        {
            if ((!warehouseItems.TryGetValue(replacementItemUid, out item) || warehouseItems[replacementItemUid].Amount <= 0) && !PurchaseFurnishingItemFromBudget(session.FieldManager, homeOwner.Value, home, shopMetadata))
            {
                NotEnoughMoneyInBudget(session, shopMetadata);
                return;
            }
        }
        else
        {
            if ((!warehouseItems.TryGetValue(replacementItemUid, out item) || warehouseItems[replacementItemUid].Amount <= 0) && !PurchaseFurnishingItem(session, shopMetadata))
            {
                NotEnoughMoney(session, shopMetadata);
                return;
            }
        }

        newFieldCube = AddCube(session, item, replacementItemId, rotation, coord.ToFloat(), plotNumber, homeOwner, home);

        if (oldFieldCube != null)
        {
            furnishingInventory.Remove(oldFieldCube.Value.Uid);
            DatabaseManager.Cubes.Delete(oldFieldCube.Value.Uid);
            homeOwner.Value.Session.Send(FurnishingInventoryPacket.Remove(oldFieldCube.Value));
            session.FieldManager.State.RemoveCube(oldFieldCube.ObjectId);
        }

        homeOwner.Value.Session.Send(FurnishingInventoryPacket.Load(newFieldCube.Value));
        if (oldFieldCube?.Value.Item != null)
        {
            _ = home.AddWarehouseItem(homeOwner.Value.Session, oldFieldCube.Value.Item.Id, 1, oldFieldCube.Value.Item);
        }

        session.FieldManager.BroadcastPacket(ResponseCubePacket.ReplaceCube(homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId, newFieldCube, false));
        session.FieldManager.AddCube(newFieldCube, homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId);

        AddFunctionCube(session, coord, newFieldCube);
    }

    private static void HandlePickup(GameSession session, IPacketReader packet)
    {
        var coords = packet.Read<CoordB>();

        var weaponId = MapEntityStorage.GetWeaponObjectItemId(session.Player.MapId, coords);
        if (weaponId == 0)
        {
            return;
        }

        session.Send(ResponseCubePacket.Pickup(session.Player.FieldPlayer, weaponId, coords));
        session.FieldManager.BroadcastPacket(UserBattlePacket.UserBattle(session.Player.FieldPlayer, true));
    }

    private static void HandleDrop(GameSession session)
    {
        // Drop item then set battle state to false
        session.Send(ResponseCubePacket.Drop(session.Player.FieldPlayer));
        session.FieldManager.BroadcastPacket(UserBattlePacket.UserBattle(session.Player.FieldPlayer, false));
    }

    private static void HandleHomeName(GameSession session, IPacketReader packet)
    {
        var name = packet.ReadUnicodeString();

        var player = session.Player;
        var home = player.Account.Home;
        if (player.AccountId != home.AccountId)
        {
            return;
        }

        home.Name = name;
        GameServer.HomeManager.GetHomeById(home.Id).Name = name;
        session.FieldManager.BroadcastPacket(ResponseCubePacket.HomeName(player));
        session.FieldManager.BroadcastPacket(ResponseCubePacket.LoadHome(session.Player.FieldPlayer.ObjectId, session.Player.Account.Home));
    }

    private static void HandleHomePassword(GameSession session, IPacketReader packet)
    {
        packet.ReadByte();
        var password = packet.ReadUnicodeString();

        var home = session.Player.Account.Home;
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        home.Password = password;
        GameServer.HomeManager.GetHomeById(home.Id).Password = password;
        session.FieldManager.BroadcastPacket(ResponseCubePacket.ChangePassword());
        session.FieldManager.BroadcastPacket(ResponseCubePacket.LoadHome(session.Player.FieldPlayer.ObjectId, session.Player.Account.Home));
    }

    private static void HandleNominateHouse(GameSession session)
    {
        var player = session.Player;
        var home = GameServer.HomeManager.GetHomeById(player.VisitingHomeId);

        home.ArchitectScoreCurrent++;
        home.ArchitectScoreTotal++;

        session.FieldManager.BroadcastPacket(ResponseCubePacket.UpdateArchitectScore(home.ArchitectScoreCurrent, home.ArchitectScoreTotal));
        IFieldObject<Player> owner = session.FieldManager.State.Players.Values.FirstOrDefault(x => x.Value.Account.Home.Id == player.VisitingHomeId);
        if (owner != default)
        {
            owner.Value.Session.Send(HomeCommandPacket.UpdateArchitectScore(owner.ObjectId, home.ArchitectScoreCurrent, home.ArchitectScoreTotal));
        }

        session.Send(ResponseCubePacket.ArchitectScoreExpiration(player.AccountId, TimeInfo.Now()));
    }

    private static void HandleHomeDescription(GameSession session, IPacketReader packet)
    {
        var description = packet.ReadUnicodeString();

        var home = session.Player.Account.Home;
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        home.Description = description;
        GameServer.HomeManager.GetHomeById(home.Id).Description = description;
        session.FieldManager.BroadcastPacket(ResponseCubePacket.HomeDescription(description));
    }

    private static void HandleClearInterior(GameSession session)
    {
        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (home == null)
        {
            return;
        }

        foreach (var cube in session.FieldManager.State.Cubes.Values)
        {
            RemoveCube(session, session.Player.FieldPlayer, cube, home);
        }

        session.SendNotice("The interior has been cleared!"); // TODO: use notice packet
    }

    private static void HandleRequestLayout(GameSession session, IPacketReader packet)
    {
        var layoutId = packet.ReadInt();

        if (!session.FieldManager.State.Cubes.IsEmpty)
        {
            session.SendNotice("Please clear the interior first."); // TODO: use notice packet
            return;
        }

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (home == null)
        {
            return;
        }

        var layout = home.Layouts.FirstOrDefault(x => x.Id == layoutId);
        if (layout == default)
        {
            return;
        }

        var groupedCubes = layout.Cubes.GroupBy(x => x.Item.Id).ToDictionary(x => x.Key, x => x.Count()); // Dictionary<item id, count> 

        var cubeCount = 0;
        Dictionary<byte, long> cubeCosts = new();
        foreach (var cube in groupedCubes)
        {
            var item = home.WarehouseInventory.Values.FirstOrDefault(x => x.Id == cube.Key);
            if (item == null)
            {
                var shopMetadata = FurnishingShopMetadataStorage.GetMetadata(cube.Key);
                if (cubeCosts.ContainsKey(shopMetadata.FurnishingTokenType))
                {
                    cubeCosts[shopMetadata.FurnishingTokenType] += shopMetadata.Price * cube.Value;
                }
                else
                {
                    cubeCosts.Add(shopMetadata.FurnishingTokenType, shopMetadata.Price * cube.Value);
                }

                cubeCount += cube.Value;
                continue;
            }

            if (item.Amount < cube.Value)
            {
                var shopMetadata = FurnishingShopMetadataStorage.GetMetadata(cube.Key);
                var missingCubes = cube.Value - item.Amount;
                if (cubeCosts.ContainsKey(shopMetadata.FurnishingTokenType))
                {
                    cubeCosts[shopMetadata.FurnishingTokenType] += shopMetadata.Price * missingCubes;
                }
                else
                {
                    cubeCosts.Add(shopMetadata.FurnishingTokenType, shopMetadata.Price * missingCubes);
                }

                cubeCount += missingCubes;
            }
        }

        session.Send(ResponseCubePacket.BillPopup(cubeCosts, cubeCount));
    }

    private static void HandleDecorPlannerLoadLayout(GameSession session, IPacketReader packet)
    {
        var layoutId = packet.ReadInt();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        var layout = home.Layouts.FirstOrDefault(x => x.Id == layoutId);

        if (layout == default)
        {
            return;
        }

        home.Size = layout.Size;
        home.Height = layout.Height;
        session.Send(ResponseCubePacket.UpdateHomeSizeAndHeight(layout.Size, layout.Height));

        var x = -1 * Block.BLOCK_SIZE * (home.Size - 1);
        foreach (IFieldObject<Player> fieldPlayer in session.FieldManager.State.Players.Values)
        {
            fieldPlayer.Value.Session.Send(UserMoveByPortalPacket.Move(fieldPlayer, CoordF.From(x, x, Block.BLOCK_SIZE * 3), CoordF.From(0, 0, 0)));
        }

        foreach (var layoutCube in layout.Cubes)
        {
            Cube cube = new(new(layoutCube.Item.Id), layoutCube.PlotNumber, layoutCube.CoordF, layoutCube.Rotation);
            var fieldCube = session.FieldManager.RequestFieldObject(layoutCube);
            fieldCube.Coord = layoutCube.CoordF;
            fieldCube.Rotation = layoutCube.Rotation;
            home.DecorPlannerInventory.Add(layoutCube.Uid, layoutCube);

            session.FieldManager.AddCube(fieldCube, session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId);
        }

        session.SendNotice("Layout loaded succesfully!"); // TODO: Use notice packet
    }

    private static void HandleLoadLayout(GameSession session, IPacketReader packet)
    {
        var layoutId = packet.ReadInt();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        var layout = home.Layouts.FirstOrDefault(x => x.Id == layoutId);

        if (layout == default)
        {
            return;
        }

        home.Size = layout.Size;
        home.Height = layout.Height;
        session.Send(ResponseCubePacket.UpdateHomeSizeAndHeight(layout.Size, layout.Height));

        var x = -1 * Block.BLOCK_SIZE * (home.Size - 1);
        foreach (IFieldObject<Player> fieldPlayer in session.FieldManager.State.Players.Values)
        {
            fieldPlayer.Value.Session.Send(UserMoveByPortalPacket.Move(fieldPlayer, CoordF.From(x, x, Block.BLOCK_SIZE * 3), CoordF.From(0, 0, 0)));
        }

        foreach (var cube in layout.Cubes)
        {
            var item = home.WarehouseInventory.Values.FirstOrDefault(x => x.Id == cube.Item.Id);
            var fieldCube = AddCube(session, item, cube.Item.Id, cube.Rotation, cube.CoordF, cube.PlotNumber, session.Player.FieldPlayer, home);
            session.Send(FurnishingInventoryPacket.Load(fieldCube.Value));
            if (fieldCube.Coord.Z == 0)
            {
                session.FieldManager.BroadcastPacket(ResponseCubePacket.ReplaceCube(session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId, fieldCube, false));
            }

            session.FieldManager.AddCube(fieldCube, session.Player.FieldPlayer.ObjectId, session.Player.FieldPlayer.ObjectId);
        }

        session.Send(WarehouseInventoryPacket.Count(home.WarehouseInventory.Count));
        session.SendNotice("Layout loaded succesfully!"); // TODO: Use notice packet
    }

    private static void HandleModifySize(GameSession session, byte operation)
    {
        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        if (operation == RequestCubeOperations.IncreaseSize && home.Size + 1 > 25 || operation == RequestCubeOperations.IncreaseHeight && home.Height + 1 > 15)
        {
            return;
        }

        if (operation == RequestCubeOperations.DecreaseSize && home.Size - 1 < 4 || operation == RequestCubeOperations.DecreaseHeight && home.Height - 1 < 3)
        {
            return;
        }

        RemoveBlocks(session, operation, home);

        if (session.Player.IsInDecorPlanner)
        {
            switch (operation)
            {
                case RequestCubeOperations.IncreaseSize:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.IncreaseSize(++home.DecorPlannerSize));
                    break;
                case RequestCubeOperations.DecreaseSize:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.DecreaseSize(--home.DecorPlannerSize));
                    break;
                case RequestCubeOperations.IncreaseHeight:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.IncreaseHeight(++home.DecorPlannerHeight));
                    break;
                case RequestCubeOperations.DecreaseHeight:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.DecreaseHeight(--home.DecorPlannerHeight));
                    break;
            }
        }
        else
        {
            switch (operation)
            {
                case RequestCubeOperations.IncreaseSize:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.IncreaseSize(++home.Size));
                    break;
                case RequestCubeOperations.DecreaseSize:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.DecreaseSize(--home.Size));
                    break;
                case RequestCubeOperations.IncreaseHeight:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.IncreaseHeight(++home.Height));
                    break;
                case RequestCubeOperations.DecreaseHeight:
                    session.FieldManager.BroadcastPacket(ResponseCubePacket.DecreaseHeight(--home.Height));
                    break;
            }
        }

        // move players to safe coord
        if (operation == RequestCubeOperations.DecreaseHeight || operation == RequestCubeOperations.DecreaseSize)
        {
            int x;
            if (session.Player.IsInDecorPlanner)
            {
                x = -1 * Block.BLOCK_SIZE * (home.DecorPlannerSize - 1);
            }
            else
            {
                x = -1 * Block.BLOCK_SIZE * (home.Size - 1);
            }

            foreach (IFieldObject<Player> fieldPlayer in session.FieldManager.State.Players.Values)
            {
                fieldPlayer.Value.Session.Send(UserMoveByPortalPacket.Move(fieldPlayer, CoordF.From(x, x, Block.BLOCK_SIZE * 3), CoordF.From(0, 0, 0)));
            }
        }
    }

    private static void HandleSaveLayout(GameSession session, IPacketReader packet)
    {
        var layoutId = packet.ReadInt();
        var layoutName = packet.ReadUnicodeString();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        var layout = home.Layouts.FirstOrDefault(x => x.Id == layoutId);
        if (layout != default)
        {
            DatabaseManager.HomeLayouts.Delete(layout.Uid);
            home.Layouts.Remove(layout);
        }

        if (session.Player.IsInDecorPlanner)
        {
            home.Layouts.Add(new(home.Id, layoutId, layoutName, home.DecorPlannerSize, home.DecorPlannerHeight, TimeInfo.Now(), home.DecorPlannerInventory.Values.ToList()));
        }
        else
        {
            home.Layouts.Add(new(home.Id, layoutId, layoutName, home.Size, home.Height, TimeInfo.Now(), home.FurnishingInventory.Values.ToList()));
        }

        session.Send(ResponseCubePacket.SaveLayout(home.AccountId, layoutId, layoutName, TimeInfo.Now()));
    }

    private static void HandleDecorationReward(GameSession session)
    {
        // Decoration score goals
        Dictionary<ItemHousingCategory, int> goals = new()
        {
            { ItemHousingCategory.Bed, 1 },
            { ItemHousingCategory.Table, 1 },
            { ItemHousingCategory.SofasChairs, 2 },
            { ItemHousingCategory.Storage, 1 },
            { ItemHousingCategory.WallDecoration, 1 },
            { ItemHousingCategory.WallTiles, 3 },
            { ItemHousingCategory.Bathroom, 1 },
            { ItemHousingCategory.Lighting, 1 },
            { ItemHousingCategory.Electronics, 1 },
            { ItemHousingCategory.Fences, 2 },
            { ItemHousingCategory.NaturalTerrain, 4 }
        };

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (home == null || session.Player.AccountId != home.AccountId)
        {
            return;
        }

        var items = home.FurnishingInventory.Values.Select(x => x.Item).ToList();
        items.ForEach(x => x.HousingCategory = ItemMetadataStorage.GetHousingCategory(x.Id));
        var current = items.GroupBy(x => x.HousingCategory).ToDictionary(x => x.Key, x => x.Count());

        var decorationScore = 0;
        foreach (var category in goals.Keys)
        {
            current.TryGetValue(category, out var currentCount);
            if (currentCount == 0)
            {
                continue;
            }

            if (currentCount >= goals[category])
            {
                switch (category)
                {
                    case ItemHousingCategory.Bed:
                    case ItemHousingCategory.SofasChairs:
                    case ItemHousingCategory.WallDecoration:
                    case ItemHousingCategory.WallTiles:
                    case ItemHousingCategory.Bathroom:
                    case ItemHousingCategory.Lighting:
                    case ItemHousingCategory.Fences:
                    case ItemHousingCategory.NaturalTerrain:
                        decorationScore += 100;
                        break;
                    case ItemHousingCategory.Table:
                    case ItemHousingCategory.Storage:
                        decorationScore += 50;
                        break;
                    case ItemHousingCategory.Electronics:
                        decorationScore += 200;
                        break;
                }
            }
        }

        List<int> rewardsIds = new()
        {
            20300039,
            20000071,
            20301018
        }; // Default rewards
        switch (decorationScore)
        {
            case < 300:
                rewardsIds.Add(20000028);
                break;
            case >= 300 and < 500:
                rewardsIds.Add(20000029);
                break;
            case >= 500 and < 1100:
                rewardsIds.Add(20300078);
                rewardsIds.Add(20000030);
                break;
            default:
                rewardsIds.Add(20300078);
                rewardsIds.Add(20000031);
                rewardsIds.Add(20300040);
                session.Player.Inventory.AddItem(session, new(20300559), true);
                break;
        }

        home.GainExp(decorationScore);
        session.Player.Inventory.AddItem(session, new(rewardsIds.OrderBy(x => RandomProvider.Get().Next()).First()), true);
        home.DecorationRewardTimestamp = TimeInfo.Now();
        session.Send(ResponseCubePacket.DecorationScore(home));
    }

    private static void HandleInteriorDesingReward(GameSession session, IPacketReader packet)
    {
        var rewardId = packet.ReadByte();
        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (home == null || session.Player.AccountId != home.AccountId)
        {
            return;
        }

        if (rewardId <= 1 || rewardId >= 11 || home == null || home.InteriorRewardsClaimed.Contains(rewardId))
        {
            return;
        }

        var metadata = MasteryUGCHousingMetadataStorage.GetMetadata(rewardId);
        if (metadata == null)
        {
            return;
        }

        session.Player.Inventory.AddItem(session, new(metadata.ItemId), true);
        home.InteriorRewardsClaimed.Add(rewardId);
        session.Send(ResponseCubePacket.DecorationScore(home));
    }

    private static void HandleKickEveryone(GameSession session)
    {
        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        var players = session.FieldManager.State.Players.Values.Where(p => p.Value.CharacterId != session.Player.CharacterId).ToList();
        foreach (IFieldObject<Player> fieldPlayer in players)
        {
            fieldPlayer.Value.Session.Send(ResponseCubePacket.KickEveryone());
        }

        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            players = session.FieldManager.State.Players.Values.Where(p => p.Value.CharacterId != session.Player.CharacterId).ToList();

            foreach (IFieldObject<Player> fieldPlayer in players)
            {
                var player = fieldPlayer.Value;
                player.Warp(player.ReturnMapId, player.ReturnCoord);
            }
        });
    }

    private static void HandleEnablePermission(GameSession session, IPacketReader packet)
    {
        var permission = (HomePermission) packet.ReadByte();
        var enabled = packet.ReadBool();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        if (enabled)
        {
            home.Permissions[permission] = 0;
        }
        else
        {
            home.Permissions.Remove(permission);
        }

        session.FieldManager.BroadcastPacket(ResponseCubePacket.EnablePermission(permission, enabled));
    }

    private static void HandleSetPermission(GameSession session, IPacketReader packet)
    {
        var permission = (HomePermission) packet.ReadByte();
        var setting = packet.ReadByte();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (session.Player.AccountId != home.AccountId)
        {
            return;
        }

        if (home.Permissions.ContainsKey(permission))
        {
            home.Permissions[permission] = setting;
        }

        session.FieldManager.BroadcastPacket(ResponseCubePacket.SetPermission(permission, setting));
    }

    private static void HandleUpdateBudget(GameSession session, IPacketReader packet)
    {
        var mesos = packet.ReadLong();
        var merets = packet.ReadLong();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        if (home == null || session.Player.AccountId != home.AccountId)
        {
            return;
        }

        home.Mesos = mesos;
        home.Merets = merets;

        session.FieldManager.BroadcastPacket(ResponseCubePacket.UpdateBudget(home));
    }

    private static void HandleModifyInteriorSettings(GameSession session, byte operation, IPacketReader packet)
    {
        var value = packet.ReadByte();

        var home = GameServer.HomeManager.GetHomeById(session.Player.VisitingHomeId);
        switch (operation)
        {
            case RequestCubeOperations.ChangeBackground:
                home.Background = value;
                session.FieldManager.BroadcastPacket(ResponseCubePacket.ChangeLighting(value));
                break;
            case RequestCubeOperations.ChangeLighting:
                home.Lighting = value;
                session.FieldManager.BroadcastPacket(ResponseCubePacket.ChangeBackground(value));
                break;
            case RequestCubeOperations.ChangeCamera:
                home.Camera = value;
                session.FieldManager.BroadcastPacket(ResponseCubePacket.ChangeCamera(value));
                break;
        }
    }

    private static void HandleGiveBuildingPermission(GameSession session, IPacketReader packet)
    {
        var characterName = packet.ReadUnicodeString();

        var player = session.Player;
        var home = GameServer.HomeManager.GetHomeById(player.VisitingHomeId);
        if (player.AccountId != home.AccountId)
        {
            return;
        }

        var target = GameServer.PlayerManager.GetPlayerByName(characterName);
        if (home.BuildingPermissions.Contains(target.AccountId))
        {
            return;
        }

        home.BuildingPermissions.Add(target.AccountId);

        session.Send(ResponseCubePacket.AddBuildingPermission(target.AccountId));
        session.SendNotice($"You have granted furnishing rights to {target.Name}."); // TODO: use the notice packet
        target.Session.Send(ResponseCubePacket.UpdateBuildingPermissions(target.AccountId, player.AccountId));
        target.Session.SendNotice("You have been granted furnishing rights."); // TODO: use the notice packet
    }

    private static void HandleRemoveBuildingPermission(GameSession session, IPacketReader packet)
    {
        var characterName = packet.ReadUnicodeString();

        var player = session.Player;
        var home = GameServer.HomeManager.GetHomeById(player.VisitingHomeId);
        if (player.AccountId != home.AccountId)
        {
            return;
        }

        var target = GameServer.PlayerManager.GetPlayerByName(characterName);
        if (!home.BuildingPermissions.Contains(target.AccountId))
        {
            return;
        }

        home.BuildingPermissions.Remove(target.AccountId);

        session.Send(ResponseCubePacket.RemoveBuildingPermission(target.AccountId, target.Name));
        target.Session.Send(ResponseCubePacket.UpdateBuildingPermissions(0, player.AccountId));
        target.Session.SendNotice("Your furnishing right has been removed."); // TODO: use the notice packet
    }

    private static bool PurchaseFurnishingItem(GameSession session, FurnishingShopMetadata shop)
    {
        switch (shop.FurnishingTokenType)
        {
            case 1: // meso
                return session.Player.Wallet.Meso.Modify(-shop.Price);
            case 3: // meret
                return session.Player.Account.RemoveMerets(shop.Price);
            default:
                session.SendNotice($"Unknown currency: {shop.FurnishingTokenType}");
                return false;
        }
    }

    private static bool PurchaseFurnishingItemFromBudget(FieldManager fieldManager, Player owner, Home home, FurnishingShopMetadata shop)
    {
        switch (shop.FurnishingTokenType)
        {
            case 1: // meso
                if (home.Mesos - shop.Price >= 0)
                {
                    home.Mesos -= shop.Price;
                    owner.Wallet.Meso.Modify(-shop.Price);
                    fieldManager.BroadcastPacket(ResponseCubePacket.UpdateBudget(home));
                    return true;
                }

                return false;
            case 3: // meret
                if (home.Merets - shop.Price >= 0)
                {
                    home.Merets -= shop.Price;
                    owner.Account.RemoveMerets(shop.Price);
                    fieldManager.BroadcastPacket(ResponseCubePacket.UpdateBudget(home));
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static bool HandlePlotPayment(GameSession session, int priceItemCode, int price)
    {
        switch (priceItemCode)
        {
            case 0:
                return true;
            case 90000001:
            case 90000002:
            case 90000003:
                return session.Player.Wallet.Meso.Modify(-price);
            case 90000004:
                return session.Player.Account.RemoveMerets(price);
            case 90000006:
                return session.Player.Wallet.ValorToken.Modify(-price);
            case 90000013:
                return session.Player.Wallet.Rue.Modify(-price);
            case 90000014:
                return session.Player.Wallet.HaviFruit.Modify(-price);
            case 90000017:
                return session.Player.Wallet.Treva.Modify(-price);
            default:
                session.SendNotice($"Unknown item currency: {priceItemCode}");
                return false;
        }
    }

    private static bool IsCoordOutsideHeightLimit(CoordS coordS, int mapId, byte height)
    {
        var mapBlocks = MapMetadataStorage.GetMetadata(mapId).Blocks;
        for (var i = 0; i <= height; i++) // checking blocks in the same Z axis
        {
            mapBlocks.TryGetValue(coordS, out var block);
            if (block == null)
            {
                coordS.Z -= Block.BLOCK_SIZE;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsCoordOutsideSizeLimit(CoordB cubeCoord, int homeSize)
    {
        var size = (homeSize - 1) * -1;

        return cubeCoord.Y < size || cubeCoord.Y > 0 || cubeCoord.X < size || cubeCoord.X > 0;
    }

    private static CoordB? GetGroundCoord(CoordB coord, int mapId, byte height)
    {
        var mapBlocks = MapMetadataStorage.GetMetadata(mapId).Blocks;
        for (var i = 0; i <= height; i++)
        {
            mapBlocks.TryGetValue(coord.ToShort(), out var block);
            if (block == null)
            {
                coord -= CoordB.From(0, 0, 1);
                continue;
            }

            return block.Coord.ToByte();
        }

        return null;
    }

    private static void RemoveBlocks(GameSession session, byte operation, Home home)
    {
        if (operation == RequestCubeOperations.DecreaseSize)
        {
            var maxSize = (home.Size - 1) * Block.BLOCK_SIZE * -1;
            for (var i = 0; i < home.Size; i++)
            {
                for (var j = 0; j <= home.Height; j++)
                {
                    var coord = CoordF.From(maxSize, i * Block.BLOCK_SIZE * -1, j * Block.BLOCK_SIZE);
                    var cube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord);
                    if (cube != default)
                    {
                        RemoveCube(session, session.Player.FieldPlayer, cube, home);
                    }
                }
            }

            for (var i = 0; i < home.Size; i++)
            {
                for (var j = 0; j <= home.Height; j++)
                {
                    var coord = CoordF.From(i * Block.BLOCK_SIZE * -1, maxSize, j * Block.BLOCK_SIZE);
                    var cube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord);
                    if (cube != default)
                    {
                        RemoveCube(session, session.Player.FieldPlayer, cube, home);
                    }
                }
            }
        }

        if (operation == RequestCubeOperations.DecreaseHeight)
        {
            for (var i = 0; i < home.Size; i++)
            {
                for (var j = 0; j < home.Size; j++)
                {
                    var coord = CoordF.From(i * Block.BLOCK_SIZE * -1, j * Block.BLOCK_SIZE * -1, home.Height * Block.BLOCK_SIZE);
                    var cube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coord);
                    if (cube != default)
                    {
                        RemoveCube(session, session.Player.FieldPlayer, cube, home);
                    }
                }
            }
        }
    }

    private static IFieldObject<Cube> AddCube(GameSession session, Item item, int itemId, CoordF rotation, CoordF coordF, int plotNumber, IFieldObject<Player> homeOwner, Home home)
    {
        IFieldObject<Cube> fieldCube;
        var warehouseItems = home.WarehouseInventory;
        var furnishingInventory = home.FurnishingInventory;
        if (item == null || item.Amount <= 0)
        {
            Cube cube = new(new(itemId), plotNumber, coordF, rotation, homeId: home.Id);

            fieldCube = session.FieldManager.RequestFieldObject(cube);
            fieldCube.Coord = coordF;
            fieldCube.Rotation = rotation;

            homeOwner.Value.Session.Send(WarehouseInventoryPacket.Load(cube.Item, warehouseItems.Values.Count));
            homeOwner.Value.Session.Send(WarehouseInventoryPacket.GainItemMessage(cube.Item, 1));
            homeOwner.Value.Session.Send(WarehouseInventoryPacket.Count(warehouseItems.Values.Count + 1));
            session.FieldManager.BroadcastPacket(ResponseCubePacket.PlaceFurnishing(fieldCube, homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId, true));
            homeOwner.Value.Session.Send(WarehouseInventoryPacket.Remove(cube.Item.Uid));
        }
        else
        {
            Cube cube = new(item, plotNumber, coordF, rotation, homeId: home.Id);

            fieldCube = session.FieldManager.RequestFieldObject(cube);
            fieldCube.Coord = coordF;
            fieldCube.Rotation = rotation;

            if (item.Amount - 1 > 0)
            {
                item.Amount--;
                session.Send(WarehouseInventoryPacket.UpdateAmount(item.Uid, item.Amount));
            }
            else
            {
                warehouseItems.Remove(item.Uid);
                session.Send(WarehouseInventoryPacket.Remove(item.Uid));
            }
        }

        furnishingInventory.Add(fieldCube.Value.Uid, fieldCube.Value);
        return fieldCube;
    }

    private static void RemoveCube(GameSession session, IFieldObject<Player> homeOwner, IFieldObject<Cube> cube, Home home)
    {
        var warehouseItems = home.WarehouseInventory;
        var furnishingInventory = home.FurnishingInventory;

        if (session.Player.IsInDecorPlanner)
        {
            home.DecorPlannerInventory.Remove(cube.Value.Uid);
            session.FieldManager.RemoveCube(cube, homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId);
            return;
        }

        furnishingInventory.Remove(cube.Value.Uid);
        homeOwner.Value.Session.Send(FurnishingInventoryPacket.Remove(cube.Value));

        DatabaseManager.Cubes.Delete(cube.Value.Uid);
        _ = home.AddWarehouseItem(homeOwner.Value.Session, cube.Value.Item.Id, 1, cube.Value.Item);
        session.FieldManager.RemoveCube(cube, homeOwner.ObjectId, session.Player.FieldPlayer.ObjectId);
        if (cube.Value.Item.Id == 50400158) // portal cube
        {
            session.FieldManager.State.Portals.TryGetValue(cube.Value.PortalSettings.PortalObjectId, out var fieldPortal);
            session.FieldManager.RemovePortal(fieldPortal);
        }
    }

    private static void NotEnoughMoney(GameSession session, FurnishingShopMetadata shopMetadata)
    {
        var currency = "";
        switch (shopMetadata.FurnishingTokenType)
        {
            case 1:
                currency = "mesos";
                break;
            case 3:
                currency = "merets";
                break;
        }

        session.SendNotice($"You don't have enough {currency}!");
    }

    private static void NotEnoughMoneyInBudget(GameSession session, FurnishingShopMetadata shopMetadata)
    {
        var currency = "";
        switch (shopMetadata.FurnishingTokenType)
        {
            case 1:
                currency = "mesos";
                break;
            case 3:
                currency = "merets";
                break;
        }

        session.SendNotice($"Budget doesn't have enough {currency}!");
    }

    private static void AddFunctionCube(GameSession session, CoordB coord, IFieldObject<Cube> newFieldCube)
    {
        if (newFieldCube.Value.Item.HousingCategory is ItemHousingCategory.Ranching or ItemHousingCategory.Farming)
        {
            session.FieldManager.BroadcastPacket(FunctionCubePacket.UpdateFunctionCube(coord, 1, 0));
            session.FieldManager.BroadcastPacket(FunctionCubePacket.UpdateFunctionCube(coord, 2, 1));
        }

        if (newFieldCube.Value.Item.Id == 50400158) // portal cube
        {
            session.FieldManager.BroadcastPacket(FunctionCubePacket.UpdateFunctionCube(coord, 0, 0));
        }
    }
}
