using System;
using System.Diagnostics;

namespace TrayAppUtility
{
    public class Timer : IDisposable
    {
        readonly Stopwatch m_Stopwatch;
        readonly string? m_OperationName;

        public Timer(string? operationName = null)
        {
            m_Stopwatch = Stopwatch.StartNew();
            m_OperationName = operationName;
        }

        public void Dispose()
        {
            if (m_OperationName != null)
                Log.Write($"'{m_OperationName}' run time: {m_Stopwatch.Elapsed}");
            else
                Log.Write($"Run time: {m_Stopwatch.Elapsed}");

            GC.SuppressFinalize(this);
        }
    }
}
