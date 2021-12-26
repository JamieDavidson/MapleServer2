using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;
using NLog;

namespace MapleServer2.PacketHandlers.Game;

internal abstract class GamePacketHandler : IPacketHandler<GameSession>
{
    public abstract RecvOp OpCode { get; }

    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public abstract void Handle(GameSession session, IPacketReader packet);

    public override string ToString()
    {
        return $"[{OpCode}] Game.{GetType().Name}";
    }
}
