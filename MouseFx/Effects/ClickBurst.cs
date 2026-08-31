using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

/// <summary>
/// 左键点击爆炸的共享运动参数（纯函数与常量，可测）。
/// 两段式运动曲线（ease-out 的粒子语言翻译）：爆发段初速极快，指数阻力在 ~200ms 内
/// 快速衰减（"突然往外炸开"的冲劲），之后重力接管、粒子低速飘落闪烁渐隐。
/// 阻力用帧率无关的指数衰减 v *= e^(-k·dt)：同一物理时长拆成任意帧数结果一致。
/// 视觉层级三级阶梯：中心闪光（唯一过曝元素）＞ 爆发粒子（常态 1.5~2 倍）＞ 落尾余烬。
/// </summary>
public static class ClickBurst
{
    /// <summary>爆炸粒子初速下限（px/s），约为常态发射速度的 2~3 倍。</summary>
    public const double MinSpeed = 900;

    /// <summary>爆炸粒子初速上限（px/s）。</summary>
    public const double MaxSpeed = 1600;

    /// <summary>爆炸粒子寿命下限（秒），明显长于常态（0.4~0.9s），留足缓慢飘落时间。</summary>
    public const double MinLife = 0.8;

    /// <summary>爆炸粒子寿命上限（秒）。</summary>
    public const double MaxLife = 1.5;

    /// <summary>阻力系数（/s）：200ms 后速度只剩 e^(-11×0.2) ≈ 11%。</summary>
    public const double DragK = 11;

    /// <summary>中心闪光时长：爆燃瞬间过曝的白亮光斑，之后无痕消失。</summary>
    public static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>稳态飘落速度 = 重力 / 阻力系数（px/s）：阻力下终端速度，形成缓慢余烬下坠。</summary>
    public static double TerminalFallSpeed(double gravity) => gravity / DragK;

    /// <summary>帧率无关的阻力衰减系数（0~1）。</summary>
    public static double DragFactor(double dtSeconds) => Math.Exp(-DragK * dtSeconds);

    /// <summary>爆发粒子相对常态的尺寸/亮度放大系数（1.5~2 倍，出生时随机）。</summary>
    public static double RandomBoost(Random random) => 1.5 + random.NextDouble() * 0.5;

    /// <summary>一帧内爆发的粒子数（40~80 × 密度系数，软件渲染时系数 <1 降密度）。</summary>
    public static int RandomCount(Random random, double scale)
        => Math.Max(1, (int)Math.Round((40 + random.NextDouble() * 40) * scale));

    // ---- 中心闪光画刷（纯白径向渐变，冻结一次全局复用） ----
    public static readonly Brush FlashCoreBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));
    public static readonly Brush FlashGlowBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));

    /// <summary>绘制中心闪光：随剩余时间淡出、半径快速扩大（爆燃感），调用方控制总时长。</summary>
    public static void DrawFlash(DrawingContext dc, Point center, double remainingRatio)
    {
        double r = 10 + 16 * (1 - remainingRatio); // 快速扩大的过曝白斑
        dc.PushOpacity(remainingRatio);
        dc.DrawEllipse(FlashGlowBrush, null, center, r * 2.4, r * 2.4);
        dc.DrawEllipse(FlashCoreBrush, null, center, r, r);
        dc.Pop();
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
