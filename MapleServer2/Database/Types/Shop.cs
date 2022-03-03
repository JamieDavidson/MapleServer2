using Maple2Storage.Enums;

namespace MapleServer2.Database.Types;

public class Shop
{
    public int Uid { get; set; }
    public int Id { get; set; }
    public int Category { get; set; }
    public string Name { get; set; }
    public ShopType ShopType { get; set; }
    public bool RestrictSales { get; set; }
    public bool CanRestock { get; set; }
    public long NextRestock { get; set; }
    public bool AllowBuyback { get; set; }
    public List<ShopItem> Items { get; set; }

    public Shop() { }

    public Shop(int uid, int id, int category, string name, byte shopType, bool restrictSales, bool canRestock, long nextRestock, bool allowBuyback)
    {
        Uid = uid;
        Id = id;
        Category = category;
        Name = name;
        ShopType = (ShopType) shopType;
        RestrictSales = restrictSales;
        CanRestock = canRestock;
        NextRestock = nextRestock;
        AllowBuyback = allowBuyback;
    }
}
