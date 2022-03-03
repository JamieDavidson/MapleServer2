namespace Maple2Storage.Types;

public static class Direction
{
    public const int SouthEast = 0;
    public const int NorthEast = 90;
    public const int NorthWest = 180;
    public const int SouthWest = 270;

    public static int GetClosestDirection(CoordF rotation)
    {
        int[] directions = new int[4]
        {
            SouthEast, NorthEast, NorthWest, SouthWest
        };

        return directions.OrderBy(x => Math.Abs(x - rotation.Z)).First();
    }
}
