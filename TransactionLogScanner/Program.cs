using TransactionLogScanner;

class Program
{
    static void Main()
    {
        try
        {
            Console.Write("Enter the database log file path (LDF only): ");

            string? ldfPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(ldfPath))
            {
                Console.WriteLine("Invalid LDF file path.");
                return;
            }

            Console.WriteLine("");

            Console.Write("Enter the output file path: ");

            string? outputPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.WriteLine("Invalid output file path.");
                return;
            }

            File.WriteAllText(outputPath, string.Empty);

            FileHeaderScanner.ProcessFileHeader(ldfPath, outputPath);

            var vlfHeaderList = VirtualLogFileScanner.ProcessVirtualLogFile(ldfPath, outputPath);

            LogBlockScanner.ProcessLogBlock(ldfPath, outputPath, vlfHeaderList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

}
