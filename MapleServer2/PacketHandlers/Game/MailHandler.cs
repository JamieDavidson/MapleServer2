using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class MailHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.MAIL;

    private static class MailOperations
    {
        public const byte Open = 0x0;
        public const byte Send = 0x1;
        public const byte Read = 0x2;
        public const byte Collect = 0xB;
        public const byte Delete = 0xD;
        public const byte ReadBatch = 0x12;
        public const byte CollectBatch = 0x13;
    }

    private static class MailErrors
    {
        public const byte CharacterNotFound = 0x01;
        public const byte ItemAmountMismatch = 0x02;
        public const byte ItemCannotBeSent = 0x03;
        public const byte MailNotSent = 0x0C;
        public const byte MailAlreadyRead = 0x10;
        public const byte ItemAlreadyRetrieved = 0x11;
        public const byte FullInventory = 0x14;
        public const byte MailItemExpired = 0x15;
        public const byte SaleEnded = 0x17;
        public const byte MailCreationFailed = 0x18;
        public const byte CannotMailYourself = 0x19;
        public const byte NotEnouhgMeso = 0x1A;
        public const byte PlayerIsBlocked = 0x1B;
        public const byte PlayerBlockedYou = 0x1C;
        public const byte GMCannotSendMail = 0x1D;
        public const byte ContainsForbiddenWord = 0x1F;
        public const byte MailPrivilegeSuspended = 0x20;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case MailOperations.Open:
                HandleOpen(session);
                break;
            case MailOperations.Send:
                HandleSend(session, packet);
                break;
            case MailOperations.Read:
                HandleRead(session, packet);
                break;
            case MailOperations.Collect:
                HandleCollect(session, packet);
                break;
            case MailOperations.Delete:
                HandleDelete(session, packet);
                break;
            case MailOperations.ReadBatch:
                HandleReadBatch(session, packet);
                break;
            case MailOperations.CollectBatch:
                HandleCollectBatch(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        session.Send(MailPacket.StartOpen());

        var packetCount = session.Player.Mailbox.SplitList(5);

        foreach (var mails in packetCount)
        {
            session.Send(MailPacket.Open(mails));
        }

        session.Send(MailPacket.EndOpen());
    }

    private static void HandleSend(GameSession session, IPacketReader packet)
    {
        var recipientName = packet.ReadUnicodeString();
        var title = packet.ReadUnicodeString();
        var body = packet.ReadUnicodeString();

        if (recipientName == session.Player.Name)
        {
            session.Send(MailPacket.Error(MailErrors.CannotMailYourself));
            return;
        }

        if (!DatabaseManager.Characters.NameExists(recipientName))
        {
            session.Send(MailPacket.Error(MailErrors.CharacterNotFound));
            return;
        }

        var recipient = GameServer.PlayerManager.GetPlayerByName(recipientName);
        if (recipient == null)
        {
            recipient = DatabaseManager.Characters.FindPartialPlayerByName(recipientName);
        }

        MailHelper.SendMail(MailType.Player, recipient.CharacterId, session.Player.CharacterId, session.Player.Name, title, body, "", "", new(), 0, 0, out var mail);

        session.Send(MailPacket.Send(mail));
    }

    private static void HandleRead(GameSession session, IPacketReader packet)
    {
        var id = packet.ReadLong();

        var mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == id);
        if (mail == null)
        {
            return;
        }

        if (mail.ReadTimestamp == 0)
        {
            mail.Read(session);
        }
    }

    private static void HandleCollect(GameSession session, IPacketReader packet)
    {
        var id = packet.ReadLong();
        var mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == id);
        if (mail == null)
        {
            return;
        }

        if (mail.Items.Count == 0 && mail.Mesos == 0 && mail.Merets == 0)
        {
            return;
        }

        if (mail.Items.Count > 0)
        {
            foreach (var item in mail.Items)
            {
                item.MailId = 0;
                DatabaseManager.Items.Update(item);
                session.Player.Inventory.AddItem(session, item, true);
            }
            mail.Items.Clear();
            session.Send(MailPacket.Collect(mail));
        }

        if (mail.Mesos > 0)
        {
            if (!session.Player.Wallet.Meso.Modify(mail.Mesos))
            {
                return;
            }
            mail.Mesos = 0;
        }

        if (mail.Merets > 0)
        {
            if (!session.Player.Account.Meret.Modify(mail.Merets))
            {
                return;
            }
            mail.Merets = 0;
        }
        DatabaseManager.Mails.Update(mail);

        session.Send(MailPacket.UpdateReadTime(mail));
    }

    private static void HandleDelete(GameSession session, IPacketReader packet)
    {
        var count = packet.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var mailId = packet.ReadLong();
            var mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == mailId);
            if (mail == null)
            {
                continue;
            }

            mail.Delete(session);
        }
    }

    private static void HandleReadBatch(GameSession session, IPacketReader packet)
    {
        var count = packet.ReadInt();

        for (var i = 0; i < count; i++)
        {
            HandleRead(session, packet);
        }
    }

    private static void HandleCollectBatch(GameSession session, IPacketReader packet)
    {
        var count = packet.ReadInt();

        for (var i = 0; i < count; i++)
        {
            HandleCollect(session, packet);
        }
    }
}
