using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

public class GlobalPortalHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.GLOBAL_PORTAL;

    private enum GlobalPortalMode : byte
    {
        Enter = 0x2
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        GlobalPortalMode mode = (GlobalPortalMode) packet.ReadByte();

        switch (mode)
        {
            case GlobalPortalMode.Enter:
                HandleEnter(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(mode);
                break;
        }
    }

    private static void HandleEnter(GameSession session, PacketReader packet)
    {
        int globalEventId = packet.ReadInt();
        int selectionIndex = packet.ReadInt();

        GlobalEvent globalEvent = GameServer.GlobalEventManager.GetEventById(globalEventId);
        if (globalEvent == null)
        {
            return;
        }

        Map map = Map.Tria;
        switch (globalEvent.Events[selectionIndex])
        {
            case GlobalEventType.OxQuiz:
                map = Map.MapleOxQuiz;
                break;
            case GlobalEventType.TrapMaster:
                map = Map.TrapMaster;
                break;
            case GlobalEventType.SpringBeach:
                map = Map.SpringBeach;
                break;
            case GlobalEventType.CrazyRunner:
                map = Map.CrazyRunners;
                break;
            case GlobalEventType.FinalSurviver:
                map = Map.SoleSurvivor;
                break;
            case GlobalEventType.GreatEscape:
                map = Map.LudibriumEscape;
                break;
            case GlobalEventType.DanceDanceStop:
                map = Map.DanceDanceStop;
                break;
            case GlobalEventType.CrazyRunnerShanghai:
                map = Map.ShanghaiCrazyRunners;
                break;
            case GlobalEventType.HideAndSeek:
                map = Map.HideAndSeek;
                break;
            case GlobalEventType.RedArena:
                map = Map.RedArena;
                break;
            case GlobalEventType.BloodMine:
                map = Map.CrimsonTearMine;
                break;
            case GlobalEventType.TreasureIsland:
                map = Map.TreasureIsland;
                break;
            case GlobalEventType.ChristmasDanceDanceStop:
                map = Map.HolidayDanceDanceStop;
                break;
            default:
                Logger.Warn($"Unknown Global Event: {globalEvent.Events[selectionIndex]}");
                return;
        }

        session.Player.Mount = null;
        MapPortal portal = MapEntityStorage.GetPortals((int) map).FirstOrDefault(portal => portal.Id == 1);
        session.Player.Warp((int) map, portal.Coord.ToFloat(), portal.Rotation.ToFloat());
    }
}
