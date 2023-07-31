using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrayAppUtility
{
    /// <summary>
    /// Interaction logic for TrayApp.xaml
    /// </summary>
    public partial class TrayApp : Window
    {
        const int k_TrayIconSize = 32;
        static readonly Color k_ProgressColor = Color.RoyalBlue;
        static readonly Color k_ErrorColor = Color.OrangeRed;
        static readonly Color k_BackgroundColor = Color.FromArgb(48, 48, 48);
        static readonly Color k_BorderColor = Color.FromArgb(24, 24, 24);

        static Task? s_ActiveTask = null;
        static bool s_Error = false;
        static string? s_ActionName;
        static string? s_LastLog;
        static MenuItem? s_DefaultAction;
        static CancellationTokenSource s_Cancel = new();

        internal static DispatcherTimer s_IconUpdate = new(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Render,
            UpdateTrayIcon,
            Dispatcher.CurrentDispatcher);

        internal static DispatcherTimer s_TooltipUpdate = new(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.Background, 
            UpdateTrayTooltip,
            Dispatcher.CurrentDispatcher);

        static readonly TaskbarIcon s_Tray = new();
        static readonly IEnumerable<MethodInfo> s_Actions = FindActions();
        static readonly Bitmap s_CachedTrayIcon = LoadBitmapFromResource("TrayIcon.png");

        static Bitmap TrayIcon => (Bitmap)s_CachedTrayIcon.Clone();
        static Color ProgressColor => Error ? k_ErrorColor : k_ProgressColor;
        static bool Error
        {
            get => s_Error;
            set
            {
                s_Error = value;
                s_IconUpdate.Start();
                s_TooltipUpdate.Start();
            }
        }

        public TrayApp()
        {
            InitializeComponent();
            UpdateContextMenu();

            s_Tray.TrayMouseDoubleClick += DoubleClick;
        }

        private void DoubleClick(object sender, RoutedEventArgs e)
        {
            if (Error)
                OnLastLog(null, null);
            else
                s_DefaultAction?.PerformClick();
        }

        private void OnLogFolder(object? o, EventArgs? e)
        {
            OpenFolderWithDefaultProgram(LogFile.k_LogFolder);

            Error = false;
            Progress.Total = 0;
        }

        private void OnLastLog(object? o, EventArgs? e)
        {
            OpenFileWithDefaultApplication(LogFile.s_LastLogFilePath);

            Error = false;
            Progress.Total = 0;
        }

        private void OnAbort(object? o, EventArgs? e)
        {
            s_Cancel.Cancel();
        }

        private void OnClose(object? o, EventArgs? e)
        {
            Application.Current.Shutdown();
        }

        private void UpdateContextMenu()
        {
            var menu = new ContextMenu();

            foreach (var action in s_Actions)
            {
                var item = new MenuItem()
                {
                    Header = NiceName(action.Name),
                    IsEnabled = s_ActiveTask?.Status != TaskStatus.Running
                };

                item.Click += async (o, e) =>
                {
                    s_ActiveTask = Task.Run(() =>
                    {
                        s_ActionName = NiceName(action.Name);
                        using var log = new Log(action.Name, (message) => s_LastLog = message);

                        s_Tray.Dispatcher.Invoke(() => UpdateContextMenu());

                        try
                        {
                            using var timer = new Timer(log);
                            s_Cancel = new CancellationTokenSource();

                            Error = false;
                            Progress.Total = 0;
                            action.Invoke(null, new object[] { log, s_Cancel });
                            Progress.Total = 0;
                        }
                        catch (Exception? ex)
                        {
                            Error = true;

                            if(ex.InnerException != null)
                            {
                                ex = ex.InnerException;

                                while (ex != null)
                                {
                                    log.Write($"{ex.Message}\n{ex.StackTrace}");
                                    ex = ex.InnerException;
                                }
                            }
                        }
                    });

                    await s_ActiveTask;
                    UpdateContextMenu();
                };

                if (action.IsDefined(typeof(TrayDefaultAttribute)))
                    s_DefaultAction = item;

                menu.Items.Add(item);
            }

            var logFolder = new MenuItem();
            logFolder.Header = "Log Folder";
            logFolder.IsEnabled = Directory.Exists(LogFile.k_LogFolder);
            logFolder.Click += OnLogFolder;
            menu.Items.Add(logFolder);

            var log = new MenuItem();
            log.Header = "Last Log";
            log.IsEnabled = !string.IsNullOrWhiteSpace(LogFile.s_LastLogFilePath);
            log.Click += OnLastLog;
            menu.Items.Add(log);

            var abort = new MenuItem();
            abort.Header = "Abort";
            abort.IsEnabled = s_ActiveTask?.Status == TaskStatus.Running;
            abort.Click += OnAbort;
            menu.Items.Add(abort);

            var close = new MenuItem();
            close.Header = "Close";
            close.Click += OnClose;
            menu.Items.Add(close);

            s_Tray.ContextMenu = menu;
        }

        static void UpdateTrayIcon(object? o, EventArgs e)
        {
            var bitmap = TrayIcon;

            if(Progress.Total > 0)
                OverlayRadialProgressBar(bitmap, Progress.Ratio, ProgressColor);

            if(Progress.Total == 0 || Error)
                s_IconUpdate.Stop();

            s_Tray.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        static void UpdateTrayTooltip(object? o, EventArgs e)
        {
            if(Progress.Total > 0)
            {
                s_Tray.ToolTipText = $"{s_ActionName}\n" +
                    $"{Progress.Processed} / {Progress.Total} ({Math.Round(Progress.Ratio * 100)}%)\n" +
                    $"{s_LastLog}\n" +
                    $"ETA: {Progress.ETA:hh\\:mm\\:ss}";
            }

            if(Progress.Total == 0 || Error)
            {
                if(!Error)
                    s_Tray.ToolTipText = AppDomain.CurrentDomain.FriendlyName;

                s_TooltipUpdate.Stop();
            }
        }

        static void OverlayRadialProgressBar(Bitmap image, float progress, Color foregroundColor)
        {
            const float backgroundSweepAngle = 360.0f;

            var diameter = Math.Min(image.Width, image.Height) - 1;
            var thickness = Math.Min(image.Width, image.Height) / 8;
            var x = (image.Width - diameter) / 2;
            var y = (image.Height - diameter) / 2;

            using Graphics graphics = Graphics.FromImage(image);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var progressSweepAngle = progress * backgroundSweepAngle;

            // Draw the outline of the progress bar
            using (var backgroundPen = new Pen(k_BorderColor, thickness + 2))
                graphics.DrawPie(backgroundPen, x, y, diameter, backgroundSweepAngle, thickness);

            // Draw the background of the progress bar
            using (var backgroundPen = new Pen(k_BackgroundColor, thickness))
                graphics.DrawPie(backgroundPen, x, y, diameter, backgroundSweepAngle, thickness);

            // Draw the progress part of the progress bar
            using (var progressPen = new Pen(foregroundColor, thickness))
                graphics.DrawPie(progressPen, x, y, diameter, progressSweepAngle, thickness);
        }

        static IEnumerable<MethodInfo> FindActions()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            ConcurrentQueue<MethodInfo>? methodsWithAttribute = new ();

            Parallel.ForEach(assemblies, assembly =>
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    var methods = type.GetMethods();

                    foreach (var method in methods)
                    {
                        if (method.IsDefined(typeof(TrayActionAttribute)))
                            methodsWithAttribute.Enqueue(method);
                    }
                }
            });

            return methodsWithAttribute;
        }

        static Bitmap LoadBitmapFromResource(string resourceName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var bitmaps = new ConcurrentBag<Bitmap>();

            Parallel.ForEach(assemblies, (assembly, state) =>
            {
                var resourceNames = assembly.GetManifestResourceNames();
                var fullResourceName = Array.Find(resourceNames, name => name.EndsWith(resourceName));

                if (fullResourceName != null)
                {
                    using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
                    if (stream != null)
                    {
                        var bitmap = new Bitmap(stream);
                        var resizedBitmap = ResizeImage(bitmap, k_TrayIconSize, k_TrayIconSize);
                        bitmaps.Add(resizedBitmap);
                    }
                }
            });

            if (!bitmaps.IsEmpty)
            {
                return bitmaps.First();
            }
            else
            {
                return new Bitmap(k_TrayIconSize, k_TrayIconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
        }

        static Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            Bitmap resizedImage = new(width, height);
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, 0, 0, width, height);
            }
            return resizedImage;
        }

        static string NiceName(string name)
        {
            StringBuilder result = new();

            for (int i = 0; i < name.Length; i++)
            {
                char currentChar = name[i];
                result.Append(currentChar);

                if (i + 1 < name.Length && char.IsLower(currentChar) && char.IsUpper(name[i + 1]))
                {
                    result.Append(' ');
                }
            }

            return result.ToString();
        }

        static void OpenFileWithDefaultApplication(string? filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new ArgumentException("File does not exist.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        static void OpenFolderWithDefaultProgram(string? folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                throw new ArgumentException("Folder does not exist.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

}
