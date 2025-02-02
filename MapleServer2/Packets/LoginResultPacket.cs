﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Types;

namespace MapleServer2.Packets;

public static class LoginResultPacket
{
    public static PacketWriter InitLogin(long accountId)
    {
        PacketWriter pWriter = PacketWriter.Of(SendOp.LoginResult);
        pWriter.Write(LoginMode.LoginOk);
        pWriter.WriteInt(); // Const
        pWriter.WriteUnicodeString(); // Ban reason
        pWriter.WriteLong(accountId);
        pWriter.WriteLong(TimeInfo.Now()); // SyncTime
        pWriter.WriteInt(Environment.TickCount); // SyncTicks
        pWriter.WriteByte(); // TimeZone
        pWriter.WriteByte(); // BlockType
        pWriter.WriteInt(); // Const
        pWriter.WriteLong(); // Const
        pWriter.WriteInt(2); // Const

        return pWriter;
    }

    /// <summary>
    /// Send a packet with the given login mode, and everything else is 0.
    /// </summary>
    /// <param name="mode"><see cref="LoginMode"/></param>
    public static PacketWriter SendLoginMode(LoginMode mode)
    {
        PacketWriter pWriter = PacketWriter.Of(SendOp.LoginResult);
        pWriter.Write(mode);
        pWriter.WriteZero(44);

        return pWriter;
    }

    public static PacketWriter SendMessage(string message, bool closeClient)
    {
        PacketWriter pWriter = PacketWriter.Of(SendOp.LoginResult);
        pWriter.Write(closeClient ? LoginMode.MessageBoxAndCloseClient : LoginMode.MessageBox);
        pWriter.WriteInt();
        pWriter.WriteUnicodeString(message);
        pWriter.WriteZero(38);

        return pWriter;
    }
}

public enum LoginMode : byte
{
    LoginOk = 0x00, // Success, continues login
    IncorrectId = 0x01, // Incorrect id
    IncorrectId2 = 0x02, // Incorrect id
    IncorrectPassword = 0x03, // Incorrect password
    AccountAlreadyLoggedIn = 0x04, // Account already logged in. Previous session will be aborted, clicking ok tries to relog
    AccountAlreadyLoggedIn2 = 0x05, // Account already logged in. Previous session will be aborted, clicking ok tries to relog
    MemberLimit = 0x06, // Member limit reached
    AccountSuspended = 0x07, // Account suspended, shows date and reason.
    SystemErrorDB = 0x08, // System Error: DB
    SystemError = 0x09, // System Error
    ChannelFull = 0x0A, // Channel is full
    MessageBox = 0x0B, // Custom message box
    MessageBoxAndCloseClient = 0x0C, // Custom message box, clicking OK closes client.
    SystemError2 = 0x0D, // System error
    AccountInProtectionMode = 0x0E, // Account is in protection mode because it is suspected of being unauthorized. Clicking ok closes client and opens nxn website
    IpBanned = 0x0F, // Connection is restricted for this IP. A connection is not possible. Clicking ok closes client.
    GameVerificationFailed = 0x10, // Game verification has failed. Please try again later.
    SystemError3 = 0x11, // System error
    AccountNotInBeta = 0x12, // This account cannot participate in the test. A connection is not possible. Clicking ok closes client.
    AccountNotVerified = 0x13, // Currently, this service is only available for players whose identities have been verified. Clicking ok closes client.
    AccountOld = 0x14, // This account is an old account. An old account must complete identity verification at least once. Clicking ok closes client and opens nxn website
    SessionVerificationFailed = 0x15, // Session verification failed. A connection is not possible. Clicking ok closes client.
    GameVerificationFailed2 = 0x16, // Game verification failed. Please restart the game.
    SystemError4 = 0x17, // System error
    IpCannotConnect = 0x18, // Connecting from an IP that cannot connect. Clicking ok closes client.
    LoginSessionTimeout = 0x19, // Your login session has timed out. Please restart your client.
    BuyFoundersPack = 0x1A, // Access this by purchasing a Founder's Pack.
    SystemError5 = 0x1B, // System error
    SystemError6 = 0x1C, // System error
    SystemError7 = 0x1D, // System error
    SystemError8 = 0x1F // System error
    // ... probably there is more
}
