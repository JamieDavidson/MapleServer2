using Maple2Storage.Enums;
using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class HomeActionHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.HOME_ACTION;

    private static class HomeActionOperations
    {
        public const byte Smite = 0x01;
        public const byte Kick = 0x02;
        public const byte Survey = 0x05;
        public const byte ChangePortalSettings = 0x06;
        public const byte UpdateBallCoord = 0x07;
        public const byte SendPortalSettings = 0x0D;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case HomeActionOperations.Kick:
                HandleKick(packet);
                break;
            case HomeActionOperations.Survey:
                HandleRespondSurvey(session, packet);
                break;
            case HomeActionOperations.ChangePortalSettings:
                HandleChangePortalSettings(session, packet);
                break;
            case HomeActionOperations.UpdateBallCoord:
                HandleUpdateBallCoord(session, packet);
                break;
            case HomeActionOperations.SendPortalSettings:
                HandleSendPortalSettings(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleRespondSurvey(GameSession session, IPacketReader packet)
    {
        packet.ReadByte();
        packet.ReadLong(); // character id
        var surveyId = packet.ReadLong();
        var responseIndex = packet.ReadByte();

        var player = session.Player;

        var home = GameServer.HomeManager.GetHomeById(player.VisitingHomeId);
        var homeSurvey = home.Survey;

        var option = homeSurvey.Options.Keys.ToList()[responseIndex];
        if (!homeSurvey.Started || homeSurvey.Ended || homeSurvey.Id != surveyId || option is null || homeSurvey.Options[option].Contains(player.Name) || !homeSurvey.AvailableCharacters.Contains(player.Name))
        {
            return;
        }

        homeSurvey.AvailableCharacters.Remove(player.Name);
        homeSurvey.Options[option].Add(player.Name);
        session.Send(HomeActionPacket.SurveyAnswer(player.Name));

        homeSurvey.Answers++;

        if (homeSurvey.Answers < homeSurvey.MaxAnswers)
        {
            return;
        }

        session.FieldManager.BroadcastPacket(HomeActionPacket.SurveyEnd(homeSurvey));
        homeSurvey.End();
    }

    private static void HandleKick(IPacketReader packet)
    {
        var characterName = packet.ReadUnicodeString();
        var target = GameServer.PlayerManager.GetPlayerByName(characterName);
        if (target == null)
        {
            return;
        }

        target.Warp(target.ReturnMapId, target.ReturnCoord);
        target.ReturnMapId = 0;
        target.VisitingHomeId = 0;
    }

    private static void HandleChangePortalSettings(GameSession session, IPacketReader packet)
    {
        packet.ReadByte();
        var coordB = packet.Read<CoordB>();
        packet.ReadByte();

        var fieldCube = session.FieldManager.State.Cubes.Values.FirstOrDefault(x => x.Coord == coordB.ToFloat());
        if (fieldCube is null)
        {
            return;
        }
        var cube = fieldCube.Value;

        cube.PortalSettings.PortalName = packet.ReadUnicodeString();
        cube.PortalSettings.Method = (UGCPortalMethod) packet.ReadByte();
        cube.PortalSettings.Destination = (UGCPortalDestination) packet.ReadByte();
        cube.PortalSettings.DestinationTarget = packet.ReadUnicodeString();

        DatabaseManager.Cubes.Update(cube);

        UpdateAllPortals(session);
    }

    private static void HandleUpdateBallCoord(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte(); // 2 move, 3 hit ball
        var objectId = packet.ReadInt();
        var coord = packet.Read<CoordF>();
        var velocity1 = packet.Read<CoordF>();

        if (!session.FieldManager.State.Guide.TryGetValue(objectId, out var ball))
        {
            return;
        }

        ball.Coord = coord;

        switch (mode)
        {
            case 2:
                var velocity2 = packet.Read<CoordF>();

                session.FieldManager.BroadcastPacket(HomeActionPacket.UpdateBall(ball, velocity1, velocity2), session);
                break;
            case 3:
                session.FieldManager.BroadcastPacket(HomeActionPacket.HitBall(ball, velocity1));
                break;
        }
    }

    private static void HandleSendPortalSettings(GameSession session, IPacketReader packet)
    {
        var coordB = packet.Read<CoordB>();

        // 50400158 = Portal Cube
        var cube = session.FieldManager.State.Cubes.Values
            .FirstOrDefault(x => x.Coord == coordB.ToFloat() && x.Value.Item.Id == 50400158);
        if (cube is null)
        {
            return;
        }

        var otherPortals = session.FieldManager.State.Cubes.Values
            .Where(x => x.Value.Item.Id == 50400158 && x.Value.Uid != cube.Value.Uid)
            .Select(x => x.Value).ToList();
        session.Send(HomeActionPacket.SendCubePortalSettings(cube.Value, otherPortals));
    }

    private static void UpdateAllPortals(GameSession session)
    {
        foreach (var fieldPortal in session.FieldManager.State.Portals.Values)
        {
            session.FieldManager.RemovePortal(fieldPortal);
        }

        // Re-add cube portals in map
        var fieldCubePortals = session.FieldManager.State.Cubes.Values.Where(x => x.Value.Item.Id == 50400158);
        foreach (var fieldCubePortal in fieldCubePortals)
        {
            var cubePortal = fieldCubePortal.Value;
            Portal portal = new(GuidGenerator.Int())
            {
                IsVisible = true,
                IsEnabled = true,
                IsMinimapVisible = false,
                Rotation = cubePortal.Rotation,
                PortalType = PortalTypes.Home,
                UGCPortalMethod = cubePortal.PortalSettings.Method
            };

            var fieldPortal = session.FieldManager.RequestFieldObject(portal);
            fieldPortal.Coord = cubePortal.CoordF;
            if (!string.IsNullOrEmpty(cubePortal.PortalSettings.DestinationTarget))
            {
                switch (cubePortal.PortalSettings.Destination)
                {
                    case UGCPortalDestination.PortalInHome:
                        fieldPortal.Value.TargetMapId = (int) Map.PrivateResidence;
                        break;
                    case UGCPortalDestination.SelectedMap:
                        fieldPortal.Value.TargetMapId = int.Parse(cubePortal.PortalSettings.DestinationTarget);
                        break;
                    case UGCPortalDestination.FriendHome:
                        fieldPortal.Value.TargetHomeAccountId = long.Parse(cubePortal.PortalSettings.DestinationTarget);
                        break;
                }
            }
            cubePortal.PortalSettings.PortalObjectId = fieldPortal.ObjectId;
            session.FieldManager.AddPortal(fieldPortal);
        }
    }
}
