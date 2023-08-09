using System.Drawing;
using System.Windows.Controls;
using System.Windows;
using System.Runtime.InteropServices;
using System;

namespace TrayAppUtility
{
    public static class GraphicsExtensions
    {
        public static void DrawPie(this Graphics graphics, Pen pen, int x, int y, int diameter, float sweepAngle, float thickness)
        {
            var halfThickness = thickness / 2.0f;
            var rect = new RectangleF(x, y, diameter, diameter);
            rect.Inflate(-halfThickness, -halfThickness);

            graphics.DrawArc(pen, rect, -90, sweepAngle);
        }
    }

    public static class MenuItemExtensions
    {
        public static void PerformClick(this MenuItem menuItem)
        {
            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menuItem));
        }
    }

    public static class IconExtensions
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        public static void DestroyHandle(this Icon icon)
        {
            DestroyIcon(icon.Handle);
        }
    }
}
