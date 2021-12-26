﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MyInfoHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.MY_INFO;

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte(); //I don't know any other modes this could have so right now just handle the one.
        switch (mode)
        {
            case 0: //Set Motto
                var newmotto = packet.ReadUnicodeString();
                session.Player.Motto = newmotto;
                session.FieldManager.BroadcastPacket(MyInfoPacket.SetMotto(session.Player.FieldPlayer, newmotto));
                break;
        }
    }
}
