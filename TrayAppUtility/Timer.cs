using System;
using System.Diagnostics;

namespace TrayAppUtility
{
    public class Timer : IDisposable
    {
        readonly Log m_Log;
        readonly Stopwatch m_Stopwatch;
        readonly string? m_OperationName;

        public Timer(Log log, string? operationName = null)
        {
            m_Log = log;
            m_Stopwatch = Stopwatch.StartNew();
            m_OperationName = operationName;
        }

        public void Dispose()
        {
            if (m_OperationName != null)
                m_Log?.Write($"{m_OperationName} run time: {m_Stopwatch.Elapsed}");
            else
                m_Log?.Write($"Run time: {m_Stopwatch.Elapsed}");

            GC.SuppressFinalize(this);
        }
    }
}
