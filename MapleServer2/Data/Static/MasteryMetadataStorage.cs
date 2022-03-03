﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MapleServer2.Enums;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class MasteryMetadataStorage
{
    private static readonly Dictionary<int, MasteryMetadata> Masteries = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.ResourcesDirectory}/ms2-mastery-metadata");
        List<MasteryMetadata> masteryList = Serializer.Deserialize<List<MasteryMetadata>>(stream);
        foreach (MasteryMetadata mastery in masteryList)
        {
            Masteries[mastery.Type] = mastery;
        }
    }

    public static List<int> GetMasteryTypes()
    {
        return new(Masteries.Keys);
    }

    public static MasteryMetadata GetMastery(int type)
    {
        return Masteries.GetValueOrDefault(type);
    }

    public static int GetGradeFromXp(MasteryType type, long xp)
    {
        return GetMastery((int) type).Grades.Count(x => x.Value <= xp);
    }
}
