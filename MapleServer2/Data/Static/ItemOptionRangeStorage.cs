using Maple2Storage.Enums;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class ItemOptionRangeStorage
{
    public static readonly Dictionary<ItemOptionRangeType, Dictionary<StatId, List<ParserStat>>> NormalRange = new();
    public static readonly Dictionary<ItemOptionRangeType, Dictionary<SpecialStatId, List<ParserSpecialStat>>> SpecialRange = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.ResourcesDirectory}/ms2-item-option-range-metadata");
        List<ItemOptionRangeMetadata> items = Serializer.Deserialize<List<ItemOptionRangeMetadata>>(stream);
        foreach (ItemOptionRangeMetadata optionRange in items)
        {
            NormalRange[optionRange.RangeType] = optionRange.Stats;
            SpecialRange[optionRange.RangeType] = optionRange.SpecialStats;
        }
    }

    public static Dictionary<StatId, List<ParserStat>> GetAccessoryRanges()
    {
        return NormalRange[ItemOptionRangeType.ItemOptionVariationAcc];
    }

    public static Dictionary<StatId, List<ParserStat>> GetArmorRanges()
    {
        return NormalRange[ItemOptionRangeType.ItemOptionVariationArmor];
    }

    public static Dictionary<StatId, List<ParserStat>> GetPetRanges()
    {
        return NormalRange[ItemOptionRangeType.ItemOptionVariationPet];
    }

    public static Dictionary<StatId, List<ParserStat>> GetWeaponRanges()
    {
        return NormalRange[ItemOptionRangeType.ItemOptionVariationWeapon];
    }

    public static Dictionary<SpecialStatId, List<ParserSpecialStat>> GetAccessorySpecialRanges()
    {
        return SpecialRange[ItemOptionRangeType.ItemOptionVariationAcc];
    }

    public static Dictionary<SpecialStatId, List<ParserSpecialStat>> GetArmorSpecialRanges()
    {
        return SpecialRange[ItemOptionRangeType.ItemOptionVariationArmor];
    }

    public static Dictionary<SpecialStatId, List<ParserSpecialStat>> GetPetSpecialRanges()
    {
        return SpecialRange[ItemOptionRangeType.ItemOptionVariationPet];
    }

    public static Dictionary<SpecialStatId, List<ParserSpecialStat>> GetWeaponSpecialRanges()
    {
        return SpecialRange[ItemOptionRangeType.ItemOptionVariationWeapon];
    }
}
