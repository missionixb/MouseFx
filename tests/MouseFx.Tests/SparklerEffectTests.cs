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
        var effect = new SparklerEffect(new Random(99)) { Enabled = true };
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
}
