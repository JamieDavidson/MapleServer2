using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class InstrumentHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.PLAY_INSTRUMENT;

    private static class InstrumentOperation
    {
        public const byte StartImprovise = 0x0;
        public const byte PlayNote = 0x1;
        public const byte StopImprovise = 0x2;
        public const byte PlayScore = 0x3;
        public const byte StopScore = 0x4;
        public const byte StartEnsemble = 0x5;
        public const byte LeaveEnsemble = 0x6;
        public const byte Compose = 0x8;
        public const byte Fireworks = 0xE;
        public const byte AudienceEmote = 0xF;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case InstrumentOperation.StartImprovise:
                HandleStartImprovise(session, packet);
                break;
            case InstrumentOperation.PlayNote:
                HandlePlayNote(session, packet);
                break;
            case InstrumentOperation.StopImprovise:
                HandleStopImprovise(session);
                break;
            case InstrumentOperation.PlayScore:
                HandlePlayScore(session, packet);
                break;
            case InstrumentOperation.StopScore:
                HandleStopScore(session);
                break;
            case InstrumentOperation.StartEnsemble:
                HandleStartEnsemble(session, packet);
                break;
            case InstrumentOperation.LeaveEnsemble:
                HandleLeaveEnsemble(session);
                break;
            case InstrumentOperation.Compose:
                HandleCompose(session, packet);
                break;
            case InstrumentOperation.Fireworks:
                HandleFireworks(session);
                break;
            case InstrumentOperation.AudienceEmote:
                HandleAudienceEmote(packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleStartImprovise(GameSession session, IPacketReader packet)
    {
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }

        var instrument = inventory.GetItemByUid(itemUid);

        var instrumentInfo = InstrumentInfoMetadataStorage.GetMetadata(instrument.Function.Id);
        var instrumentCategory = InstrumentCategoryInfoMetadataStorage.GetMetadata(instrumentInfo.Category);

        Instrument newInstrument = new(instrumentCategory.GMId, instrumentCategory.PercussionId, false, session.Player.FieldPlayer.ObjectId)
        {
            Improvise = true
        };

        session.Player.Instrument = session.FieldManager.RequestFieldObject(newInstrument);
        session.Player.Instrument.Coord = session.Player.FieldPlayer.Coord;
        session.FieldManager.AddInstrument(session.Player.Instrument);
        session.FieldManager.BroadcastPacket(InstrumentPacket.StartImprovise(session.Player.Instrument));
    }

    private static void HandlePlayNote(GameSession session, IPacketReader packet)
    {
        int note = packet.ReadInt();

        session.FieldManager.BroadcastPacket(InstrumentPacket.PlayNote(note, session.Player.FieldPlayer));
    }

    private static void HandleStopImprovise(GameSession session)
    {
        if (session.Player.Instrument == null)
        {
            return;
        }

        session.FieldManager.BroadcastPacket(InstrumentPacket.StopImprovise(session.Player.FieldPlayer));
        session.FieldManager.RemoveInstrument(session.Player.Instrument);
        session.Player.Instrument = null;
    }

    private static void HandlePlayScore(GameSession session, IPacketReader packet)
    {
        long instrumentItemUid = packet.ReadLong();
        long scoreItemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        
        if (!inventory.HasItemWithUid(scoreItemUid) || !inventory.HasItemWithUid(instrumentItemUid))
        {
            return;
        }

        var instrument = inventory.GetItemByUid(instrumentItemUid);

        var instrumentInfo = InstrumentInfoMetadataStorage.GetMetadata(instrument.Function.Id);
        var instrumentCategory = InstrumentCategoryInfoMetadataStorage.GetMetadata(instrumentInfo.Category);

        var score = inventory.GetItemByUid(scoreItemUid);

        if (score.PlayCount <= 0)
        {
            return;
        }

        Instrument newInstrument = new(instrumentCategory.GMId, instrumentCategory.PercussionId, score.IsCustomScore, session.Player.FieldPlayer.ObjectId)
        {
            InstrumentTick = session.ServerTick,
            Score = score,
            Improvise = false
        };

        score.PlayCount -= 1;
        session.Player.Instrument = session.FieldManager.RequestFieldObject(newInstrument);
        session.Player.Instrument.Coord = session.Player.FieldPlayer.Coord;
        session.FieldManager.AddInstrument(session.Player.Instrument);
        session.FieldManager.BroadcastPacket(InstrumentPacket.PlayScore(session.Player.Instrument));
        session.Send(InstrumentPacket.UpdateScoreUses(scoreItemUid, score.PlayCount));
    }

    private static void HandleStopScore(GameSession session)
    {
        int masteryExpGain = (session.ServerTick - session.Player.Instrument.Value.InstrumentTick) / 1000;
        // TODO: Find any exp cap
        session.Player.Levels.GainMasteryExp(MasteryType.Performance, masteryExpGain);
        session.FieldManager.BroadcastPacket(InstrumentPacket.StopScore(session.Player.Instrument));
        session.FieldManager.RemoveInstrument(session.Player.Instrument);
        session.Player.Instrument = null;
    }

    private static void HandleCompose(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();
        var length = packet.ReadInt();
        var instrumentType = packet.ReadInt();
        var scoreName = packet.ReadUnicodeString();
        var scoreNotes = packet.ReadString();

        var inventory = session.Player.Inventory;
        
        if (!inventory.HasItemWithUid(itemUid))
        {
            return;
        }

        var item = inventory.GetItemByUid(itemUid);

        item.Score.Length = length;
        item.Score.Type = instrumentType;
        item.Score.Title = scoreName;
        item.Score.Composer = session.Player.Name;
        item.Score.ComposerCharacterId = session.Player.CharacterId;
        item.Score.Notes = scoreNotes;

        session.Send(InstrumentPacket.Compose(item));
    }

    private static void HandleStartEnsemble(GameSession session, IPacketReader packet)
    {
        long instrumentItemUid = packet.ReadLong();
        long scoreItemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        Party party = session.Player.Party;
        if (party == null)
        {
            return;
        }

        if (!inventory.HasItemWithUid(scoreItemUid) || !inventory.HasItemWithUid(instrumentItemUid))
        {
            return;
        }


        Item score = inventory.GetItemByUid(scoreItemUid);

        if (score.PlayCount <= 0)
        {
            return;
        }

        Item instrumentItem = inventory.GetItemByUid(instrumentItemUid);
        InstrumentInfoMetadata instrumentInfo = InstrumentInfoMetadataStorage.GetMetadata(instrumentItem.Function.Id);
        InstrumentCategoryInfoMetadata instrumentCategory = InstrumentCategoryInfoMetadataStorage.GetMetadata(instrumentInfo.Category);
        Instrument instrument = new(instrumentCategory.GMId, instrumentCategory.PercussionId, score.IsCustomScore, session.Player.FieldPlayer.ObjectId)
        {
            Score = score,
            Ensemble = true,
            Improvise = false
        };

        session.Player.Instrument = session.FieldManager.RequestFieldObject(instrument);
        session.Player.Instrument.Coord = session.Player.FieldPlayer.Coord;

        if (session.Player != party.Leader)
        {
            return;
        }

        int instrumentTick = session.ServerTick;
        foreach (Player member in party.Members)
        {
            if (member.Instrument == null)
            {
                continue;
            }

            if (!member.Instrument.Value.Ensemble)
            {
                continue;
            }

            member.Instrument.Value.InstrumentTick = instrumentTick; // set the tick to be all the same
            member.Session.FieldManager.AddInstrument(member.Session.Player.Instrument);
            session.FieldManager.BroadcastPacket(InstrumentPacket.PlayScore(member.Session.Player.Instrument));
            member.Instrument.Value.Score.PlayCount -= 1;
            member.Session.Send(InstrumentPacket.UpdateScoreUses(member.Instrument.Value.Score.Uid, member.Instrument.Value.Score.PlayCount));
            member.Instrument.Value.Ensemble = false;
        }
    }

    private static void HandleLeaveEnsemble(GameSession session)
    {
        session.FieldManager.BroadcastPacket(InstrumentPacket.StopScore(session.Player.Instrument));
        session.FieldManager.RemoveInstrument(session.Player.Instrument);
        session.Player.Instrument = null;
        session.Send(InstrumentPacket.LeaveEnsemble());
    }

    private static void HandleFireworks(GameSession session)
    {
        session.Send(InstrumentPacket.Fireworks(session.Player.FieldPlayer.ObjectId));
    }

    private static void HandleAudienceEmote(IPacketReader packet)
    {
        int skillId = packet.ReadInt();
    }
}
