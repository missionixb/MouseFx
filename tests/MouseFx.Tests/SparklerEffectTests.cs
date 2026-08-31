using System.Windows;
using System.Windows.Media;
using MouseFx.Effects;
using Xunit;

namespace MouseFx.Tests;

public class SparklerEffectTests
{
    private static TimeSpan Frame(double ms = 16) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void 未收到鼠标位置前不发射()
    {
        var effect = new SparklerEffect(new Random(1)) { Enabled = true };

        effect.Update(Frame());

        Assert.Empty(effect.ActiveParticles);
    }

    [Fact]
    public void 禁用时不发射不更新()
    {
        var effect = new SparklerEffect(new Random(1)) { Enabled = false };
        effect.OnMouseMove(new Point(0, 0));

        effect.Update(Frame());

        Assert.Empty(effect.ActiveParticles);
    }

    [Fact]
    public void 与移动无关_静止时每帧高密度发射3到6颗()
    {
        var effect = new SparklerEffect(new Random(2026)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 30; i++) // 静止 ~0.5s，持续发射
        {
            int before = effect.EmittedTotal;
            effect.Update(Frame());
            Assert.InRange(effect.EmittedTotal - before, 3, 6);
        }
    }

    [Fact]
    public void 母火星参数在规格区间内且长短错落()
    {
        var effect = new SparklerEffect(new Random(99)) { Enabled = true, Size = 300 }; // 基准直径
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());

