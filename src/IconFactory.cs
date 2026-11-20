using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MchoseBatteryTray;

internal static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateBattery(int percent, bool lightTaskbar)
    {
        int safePercent = Math.Clamp(percent, 0, 100);
        string text = safePercent == 100 ? "F+" : safePercent.ToString();
        return CreateTextIcon(text, GetBatteryColor(safePercent), lightTaskbar);
    }

    public static Icon CreateDisconnected(bool unsupported, bool lightTaskbar)
    {
        Color color = lightTaskbar ? Color.FromArgb(65, 65, 65) : Color.FromArgb(238, 238, 238);
        return CreateTextIcon(unsupported ? "?" : "×", color, lightTaskbar);
    }

    private static Color GetBatteryColor(int percent)
    {
        Color mutedRed = Color.FromArgb(211, 101, 96);   // #D36560
        Color softWhite = Color.FromArgb(246, 246, 242); // #F6F6F2
        Color mutedGreen = Color.FromArgb(105, 183, 129); // #69B781
        if (percent <= 50) return Blend(mutedRed, softWhite, percent / 50.0);
        return Blend(softWhite, mutedGreen, (percent - 50) / 50.0);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private static Icon CreateTextIcon(string text, Color textColor, bool lightTaskbar)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using Font font = CreateFont(text.Length == 1 ? 30.5f : 28.5f);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;
        format.FormatFlags |= StringFormatFlags.NoWrap;

        using var path = new GraphicsPath();
        path.AddString(text, font.FontFamily, (int)font.Style, font.Size,
            new RectangleF(0, 0, 32, 32), format);
        RectangleF bounds = path.GetBounds();
        float fit = Math.Min(1.0f, Math.Min(29.0f / Math.Max(1.0f, bounds.Width), 29.0f / Math.Max(1.0f, bounds.Height)));
        if (fit < 0.999f)
        {
            using var scaleMatrix = new Matrix();
            scaleMatrix.Scale(fit, fit);
            path.Transform(scaleMatrix);
            bounds = path.GetBounds();
        }
        using (var centerMatrix = new Matrix())
        {
            centerMatrix.Translate(16.0f - bounds.X - bounds.Width / 2.0f,
                                   16.0f - bounds.Y - bounds.Height / 2.0f);
            path.Transform(centerMatrix);
        }

        Color outline = lightTaskbar ? Color.FromArgb(115, 35, 35, 35) : Color.FromArgb(155, 0, 0, 0);
        using (var pen = new Pen(outline, 0.8f) { LineJoin = LineJoin.Round }) graphics.DrawPath(pen, path);
        using (var brush = new SolidBrush(textColor)) graphics.FillPath(brush, path);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    private static Font CreateFont(float size)
    {
        try { return new Font("Bahnschrift", size, FontStyle.Bold, GraphicsUnit.Pixel); }
        catch
        {
            try { return new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { return new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel); }
        }
    }
}
