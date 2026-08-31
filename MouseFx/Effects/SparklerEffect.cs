using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

/// <summary>单颗星芒火星的状态（struct，池内复用）。</summary>
public struct SparklerParticle
{
    public double X;               // 位置（也是亮线的"火星头"）
    public double Y;
    public double VX;              // 速度（px/s）
    public double VY;
    public double Age;             // 已存活秒数
    public double Life;            // 生命总长
    public bool IsChild;           // 末端分叉出的子亮线（更短更细）
    public double RenderLength;    // 显示线长（px），父 15~60、子 5~15，参差错落
    public int ColorTier;          // 金琥珀色档（0~5，深橙→近白金）
    public int Layer;              // 球面纵深层：0=中心层（慢速、亮、粗），2=外层（快速、暗、细）
    public double FlickerPhase;    // 闪烁初相（rad）
    public double FlickerFreq;     // 闪烁角频率（rad/s）
}

/// <summary>
/// 烟花特效（星芒/蒲公英形态）：手持烟花燃烧的照片质感。以鼠标位置为中心向四周
/// 360° 高密度连续发射（每帧 3~6 颗，不做爆发—间歇），星芒轮廓始终饱满稳定；
/// 球形立体感：火星速度按幂律分布（大量低速火星聚集中心、少量高速射向外围），
/// 并按速度分三个纵深层——中心层更亮更粗、外层更暗更细，如同球形火花云投影到平面；
/// 每颗火星是一条沿速度方向的细长金色亮线（15~60px，长短错落），线身由头到尾由亮渐暗，
/// 线末端是白热亮点（火星头）；运动近乎直线放射（轻微阻力+极弱重力）；
/// 寿命终点（0.3~0.6s）在末端亮点处分叉出 2~3 根更短更细的子亮线（小叉冠状）后熄灭；
/// 中心是过曝的亮白光核（直径 10~20px，轻微闪烁脉动），全特效最亮。
/// 颜色固定（深橙 #FF8A2A ~ 近白金 #FFE6A8 六档，火星头 #FFF3D0），不读取用户颜色设置。
/// 粒子池上限 400（超限回收最早），画笔惰性缓存 + Freeze，每帧零分配。
/// </summary>
public sealed class SparklerEffect : IEffect
{
    /// <summary>重力加速度（px/s²）——极弱，轨迹近似直线放射。</summary>
    public const double Gravity = 40;

    /// <summary>空气阻力系数（/s），速度按 e^(-Drag·dt) 轻微减速。</summary>
    public const double Drag = 1.5;

    /// <summary>基准星芒直径（px），速度/线长按 Size/300 缩放。</summary>
    public const double BaseSize = 300;

    private const int EmitMinPerFrame = 3;   // 每帧发射 3~6 颗（高密度连续）
    private const int EmitMaxPerFrame = 6;
    private const int ColorTiers = 6;        // 金琥珀色档数（深橙→近白金）
    private const int AlphaBuckets = 8;      // 亮度分桶数
    private const int Layers = 3;            // 球面纵深层数
    private const int SegBright = 0;         // 亮线分段：头侧亮段
    private const int SegDim = 1;            // 亮线分段：尾侧暗段
    private static readonly double[] LayerAlpha = { 1.25, 1.0, 0.78 }; // 中心层更亮
    private static readonly double[] LayerWidth = { 1.8, 1.55, 1.25 }; // 中心层更粗

