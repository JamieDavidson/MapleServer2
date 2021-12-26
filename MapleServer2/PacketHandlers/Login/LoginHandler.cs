using System.Collections.Immutable;
using System.Net;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Network;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Servers.Login;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Login;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class LoginHandler : LoginPacketHandler
{
    public override RecvOp OpCode => RecvOp.RESPONSE_LOGIN;

    // TODO: This data needs to be dynamic
    private readonly ImmutableList<IPEndPoint> ServerIPs;
    private readonly string ServerName;

    private static class LoginOperations
    {
        public const byte Banners = 0x01;
        public const byte SendCharacters = 0x02;
    }

    public LoginHandler()
    {
        var builder = ImmutableList.CreateBuilder<IPEndPoint>();
        var ipAddress = Environment.GetEnvironmentVariable("IP");
        var port = int.Parse(Environment.GetEnvironmentVariable("LOGIN_PORT"));
        builder.Add(new(IPAddress.Parse(ipAddress), port));

        ServerIPs = builder.ToImmutable();
        ServerName = Environment.GetEnvironmentVariable("NAME");
    }

    public override void Handle(LoginSession session, IPacketReader packet)
    {
        var mode = packet.ReadByte();
        var username = packet.ReadUnicodeString();
        var password = packet.ReadUnicodeString();

        Account account;
        if (DatabaseManager.Accounts.AccountExists(username.ToLower()))
        {
            if (!DatabaseManager.Accounts.Authenticate(username, password, out account))
            {
                session.Send(LoginResultPacket.IncorrectPassword());
                return;
            }

            var loggedInAccount = MapleServer.GetSessions(MapleServer.GetLoginServer(), MapleServer.GetGameServer()).FirstOrDefault(p => p switch
            {
                LoginSession s => s.AccountId == account.Id,
                GameSession s => s.Player.AccountId == account.Id,
                _ => false
            });

            if (loggedInAccount != null)
            {
                loggedInAccount.Disconnect(logoutNotice: true);
                session.Send(LoginResultPacket.AccountAlreadyLoggedIn());
                return;
            }
        }
        else
        {
            // Hash the password with BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            account = new(username, passwordHash); // Create a new account if username doesn't exist
        }

        Logger.Debug("Logging in with account ID: {account.Id}", account.Id);
        session.AccountId = account.Id;
        account.LastLoginTime = TimeInfo.Now();
        DatabaseManager.Accounts.Update(account);

        switch (mode)
        {
            case LoginOperations.Banners:
                SendBanners(session, account);
                break;
            case LoginOperations.SendCharacters:
                SendCharacters(session, account);
                break;
        }
    }

    private void SendBanners(LoginSession session, Account account)
    {
        var banners = DatabaseManager.Banners.FindAllBanners();
        session.Send(NpsInfoPacket.SendUsername(account.Username));
        session.Send(BannerListPacket.SetBanner(banners));
        session.SendFinal(ServerListPacket.SetServers(ServerName, ServerIPs), logoutNotice: true);
    }

    private void SendCharacters(LoginSession session, Account account)
    {
        var serverIp = Environment.GetEnvironmentVariable("IP");
        var webServerPort = Environment.GetEnvironmentVariable("WEB_PORT");
        var url = $"http://{serverIp}:{webServerPort}";

        var characters = DatabaseManager.Characters.FindAllByAccountId(session.AccountId);

        Logger.Debug("Initializing login with account id: {session.AccountId}", session.AccountId);
        session.Send(LoginResultPacket.InitLogin(session.AccountId));
        session.Send(UgcPacket.SetEndpoint($"{url}/ws.asmx?wsdl", url));
        session.Send(CharacterListPacket.SetMax(account.CharacterSlots));
        session.Send(CharacterListPacket.StartList());
        // Send each character data
        session.Send(CharacterListPacket.AddEntries(characters));
        session.Send(CharacterListPacket.EndList());
    }
}
