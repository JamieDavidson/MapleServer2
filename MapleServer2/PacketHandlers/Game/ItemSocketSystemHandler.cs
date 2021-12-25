using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ItemSocketSystemHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ITEM_SOCKET_SYSTEM;

    private static class ItemSocketSystemOperations
    {
        public const byte UnlockSocket = 0x0;
        public const byte SelectUnlockSocketEquip = 0x2;
        public const byte UpgradeGem = 0x4;
        public const byte SelectGemUpgrade = 0x6;
        public const byte MountGem = 0x8;
        public const byte ExtractGem = 0xA;
    }

    private static class ItemSocketSystemNotices
    {
        public const byte TargetIsNotInYourInventory = 0x1;
        public const byte ItemIsNotInYourInventory = 0x2;
        public const byte CannotBeUsedAsMaterial = 0x3;
        public const byte ConfirmCatalystAmount = 0x4;
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ItemSocketSystemOperations.UnlockSocket:
                HandleUnlockSocket(session, packet);
                break;
            case ItemSocketSystemOperations.SelectUnlockSocketEquip:
                HandleSelectUnlockSocketEquip(session, packet);
                break;
            case ItemSocketSystemOperations.SelectGemUpgrade:
                HandleSelectGemUpgrade(session, packet);
                break;
            case ItemSocketSystemOperations.UpgradeGem:
                HandleUpgradeGem(session, packet);
                break;
            case ItemSocketSystemOperations.MountGem:
                HandleMountGem(session, packet);
                break;
            case ItemSocketSystemOperations.ExtractGem:
                HandleExtractGem(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleUnlockSocket(GameSession session, PacketReader packet)
    {
        long itemUid = packet.ReadLong();
        byte fodderAmount = packet.ReadByte();
        List<long> fodderUids = new();
        for (int i = 0; i < fodderAmount; i++)
        {
            long fodderUid = packet.ReadLong();
            fodderUids.Add(fodderUid);
        }

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }
        Item equip = inventory.GetItemByUid(itemUid);
        int equipUnlockedSlotCount = equip.Stats.GemSockets.Where(x => x.IsUnlocked).Count();

        foreach (long uid in fodderUids)
        {
            if (!inventory.HasItemWithUid(uid))
            {
                session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
                return;
            }

            Item fodder = inventory.GetItemByUid(uid);
            int fodderUnlockedSlotCount = fodder.Stats.GemSockets.Where(x => x.IsUnlocked).Count();
            if (equipUnlockedSlotCount != fodderUnlockedSlotCount)
            {
                session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.CannotBeUsedAsMaterial));
                return;
            }
        }

        // get socket slot to unlock
        int slot = equip.Stats.GemSockets.FindIndex(0, equip.Stats.GemSockets.Count, x => x.IsUnlocked != true);
        if (slot < 0)
        {
            return;
        }

        // fragmment cost. hard coded into the client?
        int crystalFragmentCost = 0;
        if (slot == 0)
        {
            crystalFragmentCost = 400;
        }
        else if (slot == 1 || slot == 2)
        {
            crystalFragmentCost = 600;
        }

        var crystalFragments = inventory.GetItemsByTag("CrystalPiece").ToArray();
        int crystalFragmentsTotalAmount = crystalFragments.Sum(x => x.Value.Amount);

        if (crystalFragmentsTotalAmount < crystalFragmentCost)
        {
            return;
        }

        foreach ((long key, Item value) in crystalFragments)
        {
            if (value.Amount >= crystalFragmentCost)
            {
                inventory.ConsumeItem(session, key, crystalFragmentCost);
                break;
            }

            crystalFragmentCost -= value.Amount;
            inventory.ConsumeItem(session, key, value.Amount);
        }
        foreach (long uid in fodderUids)
        {
            inventory.ConsumeItem(session, uid, 1);
        }

        equip.Stats.GemSockets[slot].IsUnlocked = true;
        List<GemSocket> unlockedSockets = equip.Stats.GemSockets.Where(x => x.IsUnlocked).ToList();

        session.Send(ItemSocketSystemPacket.UnlockSocket(equip, (byte) slot, unlockedSockets));
    }

    private static void HandleSelectUnlockSocketEquip(GameSession session, PacketReader packet)
    {
        long unkUid = packet.ReadLong();
        byte slot = packet.ReadByte();
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(itemUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }

        session.Send(ItemSocketSystemPacket.SelectUnlockSocketEquip(unkUid, slot, itemUid));
    }

    private static void HandleUpgradeGem(GameSession session, PacketReader packet)
    {
        long equipUid = packet.ReadLong();
        byte slot = packet.ReadByte();
        long itemUid = packet.ReadLong();

        ItemGemstoneUpgradeMetadata metadata;

        var inventory = session.Player.Inventory;
        if (equipUid == 0) // this is a gemstone in the player's inventory
        {
            if (!inventory.HasItemWithUid(itemUid))
            {
                session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
                return;
            }

            var gem = inventory.GetItemByUid(itemUid);
            if (gem == null)
            {
                return;
            }

            metadata = ItemGemstoneUpgradeMetadataStorage.GetMetadata(gem.Id);
            if (metadata == null || metadata.NextItemId == 0)
            {
                return;
            }

            if (!CheckGemUpgradeIngredients(inventory, metadata))
            {
                return;
            }

            ConsumeIngredients(session, metadata);
            inventory.ConsumeItem(session, gem.Uid, 1);

            Item upgradeGem = new(metadata.NextItemId)
            {
                Rarity = gem.Rarity
            };
            inventory.AddItem(session, upgradeGem, true);
            session.Send(ItemSocketSystemPacket.UpgradeGem(equipUid, slot, upgradeGem));
            return;
        }

        // upgrade gem mounted on a equipment
        if (!inventory.HasItemWithUid(equipUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }

        Gemstone gemstone = inventory.GetItemByUid(equipUid).Stats.GemSockets[slot].Gemstone;
        if (gemstone == null)
        {
            return;
        }

        metadata = ItemGemstoneUpgradeMetadataStorage.GetMetadata(gemstone.Id);
        if (metadata == null || metadata.NextItemId == 0)
        {
            return;
        }

        if (!CheckGemUpgradeIngredients(inventory, metadata))
        {
            return;
        }

        ConsumeIngredients(session, metadata);

        Item newGem = new(metadata.NextItemId)
        {
            IsLocked = gemstone.IsLocked,
            UnlockTime = gemstone.UnlockTime
        };

        Player owner = GameServer.PlayerManager.GetPlayerById(gemstone.OwnerId);
        if (owner != null)
        {
            newGem.OwnerCharacterId = owner.CharacterId;
            newGem.OwnerCharacterName = owner.Name;
        }

        Gemstone upgradedGemstone = new()
        {
            Id = metadata.NextItemId,
            IsLocked = gemstone.IsLocked,
            UnlockTime = gemstone.UnlockTime,
            OwnerId = gemstone.OwnerId,
            OwnerName = gemstone.OwnerName
        };

        inventory.GetItemByUid(equipUid).Stats.GemSockets[slot].Gemstone = gemstone;
        session.Send(ItemSocketSystemPacket.UpgradeGem(equipUid, slot, newGem));
    }

    private static bool CheckGemUpgradeIngredients(Inventory inventory, ItemGemstoneUpgradeMetadata metadata)
    {
        for (int i = 0; i < metadata.IngredientItems.Count; i++)
        {
            int inventoryItemCount = 0;
            var ingredients = inventory.GetItemsByTag(metadata.IngredientItems[i]).ToList();
            ingredients.ForEach(x => inventoryItemCount += x.Value.Amount);

            if (inventoryItemCount < metadata.IngredientAmounts[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void ConsumeIngredients(GameSession session, ItemGemstoneUpgradeMetadata metadata)
    {
        for (int i = 0; i < metadata.IngredientItems.Count; i++)
        {
            var inventory = session.Player.Inventory;
            var ingredients = inventory.GetItemsByTag(metadata.IngredientItems[i]).ToList();

            foreach ((long key, Item value) in ingredients)
            {
                if (value.Amount >= metadata.IngredientAmounts[i])
                {
                    inventory.ConsumeItem(session, key, metadata.IngredientAmounts[i]);
                    break;
                }

                metadata.IngredientAmounts[i] -= value.Amount;
                inventory.ConsumeItem(session, key, value.Amount);
            }
        }
    }

    private static void HandleSelectGemUpgrade(GameSession session, PacketReader packet)
    {
        long equipUid = packet.ReadLong();
        byte slot = packet.ReadByte();
        long itemUid = packet.ReadLong();

        var inventory = session.Player.Inventory;
        if (equipUid == 0) // this is a gemstone in the player's inventory
        {
            if (!inventory.HasItemWithUid(itemUid))
            {
                session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
                return;
            }

            session.Send(ItemSocketSystemPacket.SelectGemUpgrade(equipUid, slot, itemUid));
            return;
        }

        // select gem mounted on a equipment
        if (!inventory.HasItemWithUid(equipUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }

        Gemstone gemstone = inventory.GetItemByUid(equipUid).Stats.GemSockets[slot].Gemstone;
        if (gemstone == null)
        {
            return;
        }

        session.Send(ItemSocketSystemPacket.SelectGemUpgrade(equipUid, slot, itemUid));
    }

    private static void HandleMountGem(GameSession session, PacketReader packet)
    {
        long equipItemUid = packet.ReadLong();
        long gemItemUid = packet.ReadLong();
        byte slot = packet.ReadByte();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(equipItemUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.TargetIsNotInYourInventory));
            return;
        }

        if (!inventory.HasItemWithUid(gemItemUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }

        Item equipItem = inventory.GetItemByUid(equipItemUid);
        Item gemItem = inventory.GetItemByUid(gemItemUid);

        if (!equipItem.Stats.GemSockets[slot].IsUnlocked)
        {
            return;
        }

        if (equipItem.Stats.GemSockets[slot].Gemstone != null)
        {
            return;
        }

        Gemstone gemstone = new()
        {
            Id = gemItem.Id,
            IsLocked = gemItem.IsLocked,
            UnlockTime = gemItem.UnlockTime
        };
        if (gemItem.OwnerCharacterId != 0)
        {
            gemstone.OwnerId = gemItem.OwnerCharacterId;
            gemstone.OwnerName = gemItem.OwnerCharacterName;
        }

        equipItem.Stats.GemSockets[slot].Gemstone = gemstone;

        inventory.ConsumeItem(session, gemItem.Uid, 1);
        session.Send(ItemSocketSystemPacket.MountGem(equipItemUid, gemstone, slot));
    }

    private static void HandleExtractGem(GameSession session, PacketReader packet)
    {
        long equipItemUid = packet.ReadLong();
        byte slot = packet.ReadByte();

        var inventory = session.Player.Inventory;
        if (!inventory.HasItemWithUid(equipItemUid))
        {
            session.Send(ItemSocketSystemPacket.Notice(ItemSocketSystemNotices.ItemIsNotInYourInventory));
            return;
        }

        Item equipItem = inventory.GetItemByUid(equipItemUid);

        if (equipItem.Stats.GemSockets[slot].Gemstone == null)
        {
            return;
        }

        Gemstone gemstone = equipItem.Stats.GemSockets[slot].Gemstone;

        // crystal fragment cost
        Item gemstoneItem = new(gemstone.Id)
        {
            IsLocked = gemstone.IsLocked,
            UnlockTime = gemstone.UnlockTime,
            Rarity = 4
        };

        if (gemstone.OwnerId != 0)
        {
            Player owner = GameServer.PlayerManager.GetPlayerById(gemstone.OwnerId);
            if (owner != null)
            {
                gemstoneItem.OwnerCharacterId = owner.CharacterId;
                gemstoneItem.OwnerCharacterName = owner.Name;
            }
        }

        // remove gemstone from item
        equipItem.Stats.GemSockets[slot].Gemstone = null;

        inventory.AddItem(session, gemstoneItem, true);
        session.Send(ItemSocketSystemPacket.ExtractGem(equipItemUid, gemstoneItem.Uid, slot));
    }
}
