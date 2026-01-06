using SqlCore.Engine;
using System.ServiceProcess;
using System.Threading;
using System.IO;
using System.IO.MemoryMappedFiles;
using Microsoft.Data.SqlClient;

namespace Test
{
    internal class Program
    {
        const byte newByte = 0x80;
        const byte newByte2 = 0x02;

        static void Main(string[] args)
        {
            string serviceName = "MSSQL$LOCALDB";

            var results = new Dictionary<int, string>();

            for (int i = 1; i <= 255; i++)
            {
                StopSql(serviceName);

                Thread.Sleep(1000);

                ChecksumLDF((byte)i);

                Thread.Sleep(1000);

                StartSql(serviceName);

                results[i] = GetOperation() ?? string.Empty;

                Console.WriteLine($"Index atual {i}");
            }

            Console.Clear();

            foreach (var kv in results)
            {
                Console.WriteLine(
                    string.IsNullOrEmpty(kv.Value)
                        ? $"{kv.Key}: "
                        : $"0x{kv.Key:X2}: {kv.Value}"
                );
            }

            Console.WriteLine($"Finished");
        }

        static void UpdateLogBlock(long offset, long offsetByte, long bufSize, byte fixByte)
        {
            string path = @"C:\Temp\teste.ldf";

            byte[] buffer = new byte[bufSize];

            uint checksum = 0;

            using (FileStream fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                fs.Seek(offset, SeekOrigin.Begin);

                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                if (bytesRead < buffer.Length)
                {
                    Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
                }

                buffer[offsetByte] = fixByte;

                checksum = LogBlockChecksum.CalculateChecksum(buffer);

                byte[] uintBytes = BitConverter.GetBytes(checksum);

                Buffer.BlockCopy(uintBytes, 0, buffer, 24, 4);

                fs.Seek(offset, SeekOrigin.Begin);

                fs.Write(buffer, 0, buffer.Length);

                fs.Flush(true);
            }
        }

        static void ChecksumLDF2()
        {
            string path = @"C:\Temp\teste.ldf";
            long offset = 313856;

            byte[] buffer = new byte[1536];

            uint checksum = 0;

            using (FileStream fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                fs.Seek(offset, SeekOrigin.Begin);

                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                if (bytesRead < buffer.Length)
                {
                    Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
                }

                buffer[95] = newByte;
                buffer[239] = newByte + 1;
                buffer[351] = newByte + 2;
                buffer[463] = newByte + 3;
                buffer[575] = newByte + 4;
                buffer[687] = newByte + 5;
                //buffer[799] = newByte + 6;
                //buffer[911] = newByte + 7;
                //buffer[1023] = newByte + 8;
                //buffer[1135] = newByte + 9;

                checksum = LogBlockChecksum.CalculateChecksum(buffer);

                byte[] uintBytes = BitConverter.GetBytes(checksum);

                Buffer.BlockCopy(uintBytes, 0, buffer, 24, 4);

                fs.Seek(offset, SeekOrigin.Begin);

                fs.Write(buffer, 0, buffer.Length);

                fs.Flush(true);
            }

            Console.WriteLine($"Last is 0x{newByte + 9:X2}");
            Console.WriteLine($"Finished");
        }

        static void ChecksumLDF(byte fixByte)
        {
            string path = @"C:\Temp\teste.ldf";
            long offset = 313856;

            byte[] buffer = new byte[1536];

            uint checksum = 0;

            using (FileStream fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                fs.Seek(offset, SeekOrigin.Begin);

                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                if (bytesRead < buffer.Length)
                {
                    Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
                }

                buffer[94] = fixByte;

                checksum = LogBlockChecksum.CalculateChecksum(buffer);

                byte[] uintBytes = BitConverter.GetBytes(checksum);

                Buffer.BlockCopy(uintBytes, 0, buffer, 24, 4);

                fs.Seek(offset, SeekOrigin.Begin);

                fs.Write(buffer, 0, buffer.Length);

                fs.Flush(true);
            }
        }

        static void StopSql(string serviceName)
        {
            using var service = new ServiceController(serviceName);

            if (service.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("Parando SQL Server...");
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(2));
                Console.WriteLine("SQL Server parado.");
            }
        }

        static void StartSql(string serviceName)
        {
            using var service = new ServiceController(serviceName);

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                Console.WriteLine("Iniciando SQL Server...");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(2));
                Console.WriteLine("SQL Server iniciado.");
            }
        }

        static string GetOperation()
        {

            string connectionString = "Server=LAP0142\\LOCALDB;Database=teste;Integrated Security=true;TrustServerCertificate=True;";
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                using (var traceCmd = new SqlCommand("DBCC TRACEON(2537);", conn))
                {
                    traceCmd.ExecuteNonQuery();
                }

                string sql = @"
                SELECT Operation
                FROM sys.fn_dblog(NULL, NULL)
                WHERE [Transaction ID] = '0000:00000cd0'
                  AND [Current LSN] = '00000029:00000255:0001';
                ";

                using var cmd = new SqlCommand(sql, conn);

                object result = cmd.ExecuteScalar();

                return result?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
