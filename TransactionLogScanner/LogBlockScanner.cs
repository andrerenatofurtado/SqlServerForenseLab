using SqlCore.Engine.SqlTypes;
using SqlCore.Engine.TransactionLog;
using SqlCore.Utils;
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace TransactionLogScanner
{
    public static class LogBlockScanner
    {
        public static void ProcessLogBlock(string filePath, string outputPath, VirtualLogFileHeader[] vlfHeaderList)
        {
            foreach (VirtualLogFileHeader vlfHeader in vlfHeaderList)
            {
                var printTableRows = new List<string[]>();

                if (Functions.IsValidParity(vlfHeader.parity) && vlfHeader.fSeqNo > 0)
                {
                    bool completedVLFScan = false;
                    long currentVlfOffset = 8192;

                    string msgOutput;

                    string logOperations;
                    string logContexts;

                    using var vlfContentMap = new VlfMapper(filePath, vlfHeader.startOffset, vlfHeader.fileSize);

                    while (!completedVLFScan)
                    {
                        vlfContentMap.ReadLogBlockHeader(currentVlfOffset, logBlockHeaderContent =>
                        {
                            byte sectorFlags = logBlockHeaderContent[0];

                            var logBlockHeader = new LogBlockHeader
                            {
                                sectorFlags = sectorFlags,
                                parity = Functions.CalculateParityByte(sectorFlags),
                                firstSector = Functions.IsBitSet(sectorFlags, 4),
                                fSeqNo = BinaryPrimitives.ReadInt32LittleEndian(logBlockHeaderContent.Slice(12, 4)),
                            };

                            if (Functions.IsEqualParity(vlfHeader.parity, logBlockHeader.parity)
                                    && logBlockHeader.firstSector == true
                                    && vlfHeader.fSeqNo == logBlockHeader.fSeqNo)
                            {
                                logBlockHeader.numOfRecords = BinaryPrimitives.ReadUInt16LittleEndian(logBlockHeaderContent.Slice(2, 2));
                                logBlockHeader.offsetSlotArray = BinaryPrimitives.ReadUInt16LittleEndian(logBlockHeaderContent.Slice(4, 2));
                                logBlockHeader.blkSize = BinaryPrimitives.ReadUInt16LittleEndian(logBlockHeaderContent.Slice(6, 2));
                                logBlockHeader.flags = logBlockHeaderContent[8];
                                logBlockHeader.prevBlkSize = BinaryPrimitives.ReadUInt16LittleEndian(logBlockHeaderContent.Slice(10, 2));
                                logBlockHeader.checksum = BinaryPrimitives.ReadUInt32LittleEndian(logBlockHeaderContent.Slice(24, 4));
                                logBlockHeader.closeTime = SqlDateTime.Parse(BinaryPrimitives.ReadInt64LittleEndian(logBlockHeaderContent.Slice(48, 8)));
                                logBlockHeader.lastSector = Functions.IsBitSet(logBlockHeader.sectorFlags, 5);
                                logBlockHeader.hasChecksum = Functions.IsBitSet(logBlockHeader.flags, 1);
                                logBlockHeader.isTDEEncrypted = Functions.IsBitSet(logBlockHeader.flags, 2);

                                vlfContentMap.ReadLogBlock(currentVlfOffset, logBlockHeader.blkSize, logBlockContentData =>
                                {
                                    ushort blkSize = logBlockHeader.blkSize;

                                    byte[] logBlockContentBuffer = ArrayPool<byte>.Shared.Rent(blkSize);

                                    try
                                    {
                                        logBlockContentData.Slice(0, blkSize).CopyTo(logBlockContentBuffer.AsSpan(0, blkSize));

                                        Span<byte> logBlockContent = logBlockContentBuffer.AsSpan(0, blkSize);

                                        uint calculatedChecksum = 0;
                                        msgOutput = "";
                                        logOperations = "";
                                        logContexts = "";

                                        if (logBlockHeader.hasChecksum)
                                        {
                                            calculatedChecksum = LogBlockChecksum.CalculateLogBlockChecksum(logBlockContent);

                                            if (calculatedChecksum != logBlockHeader.checksum)
                                            {
                                                msgOutput = $"Incorrect checksum";
                                            }
                                        }
                                        else
                                        {
                                            msgOutput = $"No checksum verify enabled";
                                        }

                                        if (logBlockHeader.isTDEEncrypted)
                                        {
                                            msgOutput = string.IsNullOrEmpty(msgOutput)
                                                    ? "Log block encrypted with TDE"
                                                    : msgOutput + "\nLog block encrypted with TDE";
                                        }
                                        else
                                        {
                                            var numOfSectors = logBlockHeader.blkSize / 512;

                                            for (int i = 0; i < numOfSectors; i++)
                                            {
                                                if (vlfHeader.parity != Functions.CalculateParityByte(logBlockContent[i * 512]))
                                                {
                                                    msgOutput = string.IsNullOrEmpty(msgOutput)
                                                        ? "Incorrect parity"
                                                        : msgOutput + "\nIncorrect parity";
                                                }

                                                logBlockContent[i * 512] = logBlockContent[logBlockHeader.blkSize - i - 1];
                                            }

                                            logBlockHeader.version = logBlockContent[0];

                                            ushort[] slotArray = LogBlock.ReadLogBlockSlotArray(
                                                                                       logBlockContent,
                                                                                       logBlockHeader.offsetSlotArray,
                                                                                       logBlockHeader.numOfRecords);

                                            var groupOperation = new Dictionary<string, int>();
                                            var groupContext = new Dictionary<string, int>();

                                            for (int i = 0; i < logBlockHeader.numOfRecords; i++)
                                            {
                                                string logRecordOperation =
                                                    LogOperation.GetLogOperationDesc(
                                                        logBlockContent[slotArray[i] + 22]);

                                                string logRecordContext =
                                                    LogContext.GetLogContextDesc(
                                                        logBlockContent[slotArray[i] + 23]);

                                                if (groupOperation.ContainsKey(logRecordOperation))
                                                    groupOperation[logRecordOperation]++;
                                                else
                                                    groupOperation[logRecordOperation] = 1;

                                                if (groupContext.ContainsKey(logRecordContext))
                                                    groupContext[logRecordContext]++;
                                                else
                                                    groupContext[logRecordContext] = 1;
                                            }

                                            logOperations = string.Join("\n",
                                                groupOperation
                                                    .OrderByDescending(x => x.Value)
                                                    .Select(item => $"{item.Key}: {item.Value}"));

                                            logContexts = string.Join("\n",
                                                groupContext
                                                    .OrderByDescending(x => x.Value)
                                                    .Select(item => $"{item.Key}: {item.Value}"));
                                        }

                                        printTableRows.Add(new[] {
                                            $"{logBlockHeader.parity}",
                                            $"{logBlockHeader.version}",
                                            $"{logBlockHeader.numOfRecords}",
                                            $"{logBlockHeader.blkSize}",
                                            $"{logBlockHeader.prevBlkSize}",
                                            $"0x{logBlockHeader.checksum:X8}",
                                            $"{logBlockHeader.closeTime}",
                                            $"{logOperations}",
                                            $"{logContexts}",
                                            $"{msgOutput}"
                                        });
                                    }
                                    finally
                                    {
                                        ArrayPool<byte>.Shared.Return(logBlockContentBuffer);
                                    }
                                });

                            }
                            else
                            {
                                completedVLFScan = true;
                            }

                            if (currentVlfOffset + logBlockHeader.blkSize == vlfHeader.fileSize)
                            {
                                completedVLFScan = true;
                            }
                            else
                            {
                                currentVlfOffset += logBlockHeader.blkSize;
                            }

                        });
                    }

                    PrintLogBlockTable(vlfHeader.fSeqNo, printTableRows, outputPath);
                }
             }
        }

        public static void PrintLogBlockTable(int fSeqNo, List<string[]> rows, string outputPath)
        {

            string tableTitle = string.Concat("VLF ", fSeqNo);

            var headers = new[] { 
                "Parity", 
                "Version", 
                "Num of Records", 
                "Block Size", 
                "Prev Block Size", 
                "Checksum", 
                "Close Time", 
                "Operations", 
                "Contexts", 
                "Description" 
            };

            var alignments = new[]
            {
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Center,
                ASCIITableBuilder.ColumnAlignment.Left,
                ASCIITableBuilder.ColumnAlignment.Left,
                ASCIITableBuilder.ColumnAlignment.Left
            };

            string table = ASCIITableBuilder.BuildTable(tableTitle, headers, rows, alignments);

            File.AppendAllText(outputPath, table);

            File.AppendAllText(outputPath, Environment.NewLine);
        }

    }
}

