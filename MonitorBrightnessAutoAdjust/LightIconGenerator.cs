using System.Runtime.InteropServices;

namespace MonitorBrightnessAutoAdjust
{
    /// <summary>
    /// Generate notify icon from light lux value.
    /// </summary>
    public class LightIconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr handle);

        private static readonly int IconSize = 512;

        /// <summary>
        /// static ctor.
        /// </summary>
        static LightIconGenerator()
        {
            IconSize = Resources.AmbientLight.Width;
        }

        public static Icon GenerateIcon(int light)
        {
            using var bitmap = new Bitmap(IconSize, IconSize);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.TextContrast = 1;
            DrawBackgroundOnGraphics(graphics, IconSize);
            DrawLightOnGraphics(light, graphics, IconSize);
            var hIcon = bitmap.GetHicon();
            var newIcon = Icon.FromHandle(hIcon);
            var icon = new Icon(newIcon, IconSize, IconSize);
            CleanupIcon(ref newIcon);
            return icon;
        }

        private static void CleanupIcon(ref Icon icon)
        {
            if (icon is null)
            {
                return;
            }

            DestroyIcon(icon.Handle);
            icon.Dispose();
        }

        private static void DrawBackgroundOnGraphics(Graphics graphics, int size = 0)
        {
            if (size == 0) size = IconSize;
            var backgroundColor = Color.Black;
            using var backgroundBrush = new SolidBrush(backgroundColor);
            var inset = (float)Math.Abs(size * .03125);
            graphics?.FillRectangle(backgroundBrush, inset, inset, size - inset, size - inset);
        }

        private static void DrawLightOnGraphics(int light, Graphics graphics, int size = 0)
        {
            if (size == 0) size = IconSize;
            var lightString = light.ToString();
            var reSize = lightString.Length switch
            {
                1 => 0.7,
                2 => 0.6,
                3 => 0.5,
                4 => 0.35,
                5 => 0.28,
                _ => 0.6
            };
            var fontSize = (float)Math.Abs(size * reSize);

            Font font = new Font("Segoe UI", fontSize, FontStyle.Regular);
            StringFormat format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;
            graphics.DrawString(light.ToString(), font, Brushes.LawnGreen, size * 0.5f, size * 0.5f, format);
        }
    }
}
