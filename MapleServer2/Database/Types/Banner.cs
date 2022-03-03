namespace MapleServer2.Database.Types;

public class Banner
{
    public int Id { get; set; }
    public string Name { get; set; } // name must start with "homeproduct_" for Meret Market banners
    public BannerType Type { get; set; }
    public BannerSubType SubType { get; set; }
    public string ImageUrl { get; set; } // Meret Market banner resolution: 538x301
    public BannerLanguage Language { get; set; }
    public long BeginTime { get; set; }
    public long EndTime { get; set; }


    public Banner() { }

    public Banner(int id, string name, string type, string subType, string imageUrl, int language, long beginTime, long endTime)
    {
        Id = id;
        Name = name;
        _ = Enum.TryParse(type, out BannerType bannerType);
        Type = bannerType;
        _ = Enum.TryParse(subType, out BannerSubType bannerSubType);
        SubType = bannerSubType;
        ImageUrl = imageUrl;
        Language = (BannerLanguage) language;
        BeginTime = beginTime;
        EndTime = endTime;
    }
}
public enum BannerType
{
    Merat,
    PlayGift,
    PcBang,
    Right, // used for cash
    Left // used for cash
}
public enum BannerSubType
{
    Cash
}
public enum BannerLanguage
{
    All = -1,
    Korean = 2
}