        var parents = effect.ActiveParticles.Where(p => !p.IsChild).ToList();
        Assert.NotEmpty(parents);
        Assert.All(parents, s =>
        {
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            Assert.InRange(speed, 600, 1200 + 1e-9);   // 初速 600~1200
            Assert.InRange(s.Life, 0.3, 0.6 + 1e-9);   // 寿命 0.3~0.6s
            Assert.InRange(s.RenderLength, 15, 60 + 1e-9); // 线长 15~60px
            Assert.InRange(s.Layer, 0, 2);             // 球面纵深层
        });
        // 长短错落：一帧内线长极差应明显（> 10px），避免"表盘"感
        Assert.True(parents.Max(p => p.RenderLength) - parents.Min(p => p.RenderLength) > 10,
            "同一帧内线长应有明显差异");
    }

    [Fact]
    public void 速度幂律分布使中心火星密度更高()
    {
        var effect = new SparklerEffect(new Random(42)) { Enabled = true, Size = 300 }; // 基准直径
        effect.OnMouseMove(new Point(0, 0));

        var speeds = new List<double>();
        for (int i = 0; i < 50; i++) // 采集 ~200 颗
        {
            effect.Update(Frame());
            speeds.AddRange(effect.ActiveParticles.Where(p => !p.IsChild)
                .Select(p => Math.Sqrt(p.VX * p.VX + p.VY * p.VY)));
        }

        // 幂律偏置（pow 1.8）下应约 68% 的火星低于中值速度 900（均匀分布是 50%）
        double slowFraction = (double)speeds.Count(v => v < 900) / speeds.Count;
        Assert.True(slowFraction > 0.6, $"低速火星占比应 > 60%（中心密度更高），实际 {slowFraction:P0}");
    }

    [Fact]
    public void 星芒直径按比例缩放速度与线长()
    {
        var effect = new SparklerEffect(new Random(7)) { Enabled = true, Size = 150 }; // 默认直径 = 半倍基准
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());

        Assert.All(effect.ActiveParticles.Where(p => !p.IsChild), s =>
        {
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            Assert.InRange(speed, 300, 600 + 1e-9);       // 600~1200 × 0.5
            Assert.InRange(s.RenderLength, 7.5, 30 + 1e-9); // 15~60 × 0.5
        });
    }

    [Fact]
    public void 寿命终点末端分叉出2到3根子亮线()
    {
        var effect = new SparklerEffect(new Random(77)) { Enabled = true, Size = 300 }; // 基准直径
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 60 && effect.ForkCount == 0; i++) // 等首次分叉（寿命 ≤0.6s）
            effect.Update(Frame());

        Assert.True(effect.ForkCount > 0);
        Assert.Contains(effect.ActiveParticles, s => s.IsChild);          // 子亮线存在
        Assert.All(effect.ActiveParticles.Where(s => s.IsChild), s =>
        {
            Assert.InRange(s.RenderLength, 5, 15 + 1e-9);  // 更短：5~15px
            Assert.InRange(s.Life, 0.1, 0.15 + 1e-9);      // 随母火星熄灭而短存
        });
    }

    [Fact]
    public void 分叉只发生在寿命终点_中途不分叉()
    {
        var effect = new SparklerEffect(new Random(77)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 120; i++) // ~2s
        {
            effect.Update(Frame());
            // 存活母火星的年龄必须小于寿命（分叉只在 Age ≥ Life 时发生）
            Assert.All(effect.ActiveParticles.Where(p => !p.IsChild),
                s => Assert.True(s.Age < s.Life + 1e-9));
            // 子亮线寿命独立且更短
            Assert.All(effect.ActiveParticles, s => Assert.InRange(s.Life, 0.1, 0.6 + 1e-9));
        }
    }

    [Fact]
    public void 空气阻力轻微减速与极弱重力叠加()
    {
        var s = new SparklerParticle { VX = 1000, VY = -500 };

        double dt = 0.05;
        SparklerEffect.Advance(ref s, dt);

        double decay = Math.Exp(-SparklerEffect.Drag * dt);
        Assert.Equal(1000 * decay, s.VX, 6);
        Assert.Equal(-500 * decay + SparklerEffect.Gravity * dt, s.VY, 6);
        Assert.Equal(s.X, 1000 * decay * dt, 6);
    }

    [Fact]
    public void 亮线颜色六档覆盖深橙到近白金_火星头白热()
    {
        Assert.Equal(SparklerEffect.LineColor(0), Color.FromRgb(0xFF, 0x8A, 0x2A)); // 深橙下限
        Assert.Equal(SparklerEffect.LineColor(5), Color.FromRgb(0xFF, 0xE6, 0xA8)); // 近白金上限

        // 中间档单调递增（B 通道随档位升高），形成层次
        for (int tier = 1; tier < 5; tier++)
            Assert.True(SparklerEffect.LineColor(tier).B > SparklerEffect.LineColor(tier - 1).B,
                $"档位 {tier} 的 B 通道应高于前一档");

        Assert.Equal(40, SparklerEffect.Gravity);  // 极弱重力（近似直线）
        Assert.True(SparklerEffect.Drag < 2.5);    // 轻微阻力
    }

    [Fact]
    public void 池满时不再增长()
    {
        var effect = new SparklerEffect(new Random(99)) { Enabled = true, BurstReserve = 0 };
        effect.PoolLimit = 10;
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 30; i++)
            effect.Update(Frame());

        Assert.True(effect.ActiveParticles.Count <= 10);
    }

    [Fact]
    public void 静止淡出开启时断流停发且池清空()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true, IdleFade = true };
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 3; i++)
            effect.Update(Frame());
        Assert.NotEmpty(effect.ActiveParticles);

        for (int i = 0; i < 130; i++)
            effect.Update(Frame()); // ~2.08s，刚超阈值
        Assert.True(effect.InputStalled);

        for (int i = 0; i < 80; i++)
            effect.Update(Frame());

        Assert.Empty(effect.ActiveParticles); // 停发后存量自然烧尽
        Assert.Equal(0, effect.CoreFade);     // 中心光核也已淡出
    }

    [Fact]
    public void 静止淡出关闭时断流仍持续燃烧()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true, IdleFade = false };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 210; i++) // 断流 ~3.4s
            effect.Update(Frame());

        Assert.True(effect.InputStalled);
        Assert.NotEmpty(effect.ActiveParticles); // 仍在发射
        Assert.Equal(1, effect.CoreFade);        // 光核常亮
    }

    [Fact]
    public void 左键点击爆发星芒且峰值范围明显大于常态()
    {
        var effect = new SparklerEffect(new Random(7)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.OnMouseDown(new Point(500, 400)); // 爆发在按下瞬间直接入池（先于 Update，未经阻力衰减）

        var burst = effect.ActiveParticles.Where(s => s.IsBurst).ToList();
        Assert.InRange(burst.Count, 40, 80);
        Assert.All(burst, s =>
        {
            var speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            Assert.InRange(speed, SparklerEffect.BurstSpeedMin, SparklerEffect.BurstSpeedMax + 1e-9); // 绝对初速，不随 Size 缩小
            Assert.InRange(s.Life, ClickBurst.MinLife, ClickBurst.MaxLife); // 比常态(0.3~0.6)更长
            Assert.InRange(s.ColorTier, 3, 5);                              // 偏亮金琥珀档，颜色仍固定
            Assert.InRange(s.RenderLength, 25, 70);                         // 比常态更长的针状亮线
        });

        // 强阻力下射程 ≈ v0/DragK：峰值半径约 110~145px，直径 ≈ 常态星芒（100px）的 2.5~3 倍
        double peakRadius = SparklerEffect.BurstSpeedMax / ClickBurst.DragK;
        Assert.True(peakRadius * 2 > effect.Size * 2.4, "爆炸峰值直径必须明显超出常态星芒轮廓");
    }

    [Fact]
    public void 点击瞬间原有粒子被施加向外径向冲量()
    {
        var effect = new SparklerEffect(new Random(9)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame()); // 先有常态火星（位于 0,0 附近，即点击点左侧）
        var before = effect.ActiveParticles[0];

        effect.OnMouseDown(new Point(200, 0));
        var after = effect.ActiveParticles[0];

        Assert.True(after.VX < before.VX); // 被推离点击点（向 -X）
    }

    [Fact]
    public void 爆发窗口内常态发射暂停_窗口结束无痕恢复()
    {
        var effect = new SparklerEffect(new Random(13)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.OnMouseDown(new Point(0, 0));
        Assert.InRange(effect.BurstWindowRemaining, 0.3, 0.36);
        Assert.Equal(0, effect.ActiveParticles.Count(s => !s.IsBurst)); // 点击前无常态粒子

        for (int i = 0; i < 15; i++) // 窗口内 ~0.24s：无新增常态粒子
            effect.Update(Frame());
        Assert.Equal(0, effect.ActiveParticles.Count(s => !s.IsBurst));

        effect.Update(TimeSpan.FromSeconds(0.4)); // 窗口结束
        Assert.Equal(0, effect.BurstWindowRemaining);
        Assert.True(effect.ActiveParticles.Count(s => !s.IsBurst) > 0); // 常态发射恢复
    }

    [Fact]
    public void 鼠标移动不触发星芒爆发()
    {
        var effect = new SparklerEffect(new Random(2026)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());
        effect.OnMouseMove(new Point(300, 0));
        effect.Update(Frame());

        Assert.DoesNotContain(effect.ActiveParticles, s => s.IsBurst);
    }

    [Fact]
    public void 关闭点击爆裂后点击无爆发无闪光()
    {
        var effect = new SparklerEffect(new Random(3)) { Enabled = true, ClickBurstEnabled = false };
        effect.OnMouseDown(new Point(10, 10));
        effect.Update(Frame());

        Assert.DoesNotContain(effect.ActiveParticles, s => s.IsBurst);
        Assert.Equal(0, effect.ClickBurstTotal);
        Assert.Equal(0, effect.FlashRemaining);
    }

    [Fact]
    public void 爆发粒子强阻力衰减帧率无关()
    {
        // 指数衰减可分性：一次 0.2s 与两次 0.1s 末速一致（用强阻力系数）
        var a = new SparklerParticle { VX = 1200 };
        SparklerEffect.Advance(ref a, 0.2, ClickBurst.DragK);
        var b = new SparklerParticle { VX = 1200 };
        SparklerEffect.Advance(ref b, 0.1, ClickBurst.DragK);
        SparklerEffect.Advance(ref b, 0.1, ClickBurst.DragK);
        Assert.Equal(a.VX, b.VX, 6);
        Assert.True(a.VX < 1200 * 0.12); // 200ms 内速度衰减 ~89%
    }

    [Fact]
    public void 爆发粒子全程受重力_减速后加速下坠不悬停()
    {
        // 悬停 bug 根因：常态重力 40 配强阻力终端落速仅 3.6px/s。
        // 爆发粒子用 BurstGravity=400 → 终端落速 ≈ 400/11 ≈ 36px/s，可见下坠
        var s = new SparklerParticle { VX = 1400, VY = 0, IsBurst = true };
        double dt = 1 / 60.0;
        for (int i = 0; i < 60; i++) // 1 秒：炸开 → 减速 → 最高点趋停 → 下坠
            SparklerEffect.Advance(ref s, dt, ClickBurst.DragK, SparklerEffect.BurstGravity);

        Assert.InRange(s.VY, 25, 50);            // 稳定向下坠（终端 ≈ 36px/s）
        Assert.True(s.Y > 5, $"应已有向下位移，实际 Y={s.Y}");
    }

    [Fact]
    public void 爆发粒子拖尾随速度缩短至消失()
    {
        Assert.Equal(0, SparklerEffect.BurstTrailScale(0));    // 速度趋零：拖尾消失
        Assert.Equal(0.5, SparklerEffect.BurstTrailScale(60)); // 半速半长
        Assert.Equal(1, SparklerEffect.BurstTrailScale(120));  // ≥120px/s 满长
        Assert.Equal(1, SparklerEffect.BurstTrailScale(500));  // 高速不超长
    }

    [Fact]
    public void 被冲量踹飞的粒子继续遵循阻力与重力物理()
    {
        // 表现 2 验证：冲量只是速度增量，之后照常受阻力与重力，不出现无物理的直线漂移
        var s = new SparklerParticle { VX = 0, VY = 0, IsBurst = true };
        s.VX -= 300; // 模拟被向左踹一脚（径向冲量）
        double vxAfterImpulse = s.VX;

        double dt = 1 / 60.0;
        for (int i = 0; i < 60; i++)
            SparklerEffect.Advance(ref s, dt, ClickBurst.DragK, SparklerEffect.BurstGravity);

        Assert.True(s.VX > vxAfterImpulse);   // 阻力把冲量衰减掉（|VX| 收敛）
        Assert.InRange(s.VY, 25, 50);         // 重力全程生效，照常下坠
    }

    [Fact]
    public void 点击后中心闪光持续约100毫秒()
    {
        var effect = new SparklerEffect(new Random(1)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.OnMouseDown(new Point(0, 0));
        Assert.InRange(effect.FlashRemaining, 0.09, 0.101);

        effect.Update(TimeSpan.FromSeconds(0.15));
        Assert.Equal(0, effect.FlashRemaining);
    }

    [Fact]
    public void 连续快速点击池有界()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true, PoolLimit = 50 };
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 10; i++)
        {
            effect.OnMouseDown(new Point(i * 10, 0));
            effect.Update(Frame());
        }

        Assert.InRange(effect.ActiveParticles.Count, 1, 50 + effect.BurstReserve);
    }
}
