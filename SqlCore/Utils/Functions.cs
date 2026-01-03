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

        public static uint rol(uint value, int rotation)
        {
            return (value << rotation) | (value >> (32 - rotation));
        }
    }
}
