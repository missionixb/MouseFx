using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

public sealed class GlowEffect : IEffect
{
    public string Name => "常驻光晕";
    public bool Enabled { get; set; }

    /// <summary>静止光晕半径（px）。</summary>
    public double GlowRadius { get; set; } = 28;

    /// <summary>光晕中心不透明度（0-1）。</summary>
    public double Opacity { get; set; } = 0.35;

    /// <summary>跟随指数系数 k（/s），越大越跟手。</summary>
    public double FollowSpeed { get; set; } = 50;

    /// <summary>主题色色相（0-360）。</summary>
    public double Hue { get; set; } = 210;

    public Point Position { get; private set; }
    public Point Target { get; private set; }

    private Brush? _brush;
    private double _brushHue = double.NaN;
    private double _brushOpacity = double.NaN;

    public void OnMouseDown(Point position) { }

    public void OnMouseMove(Point position) => Target = position;

    public void Update(TimeSpan delta)
    {
        // 帧率无关指数平滑：factor = 1 - e^(-k·dt)，任意帧率下跟随速度一致
        double factor = 1 - Math.Exp(-FollowSpeed * delta.TotalSeconds);
        Position = new Point(
            Position.X + (Target.X - Position.X) * factor,
            Position.Y + (Target.Y - Position.Y) * factor);
    }

    public void Draw(DrawingContext dc)
    {
        dc.DrawEllipse(GetBrush(), null, Position, GlowRadius, GlowRadius);
    }

    private Brush GetBrush()
    {
        // 画刷缓存 + Freeze：只在颜色/透明度变化时重建，避免每帧创建 Freezable 对象
        if (_brush == null || _brushHue != Hue || _brushOpacity != Opacity)
        {
            _brush = new RadialGradientBrush(
                ColorUtils.FromHue(Hue, Opacity),
                ColorUtils.FromHue(Hue, 0.0));
            _brush.Freeze();
            _brushHue = Hue;
            _brushOpacity = Opacity;
        }
        return _brush;
    }
}
