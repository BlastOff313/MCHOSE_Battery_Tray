using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MchoseBatteryTray;

/// <summary>
/// Renders the battery state into a small notification-area icon.
/// The DrawString path intentionally mirrors the renderer from the last
/// working PowerShell release; GraphicsPath.AddString was used in v10.0.0
/// and could produce an empty/transparent tray glyph on some Windows setups.
/// </summary>
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
        Color color = lightTaskbar
            ? Color.FromArgb(65, 65, 65)
            : Color.FromArgb(238, 238, 238);
        return CreateTextIcon(unsupported ? "?" : "×", color, lightTaskbar);
    }

    private static Color GetBatteryColor(int percent)
    {
        Color mutedRed = Color.FromArgb(211, 101, 96);    // #D36560
        Color softWhite = Color.FromArgb(246, 246, 242);  // #F6F6F2
        Color mutedGreen = Color.FromArgb(105, 183, 129); // #69B781

        if (percent <= 50)
            return Blend(mutedRed, softWhite, percent / 50.0);

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
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var textBrush = new SolidBrush(textColor))
        using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags |= StringFormatFlags.NoWrap;

            // Bahnschrift's geometric numerals remain easiest to read at tray size.
            // This is the same DrawString-based rendering approach as the last
            // working release, rather than GraphicsPath.AddString.
            float fontSize = text.Length == 1 ? 30.5f : 28.5f;
            using Font font = CreateFont(fontSize);
            SizeF extent = graphics.MeasureString(text, font, 1000, format);

            float scale = 1.0f;
            if (extent.Width > 30.0f)
                scale = Math.Min(scale, 30.0f / extent.Width);
            if (extent.Height > 30.0f)
                scale = Math.Min(scale, 30.0f / extent.Height);

            RectangleF target = new(0.0f, 0.0f, 32.0f, 32.0f);
            if (scale < 0.999f)
            {
                using Font fittedFont = CreateFont(fontSize * scale);
                graphics.DrawString(text, fittedFont, textBrush, target, format);
            }
            else
            {
                graphics.DrawString(text, font, textBrush, target, format);
            }
        }

        // Fail visibly instead of silently registering a transparent tray icon.
        if (!ContainsVisiblePixels(bitmap))
            throw new InvalidOperationException(
                $"The tray icon renderer produced an empty bitmap for '{text}'. " +
                "Check GDI+ and the installed fonts, then restart MCHOSE Battery Tray.");

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static bool ContainsVisiblePixels(Bitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 16)
                    return true;
            }
        }
        return false;
    }

    private static Font CreateFont(float size)
    {
        try
        {
            return new Font("Bahnschrift", size, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        catch
        {
            try
            {
                return new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch
            {
                return new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
        }
    }
}
