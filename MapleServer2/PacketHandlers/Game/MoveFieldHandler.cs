﻿using Maple2Storage.Enums;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MoveFieldHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_MOVE_FIELD;

    private static class RequestMoveFieldOperations
    {
        public const byte Move = 0x0;
        public const byte LeaveInstance = 0x1;
        public const byte VisitHouse = 0x02;
        public const byte ReturnMap = 0x03;
        public const byte EnterDecorPlaner = 0x04;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case RequestMoveFieldOperations.Move:
                HandleMove(session, packet);
                break;
            case RequestMoveFieldOperations.LeaveInstance:
                HandleLeaveInstance(session);
                break;
            case RequestMoveFieldOperations.VisitHouse:
                HandleVisitHouse(session, packet);
                break;
            case RequestMoveFieldOperations.ReturnMap:
                HandleReturnMap(session);
                break;
            case RequestMoveFieldOperations.EnterDecorPlaner:
                HandleEnterDecorPlaner(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleMove(GameSession session, IPacketReader packet)
    {
        var srcMapId = packet.ReadInt();
        if (srcMapId != session.FieldManager.MapId)
        {
            return;
        }

        var portalId = packet.ReadInt();
        var fieldPortal = session.FieldManager.State.Portals.Values.FirstOrDefault(x => x.Value.Id == portalId);
        if (fieldPortal == default)
        {
            Logger.Warn($"Unable to find portal:{portalId} in map:{srcMapId}");
            return;
        }

        var srcPortal = fieldPortal.Value;
        switch (srcPortal.PortalType)
        {
            case PortalTypes.Field:
                break;
            case PortalTypes.DungeonReturnToLobby:
                var dungeonSession = GameServer.DungeonManager.GetDungeonSessionByInstanceId(session.Player.InstanceId);
                if (dungeonSession == null)
                {
                    return;
                }
                session.Player.Warp(dungeonSession.DungeonLobbyId, instanceId: dungeonSession.DungeonInstanceId);
                return;
            case PortalTypes.LeaveDungeon:
                HandleLeaveInstance(session);
                return;
            case PortalTypes.Home:
                HandleHomePortal(session, fieldPortal);
                return;
            default:
                Logger.Warn($"unknown portal type id: {srcPortal.PortalType}");
                break;
        }

        if (!MapEntityStorage.HasSafePortal(srcMapId) || srcPortal.TargetMapId == 0) // map is instance only
        {
            HandleLeaveInstance(session);
            return;
        }

        var dstPortal = MapEntityStorage.GetPortals(srcPortal.TargetMapId)
            .FirstOrDefault(portal => portal.Id == srcPortal.TargetPortalId); // target map's portal id == source portal's targetPortalId
        if (dstPortal == default)
        {
            session.Player.Warp(srcPortal.TargetMapId);
            return;
        }

        var coord = dstPortal.Coord.ToFloat();

        if (dstPortal.Name == "Portal_cube") // spawn on the next block if portal is a cube
        {
            if (dstPortal.Rotation.Z == Direction.SOUTH_EAST)
            {
                coord.Y -= Block.BLOCK_SIZE;
            }
            else if (dstPortal.Rotation.Z == Direction.NORTH_EAST)
            {
                coord.X += Block.BLOCK_SIZE;
            }
            else if (dstPortal.Rotation.Z == Direction.NORTH_WEST)
            {
                coord.Y += Block.BLOCK_SIZE;
            }
            else if (dstPortal.Rotation.Z == Direction.SOUTH_WEST)
            {
                coord.X -= Block.BLOCK_SIZE;
            }
        }

        session.Player.Warp(srcPortal.TargetMapId, coord, dstPortal.Rotation.ToFloat());
    }

    private static void HandleHomePortal(GameSession session, IFieldObject<Portal> fieldPortal)
    {
        var srcCube = session.FieldManager.State.Cubes.Values
            .FirstOrDefault(x => x.Value.PortalSettings is not null
                                && x.Value.PortalSettings.PortalObjectId == fieldPortal.ObjectId);
        if (srcCube is null)
        {
            return;
        }

        var destinationTarget = srcCube.Value.PortalSettings.DestinationTarget;
        if (string.IsNullOrEmpty(destinationTarget))
        {
            return;
        }

        switch (srcCube.Value.PortalSettings.Destination)
        {
            case UGCPortalDestination.PortalInHome:
                var destinationCube = session.FieldManager.State.Cubes.Values
                    .FirstOrDefault(x => x.Value.PortalSettings is not null
                                        && x.Value.PortalSettings.PortalName == destinationTarget);
                if (destinationCube is null)
                {
                    return;
                }
                session.Player.FieldPlayer.Coord = destinationCube.Coord;
                var coordF = destinationCube.Coord;
                coordF.Z += 25; // Without this the player falls through the ground.
                session.Send(UserMoveByPortalPacket.Move(session.Player.FieldPlayer, coordF, session.Player.FieldPlayer.Rotation));
                break;
            case UGCPortalDestination.SelectedMap:
                session.Player.Warp(int.Parse(destinationTarget));
                break;
            case UGCPortalDestination.FriendHome:
                var friendAccountId = long.Parse(destinationTarget);
                var home = GameServer.HomeManager.GetHomeById(friendAccountId);
                if (home is null)
                {
                    return;
                }
                session.Player.WarpGameToGame((int) Map.PrivateResidence, instanceId: home.InstanceId);
                break;
        }
    }

    private static void HandleLeaveInstance(GameSession session)
    {
        var player = session.Player;
        player.Warp(player.ReturnMapId, player.ReturnCoord, session.Player.FieldPlayer.Rotation);
    }

    private static void HandleVisitHouse(GameSession session, IPacketReader packet)
    {
        var returnMapId = packet.ReadInt();
        packet.Skip(8);
        var accountId = packet.ReadLong();
        var password = packet.ReadUnicodeString();

        var target = GameServer.PlayerManager.GetPlayerByAccountId(accountId);
        if (target is null)
        {
            target = DatabaseManager.Characters.FindPartialPlayerById(accountId);
            if (target is null)
            {
                return;
            }
        }
        var player = session.Player;

        var home = GameServer.HomeManager.GetHomeByAccountId(accountId);
        if (home == null)
        {
            session.SendNotice("This player does not have a home!");
            return;
        }

        if (player.VisitingHomeId == home.Id && player.MapId == (int) Map.PrivateResidence)
        {
            session.SendNotice($"You are already at {target.Name}'s home!");
            return;
        }

        if (home.IsPrivate)
        {
            if (password == "")
            {
                session.Send(EnterUGCMapPacket.RequestPassword(accountId));
                return;
            }

            if (home.Password != password)
            {
                session.Send(EnterUGCMapPacket.WrongPassword(accountId));
                return;
            }
        }

        player.VisitingHomeId = home.Id;
        session.Send(ResponseCubePacket.LoadHome(session.Player.FieldPlayer.ObjectId, home));

        player.WarpGameToGame(home.MapId, home.InstanceId, session.Player.FieldPlayer.Coord, session.Player.FieldPlayer.Rotation);
    }

    // This also leaves decor planning
    private static void HandleReturnMap(GameSession session)
    {
        var player = session.Player;
        if (player.IsInDecorPlanner)
        {
            player.IsInDecorPlanner = false;
            player.Guide = null;
            player.WarpGameToGame((int) Map.PrivateResidence, instanceId: --player.InstanceId);
            return;
        }

        var returnCoord = player.ReturnCoord;
        returnCoord.Z += Block.BLOCK_SIZE;
        player.WarpGameToGame(player.ReturnMapId, 1, returnCoord, session.Player.FieldPlayer.Rotation);
        player.ReturnMapId = 0;
        player.VisitingHomeId = 0;
    }

    private static void HandleEnterDecorPlaner(GameSession session)
    {
        var player = session.Player;
        if (player.IsInDecorPlanner)
        {
            return;
        }

        player.IsInDecorPlanner = true;
        player.Guide = null;
        var home = GameServer.HomeManager.GetHomeById(player.VisitingHomeId);
        home.DecorPlannerHeight = home.Height;
        home.DecorPlannerSize = home.Size;
        home.DecorPlannerInventory = new();
        player.WarpGameToGame((int) Map.PrivateResidence, instanceId: ++player.InstanceId);
    }
}
