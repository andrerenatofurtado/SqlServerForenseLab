using SqlCore.Utils;

namespace SqlCore.Engine
{
    public static class PageChecksum
    {
        private const int seed = 15;
        private const int pageSize = 8192;
        private const int numOfSectors = 16;
        private const int numOfElements = 128;

        public static uint CalculateChecksum(byte[] pageBuffer)
        {
            if (pageBuffer == null)
                throw new ArgumentNullException(nameof(pageBuffer));

            if (pageBuffer.Length != pageSize)
                throw new ArgumentException("Page buffer must be exactly 8192 bytes");

            uint[,] pagebuf = new uint[numOfSectors, numOfElements];

            int offset = 0;

            for (int i = 0; i < numOfSectors; i++)
            {
                for (int j = 0; j < numOfElements; j++)
                {
                    pagebuf[i, j] = BitConverter.ToUInt32(pageBuffer, offset);
                    offset += 4;
                }
            }

            uint checksum = 0;
            uint overall = 0;

            pagebuf[0, 15] = 0x00000000;

            for (int i = 0; i < numOfSectors; i++)
            {
                overall = 0;

                for (int j = 0; j < numOfElements; j++)
                    overall ^= pagebuf[i, j];

                checksum ^= Functions.rol(overall, seed - i);
            }

            return checksum;
        }

    }
}
