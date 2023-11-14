using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace TrayAppUtility
{
    public static class TrayUtils
    {
        public static string NiceName(string name)
        {
            StringBuilder result = new();
            for (int i = 0; i < name.Length; i++)
            {
                char currentChar = name[i];
                result.Append(currentChar);

                if (i + 1 < name.Length && char.IsLower(currentChar) && char.IsUpper(name[i + 1]))
                    result.Append(' ');
            }

            return result.ToString();
        }

        static void OpenFolder(string? folderPath, bool wait)
        {
            if (!System.IO.Directory.Exists(folderPath))
                throw new ArgumentException("Folder does not exist.");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open"
            });

            if (wait)
                process?.WaitForExit();
        }

        static void OpenFile(string? filePath, bool wait)
        {
            if (!System.IO.File.Exists(filePath))
                throw new ArgumentException("File does not exist.");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            if (wait)
                process?.WaitForExit();
        }

        public static void Open(string? path, bool wait = false)
        {
            if (File.Exists(path))
                OpenFile(path, wait);
            else if (Directory.Exists(path))
                OpenFolder(path, wait);
            else
                throw new ArgumentException("The path does not exist.");
        }

        public static void ShowNotification(string title, string message, Action onClick)
        {
            if(TrayApp.s_Tray == null)
            {
                throw new NullReferenceException("No tray app is running");
            }

            RoutedEventHandler? clickCallback = null;
            clickCallback = (o, e) =>
            {
                onClick.Invoke();
                TrayApp.s_Tray.TrayBalloonTipClicked -= clickCallback;
            };

            TrayApp.s_Tray.HideBalloonTip();
            TrayApp.s_Tray.ShowBalloonTip(title, message, BalloonIcon.None);
            TrayApp.s_Tray.TrayBalloonTipClicked += clickCallback;
        }

        public static void ScheduleAction(string name)
        {
            TrayApp.s_ScheduledAction = NiceName(name);
        }
    }
}
