using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace TrayAppUtility
{
    class LogFile : IDisposable
    {
        const int k_FlushInterval = 2;
        internal const string k_LogFolder = "Logs";
        internal static string? s_LastLogFilePath = null;

        readonly string m_LogPath;
        readonly System.Threading.Timer m_FlushTimer;
        readonly BlockingCollection<string> m_LogBuffer = new();
        readonly Action<string>? m_OnWrite;

        internal static LogFile s_CurrentLog;

        public LogFile(string logName, Action<string>? onWrite = null)
        {
            m_OnWrite = onWrite;

            Directory.CreateDirectory(k_LogFolder);
            DeleteOldLogFiles(k_LogFolder, DateTime.Now.AddMonths(-1));

            s_LastLogFilePath = m_LogPath
                = $"{Path.Combine(k_LogFolder, logName)} {DateTime.Now:yyyy-MM-dd HH.mm.ss}.txt";

            m_FlushTimer = new System.Threading.Timer(Flush, null,
                TimeSpan.FromSeconds(k_FlushInterval),
                TimeSpan.FromSeconds(k_FlushInterval));

            s_CurrentLog = this;
        }

        public void Write(string message)
        {
            m_LogBuffer.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            m_OnWrite?.Invoke(message);
        }

        private void Flush(object? state = null)
        {
            using var writer = new StreamWriter(m_LogPath, true);
            while (m_LogBuffer.TryTake(out var message))
                writer.WriteLine(message);
        }

        public void Dispose()
        {
            m_FlushTimer.Dispose();
            Flush();
            m_LogBuffer.CompleteAdding();
        }

        static void DeleteOldLogFiles(string directory, DateTime limit)
        {
            var fileNames = Directory.EnumerateFiles(directory);

            Parallel.ForEach(fileNames, fileName =>
            {
                if (File.GetLastWriteTime(fileName) <= limit)
                    File.Delete(fileName);
            });
        }
    }

    public static class Log
    {
        public static void Write(string message) => LogFile.s_CurrentLog.Write(message);
    }
}
