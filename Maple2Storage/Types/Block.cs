namespace Maple2Storage.Types;

public static class Block
{
    public const int BlockSize = 150;

    public static CoordF ClosestBlock(CoordF coord)
    {
        return CoordF.From(
            MathF.Round(coord.X / BlockSize) * BlockSize,
            MathF.Round(coord.Y / BlockSize) * BlockSize,
            MathF.Floor(coord.Z / BlockSize) * BlockSize
        );
    }

    public static CoordS ClosestBlock(CoordS coord)
    {
        return CoordS.From(
            (short) (MathF.Round((float) coord.X / BlockSize) * BlockSize),
            (short) (MathF.Round((float) coord.Y / BlockSize) * BlockSize),
            (short) (MathF.Floor((float) coord.Z / BlockSize) * BlockSize)
        );
    }
}
