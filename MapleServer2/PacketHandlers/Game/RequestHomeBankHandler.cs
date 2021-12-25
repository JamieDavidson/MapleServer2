using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RequestHomeBankHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_HOME_BANK;

    private static class BankOperations
    {
        public const byte House = 0x01;
        public const byte Inventory = 0x02;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case BankOperations.House:
                HandleOpen(session, TimeInfo.Now());
                break;
            case BankOperations.Inventory:
                HandleOpen(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session, long date = 0)
    {
        session.Send(HomeBank.OpenBank(date));
    }
}
