using System.Windows;
using System.Windows.Media;
using MouseFx.Settings;

namespace MouseFx.Effects;

/// <summary>单颗火星的状态（struct，存放于池列表内复用，不产生逐粒子堆分配）。</summary>
public struct Spark
{
    public double X;             // 位置
    public double Y;
    public double VX;            // 速度（px/s）
    public double VY;
    public double Age;           // 已存活秒数
    public double Life;          // 生命总长（0.4~0.9s，子火星更短）
    public bool CanBurst;        // 寿命终点是否炸裂成子火星（约 10% 的火星）
    public double FlickerPhase;  // 闪烁初相（rad）
    public double FlickerFreq;   // 闪烁角频率（rad/s）
    public double StartSize;     // 初始线宽（决定大小档位，子火星更小）
}

/// <summary>
/// 火花特效（仙女棒）：鼠标持续迸射细小、明亮的火星短线段，带重力抛物线、
/// 烧尽颜色渐变与随机闪烁，少数火星寿命终点炸裂成更小的子火星。
/// 粒子用 List&lt;Spark&gt; 池管理（struct 无逐粒子堆分配），画笔按
/// （大小档 × 烧尽色阶 × 亮度桶）惰性缓存 + Freeze，每帧绘制零分配。
/// </summary>
public sealed class SparkEffect : IEffect
{
    /// <summary>重力加速度（px/s²），火星轨迹呈抛物线下坠。</summary>
    public const double Gravity = 500;

    private const int StageCount = 6;      // 烧尽色阶数（白热 → 主色 → 烧尽暗核）
    private const int AlphaBuckets = 8;    // 亮度分桶数（避免逐粒子建画笔）
    private const double BurstFraction = 0.10;   // 炸裂比例
    private const double MovingSpeedThreshold = 60;  // 鼠标判定为"移动"的速度阈值（px/s）
    private static readonly double[] StageThickness = { 2.4, 2.0, 1.7, 1.4, 1.1, 0.8 };
    private static readonly double[] TierScale = { 0.75, 1.3 };  // 大小档位线宽系数

    public string Name => "火花";
    public bool Enabled { get; set; }

    /// <summary>火星主色色相（0-360），复用主题色设置。</summary>
    public double Hue { get; set; } = 210;

    /// <summary>粒子上限，超出回收最早发射的（软渲染降级时可调低粒子密度）。</summary>
    public int PoolLimit { get; set; } = 250;

    /// <summary>鼠标静止（或输入断流）2 秒后是否停止发射。false = 静止时持续冒火星。</summary>
    public bool IdleFade { get; set; } = true;

    /// <summary>已发生的炸裂次数（诊断/测试用）。</summary>
    public int BurstCount { get; private set; }

    private readonly Random _random;
    private readonly List<Spark> _sparks = new();

    // 画笔缓存：核心 [大小档, 色阶, 亮度桶]，光晕 [大小档, 色阶]，换色相时整体清空重建
    private readonly Pen?[,,] _corePens = new Pen?[2, StageCount, AlphaBuckets];
    private readonly Pen?[,] _haloPens = new Pen?[2, StageCount];
    private double _pensHue = double.NaN;

    private Point _emitPosition;   // 最近一次鼠标位置（发射源）
    private Point _prevPosition;   // 上一帧鼠标位置（算鼠标速度）
    private bool _hasMouse;

    // 输入断流：管理员窗口前台时 UIPI 使钩子收不到事件，停止发射，
    // 存量火星自然烧尽（≤0.9s），避免对着冻住的位置持续喷火花
    private static readonly TimeSpan StallBeforeStop = TimeSpan.FromSeconds(2);
    private TimeSpan _timeSinceInput;

    /// <summary>输入是否处于断流状态（测试/诊断用）。</summary>
    public bool InputStalled => _timeSinceInput > StallBeforeStop;

    public SparkEffect(Random? random = null) => _random = random ?? new Random();

