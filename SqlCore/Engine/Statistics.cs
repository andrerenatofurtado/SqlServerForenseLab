using SqlCore.Utils;

namespace SqlCore.Engine
{
    public static class Statistics
    {
        private static readonly uint[] crc32Table = Functions.GenerateCRC32Table(0xEDB88320);

        public static uint CalculateStatsChecksum(byte[] stats_stream)
        {
            stats_stream[16] = stats_stream[17] = stats_stream[18] = stats_stream[19] = 0;

            uint checksum = 0xFFFFFFFF;
            foreach (var b in stats_stream)
            {
                checksum = crc32Table[(checksum ^ b) & 0xFF] ^ (checksum >> 8);
            }
            checksum ^= 0xFFFFFFFF;

            checksum = Functions.BinarySwap(checksum);
            checksum = ~checksum;

            return checksum;
        }
    }
}
