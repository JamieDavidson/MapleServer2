namespace MaplePacketLib2.Crypto;

public class TableCrypter : ICrypter
{
    private const int Index = 3;

    private const int TableSize = 256;

    private readonly byte[] Decrypted;
    private readonly byte[] Encrypted;

    public TableCrypter(uint version)
    {
        Decrypted = new byte[TableSize];
        Encrypted = new byte[TableSize];

        // Init
        for (int i = 0; i < TableSize; i++)
        {
            Encrypted[i] = (byte) i;
        }
        Shuffle(Encrypted, version);
        for (int i = 0; i < TableSize; i++)
        {
            Decrypted[Encrypted[i]] = (byte) i;
        }
    }

    public static uint GetIndex(uint version)
    {
        return (version + Index) % 3 + 1;
    }

    public void Encrypt(byte[] src)
    {
        Encrypt(src, 0, src.Length);
    }

    public void Encrypt(byte[] src, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            src[i] = Encrypted[src[i]];
        }
    }

    public void Decrypt(byte[] src)
    {
        Decrypt(src, 0, src.Length);
    }

    public void Decrypt(byte[] src, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            src[i] = Decrypted[src[i]];
        }
    }

    private static void Shuffle(byte[] data, uint version)
    {
        Rand32 rand32 = new((uint) Math.Pow(version, 2));
        for (int i = TableSize - 1; i >= 1; i--)
        {
            byte rand = (byte) (rand32.Random() % (i + 1));

            byte swap = data[i];
            data[i] = data[rand];
            data[rand] = swap;
        }
    }
}
