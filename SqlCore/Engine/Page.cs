using SqlCore.Utils;
using System.Buffers.Binary;

namespace SqlCore.Engine
{
    public static class Page
    {
        private const int seed = 15;
        private const int pageSize = 8192;
        private const int numOfSectors = 16;
        private const int numOfElements = 128;

        public static ushort[] ReadSlotArray(byte[] pageContent, int numOfRecords)
        {
            ArgumentNullException.ThrowIfNull(pageContent);

            if (numOfRecords < 0)
                throw new ArgumentOutOfRangeException(nameof(numOfRecords));

            int offset = pageContent.Length - 2;

            if (numOfRecords * 2 > pageContent.Length)
                throw new ArgumentOutOfRangeException(nameof(numOfRecords));

            ushort[] slotArray = new ushort[numOfRecords];

            for (int i = 0; i < numOfRecords; i++)
            {
                slotArray[i] = BinaryPrimitives.ReadUInt16LittleEndian(
                    pageContent.AsSpan(offset, 2));

                offset -= 2;
            }

            return slotArray;
        }

        public static byte[] RestoreTornBits(byte[] pageBuffer, out byte parity)
        {
            ArgumentNullException.ThrowIfNull(pageBuffer);

            if (pageBuffer.Length != 8192)
                throw new ArgumentException(
                    "Page buffer must be exactly 8192 bytes",
                    nameof(pageBuffer));

            uint tornBits = BinaryPrimitives.ReadUInt32LittleEndian(pageBuffer.AsSpan(60, 4));

            parity = (byte)(tornBits & 0b11);

            for (int sector = 2; sector <= numOfSectors; sector++)
            {
                int lastByteOffset = (sector * 512) - 1;

                int shift = (sector - 1) * 2;

                byte originalBits = (byte)((tornBits >> shift) & 0b11);

                pageBuffer[lastByteOffset] = (byte)((pageBuffer[lastByteOffset] & 0b11111100) | originalBits);
            }

            return pageBuffer;
        }

        public static void WriteTornBits(byte[] pageBuffer, byte parity)
        {
            ArgumentNullException.ThrowIfNull(pageBuffer);

            if (pageBuffer.Length != 8192)
                throw new ArgumentException(
                    "Page buffer must be exactly 8192 bytes",
                    nameof(pageBuffer));

            if (parity > 0b11)
                throw new ArgumentOutOfRangeException(nameof(parity));

            uint tornBits = parity;

            for (int sector = 2; sector <= numOfSectors; sector++)
            {
                int lastByteOffset = (sector * 512) - 1;

                byte originalBits = (byte)(pageBuffer[lastByteOffset] & 0b11);

                int shift = (sector - 1) * 2;

                tornBits |= (uint)originalBits << shift;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(pageBuffer.AsSpan(60, 4), tornBits);

            for (int sector = 2; sector <= numOfSectors; sector++)
            {
                int lastByteOffset = (sector * 512) - 1;

                pageBuffer[lastByteOffset] = (byte)((pageBuffer[lastByteOffset] & 0b11111100) | parity);
            }
        }

        public static byte[] CalculateChecksum(byte[] pageBuffer)
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
            uint result = 0;

            pagebuf[0, 15] = 0x00000000;

            for (int i = 0; i < numOfSectors; i++)
            {
                result = 0;

                for (int j = 0; j < numOfElements; j++)
                    result ^= pagebuf[i, j];

                checksum ^= Functions.rol(result, seed - i);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(
                pageBuffer.AsSpan(60, 4),
                checksum);

            return pageBuffer;
        }
    }
}