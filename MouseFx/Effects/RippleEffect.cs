using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

/// <summary>点击波纹的状态：半径/透明度/进度由 Elapsed 推导，随 Update 原地刷新。</summary>
public readonly record struct RippleState(Point Position, double Radius, double Opacity, double Progress, TimeSpan Elapsed);

public sealed class RippleEffect : IEffect
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(600);
    private const int PoolLimit = 30;

    // 单列表原地推进：Elapsed 是真相源，半径/透明度/进度由它推导写入，Draw 与测试直接读
    private readonly List<RippleState> _ripples = new();

    public string Name => "点击波纹";
    public bool Enabled { get; set; }

    /// <summary>波纹最大扩散半径（px）。</summary>
    public double MaxRadius { get; set; } = 60;

    /// <summary>波纹初始不透明度（0-1）。</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>主题色色相（0-360）。</summary>
    public double Hue { get; set; } = 210;

    /// <summary>波纹扩散形状（圆圈/爱心/星星）。</summary>
    public RippleShape Shape { get; set; } = RippleShape.Circle;

    /// <summary>左键点击时是否显示扩散涟漪（设置项，默认开）。false = 点击对本特效零影响。</summary>
    public bool ClickEnabled { get; set; } = true;

    private Brush? _fill;
    private Pen? _pen;
    private double _fillHue = double.NaN;
    private double _penHue = double.NaN;

    public IReadOnlyList<RippleState> ActiveRipples => _ripples;

    /// <summary>是否有扩散中的波纹。</summary>
    public bool HasVisual => _ripples.Count > 0;

    public void OnMouseDown(Point position)
    {
        if (!ClickEnabled) return; // 开关关闭 = 点击不产生任何显示
        if (_ripples.Count >= PoolLimit) _ripples.RemoveAt(0);
        _ripples.Add(new RippleState(position, 0, Opacity, 0, TimeSpan.Zero));
    }

    public void OnMouseMove(Point position) { }

    public void Update(TimeSpan delta)
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var ripple = _ripples[i];
            var elapsed = ripple.Elapsed + delta;
            if (elapsed >= Duration)
            {
                _ripples.RemoveAt(i);
                continue;
            }
            double progress = elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
            double eased = 1 - Math.Pow(1 - progress, 2); // EaseOutQuad：先快后慢
            _ripples[i] = ripple with
            {
                Elapsed = elapsed,
                Radius = MaxRadius * eased,
                Opacity = Opacity * (1 - progress),
                Progress = progress,
            };
        }
    }

    public void Draw(DrawingContext dc)
    {
        var fill = GetFillBrush();
        var shape = RippleShapes.For(Shape); // 循环不变，取一次即可
        var pen = Shape == RippleShape.Circle ? GetPen() : null; // 仅圆圈走描边
        foreach (var ripple in _ripples)
        {
            // 透明度交给 PushOpacity 分层，画刷/画笔保持不透明，可整体缓存 + Freeze
            dc.PushOpacity(ripple.Opacity);
            if (Shape == RippleShape.Circle)
            {
                // 圆圈走 DrawEllipse 原路（main 版）：圆心+半径直传，渐变/描边观感与主分支完全一致
                dc.DrawEllipse(fill, pen, ripple.Position, ripple.Radius, ripple.Radius);
            }
            else
            {
                // 非圆形状走几何路径：单位几何按波纹半径缩放并平移到位置。
                // 仅填充不描边——描边笔宽会被缩放变换放大成 2×半径 px，把形状糊胖
                dc.PushTransform(CreateTransform(ripple));
                dc.DrawGeometry(fill, null, shape);
                dc.Pop();
            }
            dc.Pop();
        }
    }

    private static Transform CreateTransform(RippleState ripple)
    {
        var m = Matrix.Identity;
        m.Scale(ripple.Radius, ripple.Radius);
        m.Translate(ripple.Position.X, ripple.Position.Y);
        return new MatrixTransform(m);
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
