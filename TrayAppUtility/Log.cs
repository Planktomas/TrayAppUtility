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

        public LogFile(string logName)
        {
            Directory.CreateDirectory(k_LogFolder);
            DeleteOldLogFiles(k_LogFolder, DateTime.Now.AddMonths(-1));

            s_LastLogFilePath = m_LogPath
                = $"{Path.Combine(k_LogFolder, logName)} {DateTime.Now:yyyy-MM-dd HH.mm.ss}.txt";

            m_FlushTimer = new System.Threading.Timer(Flush, null,
                TimeSpan.FromSeconds(k_FlushInterval),
                TimeSpan.FromSeconds(k_FlushInterval));
        }

        public void Write(string message)
        {
            m_LogBuffer.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
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

    public class Log : IDisposable
    {
        readonly LogFile log;
        readonly Action<string>? onWrite;

        public Log(string logName, Action<string>? onWrite = null)
        {
            log = new LogFile(logName);
            this.onWrite = onWrite;
        }

        public void Write(string message)
        {
            log.Write(message);
            onWrite?.Invoke(message);
        }

        public void Dispose()
        {
            log.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
