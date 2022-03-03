namespace Maple2Storage.Types;

public static class Paths
{
    public static readonly string SolutionDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    public static readonly string ResourcesDirectory = $"{SolutionDirectory}/Maple2Storage/Resources";
    public static readonly string JsonDirectory = $"{SolutionDirectory}/Maple2Storage/Json";
    public static readonly string ScriptsDirectory = $"{SolutionDirectory}/Maple2Storage/Scripts";

    public static readonly string ResourcesInputDirectory = $"{SolutionDirectory}/GameDataParser/Resources";
    public static readonly string HashDirectory = $"{SolutionDirectory}/GameDataParser/Hash";

    public static readonly string AiDirectory = $"{SolutionDirectory}/MobAI";

    public static readonly string DataDirectory = $"{SolutionDirectory}/MapleWebServer/Data";
}
