using System.Threading;
using TrayAppUtility;

namespace Tutorial
{
    public class TutorialApp
    {
        [TrayAction]
        [TrayDefault]
        public static void DefaultAction(Log log, CancellationTokenSource cancel)
        {
            var length = 100;
            Progress.Total = length;

            for (int i = 0; i < length; i++)
            {
                if (cancel.IsCancellationRequested)
                {
                    log.Write($"Cancelling Default Action");
                    return;
                }

                log.Write($"Processing item {i}");
                Thread.Sleep(100);
                Progress.Increment();
            }
        }

        [TrayAction]
        public static void FaultyAction(Log log, CancellationTokenSource cancel)
        {
            Progress.Total = 5;
            Progress.Processed = 3;
            throw new System.Exception("Faulty action is throwing an exception");
        }
    }
}
