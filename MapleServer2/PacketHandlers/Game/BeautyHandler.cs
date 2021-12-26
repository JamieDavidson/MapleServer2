using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal sealed class BeautyHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.BEAUTY;

    private static class BeautyOperations
    {
        public const byte LoadShop = 0x0;
        public const byte NewBeauty = 0x3;
        public const byte ModifyExistingBeauty = 0x5;
        public const byte ModifySkin = 0x6;
        public const byte RandomHair = 0x7;
        public const byte Teleport = 0xA;
        public const byte ChooseRandomHair = 0xC;
        public const byte SaveHair = 0x10;
        public const byte DeleteSavedHair = 0x12;
        public const byte ChangeToSavedHair = 0x15;
        public const byte DyeItem = 0x16;
        public const byte BeautyVoucher = 0x17;
    }

    public override void Handle(GameSession session, IPacketReader packet)
    {
        var operation = packet.ReadByte();

        switch (operation)
        {
            case BeautyOperations.LoadShop:
                HandleLoadShop(session, packet);
                break;
            case BeautyOperations.NewBeauty:
                HandleNewBeauty(session, packet);
                break;
            case BeautyOperations.ModifyExistingBeauty:
                HandleModifyExistingBeauty(session, packet);
                break;
            case BeautyOperations.ModifySkin:
                HandleModifySkin(session, packet);
                break;
            case BeautyOperations.RandomHair:
                HandleRandomHair(session, packet);
                break;
            case BeautyOperations.ChooseRandomHair:
                HandleChooseRandomHair(session, packet);
                break;
            case BeautyOperations.SaveHair:
                HandleSaveHair(session, packet);
                break;
            case BeautyOperations.Teleport:
                HandleTeleport(session, packet);
                break;
            case BeautyOperations.DeleteSavedHair:
                HandleDeleteSavedHair(session, packet);
                break;
            case BeautyOperations.ChangeToSavedHair:
                HandleChangeToSavedHair(session, packet);
                break;
            case BeautyOperations.DyeItem:
                HandleDyeItem(session, packet);
                break;
            case BeautyOperations.BeautyVoucher:
                HandleBeautyVoucher(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(GetType(), operation);
                break;
        }
    }

    private static void HandleLoadShop(GameSession session, IPacketReader packet)
    {
        var npcId = packet.ReadInt();
        var category = (BeautyCategory) packet.ReadByte();

        var beautyNpc = NpcMetadataStorage.GetNpcMetadata(npcId);
        if (beautyNpc == null)
        {
            return;
        }

        var beautyShop = BeautyMetadataStorage.GetShopById(beautyNpc.ShopId);
        if (beautyShop == null)
        {
            return;
        }

        if (beautyShop.BeautyCategory == BeautyCategory.Dye)
        {
            if (beautyShop.BeautyType == BeautyShopType.Dye)
            {
                session.Send(BeautyPacket.LoadDyeShop(beautyShop));
                return;
            }
            session.Send(BeautyPacket.LoadBeautyShop(beautyShop));
            return;
        }

        if (beautyShop.BeautyCategory == BeautyCategory.Save)
        {
            session.Send(BeautyPacket.LoadSaveShop(beautyShop));
            session.Send(BeautyPacket.InitializeSaves());
            session.Send(BeautyPacket.LoadSaveWindow());
            session.Send(BeautyPacket.LoadSavedHairCount((short) session.Player.HairInventory.SavedHair.Count));
            if (session.Player.HairInventory.SavedHair.Count != 0)
            {
                session.Player.HairInventory.SavedHair = session.Player.HairInventory.SavedHair.OrderBy(o => o.CreationTime).ToList();
                session.Send(BeautyPacket.LoadSavedHairs(session.Player.HairInventory.SavedHair));
            }

            return;
        }

        var beautyItems = BeautyMetadataStorage.GetGenderItems(beautyShop.ShopId, session.Player.Gender);

        session.Send(BeautyPacket.LoadBeautyShop(beautyShop, beautyItems));
    }

    private static void HandleNewBeauty(GameSession session, IPacketReader packet)
    {
        var unk = packet.ReadByte();
        var useVoucher = packet.ReadBool();
        var beautyItemId = packet.ReadInt();
        var equipColor = packet.Read<EquipColor>();

        Item beautyItem = new(beautyItemId)
        {
            Color = equipColor,
            IsTemplate = false,
            IsEquipped = true,
            OwnerCharacterId = session.Player.CharacterId,
            OwnerCharacterName = session.Player.Name
        };
        var beautyShop = BeautyMetadataStorage.GetShopById(session.Player.ShopId);

        if (useVoucher)
        {
            if (!PayWithVoucher(session, beautyShop))
            {
                return;
            }
        }
        else
        {
            if (!PayWithShopItemTokenCost(session, beautyItemId, beautyShop))
            {
                return;
            }
        }

        ModifyBeauty(session, packet, beautyItem);
    }

    private static void HandleModifyExistingBeauty(GameSession session, IPacketReader packet)
    {
        var unk = packet.ReadByte();
        var useVoucher = packet.ReadBool();
        var beautyItemUid = packet.ReadLong();
        var equipColor = packet.Read<EquipColor>();

        var beautyItem = session.Player.GetEquippedItem(beautyItemUid);

        if (beautyItem.ItemSlot == ItemSlot.CP)
        {
            var hatData = packet.Read<HatData>();
            beautyItem.HatData = hatData;
            session.FieldManager.BroadcastPacket(ItemExtraDataPacket.Update(session.Player.FieldPlayer, beautyItem));
            return;
        }

        var beautyShop = BeautyMetadataStorage.GetShopById(session.Player.ShopId);

        if (!HandleShopPay(session, beautyShop, useVoucher))
        {
            return;
        }

        beautyItem.Color = equipColor;
        ModifyBeauty(session, packet, beautyItem);
    }

    private static void HandleModifySkin(GameSession session, IPacketReader packet)
    {
        var unk = packet.ReadByte();
        var skinColor = packet.Read<SkinColor>();
        var useVoucher = packet.ReadBool();

        var beautyShop = BeautyMetadataStorage.GetShopById(501);

        if (!HandleShopPay(session, beautyShop, useVoucher))
        {
            return;
        }

        session.Player.SkinColor = skinColor;
        session.FieldManager.BroadcastPacket(SkinColorPacket.Update(session.Player.FieldPlayer, skinColor));
    }
    private static void HandleRandomHair(GameSession session, IPacketReader packet)
    {
        var shopId = packet.ReadInt();
        var useVoucher = packet.ReadBool();

        var beautyShop = BeautyMetadataStorage.GetShopById(shopId);
        var beautyItems = BeautyMetadataStorage.GetGenderItems(beautyShop.ShopId, session.Player.Gender);

        if (!HandleShopPay(session, beautyShop, useVoucher))
        {
            return;
        }

        // Grab random hair
        var random = RandomProvider.Get();
        var indexHair = random.Next(beautyItems.Count);
        var chosenHair = beautyItems[indexHair];

        //Grab a preset hair and length of hair
        var beautyItemData = ItemMetadataStorage.GetMetadata(chosenHair.ItemId);
        var indexPreset = random.Next(beautyItemData.HairPresets.Count);
        var chosenPreset = beautyItemData.HairPresets[indexPreset];

        //Grab random front hair length
        var chosenFrontLength = random.NextDouble() *
            (beautyItemData.HairPresets[indexPreset].MaxScale - beautyItemData.HairPresets[indexPreset].MinScale) + beautyItemData.HairPresets[indexPreset].MinScale;

        //Grab random back hair length
        var chosenBackLength = random.NextDouble() *
            (beautyItemData.HairPresets[indexPreset].MaxScale - beautyItemData.HairPresets[indexPreset].MinScale) + beautyItemData.HairPresets[indexPreset].MinScale;

        // Grab random preset color
        var palette = ColorPaletteMetadataStorage.GetMetadata(2); // pick from palette 2. Seems like it's the correct palette for basic hair colors

        var indexColor = random.Next(palette.DefaultColors.Count);
        var color = palette.DefaultColors[indexColor];

        Item newHair = new(chosenHair.ItemId)
        {
            Color = EquipColor.Argb(color, indexColor, palette.PaletteId),
            HairData = new((float) chosenBackLength, (float) chosenFrontLength, chosenPreset.BackPositionCoord, chosenPreset.BackPositionRotation, chosenPreset.FrontPositionCoord, chosenPreset.FrontPositionRotation),
            IsTemplate = false,
            IsEquipped = true,
            OwnerCharacterId = session.Player.CharacterId,
            OwnerCharacterName = session.Player.Name
        };
        var cosmetics = session.Player.Inventory.Cosmetics;

        //Remove old hair
        if (cosmetics.Remove(ItemSlot.HR, out var previousHair))
        {
            previousHair.Slot = -1;
            session.Player.HairInventory.RandomHair = previousHair; // store the previous hair
            DatabaseManager.Items.Delete(previousHair.Uid);
            session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, previousHair));
        }

        cosmetics[ItemSlot.HR] = newHair;

        session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, newHair, ItemSlot.HR));
        session.Send(BeautyPacket.RandomHairOption(previousHair, newHair));
    }

    private static void HandleChooseRandomHair(GameSession session, IPacketReader packet)
    {
        var selection = packet.ReadByte();

        if (selection == 0) // player chose previous hair
        {
            var player = session.Player;
            var cosmetics = player.Inventory.Cosmetics;
            //Remove current hair
            if (cosmetics.Remove(ItemSlot.HR, out var newHair))
            {
                newHair.Slot = -1;
                DatabaseManager.Items.Delete(newHair.Uid);
                session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, newHair));
            }

            cosmetics[ItemSlot.HR] = player.HairInventory.RandomHair; // apply the previous hair

            session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, player.HairInventory.RandomHair, ItemSlot.HR));

            Item voucher = new(20300246); // Chic Salon Voucher
            player.Inventory.AddItem(session, voucher, true);

            session.Send(BeautyPacket.ChooseRandomHair(voucher.Id));
        }
        else // player chose new hair
        {
            session.Send(BeautyPacket.ChooseRandomHair());
        }

        session.Player.HairInventory.RandomHair = null; // remove random hair option from hair inventory
    }

    private static void HandleSaveHair(GameSession session, IPacketReader packet)
    {
        var hairUid = packet.ReadLong();

        var hair = session.Player.Inventory.Cosmetics.FirstOrDefault(x => x.Value.Uid == hairUid).Value;
        if (hair == null || hair.ItemSlot != ItemSlot.HR)
        {
            return;
        }

        if (session.Player.HairInventory.SavedHair.Count > 30) // 30 is the max slots
        {
            return;
        }

        Item hairCopy = new(hair.Id)
        {
            HairData = hair.HairData,
            Color = hair.Color,
            CreationTime = TimeInfo.Now() + Environment.TickCount
        };

        session.Player.HairInventory.SavedHair.Add(hairCopy);

        session.Send(BeautyPacket.SaveHair(hair, hairCopy));
    }

    private static void HandleTeleport(GameSession session, IPacketReader packet)
    {
        var teleportId = packet.ReadByte();

        Map mapId;
        switch (teleportId)
        {
            case 1:
                mapId = Map.RosettaBeautySalon;
                break;
            case 3:
                mapId = Map.TriaPlasticSurgery;
                break;
            case 5:
                mapId = Map.DouglasDyeWorkshop;
                break;
            default:
                Logger.Warn($"teleportId: {teleportId} not found");
                return;
        }

        session.Player.Warp((int) mapId, instanceId: session.Player.CharacterId);
    }

    private static void HandleDeleteSavedHair(GameSession session, IPacketReader packet)
    {
        var hairUid = packet.ReadLong();

        var hair = session.Player.HairInventory.SavedHair.FirstOrDefault(x => x.Uid == hairUid);
        if (hair == null)
        {
            return;
        }

        session.Send(BeautyPacket.DeleteSavedHair(hair.Uid));
        session.Player.HairInventory.SavedHair.Remove(hair);
    }

    private static void HandleChangeToSavedHair(GameSession session, IPacketReader packet)
    {
        var hairUid = packet.ReadLong();

        var hair = session.Player.HairInventory.SavedHair.FirstOrDefault(x => x.Uid == hairUid);
        if (hair == null)
        {
            return;
        }

        var beautyShop = BeautyMetadataStorage.GetShopById(510);

        if (!PayWithShopTokenCost(session, beautyShop))
        {
            return;
        }

        var cosmetics = session.Player.Inventory.Cosmetics;
        if (cosmetics.Remove(hair.ItemSlot, out var removeItem))
        {
            removeItem.Slot = -1;
            session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, removeItem));
        }

        cosmetics[removeItem.ItemSlot] = hair;

        session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, hair, hair.ItemSlot));
        session.Send(BeautyPacket.ChangetoSavedHair());
    }

    private static void HandleDyeItem(GameSession session, IPacketReader packet)
    {
        var beautyShop = BeautyMetadataStorage.GetShopById(506);

        var itemCount = packet.ReadByte();

        var quantity = new short[itemCount];
        var useVoucher = new bool[itemCount];
        var unk1 = new byte[itemCount];
        var unk2 = new long[itemCount];
        var unk3 = new int[itemCount];
        var itemUid = new long[itemCount];
        var itemId = new int[itemCount];
        var equipColor = new EquipColor[itemCount];
        var hatData = new HatData[itemCount];

        for (var i = 0; i < itemCount; i++)
        {
            quantity[i] = packet.ReadShort(); // should always be one
            useVoucher[i] = packet.ReadBool();
            unk1[i] = packet.ReadByte(); // just 0
            unk2[i] = packet.ReadLong(); // just 0
            unk3[i] = packet.ReadInt(); // also 0
            itemUid[i] = packet.ReadLong();
            itemId[i] = packet.ReadInt();
            equipColor[i] = packet.Read<EquipColor>();
            var item = session.Player.GetEquippedItem(itemUid[i]);
            if (item == null)
            {
                return;
            }

            if (!HandleShopPay(session, beautyShop, useVoucher[i]))
            {
                return;
            }

            if (item.ItemSlot == ItemSlot.CP)
            {
                hatData[i] = packet.Read<HatData>();
                item.HatData = hatData[i];
            }

            item.Color = equipColor[i];
            session.FieldManager.BroadcastPacket(ItemExtraDataPacket.Update(session.Player.FieldPlayer, item));
        }
    }

    private static void HandleBeautyVoucher(GameSession session, IPacketReader packet)
    {
        var itemUid = packet.ReadLong();

        var player = session.Player;
        var inventory = player.Inventory;
        var voucher = inventory.GetItemByUid(itemUid);
        if (voucher == null || voucher.Function.Name != "ItemChangeBeauty")
        {
            return;
        }

        var beautyShop = BeautyMetadataStorage.GetShopById(voucher.Function.Id);
        if (beautyShop == null)
        {
            return;
        }

        var beautyItems = BeautyMetadataStorage.GetGenderItems(beautyShop.ShopId, player.Gender);

        player.ShopId = beautyShop.ShopId;
        session.Send(BeautyPacket.LoadBeautyShop(beautyShop, beautyItems));
        player.Inventory.ConsumeItem(session, voucher.Uid, 1);
    }

    private static void ModifyBeauty(GameSession session, IPacketReader packet, Item beautyItem)
    {
        var itemSlot = ItemMetadataStorage.GetSlot(beautyItem.Id);
        var cosmetics = session.Player.Inventory.Cosmetics;

        if (cosmetics.TryGetValue(itemSlot, out var removeItem))
        {
            // Only remove if it isn't the same item
            if (removeItem.Uid != beautyItem.Uid)
            {
                cosmetics.Remove(itemSlot);
                removeItem.Slot = -1;
                DatabaseManager.Items.Delete(removeItem.Uid);
                session.FieldManager.BroadcastPacket(EquipmentPacket.UnequipItem(session.Player.FieldPlayer, removeItem));
            }
        }

        // equip & update new item
        switch (itemSlot)
        {
            case ItemSlot.HR:
                var backLength = BitConverter.ToSingle(packet.ReadBytes(4), 0);
                var backPositionCoord = packet.Read<CoordF>();
                var backPositionRotation = packet.Read<CoordF>();
                var frontLength = BitConverter.ToSingle(packet.ReadBytes(4), 0);
                var frontPositionCoord = packet.Read<CoordF>();
                var frontPositionRotation = packet.Read<CoordF>();

                beautyItem.HairData = new(backLength, frontLength, backPositionCoord, backPositionRotation, frontPositionCoord, frontPositionRotation);

                cosmetics[itemSlot] = beautyItem;

                session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, beautyItem, itemSlot));
                break;
            case ItemSlot.FA:
                cosmetics[itemSlot] = beautyItem;

                session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, beautyItem, itemSlot));
                break;
            case ItemSlot.FD:
                var faceDecorationPosition = packet.ReadBytes(16);

                beautyItem.FaceDecorationData = faceDecorationPosition;

                cosmetics[itemSlot] = beautyItem;

                session.FieldManager.BroadcastPacket(EquipmentPacket.EquipItem(session.Player.FieldPlayer, beautyItem, itemSlot));
                break;
        }
    }

    private static bool HandleShopPay(GameSession session, BeautyMetadata shop, bool useVoucher)
    {
        return useVoucher ? PayWithVoucher(session, shop) : PayWithShopTokenCost(session, shop);
    }

    private static bool PayWithVoucher(GameSession session, BeautyMetadata shop)
    {
        string voucherTag; // using an Item's tag to search for any applicable voucher
        switch (shop.BeautyType)
        {
            case BeautyShopType.Hair:
                if (shop.BeautyCategory == BeautyCategory.Special)
                {
                    voucherTag = "beauty_hair_special";
                    break;
                }
                voucherTag = "beauty_hair";
                break;
            case BeautyShopType.Face:
                voucherTag = "beauty_face";
                break;
            case BeautyShopType.Makeup:
                voucherTag = "beauty_makeup";
                break;
            case BeautyShopType.Skin:
                voucherTag = "beauty_skin";
                break;
            case BeautyShopType.Dye:
                voucherTag = "beauty_itemcolor";
                break;
            default:
                session.Send(NoticePacket.Notice("Unknown Beauty Shop", NoticeType.FastText));
                return false;
        }

        var inventory = session.Player.Inventory;
        var voucher = inventory.GetItemByTag(voucherTag);
        if (voucher == null)
        {
            session.Send(NoticePacket.Notice(SystemNotice.ItemNotFound, NoticeType.FastText));
            return false;
        }

        session.Send(BeautyPacket.UseVoucher(voucher.Id, 1));
        session.Player.Inventory.ConsumeItem(session, voucher.Uid, 1);
        return true;
    }

    private static bool PayWithShopTokenCost(GameSession session, BeautyMetadata beautyShop)
    {
        var cost = beautyShop.TokenCost;
        if (beautyShop.SpecialCost != 0)
        {
            cost = beautyShop.SpecialCost;
        }

        return Pay(session, beautyShop.TokenType, cost, beautyShop.RequiredItemId);
    }

    private static bool PayWithShopItemTokenCost(GameSession session, int beautyItemId, BeautyMetadata beautyShop)
    {
        var item = beautyShop.Items.FirstOrDefault(x => x.ItemId == beautyItemId);

        return Pay(session, item.TokenType, item.TokenCost, item.RequiredItemId);
    }

    private static bool Pay(GameSession session, ShopCurrencyType type, int tokenCost, int requiredItemId)
    {
        switch (type)
        {
            case ShopCurrencyType.Meso:
                return session.Player.Wallet.Meso.Modify(-tokenCost);
            case ShopCurrencyType.ValorToken:
                return session.Player.Wallet.ValorToken.Modify(-tokenCost);
            case ShopCurrencyType.Treva:
                return session.Player.Wallet.Treva.Modify(-tokenCost);
            case ShopCurrencyType.Rue:
                return session.Player.Wallet.Rue.Modify(-tokenCost);
            case ShopCurrencyType.HaviFruit:
                return session.Player.Wallet.HaviFruit.Modify(-tokenCost);
            case ShopCurrencyType.Meret:
            case ShopCurrencyType.GameMeret:
            case ShopCurrencyType.EventMeret:
                return session.Player.Account.RemoveMerets(tokenCost);
            case ShopCurrencyType.Item:
                var inventory = session.Player.Inventory;
                var itemCost = inventory.GetItemByItemId(requiredItemId);
                if (itemCost == null)
                {
                    return false;
                }
                if (itemCost.Amount < tokenCost)
                {
                    return false;
                }
                session.Player.Inventory.ConsumeItem(session, itemCost.Uid, tokenCost);
                return true;
            default:
                session.SendNotice($"Unknown currency: {type}");
                return false;
        }
    }
}
