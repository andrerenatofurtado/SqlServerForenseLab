using SqlCore.Engine;
using System.IO.MemoryMappedFiles;

namespace PageChecksum
{
    internal class Program
    {
        const int pageSize = 8192;

        static void Main(string[] args)
        {
            // Check whether each database page has the checksum flag set,
            // and if so, validate that the stored checksum is correct.
            // Additionally prints some page info

            CheckDatabase();
        }

        static void CheckDatabase()
        {
            Console.Write("Enter the database file path (MDF and NDF only): ");

            string? path = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Invalid path.");
                return;
            }

            using var mappedFile = MemoryMappedFile.CreateFromFile(
                path,
                FileMode.Open,
                null,
                0,
                MemoryMappedFileAccess.Read);

            using var viewAccessor = mappedFile.CreateViewAccessor(
                0,
                0,
                MemoryMappedFileAccess.Read);

            long fileSize = viewAccessor.Capacity;
            long pageCount = fileSize / pageSize;

            byte[] pageBuffer = new byte[pageSize];

            byte pageType;
            string pageTypeDesc;

            short flagBits;
            string flagBitsDesc;
            bool hasChecksum;

            uint storedChecksum;

            for (long pageId = 0; pageId < pageCount; pageId++)
            {
                long offset = pageId * pageSize;

                viewAccessor.ReadArray(
                    offset,
                    pageBuffer,
                    0,
                    pageSize);

                pageType = pageBuffer[1];
                pageTypeDesc = PageHeader.GetPageTypeDesc(pageType);

                flagBits = BitConverter.ToInt16(pageBuffer, 4);
                flagBitsDesc = PageHeader.GetFlagBitsDesc(flagBits);

                hasChecksum = PageHeader.HasChecksum(flagBits);

                Console.WriteLine($"Page ID: {pageId}");
                Console.WriteLine($"Page Type: {pageTypeDesc}");
                Console.WriteLine($"Flag Bits: {flagBitsDesc}");

                if (hasChecksum)
                {
                    storedChecksum = BitConverter.ToUInt32(pageBuffer, 60);
                    uint calculatedChecksum = SqlCore.Engine.PageChecksum.CalculateChecksum(pageBuffer);

                    if (calculatedChecksum != storedChecksum)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }

                    Console.WriteLine($"Calculated Checksum: 0x{calculatedChecksum:X8}");
                    Console.WriteLine($"Stored Checksum: 0x{storedChecksum:X8}");
                }

                Console.WriteLine();

                Console.ResetColor();
            }

            Console.WriteLine("CheckDatabase Finished.");
        }

    }
}
