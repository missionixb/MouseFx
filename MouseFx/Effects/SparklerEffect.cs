using System.Windows;
using System.Windows.Media;

namespace MouseFx.Effects;

/// <summary>单颗仙女棒火星的状态（struct，池内复用）。</summary>
public struct SparklerParticle
{
    public double X;               // 位置
    public double Y;
    public double VX;              // 速度（px/s）
    public double VY;
    public double Age;             // 已存活秒数
    public double Life;            // 生命总长
    public bool IsChild;           // 二次爆裂产生的子火星（更快、更细、更短命）
    public bool IsFlash;           // 爆裂瞬间的亮白闪光点（原地 2~3px）
    public bool HasBurst;          // 是否已完成二次爆裂（子火星/闪光点不再爆裂）
    public double BurstAt;         // 二次爆裂时间阈值（80~150ms）
    public double BurstDistance;   // 二次爆裂距离阈值（30~80px）
    public double DistanceTravelled; // 累计飞行路程
    public double FlickerPhase;    // 闪烁初相（rad）
    public double FlickerFreq;     // 闪烁角频率（rad/s）
}

/// <summary>
/// 仙女棒特效：手持仙女棒/镁条爆燃观感。无论鼠标移动还是静止，火星始终以鼠标位置为中心
/// 向四周 360° 均匀喷射（中心对称"烟花花朵"，无拖尾偏移，移动时整体跟随不变形）。
/// 爆发—间歇节奏（每次爆发 50~100ms 喷 8~15 颗，随后短暂间歇）；
/// 初速极快（700~1400 px/s）、强空气阻力、极弱重力（近乎直线飞行）；
/// 每颗火星飞行 30~80px 或 80~150ms 后二次爆裂成 2~4 颗更细的子火星并留下亮白闪光点；
/// 颜色固定（蓝白→纯白→黄白→橙红余烬），不读取用户颜色设置。
/// 粒子池上限 400（超限回收最早），画笔按（类型 × 色阶 × 亮度桶）惰性缓存 + Freeze，每帧零分配。
/// </summary>
public sealed class SparklerEffect : IEffect
{
    /// <summary>重力加速度（px/s²）——极弱，细火星几乎直线飞行。</summary>
    public const double Gravity = 80;

    /// <summary>空气阻力系数（/s），速度按 e^(-Drag·dt) 衰减。</summary>
    public const double Drag = 2.5;

    /// <summary>寿命最后 20% 渐隐为橙红余烬的起始进度。</summary>
    public const double EmberStart = 0.8;

    private const int StageCount = 5;       // 颜色分桶数
    private const int AlphaBuckets = 8;     // 亮度分桶数
    private const int ParentType = 0;       // 画笔类型：母火星
    private const int ChildType = 1;        // 子火星
    private const int FlashType = 2;        // 爆裂闪光点
    private static readonly double[] TypeThickness = { 1.6, 1.0, 2.6 }; // 1~2px 细火星；闪光点 2~3px

    private static readonly Color BlueWhite = Color.FromRgb(0xE8, 0xF1, 0xFF);
    private static readonly Color Ember = Color.FromRgb(0xFF, 0x6A, 0x3C);
    private static readonly Color EmberDark = Color.FromRgb(0x66, 0x14, 0x00);
    private static readonly Color WarmWhite = Color.FromRgb(0xFF, 0xF4, 0xD6);

