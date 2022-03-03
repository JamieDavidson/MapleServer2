using Maple2Storage.Types;
using MapleServer2.Types;
using Newtonsoft.Json;

namespace MapleServer2.Data.Static;

public static class OxQuizMetadataStorage
{
    private static readonly Dictionary<int, OxQuizQuestion> Questions = new();

    public static void Init()
    {
        string json = File.ReadAllText($"{Paths.JsonDirectory}/OXQuizQuestions.json");
        List<OxQuizQuestion> items = JsonConvert.DeserializeObject<List<OxQuizQuestion>>(json);
        foreach (OxQuizQuestion item in items)
        {
            Questions[item.Id] = item;
        }
    }

    public static OxQuizQuestion GetQuestion()
    {
        Random random = new();
        int index = random.Next(Questions.Count);
        return Questions[index];
    }
}