    private static readonly Color Amber = Color.FromRgb(0xFF, 0x8A, 0x2A); // 深橙（色档下限）
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xE6, 0xA8);  // 近白金（色档上限）
    private static readonly Color HotHead = Color.FromRgb(0xFF, 0xF3, 0xD0); // 白热火星头

    // 中心过曝光核画刷（纯白，冻结一次）
    private static readonly Brush CoreBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));
    private static readonly Brush GlowBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));

    public string Name => "烟花";
    public bool Enabled { get; set; }

    /// <summary>鼠标静止（或输入断流）2 秒后是否停止发射并淡出中心光核。false = 持续燃烧。</summary>
    public bool IdleFade { get; set; } = true;

    /// <summary>粒子上限，超出回收最早发射的（软渲染降级时可调低密度）。</summary>
    public int PoolLimit { get; set; } = 200;

    /// <summary>星芒直径（px），火星初速与线长按 Size/BaseSize 缩放。</summary>
    public double Size { get; set; } = 150;

    /// <summary>已发生的末端分叉次数（诊断/测试用）。</summary>
    public int ForkCount { get; private set; }

    /// <summary>累计发射的火星总数（诊断/测试用）。</summary>
    public int EmittedTotal { get; private set; }

    private readonly Random _random;
    private readonly List<SparklerParticle> _sparks = new();
    private readonly Pen?[,,,] _linePens = new Pen?[ColorTiers, 2, AlphaBuckets, Layers]; // 色档 × 段 × 亮度桶 × 纵深层
    private readonly Pen?[,,] _childPens = new Pen?[ColorTiers, AlphaBuckets, Layers];    // 子亮线（细）
    private readonly Pen?[] _headPens = new Pen?[AlphaBuckets];                            // 白热火星头

    private Point _pos;          // 鼠标当前位置（发射中心）
    private bool _hasMouse;
    private double _corePhase;   // 中心光核脉动相位

    // 输入断流：管理员窗口前台 / 鼠标静止（IdleFade 开启时）→ 停发，存量火星自然烧尽
    private static readonly TimeSpan StallBeforeStop = TimeSpan.FromSeconds(2);
    private const double CoreFadeSeconds = 0.5;
    private TimeSpan _timeSinceInput;
    private double _coreFade = 1;

    /// <summary>当前存活的火星（测试/诊断用）。</summary>
    public IReadOnlyList<SparklerParticle> ActiveParticles => _sparks;

    /// <summary>输入是否处于断流状态（测试/诊断用）。</summary>
    public bool InputStalled => _timeSinceInput > StallBeforeStop;

    /// <summary>中心光核淡出系数（1=正常，0=隐藏。测试/诊断用）。</summary>
    public double CoreFade => _coreFade;

    public SparklerEffect(Random? random = null) => _random = random ?? new Random();

    public void OnMouseDown(Point position) => _timeSinceInput = TimeSpan.Zero;

    public void OnMouseMove(Point position)
    {
        _pos = position;
        _hasMouse = true;
        _timeSinceInput = TimeSpan.Zero;
    }

    public void Update(TimeSpan delta)
    {
        if (!Enabled) return;
        double dt = Math.Clamp(delta.TotalSeconds, 0, 0.1);
        if (dt <= 0) return;
        _timeSinceInput += delta;
        _corePhase += dt;
        bool stalled = IdleFade && InputStalled;

        // 中心光核淡出（断流时 0.5s 线性淡出，恢复立即回 1）
        double coreStall = _timeSinceInput.TotalSeconds - StallBeforeStop.TotalSeconds;
        _coreFade = !IdleFade || coreStall <= 0 ? 1 : Math.Max(0, 1 - coreStall / CoreFadeSeconds);

        // 高密度连续发射：每帧 3~6 颗，与移动/静止无关，星芒轮廓始终饱满
        if (_hasMouse && !stalled)
        {
            int count = EmitMinPerFrame + _random.Next(EmitMaxPerFrame - EmitMinPerFrame + 1);
            for (int i = 0; i < count; i++)
                Emit();
        }

        // 物理 + 寿命终点分叉 + 回收（倒序遍历）
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                if (!s.IsChild) Fork(s, i); // 分叉发生在飞行末端（寿命终点）
                else _sparks.RemoveAt(i);
                continue;
            }
            Advance(ref s, dt);
            _sparks[i] = s;
        }
    }

    /// <summary>
    /// 推进一颗火星的物理（纯函数，可测）：空气阻力指数衰减 + 极弱重力 + 位移积分。
    /// </summary>
    public static void Advance(ref SparklerParticle s, double dt)
    {
        double decay = Math.Exp(-Drag * dt);
        s.VX *= decay;
        s.VY = s.VY * decay + Gravity * dt;
        s.X += s.VX * dt;
        s.Y += s.VY * dt;
    }

    public void Draw(DrawingContext dc)
    {
        if (!Enabled) return;

        // 中心过曝亮白光核（直径 10~20px 随脉动）+ 柔和辉光，全特效最亮
        if (_hasMouse && _coreFade > 0)
        {
            double pulse = 0.85 + 0.15 * Math.Sin(_corePhase * 7); // 轻微闪烁脉动
            double coreR = (5 + 5 * pulse) * _coreFade;
            var corePos = new Point(_pos.X, _pos.Y);
            if (_coreFade < 1) dc.PushOpacity(_coreFade);
            dc.DrawEllipse(GlowBrush, null, corePos, coreR * 2.2, coreR * 2.2);
            dc.DrawEllipse(CoreBrush, null, corePos, coreR, coreR);
            if (_coreFade < 1) dc.Pop();
        }

        if (_sparks.Count == 0) return;
        foreach (var s in _sparks)
        {
            double t = s.Age / s.Life;
            double flicker = 0.85 + 0.15 * Math.Sin(s.FlickerPhase + s.Age * s.FlickerFreq);
            // 球面纵深：中心层更亮，外层更暗
            double alpha = Math.Clamp((1 - t) * flicker * LayerAlpha[s.Layer], 0, 1);
            int bucket = Math.Min(AlphaBuckets - 1, (int)(alpha * AlphaBuckets));

            // 亮线方向：沿速度方向；速度≈0 时朝上
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            double ux, uy;
            if (speed < 1) { ux = 0; uy = -1; speed = 1; }
            else { ux = s.VX / speed; uy = s.VY / speed; }
            var head = new Point(s.X, s.Y);
            var tail = new Point(s.X - ux * s.RenderLength, s.Y - uy * s.RenderLength);

            if (s.IsChild)
            {
                // 子亮线：更短更细，单段
                dc.DrawLine(GetChildPen(s.ColorTier, bucket, s.Layer), head, tail);
                continue;
            }

            // 母亮线：分两段近似"由亮渐暗"——头侧亮段 + 尾侧暗段
            var mid = new Point(s.X - ux * s.RenderLength * 0.45, s.Y - uy * s.RenderLength * 0.45);
            int dimBucket = Math.Max(0, bucket * 45 / 100);
            dc.DrawLine(GetLinePen(s.ColorTier, SegBright, bucket, s.Layer), head, mid);
            dc.DrawLine(GetLinePen(s.ColorTier, SegDim, dimBucket, s.Layer), mid, tail);

            // 白热火星头（线末端更亮的亮点）
            int headBucket = Math.Min(AlphaBuckets - 1, (int)(Math.Min(1, alpha * 1.4) * AlphaBuckets));
            var headTip = new Point(s.X - ux * 1.5, s.Y - uy * 1.5);
            dc.DrawLine(GetHeadPen(headBucket), head, headTip);
        }
    }

    /// <summary>
    /// 发射一颗母火星：360° 均匀方向；速度幂律分布（大量低速聚集中心、少量高速射向外围，
    /// 形成球形投影的中心密度感），并按速度归入球面纵深层。
    /// </summary>
    private void Emit()
    {
        double k = Size / BaseSize;
        double angle = _random.NextDouble() * Math.PI * 2;
        double t = _random.NextDouble();
        double baseSpeed = 600 + 600 * Math.Pow(t, 1.8);  // 幂律偏置：中位速度明显低于均匀分布
        double lt = Math.Clamp((baseSpeed - 600) / 600, 0, 1);
        int layer = lt < 0.33 ? 0 : lt < 0.66 ? 1 : 2;    // 慢=中心层（亮粗），快=外层（暗细）
        EmittedTotal++;
        Add(new SparklerParticle
        {
            X = _pos.X + (_random.NextDouble() - 0.5) * 2,
            Y = _pos.Y + (_random.NextDouble() - 0.5) * 2,
            VX = Math.Cos(angle) * baseSpeed * k,
            VY = Math.Sin(angle) * baseSpeed * k,
            Life = 0.3 + _random.NextDouble() * 0.3,                    // 0.3~0.6s
            RenderLength = (15 + _random.NextDouble() * 45) * k,        // 15~60px × 缩放
            ColorTier = _random.Next(ColorTiers),
            Layer = layer,
            FlickerPhase = _random.NextDouble() * Math.PI * 2,
            FlickerFreq = 10 + _random.NextDouble() * 15,
        });
    }

    /// <summary>寿命终点末端分叉：在火星头位置叉出 2~3 根更短更细的子亮线（小叉冠状），母火星熄灭。</summary>
    private void Fork(SparklerParticle parent, int index)
    {
        ForkCount++;
        _sparks.RemoveAt(index);
        int n = 2 + _random.Next(2); // 2~3 根
        double parentAngle = Math.Atan2(parent.VY, parent.VX);
        double k = Size / BaseSize;
        for (int i = 0; i < n; i++)
        {
            double angle = parentAngle + (_random.NextDouble() - 0.5) * 2 * 1.22; // ±70° 小叉冠
            double speed = (300 + _random.NextDouble() * 300) * k;
            Add(new SparklerParticle
            {
                X = parent.X,
                Y = parent.Y,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed,
                Life = 0.1 + _random.NextDouble() * 0.05,                 // 100~150ms
                RenderLength = (5 + _random.NextDouble() * 10) * k,       // 5~15px × 缩放
                IsChild = true,
                ColorTier = parent.ColorTier,
                Layer = parent.Layer,
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                FlickerFreq = 15 + _random.NextDouble() * 15,
            });
        }
    }

    private void Add(SparklerParticle spark)
    {
        if (_sparks.Count >= PoolLimit) _sparks.RemoveAt(0); // 池满回收最早发射的
        _sparks.Add(spark);
    }

    private Pen GetLinePen(int tier, int segment, int bucket, int layer)
    {
        var pen = _linePens[tier, segment, bucket, layer];
        if (pen == null)
        {
            pen = CreatePen(LineColor(tier), bucket, 1.6 * LayerWidth[layer]);
            _linePens[tier, segment, bucket, layer] = pen;
        }
        return pen;
    }

    private Pen GetChildPen(int tier, int bucket, int layer)
    {
        var pen = _childPens[tier, bucket, layer];
        if (pen == null)
        {
            pen = CreatePen(LineColor(tier), bucket, 1.0 * LayerWidth[layer]); // 子亮线更细
            _childPens[tier, bucket, layer] = pen;
        }
        return pen;
    }

    private Pen GetHeadPen(int bucket)
    {
        var pen = _headPens[bucket];
        if (pen == null)
        {
            pen = CreatePen(HotHead, bucket, 2.2); // 白热火星头更粗更亮
            _headPens[bucket] = pen;
        }
        return pen;
    }

    private static Pen CreatePen(Color color, int bucket, double thickness)
    {
        double alpha = (bucket + 0.5) / AlphaBuckets;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(alpha * 255), color.R, color.G, color.B)), thickness);
        pen.Freeze();
        return pen;
    }

    /// <summary>
    /// 亮线颜色（纯函数，可测）：深橙→近白金区间离散为 6 档
    /// （#FF8A2A 深橙 ~ #FFE6A8 近白金）；火星头固定白热 #FFF3D0。
    /// </summary>
    public static Color LineColor(int tier)
    {
        double t = Math.Clamp(tier, 0, ColorTiers - 1) / (double)(ColorTiers - 1);
        return Color.FromRgb(
            (byte)Math.Round(Amber.R + (Gold.R - Amber.R) * t),
            (byte)Math.Round(Amber.G + (Gold.G - Amber.G) * t),
            (byte)Math.Round(Amber.B + (Gold.B - Amber.B) * t));
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