    // 中心光核/辉光画刷（固定白色，冻结一次）
    private static readonly Brush CoreBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));
    private static readonly Brush GlowBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)));

    public string Name => "仙女棒";
    public bool Enabled { get; set; }

    /// <summary>鼠标静止（或输入断流）2 秒后是否停止爆发并淡出中心光核。false = 持续爆燃。</summary>
    public bool IdleFade { get; set; } = true;

    /// <summary>粒子上限，超出回收最早发射的（软渲染降级时可调低密度）。</summary>
    public int PoolLimit { get; set; } = 400;

    /// <summary>爆发—间歇的完整节奏次数（诊断/测试用）。</summary>
    public int BurstCycles { get; private set; }

    /// <summary>已发生的二次爆裂次数（诊断/测试用）。</summary>
    public int SecondaryBurstCount { get; private set; }

    /// <summary>累计发射的母火星总数（诊断/测试用）。</summary>
    public int EmittedTotal { get; private set; }

    private readonly Random _random;
    private readonly List<SparklerParticle> _sparks = new();
    private readonly Pen?[,,] _pens = new Pen?[3, StageCount, AlphaBuckets]; // 类型 × 色阶 × 亮度桶，调色板固定不重建

    private Point _pos;             // 鼠标当前位置（发射中心）
    private bool _hasMouse;
    private double _burstRemain;    // 本次爆发剩余时间（s），0 = 间歇中
    private int _burstEmitLeft;     // 本次爆发剩余发射颗数
    private double _restRemain;     // 间歇剩余时间（s）

    // 输入断流：管理员窗口前台 / 鼠标静止（IdleFade 开启时）→ 暂停爆发，存量火星自然烧尽
    private static readonly TimeSpan StallBeforeStop = TimeSpan.FromSeconds(2);
    private const double CoreFadeSeconds = 0.5;
    private TimeSpan _timeSinceInput;
    private double _coreFade = 1;   // 中心光核淡出系数

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
        bool stalled = IdleFade && InputStalled;

        // 中心光核淡出（断流时 0.5s 线性淡出，恢复立即回 1）
        double coreStall = _timeSinceInput.TotalSeconds - StallBeforeStop.TotalSeconds;
        _coreFade = !IdleFade || coreStall <= 0 ? 1 : Math.Max(0, 1 - coreStall / CoreFadeSeconds);

        // 爆发—间歇节奏状态机
        if (_hasMouse && !stalled)
        {
            if (_burstRemain > 0)
            {
                double portion = Math.Min(1, dt / Math.Max(_burstRemain, 1e-4));
                int emit = Math.Min(_burstEmitLeft, (int)Math.Ceiling(_burstEmitLeft * portion));
                for (int i = 0; i < emit; i++)
                    Emit();
                _burstEmitLeft -= emit;
                _burstRemain -= dt;
                if (_burstRemain <= 0 || _burstEmitLeft <= 0)
                {
                    _burstRemain = 0;
                    _restRemain = 0.06 + _random.NextDouble() * 0.12; // 短暂间歇 60~180ms
                }
            }
            else
            {
                _restRemain -= dt;
                if (_restRemain <= 0)
                {
                    _burstRemain = 0.05 + _random.NextDouble() * 0.05; // 爆发 50~100ms
                    _burstEmitLeft = 8 + _random.Next(8);              // 8~15 颗
                    BurstCycles++;
                }
            }
        }

        // 物理 + 二次爆裂 + 寿命回收（倒序遍历）
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                _sparks.RemoveAt(i);
                continue;
            }
            if (Advance(ref s, dt))
            {
                SecondaryBurstCount++;
                Burst(s, i);
                continue;
            }
            _sparks[i] = s;
        }
    }

    /// <summary>
    /// 推进一颗火星的物理（纯函数，可测）：空气阻力指数衰减 + 极弱重力 + 位移积分；
    /// 闪光点原地不动。返回该火星此帧是否应触发二次爆裂
    /// （飞行 30~80px 或 80~150ms，先到者触发）。
    /// </summary>
    public static bool Advance(ref SparklerParticle s, double dt)
    {
        if (s.IsFlash) return false;
        double decay = Math.Exp(-Drag * dt);       // 空气阻力
        s.VX *= decay;
        s.VY = s.VY * decay + Gravity * dt;        // 极弱重力
        s.X += s.VX * dt;
        s.Y += s.VY * dt;
        s.DistanceTravelled += Math.Sqrt(s.VX * s.VX + s.VY * s.VY) * dt;
        return !s.HasBurst && (s.DistanceTravelled >= s.BurstDistance || s.Age >= s.BurstAt);
    }

    public void Draw(DrawingContext dc)
    {
        if (!Enabled) return;

        // 中心亮白光核 + 柔和辉光，随爆发节奏脉动（爆发时更亮更大）
        if (_hasMouse && _coreFade > 0)
        {
            double pulse = _burstRemain > 0 ? 1 : 0.55;
            var corePos = new Point(_pos.X, _pos.Y);
            if (_coreFade < 1) dc.PushOpacity(_coreFade);
            dc.DrawEllipse(GlowBrush, null, corePos, 6 + 4 * pulse, 6 + 4 * pulse);
            dc.DrawEllipse(CoreBrush, null, corePos, 3 + 2 * pulse, 3 + 2 * pulse);
            if (_coreFade < 1) dc.Pop();
        }

        if (_sparks.Count == 0) return;
        foreach (var s in _sparks)
        {
            double t = s.Age / s.Life;
            int type = s.IsFlash ? FlashType : s.IsChild ? ChildType : ParentType;
            int stage = Math.Min(StageCount - 1, (int)(t * StageCount));
            double flicker = 0.875 + 0.125 * Math.Sin(s.FlickerPhase + s.Age * s.FlickerFreq);
            double alpha = Math.Clamp((1 - t) * flicker, 0, 1);
            int bucket = Math.Min(AlphaBuckets - 1, (int)(alpha * AlphaBuckets));
            var pen = GetPen(type, stage, bucket);

            // 沿速度方向的短亮线（速度≈0 或闪光点画 1px 点）
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            double ux, uy;
            if (speed < 1) { ux = 0; uy = -1; speed = 1; }
            else { ux = s.VX / speed; uy = s.VY / speed; }
            double len = s.IsFlash ? 1 : Math.Clamp(speed * 0.008, 1, 5);
            var head = new Point(s.X, s.Y);
            var tail = new Point(s.X - ux * len, s.Y - uy * len);
            dc.DrawLine(pen, head, tail);
        }
    }

    /// <summary>发射一颗母火星：360° 均匀方向，初速 700~1400 px/s，几乎不受鼠标运动影响。</summary>
    private void Emit()
    {
        double angle = _random.NextDouble() * Math.PI * 2;
        double speed = 700 + _random.NextDouble() * 700;
        EmittedTotal++;
        Add(new SparklerParticle
        {
            X = _pos.X + (_random.NextDouble() - 0.5) * 2,
            Y = _pos.Y + (_random.NextDouble() - 0.5) * 2,
            VX = Math.Cos(angle) * speed,
            VY = Math.Sin(angle) * speed,
            Life = 0.45,                                     // 兜底寿命（正常先二次爆裂）
            BurstAt = 0.08 + _random.NextDouble() * 0.07,    // 80~150ms
            BurstDistance = 30 + _random.NextDouble() * 50,  // 30~80px
            FlickerPhase = _random.NextDouble() * Math.PI * 2,
            FlickerFreq = 15 + _random.NextDouble() * 20,
        });
    }

    /// <summary>二次爆裂：移除母火星，原地炸出 2~4 颗更快更细的子火星 + 1 个亮白闪光点。</summary>
    private void Burst(SparklerParticle parent, int index)
    {
        _sparks.RemoveAt(index);
        int n = 2 + _random.Next(3); // 2~4 颗
        double parentSpeed = Math.Sqrt(parent.VX * parent.VX + parent.VY * parent.VY);
        for (int i = 0; i < n; i++)
        {
            double angle = _random.NextDouble() * Math.PI * 2;
            double speed = Math.Min(2000, parentSpeed * (1.15 + _random.NextDouble() * 0.3)); // 更快
            Add(new SparklerParticle
            {
                X = parent.X,
                Y = parent.Y,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed,
                Life = 0.1 + _random.NextDouble() * 0.1,  // 100~200ms
                IsChild = true,
                HasBurst = true,                          // 子火星不再爆裂
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                FlickerFreq = 20 + _random.NextDouble() * 20,
            });
        }
        Add(new SparklerParticle                           // 爆裂瞬间亮白闪光点
        {
            X = parent.X,
            Y = parent.Y,
            Life = 0.06 + _random.NextDouble() * 0.04,     // 60~100ms
            IsFlash = true,
            HasBurst = true,
        });
    }

    private void Add(SparklerParticle spark)
    {
        if (_sparks.Count >= PoolLimit) _sparks.RemoveAt(0); // 池满回收最早发射的
        _sparks.Add(spark);
    }

    private Pen GetPen(int type, int stage, int bucket)
    {
        var pen = _pens[type, stage, bucket];
        if (pen == null)
        {
            // 闪光点恒为亮白；母/子火星走固定色阶（蓝白→纯白→黄白→橙红余烬）
            var color = type == FlashType ? Colors.White : LifeColor((stage + 0.5) / StageCount);
            double alpha = (bucket + 0.5) / AlphaBuckets;
            pen = new Pen(new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(alpha * 255), color.R, color.G, color.B)), TypeThickness[type]);
            pen.Freeze();
            _pens[type, stage, bucket] = pen;
        }
        return pen;
    }

    /// <summary>
    /// 固定颜色生命周期（纯函数，可测）：蓝白 → 纯白 → 黄白；
    /// 寿命最后 20% 渐隐过渡到暗淡的橙红色余烬再消失。
    /// </summary>
    public static Color LifeColor(double t) => t switch
    {
        < 0.3 => Lerp(BlueWhite, Colors.White, t / 0.3),
        < 0.6 => Lerp(Colors.White, WarmWhite, (t - 0.3) / 0.3),
        < EmberStart => WarmWhite,
        < 0.9 => Lerp(WarmWhite, Ember, (t - EmberStart) / 0.1),
        _ => Lerp(Ember, EmberDark, (t - 0.9) / 0.1),
    };

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
