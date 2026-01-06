namespace SqlCore.Engine
{
    public static class PageHeader
    {
        public static string GetPageTypeDesc(byte pageType) =>
            pageType switch
            {
                0x00 => "UNALLOCATED_PAGE",
                0x01 => "DATA_PAGE",
                0x02 => "INDEX_PAGE",
                0x03 => "TEXT_MIX_PAGE",
                0x04 => "TEXT_TREE_PAGE",
                0x06 => "WORK_FILE_PAGE",
                0x07 => "SORT_PAGE",
                0x08 => "GAM_PAGE",
                0x09 => "SGAM_PAGE",
                0x0a => "IAM_PAGE",
                0x0b => "PFS_PAGE",
                0x0d => "BOOT_PAGE",
                0x0e => "SYSCONFIG_PAGE",
                0x0f => "FILEHEADER_PAGE",
                0x10 => "DIFF_MAP_PAGE",
                0x11 => "ML_MAP_PAGE",
                0x12 => "DBCC_FORMATTED_PAGE",
                0x13 => "UNLINKED_REORG_PAGE",
                0x14 => "BULK_OPERATION_PAGE",
                0x15 => "ENCRYPT_UNALLOC_PAGE",
                0x64 => "DUMP_HEADER_PAGE",
                0x65 => "DUMP_TRAILER_PAGE",
                0x66 => "UNDOFILE_HEADER_PAGE",
                _    => "UNKNOWN_PAGE"
            };

        public static string GetFlagBitsDesc(short flagBits)
        {
            var bits = new List<string>();

            for (int i = 0; i < 16; i++)
            {
                if ((flagBits & (1 << i)) != 0)
                {
                    bits.Add(GetBitDesc(i));
                }
            }

            return bits.Count > 0
                ? string.Join(" | ", bits)
                : "";
        }

        public static bool HasChecksum(short flagBits)
        {
            return (flagBits & (1 << 9)) != 0;
        }

        static string GetBitDesc(int bitIndex) =>
            bitIndex switch
            {
                0 => "IS_IN_SYSXACT",
                1 => "PG_ALIGNED4",
                2 => "FIXEDLEN_ROW",
                3 => "HAS_FREESLOT",
                4 => "DIRTIED_BY_LC_XACT",
                5 => "ALLOC_NONLOGGED",
                6 => "RestorePending",
                7 => "RestoreBulkPage",
                8 => "TEAR_PROOF",
                9 => "HAS_CHECKSUM",
                10 => "ENCRYPTED0",
                11 => "ENCRYPTED1",
                12 => "ARRAYED",
                13 => "VERSION_INFO",
                14 => "ADD_BEG",
                15 => "ADD_END"
            };
    }
}
