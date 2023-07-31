using System;
using System.Diagnostics;

namespace TrayAppUtility
{
    public static class Progress
    {
        static readonly object m_LockObject = new();
        static readonly Stopwatch m_Stopwatch = new();

        static int m_Total;
        public static int Total
        {
            get => m_Total;
            set
            {
                if (m_Total < 0)
                    throw new ArgumentException("Total items cannot be negative.");

                lock (m_LockObject)
                {
                    if (m_Total == 0 && value > 0)
                        m_Stopwatch.Restart();

                    m_Total = value;
                    Processed = m_Processed;

                    TrayApp.s_IconUpdate.Start();
                    TrayApp.s_TooltipUpdate.Start();
                }
            }
        }

        static int m_Processed;
        public static int Processed
        {
            get => m_Processed;
            set
            {
                lock (m_LockObject)
                {
                    m_Processed = Math.Clamp(value, 0, m_Total);
                }
            }
        }

        public static float Ratio
        {
            get
            {
                if (m_Total == 0)
                    return 0;

                return (float)m_Processed / m_Total;
            }
        }

        public static TimeSpan ETA
        {
            get
            {
                if(Processed < 1)
                    return TimeSpan.Zero;

                var elapsedRatio = (float)Processed / Total;
                var etaRatio = 1 - elapsedRatio;
                return m_Stopwatch.Elapsed * (etaRatio / elapsedRatio);
            }
        }

        public static void Increment(int amount = 1)
        {
            lock (m_LockObject)
            {
                m_Processed = Math.Clamp(m_Processed + amount, 0, m_Total);
            }
        }
    }
}
