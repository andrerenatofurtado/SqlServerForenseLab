using SqlCore.Engine;
using SqlCore.Utils;
using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;

namespace LoginPasswordRecovery
{
    internal class Program
    {

        private const int PAGESIZE = 8192;
        private const int PWDHASH_LEN = 70;

        static void Main(string[] args)
        {
            try
            {
                string path = ReadRequired("Enter the master.mdf file path: ");

                Console.WriteLine();

                ProcessLogin(path);

                string loginName = ReadRequired("Enter login name to reset password: ");
                string newPassword = ReadRequired("Enter new password: ");

                ProcessLogin(path, loginName, newPassword);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void ProcessLogin(string path, string login = null, string pwd = null)
        {
            bool reset = false;

            if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(pwd))
                reset = true;

            using var mappedFile = 
                MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);

            using var viewAccessor = 
                mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            long fileSize = viewAccessor.Capacity;
            long pageCount = fileSize / PAGESIZE;

            byte[] pageBuffer = new byte[PAGESIZE];

            byte pageType, typeFlagBits;
            short flagBits, slotCnt, ghostRecCnt;
            int objectId;

            bool hasTearproof, hasChecksum, isEncrypted, isCompressed;
            byte parity = 0b01;

            for (long pageId = 0; pageId < pageCount; pageId++)
            {
                long pageOffset = pageId * PAGESIZE;

                viewAccessor.ReadArray(pageOffset, pageBuffer, 0, PAGESIZE);

                pageType = pageBuffer[1];

                objectId = BitConverter.ToInt32(pageBuffer, 24);

                if (pageType == 1 && objectId == 42)
                {
                    typeFlagBits = pageBuffer[2];

                    flagBits = BitConverter.ToInt16(pageBuffer, 4);

                    hasTearproof = PageHeader.HasTearproof(flagBits);
                    hasChecksum = PageHeader.HasChecksum(flagBits);

                    isEncrypted = PageHeader.IsEncrypted(flagBits);
                    isCompressed = PageHeader.IsCompressed(typeFlagBits);

                    if (isEncrypted) { Console.WriteLine($"Page ID {pageId} is encrypted"); continue; }

                    if (isCompressed) { Console.WriteLine($"Page ID {pageId} is compressed"); continue; }

                    slotCnt = BitConverter.ToInt16(pageBuffer, 22);
                    ghostRecCnt = BitConverter.ToInt16(pageBuffer, 58);

                    if (hasTearproof)
                        pageBuffer = Page.RestoreTornBits(pageBuffer, out parity);

                    ushort[] slotArray = Page.ReadSlotArray(pageBuffer, slotCnt);

                    for (int i = 0; i < slotCnt; i++)
                    {
                        if (slotArray[i] > 0)
                        {
                            short nullBitmapOffset = BitConverter.ToInt16(pageBuffer, slotArray[i] + 2);
                            short nullBitmap = BitConverter.ToInt16(pageBuffer, slotArray[i] + nullBitmapOffset + 2);
                            short varColsCount = BitConverter.ToInt16(pageBuffer, slotArray[i] + nullBitmapOffset + 4);

                            short loginNameOffset = checked((short)(nullBitmapOffset + 6 + (varColsCount * 2)));

                            short[] varColsOffsets = ReadVarColsOffsets(pageBuffer, slotArray[i] + nullBitmapOffset + 6, varColsCount);

                            string loginName = Encoding.Unicode.GetString(pageBuffer, slotArray[i] + loginNameOffset, varColsOffsets[0] - loginNameOffset);

                            bool hasPwdHash = !Functions.IsBitSet(nullBitmap, 9);

                            if (hasPwdHash && (!reset || loginName == login))
                            {
                                int pwdHashOffsetIndex =
                                        (Functions.IsBitSet(nullBitmap, 2) ? 0 : 1) +
                                        (Functions.IsBitSet(nullBitmap, 7) ? 0 : 1) +
                                        (Functions.IsBitSet(nullBitmap, 8) ? 0 : 1);

                                if ((varColsOffsets[pwdHashOffsetIndex + 1] - varColsOffsets[pwdHashOffsetIndex]) != PWDHASH_LEN)
                                {
                                    Console.WriteLine($"Error reading pwdHash for login {loginName}");
                                    continue;
                                }

                                byte[] pwdHash = new byte[PWDHASH_LEN];

                                Array.Copy(
                                    pageBuffer,
                                    slotArray[i] + varColsOffsets[pwdHashOffsetIndex],
                                    pwdHash,
                                    0,
                                    PWDHASH_LEN
                                );

                                if (pwdHash[0] != 3)
                                {
                                    Console.WriteLine($"Error reading pwdHash for login {loginName}");
                                    continue;
                                }

                                if (reset)
                                {
                                    pwdHash = CreatePwdHash(pwd);

                                    Array.Copy(
                                        pwdHash,
                                        0,
                                        pageBuffer,
                                        slotArray[i] + varColsOffsets[pwdHashOffsetIndex],
                                        PWDHASH_LEN
                                    );

                                    if (hasTearproof) 
                                        Page.WriteTornBits(pageBuffer, parity);

                                    if (hasChecksum) 
                                        Page.CalculateChecksum(pageBuffer);

                                    viewAccessor.WriteArray(pageOffset, pageBuffer, 0, PAGESIZE);

                                    viewAccessor.Flush();

                                    Console.WriteLine();
                                    Console.ForegroundColor = System.ConsoleColor.Green;
                                    Console.WriteLine($"Password changed for login {loginName}");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.WriteLine($"CREATE LOGIN {loginName} WITH PASSWORD = {Functions.FormatBytesAsHex(pwdHash, 0, 70)} HASHED;");
                                    Console.WriteLine();
                                }
                            }
                        }
                    }
                }
            }
        }

        static short[] ReadVarColsOffsets(byte[] buffer, int start, short count)
        {
            short[] offsets = new short[count];

            for (int i = 0; i < count; i++)
                offsets[i] = BinaryPrimitives.ReadInt16LittleEndian(
                    buffer.AsSpan(start + i * 2, 2));

            return offsets;
        }

        static byte[] CreatePwdHash(string password)
        {
            byte[] pwd = Encoding.Unicode.GetBytes(password);
            byte[] salt = Functions.GenerateRandomSalt();

            byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(pwd, salt, 100_000, HashAlgorithmName.SHA512, 64);

            byte[] hash = new byte[PWDHASH_LEN];

            hash[0] = 0x03;
            hash[1] = 0x00;

            Buffer.BlockCopy(salt, 0, hash, 2, 4);
            Buffer.BlockCopy(derivedKey, 0, hash, 6, 64);

            return hash;
        }

        static string ReadRequired(string prompt)
        {
            Console.Write(prompt);
            string? value = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Input cannot be empty");

            return value;
        }

    }
}