using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

public sealed class GlowEffect : IEffect
{
    public const double GlowRadius = 28;
    public const double Smoothing = 0.3;

    public string Name => "常驻光晕";
    public bool Enabled { get; set; }

    public Point Position { get; private set; }
    public Point Target { get; private set; }

    public void OnMouseDown(Point position) { }

    public void OnMouseMove(Point position) => Target = position;

    public void Update(TimeSpan delta)
    {
        Position = new Point(
            Position.X + (Target.X - Position.X) * Smoothing,
            Position.Y + (Target.Y - Position.Y) * Smoothing);
    }

    public void Draw(DrawingContext dc)
    {
        var brush = new RadialGradientBrush(
            Color.FromArgb(90, 120, 200, 255),
            Color.FromArgb(0, 120, 200, 255));
        dc.DrawEllipse(brush, null, Position, GlowRadius, GlowRadius);
    }
}
