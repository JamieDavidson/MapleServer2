using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class SuperChatHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.SUPER_WORLDCHAT;

    private static class SuperChatOperations
    {
        public const byte Select = 0x0;
        public const byte Deselect = 0x1;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case SuperChatOperations.Select:
                HandleSelect(session, packet);
                break;
            case SuperChatOperations.Deselect:
                HandleDeselect(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleSelect(GameSession session, IPacketReader packet)
    {
        int itemId = packet.ReadInt();

        var inventory = session.Player.Inventory;
        Item superChatItem = inventory.GetItemByItemId(itemId);
        if (superChatItem == null)
        {
            return;
        }

        session.Player.SuperChat = superChatItem.Function.Id;
        session.Send(SuperChatPacket.Select(session.Player.FieldPlayer, superChatItem.Id));
    }

    private static void HandleDeselect(GameSession session)
    {
        session.Send(SuperChatPacket.Deselect(session.Player.FieldPlayer));
    }
}
