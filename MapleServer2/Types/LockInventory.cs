using MapleServer2.Packets;
using MapleServer2.Servers.Game;

namespace MapleServer2.Types;

public class LockInventory
{
    public long[][] Items; // [0][18] lock items, [1][18] unlock items

    public LockInventory()
    {
        Items = new long[2][];
        Items[0] = new long[18];
        Items[1] = new long[18];
    }

    public void Add(GameSession session, byte mode, long uid)
    {
        short index = (short) Array.FindIndex(Items[mode], 0, Items[mode].Length, x => x == 0);
        if (index == -1)
        {
            return;
        }

        Items[mode][index] = uid;
        session.Send(ItemLockPacket.Add(uid, index));
    }

    public void Remove(GameSession session, long uid)
    {
        byte mode = 0;
        int index = Array.FindIndex(Items[mode], 0, Items[mode].Length, x => x == uid);
        if (index == -1)
        {
            mode = 1;
            index = Array.FindIndex(Items[mode], 0, Items[mode].Length, x => x == uid);
            if (index == -1)
            {
                return;
            }
        }

        Items[mode][index] = 0;
        session.Send(ItemLockPacket.Remove(uid));
    }

    public void Update(GameSession session, byte operation)
    {
        var inventory = session.Player.Inventory;

        var itemIdsToUpdate = Items[operation].Where(x => x != 0);
        var changedItems = inventory.LockItems(itemIdsToUpdate, operation);
        
        session.Send(ItemLockPacket.UpdateItems(changedItems));
    }
}
