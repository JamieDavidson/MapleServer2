using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BuildModeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_SET_BUILD_MODE;

    private static class BuildModeOperations
    {
        public const byte Stop = 0x0;
        public const byte Start = 0x1;
    }

    public static class BuildModeTypes
    {
        public const byte Stop = 0x0;
        public const byte House = 0x1;
        public const byte Liftables = 0x2;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();

        switch (mode)
        {
            case BuildModeOperations.Stop:
                HandleStop(session);
                break;
            case BuildModeOperations.Start:
                HandleStart(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), mode);
                break;
        }
    }

    private static void HandleStop(GameSession session)
    {
        if (session.Player.Guide == null)
        {
            return;
        }
        session.FieldManager.BroadcastPacket(BuildModePacket.Use(session.Player.FieldPlayer, BuildModeTypes.Stop));
        session.FieldManager.BroadcastPacket(GuideObjectPacket.Remove(session.Player.Guide));
        session.FieldManager.RemoveGuide(session.Player.Guide);
        session.Player.Guide = null; // remove guide from player
    }

    private static void HandleStart(GameSession session, IPacketReader packet)
    {
        if (session.Player.Guide != null)
        {
            return;
        }

        byte unk = packet.ReadByte();
        int furnishingItemId = packet.ReadInt();
        long furnishingItemUid = packet.ReadLong();

        // Add Guide Object
        CoordF startCoord = Block.ClosestBlock(session.Player.FieldPlayer.Coord);
        startCoord.Z += Block.BLOCK_SIZE;
        GuideObject guide = new(0, session.Player.CharacterId);
        IFieldObject<GuideObject> fieldGuide = session.FieldManager.RequestFieldObject(guide);
        fieldGuide.Coord = startCoord;
        session.Player.Guide = fieldGuide;
        session.FieldManager.AddGuide(fieldGuide);

        session.FieldManager.BroadcastPacket(GuideObjectPacket.Add(fieldGuide));
        session.FieldManager.BroadcastPacket(BuildModePacket.Use(session.Player.FieldPlayer, BuildModeTypes.House, furnishingItemId, furnishingItemUid));
    }
}
