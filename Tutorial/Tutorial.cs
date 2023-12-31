﻿using System.Threading;
using TrayAppUtility;

namespace Tutorial
{
    public class TutorialApp
    {
        [TrayDefault]
        public static void DefaultAction(CancellationTokenSource cancel)
        {
            var length = 100;
            Progress.Total = length;

            for (int i = 0; i < length; i++)
            {
                if (cancel.IsCancellationRequested)
                {
                    Log.Write($"Cancelling Default Action");
                    return;
                }

                Thread.Sleep(100);
                Progress.Increment($"Processed item {i}");
            }
        }

        [TrayAction]
        public static void FaultyAction(CancellationTokenSource cancel)
        {
            Progress.Total = 5;
            Progress.Processed = 3;
            throw new System.Exception("Faulty action is throwing an exception");
        }

        [TrayAction]
        [Autorun("00:00:30")]
        public static void Notification(CancellationTokenSource cancel)
        {
            TrayUtils.ShowNotification("Notification", "Message text",
                () => Log.Write("Log entry from notification"));
        }
    }
}
