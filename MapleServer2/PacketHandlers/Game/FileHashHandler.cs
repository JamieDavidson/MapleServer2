using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class FileHashHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.FILE_HASH;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        packet.ReadInt();
        var filename = packet.ReadString();
        var md5 = packet.ReadString();

        Logger.Debug("Hash for {filename}: {md5}", filename, md5);
    }
}
