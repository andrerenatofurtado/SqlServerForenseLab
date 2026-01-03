using SqlCore.Engine;
using SqlCore.Utils;

namespace ChecksumStatsStream
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Calculate checksum of a stats_stream BLOB

            CalculateStatsStreamChecksum();
        }

        static void CalculateStatsStreamChecksum()
        {
            Console.Write("Paste BLOB: ");
            string input = Console.ReadLine();

            byte[] stats_stream = Functions.BlobStringToBytes(input);
            string inputChecksum = Functions.FormatBytesAsHex(stats_stream, 16, 4);

            uint checksum = Statistics.CalculateStatsChecksum(stats_stream);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Blob checksum: " + inputChecksum);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Calculated checksum: {$"0x{checksum:X}"}");
            Console.ReadKey();
        }

    }
}
