using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

public readonly record struct RippleState(Point Position, double Radius, double Opacity, double Progress);

public sealed class RippleEffect : IEffect
{
    public const double MaxRadius = 60;
    public const double MaxOpacity = 0.9;
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(600);
    private const int PoolLimit = 30;

    private readonly List<RippleState> _ripples = new();
    private readonly List<(Point Position, TimeSpan Elapsed)> _active = new();

    public string Name => "点击波纹";
    public bool Enabled { get; set; }

    public IReadOnlyList<RippleState> ActiveRipples => _ripples;

    public void OnMouseDown(Point position)
    {
        if (_active.Count >= PoolLimit) _active.RemoveAt(0);
        _active.Add((position, TimeSpan.Zero));
        if (_ripples.Count >= PoolLimit) _ripples.RemoveAt(0);
        _ripples.Add(new RippleState(position, 0, MaxOpacity, 0));
    }

    public void OnMouseMove(Point position) { }

    public void Update(TimeSpan delta)
    {
        _ripples.Clear();
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (position, elapsed) = _active[i];
            elapsed += delta;
            if (elapsed >= Duration)
            {
                _active.RemoveAt(i);
                continue;
            }
            _active[i] = (position, elapsed);
            double progress = elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
            double eased = 1 - Math.Pow(1 - progress, 2); // EaseOutQuad：先快后慢
            _ripples.Add(new RippleState(position, MaxRadius * eased, MaxOpacity * (1 - progress), progress));
        }
    }

    public void Draw(DrawingContext dc)
    {
        foreach (var ripple in _ripples)
        {
            byte alpha = (byte)(ripple.Opacity * 255);
            var color = Color.FromArgb(alpha, 120, 200, 255);
            var brush = new RadialGradientBrush(
                Color.FromArgb((byte)(alpha * 0.3), 120, 200, 255), color);
            dc.DrawEllipse(brush, new Pen(new SolidColorBrush(color), 2), ripple.Position, ripple.Radius, ripple.Radius);
        }
    }
}
