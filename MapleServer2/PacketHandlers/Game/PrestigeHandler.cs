﻿using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class PrestigeHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.PRESTIGE;

    private static class PrestigeOperations
    {
        public const byte Reward = 0x03;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();
        switch (operation)
        {
            case PrestigeOperations.Reward: // Receive reward
                HandleReward(session, packet);
                break;
        }
    }

    private static void HandleReward(GameSession session, IPacketReader packet)
    {
        var rank = packet.ReadInt();

        if (session.Player.PrestigeRewardsClaimed.Contains(rank))
        {
            return;
        }

        // Get reward data
        var reward = PrestigeMetadataStorage.GetReward(rank);

        if (reward.Type.Equals("item"))
        {
            Item item = new(reward.Id)
            {
                CreationTime = TimeInfo.Now(),
                Rarity = 4
            };

            session.Player.Inventory.AddItem(session, item, true);
        }
        else if (reward.Type.Equals("statPoint"))
        {
            session.Player.StatPointDistribution.AddTotalStatPoints(reward.Value);
        }

        session.Send(PrestigePacket.Reward(rank));
        session.Player.PrestigeRewardsClaimed.Add(rank);
    }
}
