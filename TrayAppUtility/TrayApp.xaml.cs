using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;

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
        static Mutex? m_Mutex;
        static bool s_ToastNotificationInProgress;

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

        internal static readonly TaskbarIcon s_Tray = new() { Visibility = Visibility.Hidden};
        internal static DispatcherTimer? s_AutorunTimer;
        internal static Action? s_AutorunCallback;
        internal static string? s_ScheduledAction;

        static readonly IEnumerable<MethodInfo> s_Actions = FindActions();
        static readonly Bitmap s_CachedTrayIcon = LoadBitmapFromResource("TrayIcon.png");

        static Color ProgressColor => Error ? k_ErrorColor : k_ProgressColor;
        static bool Error
        {
            get => s_Error;
            set
            {
                if (s_Error == true && value == false)
                    Progress.Total = 0;

                s_Error = value;
                s_IconUpdate.Start();
                s_TooltipUpdate.Start();
            }
        }

        public TrayApp()
        {
            m_Mutex = new Mutex(true, AppDomain.CurrentDomain.FriendlyName, out var createdNew);

            if (!createdNew)
            {
                MessageBox.Show($"Another instance of the '{AppDomain.CurrentDomain.FriendlyName}' application is already running.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }

            InitializeComponent();
            UpdateContextMenu();

            s_Tray.TrayMouseDoubleClick += DoubleClick;
            s_Tray.TrayBalloonTipShown += (o, e) => s_ToastNotificationInProgress = true;
            s_Tray.TrayBalloonTipClosed += (o, e) => s_ToastNotificationInProgress = false;
            s_Tray.TrayBalloonTipClicked += (o, e) => s_ToastNotificationInProgress = false;
            Log.s_OnWrite = (message) => s_LastLog = message;
            s_Tray.Visibility = Visibility.Visible;

            s_AutorunCallback?.Invoke();
        }

        protected override void OnClosed(EventArgs e)
        {
            m_Mutex.ReleaseMutex();
            m_Mutex.Dispose();

            base.OnClosed(e);
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
            TrayUtils.Open(LogFile.k_LogFolder);
            Error = false;
        }

        private void OnLastLog(object? o, EventArgs? e)
        {
            TrayUtils.Open(LogFile.s_LastLogFilePath);
            Error = false;
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
                    Header = TrayUtils.NiceName(action.Name),
                    IsEnabled = s_ActiveTask?.Status != TaskStatus.Running
                };

                item.Click += async (o, e) =>
                {
                    s_ActiveTask = Task.Run(() =>
                    {
                        s_ActionName = TrayUtils.NiceName(action.Name);

                        LogFile? log = null;
                        if(action.GetCustomAttribute<NoLogAttribute>() == null)
                        {
                            log = new LogFile(action.Name);
                            Log.s_CurrentLog = log;
                        }

                        s_Tray.Dispatcher.Invoke(() => UpdateContextMenu());

                        try
                        {
                            using var timer = new Timer();
                            s_Cancel = new CancellationTokenSource();

                            Error = false;
                            Progress.Total = 0;
                            action.Invoke(null, new object[] { s_Cancel });

                            while(s_ToastNotificationInProgress)
                            {
                                Thread.Sleep(50);
                            }

                            Progress.Total = 0;
                        }
                        catch (Exception? ex)
                        {
                            Error = true;
                            Log.Write(ex.InnerException);
                        }
                        finally
                        {
                            log?.Dispose();
                            Log.s_CurrentLog = null;
                        }
                    });

                    await s_ActiveTask;
                    UpdateContextMenu();

                    if(s_ScheduledAction != null)
                    {
                        var menuItem = FindMenuItemByName(s_Tray?.ContextMenu, s_ScheduledAction);
                        menuItem?.PerformClick();
                        s_ScheduledAction = null;
                    }
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
            using var bitmap = s_CachedTrayIcon.Clone() as Bitmap;

            if(Progress.Total > 0)
                OverlayRadialProgressBar(bitmap, Progress.Ratio, ProgressColor);

            if(Progress.Total == 0 || Error)
                s_IconUpdate.Stop();

            s_Tray.Icon?.DestroyHandle();
            s_Tray.Icon?.Dispose();
            s_Tray.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        static void UpdateTrayTooltip(object? o, EventArgs e)
        {
            if(Progress.Total > 0)
            {
                s_Tray.ToolTipText = $"{s_ActionName}\n" +
                    $"{Progress.Processed} / {Progress.Total} ({Math.Round(Progress.Ratio * 100)}%)\n" +
                    $"ETA: {Progress.ETA:hh\\:mm\\:ss}\n" +
                    $"{s_LastLog}";
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
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
            ConcurrentQueue<MethodInfo>? methodsWithAttribute = new ();

            MethodInfo? autorunMethod = null;

            Parallel.ForEach(assemblies, assembly =>
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    var methods = type.GetMethods();

                    foreach (var method in methods)
                    {
                        if (method.IsDefined(typeof(TrayActionAttribute))
                        || method.IsDefined(typeof(TrayDefaultAttribute)))
                        {
                            methodsWithAttribute.Enqueue(method);

                            if (method.IsDefined(typeof(AutorunAttribute)))
                            {
                                autorunMethod = method;
                            }
                        }
                    }
                }
            });

            if(autorunMethod != null)
            {
                var autorunAttribute = autorunMethod.GetCustomAttribute<AutorunAttribute>();
                var actionName = TrayUtils.NiceName(autorunMethod.Name);
                s_AutorunCallback = () =>
                {
                    var menuItem = FindMenuItemByName(s_Tray?.ContextMenu, actionName);
                    menuItem?.PerformClick();
                };
                if(TimeSpan.TryParse(autorunAttribute.TimeSpanString, out var timespan))
                {
                    s_AutorunTimer = new(timespan, DispatcherPriority.Background,
                        (o, e) => s_AutorunCallback?.Invoke(),
                        s_Tray.Dispatcher);
                    s_AutorunTimer.Start();
                }
            }

            return methodsWithAttribute;
        }

        static Bitmap LoadBitmapFromResource(string resourceName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);
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
                        using var bitmap = new Bitmap(stream);
                        var resizedBitmap = MaskAndResizeImage(bitmap, k_TrayIconSize);
                        bitmaps.Add(resizedBitmap);
                    }
                }
            });

            if (!bitmaps.IsEmpty)
            {
                for (int i = 1; i < bitmaps.Count; i++)
                    bitmaps.ElementAt(i).Dispose();

                return bitmaps.First();
            }
            else
            {
                return new Bitmap(k_TrayIconSize, k_TrayIconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
        }

        static Bitmap MaskAndResizeImage(Bitmap image, int size)
        {
            Bitmap resizedImage = new(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                graphics.Clear(Color.Transparent);

                using var path = new GraphicsPath();
                path.AddEllipse(new RectangleF(0.5f, 0.5f, size - 1, size - 1));
                graphics.SetClip(path);

                graphics.DrawImage(image, 0, 0, size, size);
            }
            return resizedImage;
        }

        static MenuItem? FindMenuItemByName(ItemsControl itemsControl, string name)
        {
            foreach (var item in itemsControl.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (((string)menuItem.Header).Equals(name))
                    {
                        return menuItem;
                    }

                    var subMenuItem = FindMenuItemByName(menuItem, name);
                    if (subMenuItem != null)
                    {
                        return subMenuItem;
                    }
                }
            }

            return null;
        }
    }
}
