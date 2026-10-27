using System.Security.Cryptography;

namespace SqlCore.Utils
{
    public static class Functions
    {
        public static uint[] GenerateCRC32Table(uint polynomial)
        {
            uint[] crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? polynomial ^ (c >> 1) : c >> 1;
                crcTable[i] = c;
            }
            return crcTable;
        }

        public static uint BinarySwap(uint value)
        {
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }

        public static bool IsBitSet(byte value, int bitPosition)
        {
            if (bitPosition < 0 || bitPosition > 7)
                throw new ArgumentOutOfRangeException(nameof(bitPosition));

            return (value & (1 << bitPosition)) != 0;
        }

        public static bool IsBitSet(short value, int bitPosition)
        {
            if (bitPosition < 0 || bitPosition > 15)
                throw new ArgumentOutOfRangeException(nameof(bitPosition));

            return (value & (1 << bitPosition)) != 0;
        }

        public static bool IsValidParity(byte value)
        {
            return ((value & 0xC0) == 0x80) || ((value & 0xC0) == 0x40);
        }

        public static bool IsEqualParity(byte value1, byte value2)
        {
            const byte mask = 0xC0;

            byte parity1 = (byte)(value1 & mask);
            byte parity2 = (byte)(value2 & mask);

            return (parity1 == parity2) && (parity1 == 0x80 || parity1 == 0x40);
        }

        public static byte CalculateParityByte(byte value)
        {
            return (byte)(value & 0xC0);
        }

        public static byte[] BlobStringToBytes(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            if (hex.Length % 2 != 0)
                hex = "0" + hex;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        public static string FormatBytesAsHex(byte[] data, int offset, int size)
        {
            var hex = "0x";

            for (int i = 0; i < size; i++)
                hex += data[offset + i].ToString("X2");

            return hex;
        }

        public static string FormatLsn(ReadOnlySpan<byte> lsnBytes)
        {
            if (lsnBytes.Length != 10)
                throw new ArgumentException("LSN must be exactly 10 bytes");

            Span<byte> tmp4 = stackalloc byte[4];
            Span<byte> tmp2 = stackalloc byte[2];

            lsnBytes.Slice(0, 4).CopyTo(tmp4);
            uint vlfId = BitConverter.ToUInt32(tmp4);

            lsnBytes.Slice(4, 4).CopyTo(tmp4);
            uint logBlockId = BitConverter.ToUInt32(tmp4);

            lsnBytes.Slice(8, 2).CopyTo(tmp2);
            ushort logRecordId = BitConverter.ToUInt16(tmp2);

            return $"{vlfId:X8}:{logBlockId:X8}:{logRecordId:X4}";
        }

        public static uint rol(uint value, int rotation)
        {
            return (value << rotation) | (value >> (32 - rotation));
        }

        public static uint ror(uint value, int rotation)
        {
            return (value >> rotation) | (value << (32 - rotation));
        }

        public static byte[] GenerateRandomSalt()
        {
            return RandomNumberGenerator.GetBytes(4);
        }
    }
}
