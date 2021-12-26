﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Network;
using NLog;

namespace MapleServer2.PacketHandlers;

// All implementing classes should be thread safe and stateless.
// All state should be stored in Session
public interface IPacketHandler<in T> where T : Session
{
    public RecvOp OpCode { get; }

    public void Handle(T session, IPacketReader packet);

    public static void LogUnknownMode(Enum mode)
    {
        LogManager.GetCurrentClassLogger().Warn("New Unknown " + mode.GetType().Name + ": 0x" + mode.ToString("X"));
    }

    public static void LogUnknownMode(Type packetHandlerType, byte mode)
    {
        LogManager.GetCurrentClassLogger().Warn($"Unknown mode in {packetHandlerType}: 0x{mode.ToString("X")}");
    }
}
