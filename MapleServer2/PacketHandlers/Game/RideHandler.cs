﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Enums;
using MapleServer2.Managers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class RideHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.REQUEST_RIDE;

    private static class RideOperations
    {
        public const byte StartRide = 0x0;
        public const byte StopRide = 0x1;
        public const byte ChangeRide = 0x2;
        public const byte StartMultiPersonRide = 0x3;
        public const byte StopMultiPersonRide = 0x4;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case RideOperations.StartRide:
                HandleStartRide(session, packet);
                break;
            case RideOperations.StopRide:
                HandleStopRide(session, packet);
                break;
            case RideOperations.ChangeRide:
                HandleChangeRide(session, packet);
                break;
            case RideOperations.StartMultiPersonRide:
                HandleStartMultiPersonRide(session, packet);
                break;
            case RideOperations.StopMultiPersonRide:
                HandleStopMultiPersonRide(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleStartRide(GameSession session, PacketReader packet)
    {
        RideType type = (RideType) packet.ReadByte();
        int mountId = packet.ReadInt();
        packet.ReadLong();
        long mountUid = packet.ReadLong();
        // [46-0s] (UgcPacketHelper.Ugc()) but client doesn't set this data?

        var inventory = session.Player.Inventory;
        if (type == RideType.UseItem && !inventory.HasItemWithUid(mountUid))
        {
            return;
        }

        IFieldObject<Mount> fieldMount =
            session.FieldManager.RequestFieldObject(new Mount
            {
                Type = type,
                Id = mountId,
                Uid = mountUid
            });

        fieldMount.Value.Players[0] = session.Player.FieldPlayer;
        session.Player.Mount = fieldMount;

        PacketWriter startPacket = MountPacket.StartRide(session.Player.FieldPlayer);
        session.FieldManager.BroadcastPacket(startPacket);
    }

    private static void HandleStopRide(GameSession session, PacketReader packet)
    {
        packet.ReadByte();
        bool forced = packet.ReadBool(); // Going into water without amphibious riding

        session.Player.Mount = null; // Remove mount from player
        PacketWriter stopPacket = MountPacket.StopRide(session.Player.FieldPlayer, forced);
        session.FieldManager.BroadcastPacket(stopPacket);
    }

    private static void HandleChangeRide(GameSession session, PacketReader packet)
    {
        int mountId = packet.ReadInt();
        long mountUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(mountUid))
        {
            return;
        }

        PacketWriter changePacket = MountPacket.ChangeRide(session.Player.FieldPlayer.ObjectId, mountId, mountUid);
        session.FieldManager.BroadcastPacket(changePacket);
    }

    private static void HandleStartMultiPersonRide(GameSession session, PacketReader packet)
    {
        int otherPlayerObjectId = packet.ReadInt();

        if (!session.FieldManager.State.Players.TryGetValue(otherPlayerObjectId, out IFieldActor<Player> otherPlayer) || otherPlayer.Value.Mount == null)
        {
            return;
        }

        bool isFriend = BuddyManager.IsFriend(session.Player, otherPlayer.Value);
        bool isGuildMember = session.Player != null && otherPlayer.Value.Guild != null && session.Player.Guild.Id == otherPlayer.Value.Guild.Id;
        bool isPartyMember = session.Player.Party == otherPlayer.Value.Party;

        if (!isFriend &&
            !isGuildMember &&
            !isPartyMember)
        {
            return;
        }

        int index = Array.FindIndex(otherPlayer.Value.Mount.Value.Players, 0, otherPlayer.Value.Mount.Value.Players.Length, x => x == null);
        otherPlayer.Value.Mount.Value.Players[index] = session.Player.FieldPlayer;
        session.Player.Mount = otherPlayer.Value.Mount;
        session.FieldManager.BroadcastPacket(MountPacket.StartTwoPersonRide(otherPlayerObjectId, session.Player.FieldPlayer.ObjectId, (byte) (index - 1)));
    }

    private static void HandleStopMultiPersonRide(GameSession session)
    {
        IFieldObject<Player> otherPlayer = session.Player.Mount.Value.Players[0];
        if (otherPlayer == null)
        {
            return;
        }

        session.FieldManager.BroadcastPacket(MountPacket.StopTwoPersonRide(otherPlayer.ObjectId, session.Player.FieldPlayer.ObjectId));
        session.Send(UserMoveByPortalPacket.Move(session.Player.FieldPlayer, otherPlayer.Coord, otherPlayer.Rotation));
        session.Player.Mount = null;
        if (otherPlayer.Value.Mount != null)
        {
            int index = Array.FindIndex(otherPlayer.Value.Mount.Value.Players, 0, otherPlayer.Value.Mount.Value.Players.Length, x => x.ObjectId == session.Player.FieldPlayer.ObjectId);
            otherPlayer.Value.Mount.Value.Players[index] = null;
        }
    }
}
