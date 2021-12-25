﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Network;

namespace MapleServer2.PacketHandlers.Common;

internal sealed class SystemInfoHandler : CommonPacketHandler
{
    public override RecvOp OpCode => RecvOp.SYSTEM_INFO;

    protected override void HandleCommon(Session session, PacketReader packet)
    {
        string info = packet.ReadUnicodeString();
        Logger.Debug("System Info: {info}", info);
    }
}
