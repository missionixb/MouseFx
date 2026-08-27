using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

public readonly record struct RippleState(Point Position, double Radius, double Opacity, double Progress);

public sealed class RippleEffect : IEffect
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(600);
    private const int PoolLimit = 30;

    private readonly List<RippleState> _ripples = new();
    private readonly List<(Point Position, TimeSpan Elapsed)> _active = new();

    public string Name => "点击波纹";
    public bool Enabled { get; set; }

    /// <summary>波纹最大扩散半径（px）。</summary>
    public double MaxRadius { get; set; } = 60;

    /// <summary>波纹初始不透明度（0-1）。</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>主题色色相（0-360）。</summary>
    public double Hue { get; set; } = 210;

    private Brush? _fill;
    private Pen? _pen;
    private double _fillHue = double.NaN;
    private double _penHue = double.NaN;

    public IReadOnlyList<RippleState> ActiveRipples => _ripples;

    public void OnMouseDown(Point position)
    {
        if (_active.Count >= PoolLimit) _active.RemoveAt(0);
        _active.Add((position, TimeSpan.Zero));
        if (_ripples.Count >= PoolLimit) _ripples.RemoveAt(0);
        _ripples.Add(new RippleState(position, 0, Opacity, 0));
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
            _ripples.Add(new RippleState(position, MaxRadius * eased, Opacity * (1 - progress), progress));
        }
    }

    public void Draw(DrawingContext dc)
    {
        var fill = GetFillBrush();
        var pen = GetPen();
        foreach (var ripple in _ripples)
        {
            // 透明度交给 PushOpacity 分层，画刷/画笔保持不透明，可整体缓存 + Freeze
            dc.PushOpacity(ripple.Opacity);
            dc.DrawEllipse(fill, pen, ripple.Position, ripple.Radius, ripple.Radius);
            dc.Pop();
        }
    }

    private Brush GetFillBrush()
    {
        // 画刷缓存 + Freeze：只在色相变化时重建
        if (_fill == null || _fillHue != Hue)
        {
            _fill = new RadialGradientBrush(
                ColorUtils.FromHue(Hue, 0.3),
                ColorUtils.FromHue(Hue, 1.0));
            _fill.Freeze();
            _fillHue = Hue;
        }
        return _fill;
    }

    private Pen GetPen()
    {
        // 画笔与画刷分别用独立色相标记，避免色相变化时其中一个未重建
        if (_pen == null || _penHue != Hue)
        {
            _pen = new Pen(new SolidColorBrush(ColorUtils.FromHue(Hue)), 2);
            _pen.Freeze();
            _penHue = Hue;
        }
        return _pen;
    }
}
