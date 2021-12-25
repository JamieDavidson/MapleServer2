using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Packets;
using MapleServer2.Servers.Login;

namespace MapleServer2.PacketHandlers.Login;

public class LoginUgcHandler : LoginPacketHandler
{
    public override RecvOp OpCode => RecvOp.UGC;

    private static class UgcMode
    {
        public const byte ProfilePicture = 0x0B;
    }

    public override void Handle(LoginSession session, PacketReader packet)
    {
        var function = packet.ReadByte();
        switch (function)
        {
            case UgcMode.ProfilePicture:
                HandleProfilePicture(session, packet);
                break;
            default:
                IPacketHandler<LoginSession>.LogUnknownMode(GetType(), function);
                break;
        }
    }

    private static void HandleProfilePicture(LoginSession session, PacketReader packet)
    {
        string path = packet.ReadUnicodeString();
        DatabaseManager.Characters.UpdateProfileUrl(session.CharacterId, path);

        session.Send(UgcPacket.SetProfilePictureURL(0, session.CharacterId, path));
    }
}
