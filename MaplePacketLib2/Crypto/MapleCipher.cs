using System.Buffers;
using MaplePacketLib2.Tools;

namespace MaplePacketLib2.Crypto;

public class MapleCipher
{
    private const int HeaderSize = 6;

    private static readonly ArrayPool<byte> ArrayProvider = ArrayPool<byte>.Shared;

    private readonly uint Version;
    private uint Iv;

    private MapleCipher(uint version, uint iv)
    {
        Version = version;
        Iv = iv;
    }

    private void AdvanceIv()
    {
        Iv = Rand32.CrtRand(Iv);
    }

    private static List<ICrypter> InitCryptSeq(uint version, uint blockIv)
    {
        ICrypter[] crypt = new ICrypter[4];
        crypt[RearrangeCrypter.GetIndex(version)] = new RearrangeCrypter();
        crypt[XorCrypter.GetIndex(version)] = new XorCrypter(version);
        crypt[TableCrypter.GetIndex(version)] = new TableCrypter(version);

        List<ICrypter> cryptSeq = new();
        while (blockIv > 0)
        {
            ICrypter crypter = crypt[blockIv % 10];
            if (crypter != null)
            {
                cryptSeq.Add(crypter);
            }
            blockIv /= 10;
        }

        return cryptSeq;
    }

    // These classes are used for encryption/decryption
    public class Encryptor
    {
        private readonly MapleCipher Cipher;
        private readonly ICrypter[] EncryptSeq;

        public Encryptor(uint version, uint iv, uint blockIv)
        {
            Cipher = new(version, iv);
            EncryptSeq = InitCryptSeq(version, blockIv).ToArray();
        }

        public PoolPacketWriter WriteHeader(byte[] packet, int offset, int length)
        {
            short encSeq = EncodeSeqBase();

            PoolPacketWriter writer = new(length + HeaderSize, ArrayProvider);
            writer.WriteShort(encSeq);
            writer.WriteInt(length);
            writer.WriteBytes(packet, offset, length);

            return writer;
        }

        private short EncodeSeqBase()
        {
            short encSeq = (short) (Cipher.Version ^ Cipher.Iv >> 16);
            Cipher.AdvanceIv();
            return encSeq;
        }

        public PoolPacketWriter Encrypt(byte[] packet, int offset, int length)
        {
            PoolPacketWriter result = WriteHeader(packet, offset, length);
            foreach (ICrypter crypter in EncryptSeq)
            {
                crypter.Encrypt(result.Buffer, HeaderSize, HeaderSize + length);
            }

            return result;
        }

        public PacketWriter Encrypt(PacketWriter packet)
        {
            return Encrypt(packet.Buffer, 0, packet.Length).Managed();
        }
    }

    public class Decryptor
    {
        private readonly MapleCipher Cipher;
        private readonly ICrypter[] DecryptSeq;

        public Decryptor(uint version, uint iv, uint blockIv)
        {
            Cipher = new(version, iv);
            List<ICrypter> cryptSeq = InitCryptSeq(version, blockIv);
            cryptSeq.Reverse();
            DecryptSeq = cryptSeq.ToArray();
        }

        private short DecodeSeqBase(short encSeq)
        {
            short decSeq = (short) (Cipher.Iv >> 16 ^ encSeq);
            Cipher.AdvanceIv();
            return decSeq;
        }

        // For use with System.IO.Pipelines
        // Decrypt packets directly from underlying data stream without needing to buffer
        public int TryDecrypt(ReadOnlySequence<byte> buffer, out PoolPacketReader packet)
        {
            if (buffer.Length < HeaderSize)
            {
                packet = null;
                return 0;
            }

            SequenceReader<byte> reader = new(buffer);
            reader.TryReadLittleEndian(out short encSeq);
            reader.TryReadLittleEndian(out int packetSize);
            int rawPacketSize = HeaderSize + packetSize;
            if (buffer.Length < rawPacketSize)
            {
                packet = null;
                return 0;
            }

            // Only decode sequence once we know there is sufficient data because it mutates IV
            short decSeq = DecodeSeqBase(encSeq);
            if (decSeq != Cipher.Version)
            {
                throw new ArgumentException($"Packet has invalid sequence header: {decSeq}");
            }

            byte[] data = ArrayProvider.Rent(packetSize);
            buffer.Slice(HeaderSize, packetSize).CopyTo(data);
            foreach (ICrypter crypter in DecryptSeq)
            {
                crypter.Decrypt(data, 0, packetSize);
            }

            packet = new(ArrayProvider, data, packetSize);
            return rawPacketSize;
        }

        public PacketReader Decrypt(byte[] rawPacket, int offset = 0)
        {
            PacketReader reader = new(rawPacket, offset);

            short encSeq = reader.ReadShort();
            short decSeq = DecodeSeqBase(encSeq);
            if (decSeq != Cipher.Version)
            {
                throw new ArgumentException($"Packet has invalid sequence header: {decSeq}");
            }

            int packetSize = reader.ReadInt();
            if (rawPacket.Length < packetSize + HeaderSize)
            {
                throw new ArgumentException($"Packet has invalid length: {rawPacket.Length}");
            }

            byte[] packet = reader.ReadBytes(packetSize);
            foreach (ICrypter crypter in DecryptSeq)
            {
                crypter.Decrypt(packet);
            }

            return new(packet);
        }
    }
}
