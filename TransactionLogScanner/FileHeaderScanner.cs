using SqlCore.Engine;
using SqlCore.Utils;
using System;

namespace TransactionLogScanner
{
    public static class FileHeaderScanner
    {
        public static void ProcessFileHeader(string filePath, string outputPath)
        {
            string printTableTitle = "FILE HEADER";

            string msgOutput = "";

            short flagBits;
            bool hasChecksum;

            uint storedChecksum = 0x0;
            uint calculatedChecksum = 0x0;

            byte[] headerContent = FileManager.ReadFileBytes(filePath, 0, 8192);

            flagBits = BitConverter.ToInt16(headerContent, 4);

            hasChecksum = PageHeader.HasChecksum(flagBits);

            storedChecksum = BitConverter.ToUInt32(headerContent, 60);

            if (hasChecksum)
            {
                calculatedChecksum = PageChecksum.CalculateChecksum(headerContent);

                if (calculatedChecksum != storedChecksum)
                {
                    msgOutput = "Invalid checksum";
                }
            }
            else
            {
                msgOutput = "No checksum verify enabled";
            }

            var printTableHeaders = new[] { "Stored checksum", "Calculated checksum", "Description" };

            var printTableRows = new List<string[]>
            {
                new[] { $"0x{storedChecksum:X8}", $"0x{calculatedChecksum:X8}", msgOutput }
            };

            var printTableAlignments = new[]
            {
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Left
            };

            string printTable = ASCIITableBuilder.BuildTable(printTableTitle, 
                                                             printTableHeaders, 
                                                             printTableRows, 
                                                             printTableAlignments);

            File.AppendAllText(outputPath, printTable);

            File.AppendAllText(outputPath, Environment.NewLine);
            File.AppendAllText(outputPath, Environment.NewLine);
        }
    }
}
