using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class GuideObjectSync : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.GUIDE_OBJECT_SYNC;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var objectType = packet.ReadByte(); // 0 = build, 1 = fish
        var unk = packet.ReadByte();
        var unk2 = packet.ReadByte();
        var unk3 = packet.ReadByte();
        var unk4 = packet.ReadByte();
        var unk5 = packet.ReadByte();
        var coord = packet.Read<CoordS>();
        var unkCoord = packet.Read<CoordS>();
        var rotation = packet.Read<CoordS>();
        var unk6 = packet.ReadShort();
        var unk7 = packet.ReadInt(); // always -1 ?
        var playerTick = packet.ReadInt();
        var playerTick2 = packet.ReadInt(); // packet is given twice for some reason

        if (session.Player.Guide == null)
        {
            return;
        }

        // TODO: If possible, find a way to stop having the client spam the server with this packet

        if (coord.ToFloat() == session.Player.Guide.Coord) // Possibly a temp fix to avoid spamming all players
        {
            return;
        }

        session.Player.Guide.Rotation = rotation.ToFloat();
        session.Player.Guide.Coord = coord.ToFloat();
        session.FieldManager.BroadcastPacket(GuideObjectPacket.Sync(session.Player.Guide, unk2, unk3, unk4, unk5, unkCoord, unk6, unk7), session);
    }
}
