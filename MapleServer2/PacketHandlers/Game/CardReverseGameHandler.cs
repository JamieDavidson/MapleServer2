﻿using Maple2Storage.Tools;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Database;
using MapleServer2.Database.Types;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class CardReverseGameHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CARD_REVERSE_GAME;

    private static class CardReverseGameOperations
    {
        public const byte Open = 0x0;
        public const byte Mix = 0x1;
        public const byte Select = 0x2;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case CardReverseGameOperations.Open:
                HandleOpen(session);
                break;
            case CardReverseGameOperations.Mix:
                HandleMix(session);
                break;
            case CardReverseGameOperations.Select:
                HandleSelect(session);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleOpen(GameSession session)
    {
        List<CardReverseGame> cards = DatabaseManager.CardReverseGame.FindAll();
        session.Send(CardReverseGamePacket.Open(cards));
    }

    private static void HandleMix(GameSession session)
    {
        Item token = session.Player.Inventory.Items.FirstOrDefault(x => x.Value.Id == CardReverseGame.TOKEN_ITEM_ID).Value;
        if (token == null || token.Amount < CardReverseGame.TOKEN_COST)
        {
            session.Send(CardReverseGamePacket.Notice());
            return;
        }
        session.Player.Inventory.ConsumeItem(session, token.Uid, CardReverseGame.TOKEN_COST);

        session.Send(CardReverseGamePacket.Mix());
    }

    private static void HandleSelect(GameSession session)
    {
        // Unknown how this game works as to whether it's weighted or not
        // Currently being handled by each item having an equal chance

        List<CardReverseGame> cards = DatabaseManager.CardReverseGame.FindAll();

        int index = RandomProvider.Get().Next(cards.Count);

        CardReverseGame card = cards[index];
        Item item = new(card.ItemId)
        {
            Amount = card.ItemAmount,
            Rarity = card.ItemRarity
        };

        session.Send(CardReverseGamePacket.Select(index));
        session.Player.Inventory.AddItem(session, item, true);
    }
}