    /// <summary>当前存活的火星（测试/诊断用）。</summary>
    public IReadOnlyList<Spark> ActiveSparks => _sparks;

    public void OnMouseDown(Point position) => _timeSinceInput = TimeSpan.Zero;

    public void OnMouseMove(Point position)
    {
        if (!_hasMouse)
        {
            _prevPosition = position;
            _hasMouse = true;
        }
        _emitPosition = position;
        _timeSinceInput = TimeSpan.Zero;
    }

    public void Update(TimeSpan delta)
    {
        if (!Enabled) return;
        double dt = Math.Clamp(delta.TotalSeconds, 0, 0.1);
        if (dt <= 0) return;
        _timeSinceInput += delta;

        // 鼠标速度（px/s）＝本帧位移 / 帧间隔；无移动事件时自然衰减为 0（静止发射）
        double mouseVX = (_emitPosition.X - _prevPosition.X) / dt;
        double mouseVY = (_emitPosition.Y - _prevPosition.Y) / dt;
        _prevPosition = _emitPosition;
        bool moving = Math.Sqrt(mouseVX * mouseVX + mouseVY * mouseVY) > MovingSpeedThreshold;

        // 每帧发射 1~3 颗，静止时频率略降低（1~2 颗）；断流时停发（IdleFade 开启时），存量自然烧尽
        if (_hasMouse && !(IdleFade && InputStalled))
        {
            int count = moving ? _random.Next(1, 4) : _random.Next(1, 3);
            for (int i = 0; i < count; i++)
                Emit(mouseVX, mouseVY, moving);
        }

        // 物理推进 + 寿命回收（倒序遍历，边走边炸裂/移除）
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                if (s.CanBurst) Burst(s);
                _sparks.RemoveAt(i);
                continue;
            }
            s.VY += Gravity * dt;          // 半隐式欧拉：先更新速度再积分位置，稳定
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            _sparks[i] = s;
        }
    }

    /// <summary>绘制：火星是沿速度方向拉长的短亮线段（拖尾），年轻火星外加淡色光晕。</summary>
    public void Draw(DrawingContext dc)
    {
        if (!Enabled || _sparks.Count == 0) return;
        if (_pensHue != Hue)
        {
            Array.Clear(_corePens);
            Array.Clear(_haloPens);
            _pensHue = Hue;
        }

        foreach (var s in _sparks)
        {
            double t = s.Age / s.Life;                    // 0→1 烧尽进度
            int stage = Math.Min(StageCount - 1, (int)(t * StageCount));
            // 随机闪烁：透明度 0.65~1.0 抖动
            double flicker = 0.825 + 0.175 * Math.Sin(s.FlickerPhase + s.Age * s.FlickerFreq);
            double alpha = Math.Clamp((1 - t) * flicker, 0, 1);
            int bucket = Math.Min(AlphaBuckets - 1, (int)(alpha * AlphaBuckets));
            int tier = s.StartSize > 2.2 ? 1 : 0;

            // 拖尾方向：沿速度方向；速度≈0 时朝上（静止上溅的火星仍有可见线段）
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            double ux, uy;
            if (speed < 1) { ux = 0; uy = -1; speed = 1; }
            else { ux = s.VX / speed; uy = s.VY / speed; }
            double len = Math.Clamp(speed * 0.035, 1.8, 9);
            var head = new Point(s.X, s.Y);
            var tail = new Point(s.X - ux * len, s.Y - uy * len);

            dc.DrawLine(GetCorePen(tier, stage, bucket), head, tail);
            if (t < 0.3) dc.DrawLine(GetHaloPen(tier, stage), head, tail); // 年轻火星的外围淡光晕
        }
    }

    /// <summary>发射一颗火星：四周飞溅 + 向上溅偏置；鼠标移动时叠加运动反方向（拖尾）。</summary>
    private void Emit(double mouseVX, double mouseVY, bool moving)
    {
        double angle = _random.NextDouble() * Math.PI * 2;
        double radial = 20 + _random.NextDouble() * 70;                     // 四周飞溅 20~90 px/s
        double vx = Math.Cos(angle) * radial;
        double vy = Math.Sin(angle) * radial - (20 + _random.NextDouble() * 70); // 向上溅偏置
        if (moving)
        {
            double k = 0.4 + _random.NextDouble() * 0.4;                    // 反方向分量 0.4~0.8 倍鼠标速度
            vx -= mouseVX * k;
            vy -= mouseVY * k;
        }
        double speed = Math.Sqrt(vx * vx + vy * vy);
        if (speed > 350) { vx *= 350 / speed; vy *= 350 / speed; }          // 限幅，防甩鼠标时飞出

        Add(new Spark
        {
            X = _emitPosition.X + (_random.NextDouble() - 0.5) * 4,
            Y = _emitPosition.Y + (_random.NextDouble() - 0.5) * 4,
            VX = vx,
            VY = vy,
            Life = 0.4 + _random.NextDouble() * 0.5,                        // 0.4~0.9s
            CanBurst = _random.NextDouble() < BurstFraction,
            FlickerPhase = _random.NextDouble() * Math.PI * 2,
            FlickerFreq = 15 + _random.NextDouble() * 20,
            StartSize = 1.6 + _random.NextDouble() * 1.2,                   // 1.6~2.8 px
        });
    }

    /// <summary>寿命终点炸裂：原地炸出 2~3 颗更小、更快、更短命的子火星。</summary>
    private void Burst(Spark parent)
    {
        BurstCount++;
        int n = 2 + _random.Next(2);                                        // 2~3 颗
        for (int i = 0; i < n; i++)
        {
            double angle = _random.NextDouble() * Math.PI * 2;
            double speed = 60 + _random.NextDouble() * 100;
            Add(new Spark
            {
                X = parent.X,
                Y = parent.Y,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed - 40,
                Life = 0.2 + _random.NextDouble() * 0.25,                   // 0.2~0.45s
                FlickerPhase = _random.NextDouble() * Math.PI * 2,
                FlickerFreq = 20 + _random.NextDouble() * 20,
                StartSize = parent.StartSize * 0.6,
            });
        }
    }

    private void Add(Spark spark)
    {
        if (_sparks.Count >= PoolLimit) _sparks.RemoveAt(0);                // 池满回收最早发射的
        _sparks.Add(spark);
    }

    private Pen GetCorePen(int tier, int stage, int bucket)
    {
        var pen = _corePens[tier, stage, bucket];
        if (pen == null)
        {
            var color = StageColor(stage);
            double alpha = (bucket + 0.5) / AlphaBuckets;
            pen = new Pen(new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(alpha * 255), color.R, color.G, color.B)),
                StageThickness[stage] * TierScale[tier]);
            pen.Freeze();
            _corePens[tier, stage, bucket] = pen;
        }
        return pen;
    }

    private Pen GetHaloPen(int tier, int stage)
    {
        var pen = _haloPens[tier, stage];
        if (pen == null)
        {
            var color = ColorUtils.FromHue(Hue);
            pen = new Pen(new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                StageThickness[stage] * TierScale[tier] * 3.2);
            pen.Freeze();
            _haloPens[tier, stage] = pen;
        }
        return pen;
    }

    /// <summary>烧尽色阶：白热核心 → 主题主色 → 逐渐变暗的余烬，色相始终跟随用户设置。</summary>
    private Color StageColor(int stage)
    {
        var baseColor = ColorUtils.FromHue(Hue);
        return stage switch
        {
            0 => Lerp(baseColor, Colors.White, 0.80),
            1 => Lerp(baseColor, Colors.White, 0.35),
            2 => baseColor,
            3 => Lerp(baseColor, Colors.Black, 0.25),
            4 => Lerp(baseColor, Colors.Black, 0.45),
            _ => Lerp(baseColor, Colors.Black, 0.65),
        };
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
