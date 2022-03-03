using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Tools;

namespace MapleServer2.Types;

public class GlobalEvent
{
    public readonly int Id;
    public List<GlobalEventType> Events = new();

    public GlobalEvent()
    {
        Id = GuidGenerator.Int();
    }

    public async Task Start()
    {
        MapleServer.BroadcastPacketAll(GlobalPortalPacket.Notice(this));

        await Task.Delay(60000);

        MapleServer.BroadcastPacketAll(GlobalPortalPacket.Clear(this));
        GameServer.GlobalEventManager.RemoveEvent(this);
    }
}
public enum GlobalEventType : byte
{
    OxQuiz = 1,
    TrapMaster = 2,
    SpringBeach = 3,
    CrazyRunner = 4,
    FinalSurviver = 5,
    GreatEscape = 6,
    DanceDanceStop = 7,
    CrazyRunnerShanghai = 8,
    HideAndSeek = 9,
    RedArena = 10,
    BloodMine = 11,
    TreasureIsland = 12,
    ChristmasDanceDanceStop = 13
}
