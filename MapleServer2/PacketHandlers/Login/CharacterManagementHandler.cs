﻿using System.Net;
using Maple2Storage.Enums;
using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Database;
using MapleServer2.Enums;
using MapleServer2.Packets;
using MapleServer2.Servers.Login;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Login;

public class CharacterManagementHandler : LoginPacketHandler
{
    public override RecvOp OpCode => RecvOp.CHARACTER_MANAGEMENT;

    private enum CharacterManagementMode : byte
    {
        Login = 0x0,
        Create = 0x1,
        Delete = 0x2
    }

    public override void Handle(LoginSession session, PacketReader packet)
    {
        CharacterManagementMode mode = (CharacterManagementMode) packet.ReadByte();
        switch (mode)
        {
            case CharacterManagementMode.Login:
                HandleSelect(session, packet);
                break;
            case CharacterManagementMode.Create:
                HandleCreate(session, packet);
                break;
            case CharacterManagementMode.Delete:
                HandleDelete(session, packet);
                break;
            default:
                IPacketHandler<LoginSession>.LogUnknownMode(mode);
                break;
        }
    }

    private void HandleDelete(LoginSession session, PacketReader packet)
    {
        long characterId = packet.ReadLong();
        if (!DatabaseManager.Characters.SetCharacterDeleted(characterId))
        {
            Logger.Error("Could not delete character");
            return;
        }
        session.Send(CharacterListPacket.DeleteCharacter(characterId));
        Logger.Info($"Character id {characterId} deleted!");
    }

    private void HandleSelect(LoginSession session, PacketReader packet)
    {
        long characterId = packet.ReadLong();
        packet.ReadShort(); // 01 00
        Logger.Info($"Logging in to game with char id: {characterId}");

        string ipAddress = Environment.GetEnvironmentVariable("IP");
        int port = int.Parse(Environment.GetEnvironmentVariable("GAME_PORT"));
        IPEndPoint endpoint = new(IPAddress.Parse(ipAddress), port);

        AuthData authData = DatabaseManager.AuthData.GetByAccountId(session.AccountId);
        Player player = DatabaseManager.Characters.FindPartialPlayerById(characterId);
        if (player is null)
        {
            Logger.Error("Character not found!");
            return;
        }

        authData.OnlineCharacterId = characterId;

        DatabaseManager.AuthData.UpdateOnlineCharacterId(authData);
        DatabaseManager.Characters.UpdateChannelId(characterId, channelId: 1, instanceId: 1, isMigrating: false);

        session.SendFinal(MigrationPacket.LoginToGame(endpoint, player.MapId, authData), logoutNotice: false);
    }

    private static void HandleCreate(LoginSession session, PacketReader packet)
    {
        Gender gender = (Gender) packet.ReadByte();
        Job job = (Job) packet.ReadShort();
        string name = packet.ReadUnicodeString();
        SkinColor skinColor = packet.Read<SkinColor>();
        packet.Skip(2);

        if (DatabaseManager.Characters.NameExists(name))
        {
            session.Send(ResponseCharCreatePacket.NameTaken());
            return;
        }

        Account account = DatabaseManager.Accounts.FindById(session.AccountId);
        Player newCharacter = new(account, name, gender, job, skinColor);
        session.CharacterId = newCharacter.CharacterId;

        byte equipCount = packet.ReadByte();
        for (int i = 0; i < equipCount; i++)
        {
            int id = packet.ReadInt();
            string typeStr = packet.ReadUnicodeString();
            if (!Enum.TryParse(typeStr, out ItemSlot type))
            {
                throw new ArgumentException($"Unknown equip type: {typeStr}");
            }

            EquipColor equipColor = packet.Read<EquipColor>();

            switch (type)
            {
                case ItemSlot.Hair: // Hair
                    // Hair Length/Position
                    float backLength = packet.ReadFloat();
                    CoordF backPositionCoord = packet.Read<CoordF>();
                    CoordF backPositionRotation = packet.Read<CoordF>();
                    float frontLength = packet.ReadFloat();
                    CoordF frontPositionCoord = packet.Read<CoordF>();
                    CoordF frontPositionRotation = packet.Read<CoordF>();
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Hair, new(id)
                    {
                        Color = equipColor,
                        HairData = new(backLength, frontLength, backPositionCoord, backPositionRotation, frontPositionCoord, frontPositionRotation),
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.Face: // Face
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Face, new(id)
                    {
                        Color = equipColor,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.FaceDecoration: // Face Decoration
                    byte[] faceDecoration = packet.ReadBytes(16); // Face decoration position

                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.FaceDecoration, new(id)
                    {
                        Color = equipColor,
                        FaceDecorationData = faceDecoration,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.Clothes: // Clothes
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Clothes, new(id)
                    {
                        Color = equipColor,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.Pants: // Pants
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Pants, new(id)
                    {
                        Color = equipColor,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.Shoes: // Shoes
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Shoes, new(id)
                    {
                        Color = equipColor,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
                case ItemSlot.Ear: // Ear
                    if (!DefaultItemsMetadataStorage.IsValid((int) job, id))
                    {
                        continue;
                    }

                    newCharacter.Inventory.Cosmetics.Add(ItemSlot.Ear, new(id)
                    {
                        Color = equipColor,
                        IsTemplate = false,
                        IsEquipped = true
                    });
                    break;
            }
        }
        packet.ReadInt(); // const? (4)

        DatabaseManager.Inventories.Update(newCharacter.Inventory);

        // Send updated CHAR_MAX_COUNT
        session.Send(CharacterListPacket.SetMax(account.CharacterSlots));

        // Send CHARACTER_LIST for new character only (append)
        session.Send(CharacterListPacket.AppendEntry(newCharacter));
    }
}
