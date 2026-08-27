using System.Windows.Media;

namespace MouseFx.Settings;

public static class ColorUtils
{
    /// <summary>色相（0-360，饱和度=1，明度=1）→ 不透明 RGB 颜色。任意色相值自动归一化。</summary>
    public static Color FromHue(double hue)
    {
        hue = ((hue % 360) + 360) % 360;
        double c = 1.0;
        double x = 1.0 - Math.Abs((hue / 60.0) % 2 - 1.0);
        double m = 0.0;
        (double r, double g, double b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    /// <summary>色相 + 透明度（0-1）→ RGBA 颜色。</summary>
    public static Color FromHue(double hue, double alpha)
    {
        var c = FromHue(hue);
        return Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);
    }
}
