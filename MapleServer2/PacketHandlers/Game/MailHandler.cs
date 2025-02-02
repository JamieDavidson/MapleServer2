﻿using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

public class MailHandler : GamePacketHandler<MailHandler>
{
    public override RecvOp OpCode => RecvOp.Mail;

    private enum MailMode : byte
    {
        Open = 0x0,
        Send = 0x1,
        Read = 0x2,
        Collect = 0xB,
        Delete = 0xD,
        ReadBatch = 0x12,
        CollectBatch = 0x13
    }

    private enum MailErrorCode : byte
    {
        CharacterNotFound = 0x01,
        ItemAmountMismatch = 0x02,
        ItemCannotBeSent = 0x03,
        MailNotSent = 0x0C,
        MailAlreadyRead = 0x10,
        ItemAlreadyRetrieved = 0x11,
        FullInventory = 0x14,
        MailItemExpired = 0x15,
        SaleEnded = 0x17,
        MailCreationFailed = 0x18,
        CannotMailYourself = 0x19,
        NotEnouhgMeso = 0x1A,
        PlayerIsBlocked = 0x1B,
        PlayerBlockedYou = 0x1C,
        GMCannotSendMail = 0x1D,
        ContainsForbiddenWord = 0x1F,
        MailPrivilegeSuspended = 0x20
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        MailMode mode = (MailMode) packet.ReadByte();

        switch (mode)
        {
            case MailMode.Open:
                HandleOpen(session);
                break;
            case MailMode.Send:
                HandleSend(session, packet);
                break;
            case MailMode.Read:
                HandleRead(session, packet);
                break;
            case MailMode.Collect:
                HandleCollect(session, packet);
                break;
            case MailMode.Delete:
                HandleDelete(session, packet);
                break;
            case MailMode.ReadBatch:
                HandleReadBatch(session, packet);
                break;
            case MailMode.CollectBatch:
                HandleCollectBatch(session, packet);
                break;
            default:
                LogUnknownMode(mode);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        session.Send(MailPacket.StartOpen());

        IEnumerable<List<Mail>> packetCount = session.Player.Mailbox.SplitList(5);

        foreach (List<Mail> mails in packetCount)
        {
            session.Send(MailPacket.Open(mails));
        }

        session.Send(MailPacket.EndOpen());
    }

    private static void HandleSend(GameSession session, PacketReader packet)
    {
        string recipientName = packet.ReadUnicodeString();
        string title = packet.ReadUnicodeString();
        string body = packet.ReadUnicodeString();

        if (recipientName == session.Player.Name)
        {
            session.Send(MailPacket.Error((byte) MailErrorCode.CannotMailYourself));
            return;
        }

        if (!DatabaseManager.Characters.NameExists(recipientName))
        {
            session.Send(MailPacket.Error((byte) MailErrorCode.CharacterNotFound));
            return;
        }

        Player recipient = GameServer.PlayerManager.GetPlayerByName(recipientName);
        if (recipient == null)
        {
            recipient = DatabaseManager.Characters.FindPartialPlayerByName(recipientName);
        }

        MailHelper.SendMail(MailType.Player, recipient.CharacterId, session.Player.CharacterId, session.Player.Name, title, body, "", "", new(), 0, 0, out Mail mail);

        session.Send(MailPacket.Send(mail));
    }

    private static void HandleRead(GameSession session, PacketReader packet)
    {
        long id = packet.ReadLong();

        Mail mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == id);
        if (mail == null)
        {
            return;
        }

        if (mail.ReadTimestamp == 0)
        {
            mail.Read(session);
        }
    }

    private static void HandleCollect(GameSession session, PacketReader packet)
    {
        long id = packet.ReadLong();
        Mail mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == id);
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
            foreach (Item item in mail.Items)
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

    private static void HandleDelete(GameSession session, PacketReader packet)
    {
        int count = packet.ReadInt();
        for (int i = 0; i < count; i++)
        {
            long mailId = packet.ReadLong();
            Mail mail = session.Player.Mailbox.FirstOrDefault(x => x.Id == mailId);
            if (mail == null)
            {
                continue;
            }

            mail.Delete(session);
        }
    }

    private static void HandleReadBatch(GameSession session, PacketReader packet)
    {
        int count = packet.ReadInt();

        for (int i = 0; i < count; i++)
        {
            HandleRead(session, packet);
        }
    }

    private static void HandleCollectBatch(GameSession session, PacketReader packet)
    {
        int count = packet.ReadInt();

        for (int i = 0; i < count; i++)
        {
            HandleCollect(session, packet);
        }
    }
}
