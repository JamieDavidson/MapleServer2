using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class KeyTableHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.KEY_TABLE;

    private static class KeyTableOperations
    {
        public const byte SetKeyBind = 0x02;
        public const byte MoveQuickSlot = 0x03;
        public const byte AddToFirstSlot = 0x04;
        public const byte RemoveQuickSlot = 0x05;
        public const byte SetActiveHotbar = 0x08;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var requestType = packet.ReadByte();

        switch (requestType)
        {
            case KeyTableOperations.SetKeyBind:
                SetKeyBinds(session, packet);
                break;
            case KeyTableOperations.MoveQuickSlot:
                MoveQuickSlot(session, packet);
                break;
            case KeyTableOperations.AddToFirstSlot:
                AddToQuickSlot(session, packet);
                break;
            case KeyTableOperations.RemoveQuickSlot:
                RemoveQuickSlot(session, packet);
                break;
            case KeyTableOperations.SetActiveHotbar:
                SetActiveHotbar(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), requestType);
                break;
        }
    }

    private static void AddToQuickSlot(GameSession session, IPacketReader packet)
    {
        var hotbarId = packet.ReadShort();
        if (!session.Player.GameOptions.TryGetHotbar(hotbarId, out var targetHotbar))
        {
            Logger.Warn($"Invalid hotbar id {hotbarId}");
            return;
        }

        var quickSlot = packet.Read<QuickSlot>();
        var targetSlot = packet.ReadInt();
        if (targetHotbar.AddToFirstSlot(quickSlot))
        {
            session.Send(KeyTablePacket.SendHotbars(session.Player.GameOptions));
        }
    }

    private static void SetKeyBinds(GameSession session, IPacketReader packet)
    {
        var numBindings = packet.ReadInt();

        for (var i = 0; i < numBindings; i++)
        {
            var keyBind = packet.Read<KeyBind>();
            session.Player.GameOptions.SetKeyBind(ref keyBind);
        }
    }

    private static void MoveQuickSlot(GameSession session, IPacketReader packet)
    {
        var hotbarId = packet.ReadShort();
        if (!session.Player.GameOptions.TryGetHotbar(hotbarId, out var targetHotbar))
        {
            Logger.Warn($"Invalid hotbar id {hotbarId}");
            return;
        }

        // Adds or moves a quickslot around
        var quickSlot = packet.Read<QuickSlot>();
        var targetSlot = packet.ReadInt();
        targetHotbar.MoveQuickSlot(targetSlot, quickSlot);

        session.Send(KeyTablePacket.SendHotbars(session.Player.GameOptions));
    }

    private static void RemoveQuickSlot(GameSession session, IPacketReader packet)
    {
        var hotbarId = packet.ReadShort();
        if (!session.Player.GameOptions.TryGetHotbar(hotbarId, out var targetHotbar))
        {
            Logger.Warn($"Invalid hotbar id {hotbarId}");
            return;
        }

        var skillId = packet.ReadInt();
        var itemUid = packet.ReadLong();
        if (targetHotbar.RemoveQuickSlot(skillId, itemUid))
        {
            session.Send(KeyTablePacket.SendHotbars(session.Player.GameOptions));
        }
    }

    private static void SetActiveHotbar(GameSession session, IPacketReader packet)
    {
        var hotbarId = packet.ReadShort();

        session.Player.GameOptions.SetActiveHotbar(hotbarId);
    }
}
