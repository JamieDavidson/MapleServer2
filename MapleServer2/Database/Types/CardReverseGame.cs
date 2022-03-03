namespace MapleServer2.Database.Types;

public class CardReverseGame
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public byte ItemRarity { get; set; }
    public int ItemAmount { get; set; }

    // Temporarily hardcoding the item and cost
    public const int TokenItemId = 30000782; // 2nd Anniversary Commemorative Coin
    public const int TokenCost = 2;
}
