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

    /// <summary>鼠标静止（或输入断流）2 秒后是否淡出消失。false = 永不淡出（常亮）。</summary>
    public bool IdleFade { get; set; } = true;

    public Point Position { get; private set; }
    public Point Target { get; private set; }

    /// <summary>当前输入淡出系数（1=正常，0=完全隐藏）。测试/诊断用。</summary>
    public double InputFade => _inputFade;

    // —— 输入断流优雅降级 ——
    // 管理员窗口（任务管理器等）位于前台时，UIPI 使非管理员进程的低层钩子收不到输入，
    // 光晕会冻在原地。断流超过阈值后把光晕淡出，恢复输入后立即淡入。
    private static readonly TimeSpan StallBeforeFade = TimeSpan.FromSeconds(2);
    private const double FadeSeconds = 0.5;

    private TimeSpan _timeSinceInput;
    private double _inputFade = 1;

    private Brush? _brush;
    private double _brushHue = double.NaN;
    private double _brushOpacity = double.NaN;

    public void OnMouseDown(Point position) => _timeSinceInput = TimeSpan.Zero;

    public void OnMouseMove(Point position)
    {
        Target = position;
        _timeSinceInput = TimeSpan.Zero;
    }

    public void Update(TimeSpan delta)
    {
        // 静止/输入断流 → 淡出（0.5s 线性），恢复输入 → 下一帧立即回到 1；IdleFade 关闭则常亮
        _timeSinceInput += delta;
        double stall = _timeSinceInput.TotalSeconds - StallBeforeFade.TotalSeconds;
        _inputFade = !IdleFade || stall <= 0 ? 1 : Math.Max(0, 1 - stall / FadeSeconds);

        // 帧率无关指数平滑：factor = 1 - e^(-k·dt)，任意帧率下跟随速度一致
        double factor = 1 - Math.Exp(-FollowSpeed * delta.TotalSeconds);
        Position = new Point(
            Position.X + (Target.X - Position.X) * factor,
            Position.Y + (Target.Y - Position.Y) * factor);
    }

    public void Draw(DrawingContext dc)
    {
        if (_inputFade <= 0) return; // 已淡出，不再画"僵尸光斑"
        var brush = GetBrush();
        if (_inputFade < 1)
        {
            dc.PushOpacity(_inputFade);
            dc.DrawEllipse(brush, null, Position, GlowRadius, GlowRadius);
            dc.Pop();
        }
        else
        {
            dc.DrawEllipse(brush, null, Position, GlowRadius, GlowRadius);
        }
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
