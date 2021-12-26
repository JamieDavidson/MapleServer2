using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;
using MoonSharp.Interpreter;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestTaxiHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_TAXI;

    private static class RequestTaxiOperation
    {
        public const byte Car = 0x1;
        public const byte RotorsMeso = 0x3;
        public const byte RotorsMeret = 0x4;
        public const byte DiscoverTaxi = 0x5;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        int mapId = 0;
        long meretPrice = 15;

        if (operation != RequestTaxiOperation.DiscoverTaxi)
        {
            mapId = packet.ReadInt();
        }

        switch (operation)
        {
            case RequestTaxiOperation.Car:
                HandleCarTaxi(session, mapId);
                break;
            case RequestTaxiOperation.RotorsMeso:
                HandleRotorMeso(session, mapId);
                break;
            case RequestTaxiOperation.RotorsMeret:
                HandleRotorMeret(session, mapId, meretPrice);
                break;
            case RequestTaxiOperation.DiscoverTaxi:
                HandleDiscoverTaxi(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleCarTaxi(GameSession session, int mapId)
    {
        if (!WorldMapGraphStorage.CanPathFind(session.Player.MapId.ToString(), mapId.ToString(), out int mapCount))
        {
            Logger.Warn("Path not found.");
            return;
        }

        ScriptLoader scriptLoader = new("Functions/calcTaxiCost");

        DynValue result = scriptLoader.Call("calcTaxiCost", mapCount, session.Player.Levels.Level);
        if (result == null)
        {
            return;
        }

        if (!session.Player.Wallet.Meso.Modify((long) -result.Number))
        {
            return;
        }
        session.Player.Warp(mapId);
    }

    private static void HandleRotorMeso(GameSession session, int mapId)
    {
        // VIP Travel
        Account account = session.Player.Account;
        if (account.IsVip())
        {
            session.Player.Warp(mapId);
            return;
        }

        ScriptLoader scriptLoader = new("Functions/calcAirTaxiCost");

        DynValue result = scriptLoader.Call("calcAirTaxiCost", session.Player.Levels.Level);
        if (result == null)
        {
            return;
        }

        if (!session.Player.Wallet.Meso.Modify((long) -result.Number))
        {
            return;
        }

        session.Player.Warp(mapId);
    }

    private static void HandleRotorMeret(GameSession session, int mapId, long meretPrice)
    {
        if (!session.Player.Account.RemoveMerets(meretPrice))
        {
            return;
        }

        session.Player.Warp(mapId);
    }

    private static void HandleDiscoverTaxi(GameSession session)
    {
        List<int> unlockedTaxis = session.Player.UnlockedTaxis;
        int mapId = session.Player.MapId;
        if (!unlockedTaxis.Contains(mapId))
        {
            unlockedTaxis.Add(mapId);
        }
        session.Send(TaxiPacket.DiscoverTaxi(mapId));
    }
}
