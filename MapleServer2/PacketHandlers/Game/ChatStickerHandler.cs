using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ChatStickerHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CHAT_STICKER;

    private static class ChatStickerOperations
    {
        public const byte OpenWindow = 0x1;
        public const byte UseSticker = 0x3;
        public const byte GroupChatSticker = 0x4;
        public const byte Favorite = 0x5;
        public const byte Unfavorite = 0x6;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ChatStickerOperations.OpenWindow:
                HandleOpenWindow( /*session, packet*/);
                break;
            case ChatStickerOperations.UseSticker:
                HandleUseSticker(session, packet);
                break;
            case ChatStickerOperations.GroupChatSticker:
                HandleGroupChatSticker(session, packet);
                break;
            case ChatStickerOperations.Favorite:
                HandleFavorite(session, packet);
                break;
            case ChatStickerOperations.Unfavorite:
                HandleUnfavorite(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpenWindow( /*GameSession session, ByteReader packet*/)
    {
        // TODO: if user has any expired stickers, use the packet below
        //session.Send(ChatStickerPacket.ExpiredStickerNotification());
    }

    private static void HandleUseSticker(GameSession session, IPacketReader packet)
    {
        var stickerId = packet.ReadInt();
        var script = packet.ReadUnicodeString();

        var groupId = ChatStickerMetadataStorage.GetGroupId(stickerId);

        if (!session.Player.ChatSticker.Any(p => p.GroupId == groupId))
        {
            return;
        }

        session.Send(ChatStickerPacket.UseSticker(stickerId, script));
    }

    private static void HandleGroupChatSticker(GameSession session, IPacketReader packet)
    {
        var stickerId = packet.ReadInt();
        var groupChatName = packet.ReadUnicodeString();

        var groupId = ChatStickerMetadataStorage.GetGroupId(stickerId);

        if (!session.Player.ChatSticker.Any(p => p.GroupId == groupId))
        {
            return;
        }

        session.Send(ChatStickerPacket.GroupChatSticker(stickerId, groupChatName));
    }

    private static void HandleFavorite(GameSession session, IPacketReader packet)
    {
        var stickerId = packet.ReadInt();

        if (session.Player.FavoriteStickers.Contains(stickerId))
        {
            return;
        }
        session.Player.FavoriteStickers.Add(stickerId);
        session.Send(ChatStickerPacket.Favorite(stickerId));
    }

    private static void HandleUnfavorite(GameSession session, IPacketReader packet)
    {
        var stickerId = packet.ReadInt();

        if (!session.Player.FavoriteStickers.Contains(stickerId))
        {
            return;
        }
        session.Player.FavoriteStickers.Remove(stickerId);
        session.Send(ChatStickerPacket.Unfavorite(stickerId));
    }
}
