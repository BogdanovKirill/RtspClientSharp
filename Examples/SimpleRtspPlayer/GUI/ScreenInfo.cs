using System.Reflection;
using System.Windows;

namespace SimpleRtspPlayer.GUI
{
    static class ScreenInfo
    {
        public static readonly double DpiX;
        public static readonly double DpiY;

        static ScreenInfo()
        {
            const double defaultDpi = 96.0;

            PropertyInfo dpiXProperty =
                typeof(SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            PropertyInfo dpiYProperty =
                typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static);

            DpiX = dpiXProperty != null ? (int) dpiXProperty.GetValue(null, null) : defaultDpi;
            DpiY = dpiYProperty != null ? (int) dpiYProperty.GetValue(null, null) : defaultDpi;
        }
    }
}