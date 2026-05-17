using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LLMBalanceMonitor;

public static class IconGenerator
{
    public static Icon CreateAppIcon()
    {
        int size = 64;
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Gradient background circle
            var rect = new Rectangle(0, 0, size, size);
            var brush = new LinearGradientBrush(rect,
                Color.FromArgb(0, 180, 120),
                Color.FromArgb(0, 100, 200),
                LinearGradientMode.ForwardDiagonal);
            g.FillEllipse(brush, rect);

            // Outer ring
            using var pen = new Pen(Color.FromArgb(60, Color.White), 2);
            g.DrawEllipse(pen, rect with { X = 3, Y = 3, Width = 58, Height = 58 });

            // Dollar/Yuan symbol
            string symbol = "¥";
            using var font = new Font("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);
            var textSize = g.MeasureString(symbol, font);
            var textPoint = new PointF(
                (size - textSize.Width) / 2,
                (size - textSize.Height) / 2 - 1);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString(symbol, font, textBrush, textPoint);

            // Small up-arrow indicator (subtle)
            var arrowPts = new[]
            {
                new PointF(38, 12),
                new PointF(42, 20),
                new PointF(34, 20),
            };
            using var arrowBrush = new SolidBrush(Color.FromArgb(180, Color.White));
            g.FillPolygon(arrowBrush, arrowPts);
        }

        // Convert to Icon
        IntPtr hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        return icon;
    }
}
