using Maple2Storage.Tools;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BonusGameHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BONUS_GAME;

    private static class BonusGameOperations
    {
        public const byte Open = 0x00;
        public const byte Spin = 0x02;
        public const byte Close = 0x03;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case BonusGameOperations.Open:
                HandleOpen(session, packet);
                break;
            case BonusGameOperations.Spin:
                HandleSpin(session);
                break;
            case BonusGameOperations.Close:
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session, PacketReader packet)
    {
        int gameId = packet.ReadInt();

        // Static data for now
        // Tuple<item id, rarity, quantity>
        List<Tuple<int, byte, int>> items = new()
        {
            new(20000527, 1, 1),
            new(20000528, 1, 1),
            new(20000529, 1, 1),
            new(12100073, 3, 1),
            new(12088889, 3, 1),
            new(11200069, 3, 1),
            new(11900089, 3, 1),
            new(11800087, 3, 1),
            new(40220121, 4, 1),
            new(11050011, 1, 1),
            new(11050020, 1, 1),
            new(20300041, 1, 1)
        };
        session.Send(BonusGamePacket.OpenWheel(items));
    }

    private static void HandleSpin(GameSession session)
    {
        List<Tuple<int, byte, int>> items = new()
        {
            new(20000527, 1, 1),
            new(20000528, 1, 1),
            new(20000529, 1, 1),
            new(12100073, 3, 1),
            new(12088889, 3, 1),
            new(11200069, 3, 1),
            new(11900089, 3, 1),
            new(11800087, 3, 1),
            new(40220121, 4, 1),
            new(11050011, 1, 1),
            new(11050020, 1, 1),
            new(20300041, 1, 1)
        };
        int randomIndex = RandomProvider.Get().Next(0, items.Count);
        session.Send(BonusGamePacket.SpinWheel(randomIndex, items[randomIndex]));
    }
}
