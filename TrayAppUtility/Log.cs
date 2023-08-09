using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public string Write(Exception ex)
        {
            var messageBuffer = new StringBuilder();
            while (ex != null)
            {
                messageBuffer.AppendLine($"{ex.Message}\n{ex.StackTrace}");
                ex = ex.InnerException;
            }

            if (messageBuffer.Length == 0)
                return null;

            var message = messageBuffer.ToString();
            m_LogBuffer.Add(message);
            return message;
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
        internal static LogFile s_CurrentLog;
        internal static Action<string>? s_OnWrite;

        public static void Write(string message)
        {
            s_CurrentLog?.Write(message);
            s_OnWrite?.Invoke(message);
        }

        public static void Write(Exception ex)
        {
            var message = s_CurrentLog?.Write(ex);
            s_OnWrite?.Invoke(message);
        }
    }
}
