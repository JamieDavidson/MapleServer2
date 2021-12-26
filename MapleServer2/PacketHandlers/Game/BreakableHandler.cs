using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BreakableHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BREAKABLE;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var entityId = packet.ReadString();
        var someId = packet.ReadLong();
        var randId = packet.ReadInt(); //unk
        var unk = packet.ReadInt();

        var breakable = session.FieldManager.State.BreakableActors.GetValueOrDefault(entityId);
        if (breakable == null)
        {
            return;
        }

        breakable.BreakObject(session.FieldManager);
    }
}
