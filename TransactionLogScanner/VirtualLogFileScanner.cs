using SqlCore.Engine.TransactionLog;
using SqlCore.Utils;

namespace TransactionLogScanner
{
    public static class VirtualLogFileScanner
    {
        public static VirtualLogFileHeader[] ProcessVirtualLogFile(string filePath, string outputPath)
        {
            bool completedLogScan = false;
            long currentFileOffset = 8192;

            long fileSize = FileManager.GetFileSizeBytes(filePath);

            var headers = new List<VirtualLogFileHeader>();

            while (!completedLogScan)
            {
                var vlfHeader = ReadVLFHeader(filePath, currentFileOffset);

                if (vlfHeader == null)
                    break;

                headers.Add(vlfHeader);

                currentFileOffset += vlfHeader.fileSize;

                if (currentFileOffset >= fileSize)
                    completedLogScan = true;
            }

            if (headers.Any())
            {
                PrintVLFTable(headers, outputPath);
            }

            return headers.ToArray();
        }

        public static VirtualLogFileHeader ReadVLFHeader(string filePath, long offset)
        {
            VirtualLogFileHeader vlfHeader = new VirtualLogFileHeader();

            byte[] vlfHeaderContent = FileManager.ReadFileBytes(filePath, offset, 8192);

            vlfHeader.parity = vlfHeaderContent[1];
            vlfHeader.version = vlfHeaderContent[2];
            vlfHeader.fSeqNo = BitConverter.ToInt32(vlfHeaderContent, 4);
            vlfHeader.writeSeqNo = BitConverter.ToInt32(vlfHeaderContent, 12);
            vlfHeader.fileSize = BitConverter.ToInt64(vlfHeaderContent, 16);
            vlfHeader.startOffset = BitConverter.ToInt64(vlfHeaderContent, 24);
            vlfHeader.createLsn = Functions.FormatLsn(vlfHeaderContent.AsSpan(32, 10));

            return vlfHeader;
        }

        public static void PrintVLFTable(List<VirtualLogFileHeader> vlfHeaderList, string outputPath)
        {
            string printTableTitle = "VLF LIST";

            string msgOutput = "";

            var printTableHeaders = new[] { 
                "FSeqNo", 
                "Parity", 
                "Version", 
                "WriteSeqNo", 
                "FileSize", 
                "StartOffset", 
                "Create LSN" 
            };

            var printTableAlignments = new[]
            {
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center
            };

            var printTableRows = new List<string[]>();

            foreach (VirtualLogFileHeader vlfHeader in vlfHeaderList)
            {
                printTableRows.Add(new[] {
                    $"{vlfHeader.fSeqNo}",
                    $"{vlfHeader.parity}",
                    $"{vlfHeader.version}",
                    $"{vlfHeader.writeSeqNo}",
                    $"{vlfHeader.fileSize}",
                    $"{vlfHeader.startOffset}",
                    $"{vlfHeader.createLsn}"
            });
            }

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
