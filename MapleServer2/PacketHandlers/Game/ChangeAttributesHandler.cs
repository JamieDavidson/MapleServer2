using Maple2Storage.Enums;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class ChangeAttributesHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.CHANGE_ATTRIBUTES;

    private static class ChangeAttributesMode
    {
        public const byte ChangeAttributes = 0x0;
        public const byte SelectNewAttributes = 0x02;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case ChangeAttributesMode.ChangeAttributes:
                HandleChangeAttributes(session, packet);
                break;
            case ChangeAttributesMode.SelectNewAttributes:
                HandleSelectNewAttributes(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleChangeAttributes(GameSession session, IPacketReader packet)
    {
        short lockStatId = -1;
        bool isSpecialStat = false;
        long itemUid = packet.ReadLong();
        packet.Skip(8);
        bool useLock = packet.ReadBool();
        if (useLock)
        {
            isSpecialStat = packet.ReadBool();
            lockStatId = packet.ReadShort();
        }

        Inventory inventory = session.Player.Inventory;

        // There are multiple ids for each type of material
        var greenCrystals = inventory.GetItemsByTag("GreenCrystal").ToArray();
        var metaCells = inventory.GetItemsByTag("MetaCell").ToArray();
        var crystalFragments = inventory.GetItemsByTag("CrystalPiece").ToArray();

        int greenCrystalTotalAmount = greenCrystals.Sum(i => i.Value.Amount);
        int metacellTotalAmount = metaCells.Sum(i => i.Value.Amount);
        int crystalFragmentTotalAmount = crystalFragments.Sum(i => i.Value.Amount);
        
        Item gear = inventory.GetItemByUid(itemUid);
        Item scrollLock = null;

        // Check if gear exist in inventory
        if (gear == null)
        {
            return;
        }

        string tag = "";
        if (Item.IsAccessory(gear.ItemSlot))
        {
            tag = "LockItemOptionAccessory";
        }
        else if (Item.IsArmor(gear.ItemSlot))
        {
            tag = "LockItemOptionArmor";
        }
        else if (Item.IsWeapon(gear.ItemSlot))
        {
            tag = "LockItemOptionWeapon";
        }
        else if (Item.IsPet(gear.Id))
        {
            tag = "LockItemOptionPet";
        }

        if (useLock)
        {
            scrollLock = inventory.GetItemByTagAndRarity(tag, gear.Rarity);
            // Check if scroll lock exist in inventory
            if (scrollLock == null)
            {
                return;
            }
        }

        int greenCrystalCost = 5;
        int metacellCosts = Math.Min(11 + gear.TimesAttributesChanged, 25);

        // Relation between TimesAttributesChanged to amount of crystalFragments for epic gear
        int[] crystalFragmentsEpicGear = {
            200, 250, 312, 390, 488, 610, 762, 953, 1192, 1490, 1718, 2131, 2642, 3277, 4063
        };

        int crystalFragmentsCosts = crystalFragmentsEpicGear[Math.Min(gear.TimesAttributesChanged, 14)];

        if (gear.Rarity > (short) RarityType.Epic)
        {
            greenCrystalCost = 25;
            metacellCosts = Math.Min(165 + gear.TimesAttributesChanged * 15, 375);
            if (gear.Rarity == (short) RarityType.Legendary)
            {
                crystalFragmentsCosts = Math.Min(400 + gear.TimesAttributesChanged * 400, 6000);
            }
            else if (gear.Rarity == (short) RarityType.Ascendant)
            {
                crystalFragmentsCosts = Math.Min(600 + gear.TimesAttributesChanged * 600, 9000);
            }
        }

        // Check if player has enough materials
        if (greenCrystalTotalAmount < greenCrystalCost || metacellTotalAmount < metacellCosts || crystalFragmentTotalAmount < crystalFragmentsCosts)
        {
            return;
        }

        gear.TimesAttributesChanged++;

        Item newItem = new(gear);

        // Get random stats except stat that is locked
        List<ItemStat> randomList = ItemStats.RollBonusStatsWithStatLocked(newItem, lockStatId, isSpecialStat);

        for (int i = 0; i < newItem.Stats.BonusStats.Count; i++)
        {
            // Check if BonusStats[i] is NormalStat and isSpecialStat is false
            // Check if BonusStats[i] is SpecialStat and isSpecialStat is true
            switch (newItem.Stats.BonusStats[i])
            {
                case NormalStat when !isSpecialStat:
                case SpecialStat when isSpecialStat:
                    ItemStat stat = newItem.Stats.BonusStats[i];
                    switch (stat)
                    {
                        case NormalStat ns when ns.ItemAttribute == (StatId) lockStatId:
                        case SpecialStat ss when ss.ItemAttribute == (SpecialStatId) lockStatId:
                            continue;
                    }
                    break;
            }

            newItem.Stats.BonusStats[i] = randomList[i];
        }

        // Consume materials from inventory
        ConsumeMaterials(session, greenCrystalCost, metacellCosts, crystalFragmentsCosts, greenCrystals, metaCells, crystalFragments);

        if (useLock)
        {
            session.Player.Inventory.ConsumeItem(session, scrollLock.Uid, 1);
        }
        inventory.TemporaryStorage[newItem.Uid] = newItem;

        session.Send(ChangeAttributesPacket.PreviewNewItem(newItem));
    }

    private static void HandleSelectNewAttributes(GameSession session, IPacketReader packet)
    {
        long itemUid = packet.ReadLong();

        Inventory inventory = session.Player.Inventory;
        Item gear = inventory.TemporaryStorage.FirstOrDefault(x => x.Key == itemUid).Value;
        if (gear == null)
        {
            return;
        }

        inventory.TemporaryStorage.Remove(itemUid);
        inventory.Replace(gear);
        session.Send(ChangeAttributesPacket.AddNewItem(gear));
    }

    private static void ConsumeMaterials(GameSession session, int greenCrystalCost, int metacellCosts, int crystalFragmentsCosts, IEnumerable<KeyValuePair<long, Item>> greenCrystals, IEnumerable<KeyValuePair<long, Item>> metacells, IEnumerable<KeyValuePair<long, Item>> crystalFragments)
    {
        Inventory inventory = session.Player.Inventory;
        foreach ((long key, Item value) in greenCrystals)
        {
            if (value.Amount >= greenCrystalCost)
            {
                inventory.ConsumeItem(session, key, greenCrystalCost);
                break;
            }

            greenCrystalCost -= value.Amount;
            inventory.ConsumeItem(session, key, value.Amount);
        }

        foreach ((long key, Item value) in metacells)
        {
            if (value.Amount >= metacellCosts)
            {
                inventory.ConsumeItem(session, key, metacellCosts);
                break;
            }

            metacellCosts -= value.Amount;
            inventory.ConsumeItem(session, key, value.Amount);
        }

        foreach ((long key, Item value) in crystalFragments)
        {
            if (value.Amount >= crystalFragmentsCosts)
            {
                inventory.ConsumeItem(session, key, crystalFragmentsCosts);
                break;
            }

            crystalFragmentsCosts -= value.Amount;
            inventory.ConsumeItem(session, key, value.Amount);
        }
    }
}
