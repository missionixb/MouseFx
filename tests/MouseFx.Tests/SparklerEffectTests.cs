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
    public void 静止时仍持续爆发且与移动无关()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 60; i++) // ~1s，无任何移动事件 → 爆发照常
            effect.Update(Frame());

        Assert.True(effect.BurstCycles >= 2, $"1s 内应至少 2 轮爆发，实际 {effect.BurstCycles}");
        Assert.NotEmpty(effect.ActiveParticles);
    }

    [Fact]
    public void 每轮爆发喷射颗数在8到15之间()
    {
        var effect = new SparklerEffect(new Random(2026)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame()); // 第一轮爆发开始

        int before = effect.EmittedTotal;
        for (int i = 0; i < 40 && effect.BurstCycles < 2; i++) // 等第一轮爆发结束
            effect.Update(Frame());

        Assert.InRange(effect.EmittedTotal - before, 8, 15);
    }

    [Fact]
    public void 二次爆裂发生且伴随亮白闪光点()
    {
        var effect = new SparklerEffect(new Random(77)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 40 && effect.SecondaryBurstCount == 0; i++) // 等首次二次爆裂
            effect.Update(Frame());

        Assert.True(effect.SecondaryBurstCount > 0);
        Assert.Contains(effect.ActiveParticles, s => s.IsFlash);   // 爆裂瞬间的闪光点
        Assert.Contains(effect.ActiveParticles, s => s.IsChild);   // 子火星
    }

    [Fact]
    public void 子火星更短命且不再二次爆裂()
    {
        var effect = new SparklerEffect(new Random(77)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 120; i++) // ~2s
        {
            effect.Update(Frame());
            // 子火星寿命 100~200ms；母火星兜底 0.45s；闪光点 60~100ms
            Assert.All(effect.ActiveParticles, s => Assert.InRange(s.Life, 0.06, 0.45 + 1e-9));
            Assert.All(effect.ActiveParticles, s => Assert.True(s.Age < s.Life + 1e-9));
            // 子火星与闪光点都带 HasBurst 标记，不会再触发二次爆裂
            Assert.All(effect.ActiveParticles, s => Assert.True(!s.IsChild || s.HasBurst));
        }
    }

    [Fact]
    public void 空气阻力指数衰减与弱重力叠加()
    {
        var s = new SparklerParticle { VX = 1000, VY = -500, BurstAt = 10, BurstDistance = 1e6 };

        double dt = 0.05;
        bool shouldBurst = SparklerEffect.Advance(ref s, dt);

        Assert.False(shouldBurst); // 距离/时间阈值都未达到
        double decay = Math.Exp(-SparklerEffect.Drag * dt);
        Assert.Equal(1000 * decay, s.VX, 6);
        Assert.Equal(-500 * decay + SparklerEffect.Gravity * dt, s.VY, 6);
        double expectedDist = Math.Sqrt(s.VX * s.VX + s.VY * s.VY) * dt;
        Assert.Equal(expectedDist, s.DistanceTravelled, 6);
    }

    [Fact]
    public void 二次爆裂条件距离或时间先到先触发()
    {
        // 距离触发：BurstDistance 30px，速度 1000 px/s × 0.05s = 50px ≥ 30
        var byDistance = new SparklerParticle { VX = 1000, VY = 0, BurstAt = 10, BurstDistance = 30 };
        Assert.True(SparklerEffect.Advance(ref byDistance, 0.05));

        // 时间触发：速度慢到距离不够；Age 已含本帧增量（Update 中先加 Age 再调 Advance）
        var byTime = new SparklerParticle { VX = 100, VY = 0, Age = 0.08, BurstAt = 0.08, BurstDistance = 1e6 };
        Assert.True(SparklerEffect.Advance(ref byTime, 0.1));

        // 闪光点永不触发也不移动
        var flash = new SparklerParticle { VX = 999, VY = 999, IsFlash = true, BurstAt = 0, BurstDistance = 0 };
        Assert.False(SparklerEffect.Advance(ref flash, 0.1));
        Assert.Equal(999, flash.VX);
    }

    [Fact]
    public void 母火星初速度在700到1400之间()
    {
        var effect = new SparklerEffect(new Random(99)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());

        Assert.All(effect.ActiveParticles.Where(p => !p.IsChild && !p.IsFlash), s =>
        {
            double speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            Assert.InRange(speed, 700, 1400 + 1e-9);
        });
    }

    [Fact]
    public void 颜色生命周期蓝白到纯白到黄白最后橙红余烬()
    {
        Assert.Equal(SparklerEffect.LifeColor(0), Color.FromRgb(0xE8, 0xF1, 0xFF));   // 蓝白
        Assert.Equal(SparklerEffect.LifeColor(0.3), Colors.White);                     // 纯白核心
        Assert.Equal(SparklerEffect.LifeColor(0.5), Color.FromRgb(0xFF, 0xF8, 0xE4)); // 白→黄白途中
        Assert.Equal(SparklerEffect.LifeColor(0.7), Color.FromRgb(0xFF, 0xF4, 0xD6));  // 黄白
        Assert.Equal(SparklerEffect.LifeColor(1), Color.FromRgb(0x66, 0x14, 0x00));    // 暗余烬

        // 最后 20% 内：红通道始终最高 → 橙红色系，且逐渐变暗
        var mid = SparklerEffect.LifeColor(0.85);
        var late = SparklerEffect.LifeColor(0.95);
        Assert.True(mid.R > mid.G && mid.G > mid.B);
        Assert.True(late.R < mid.R);
    }

    [Fact]
    public void 池满时不再增长()
    {
        var effect = new SparklerEffect(new Random(99)) { Enabled = true };
        effect.PoolLimit = 5;
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 30; i++)
            effect.Update(Frame());

        Assert.True(effect.ActiveParticles.Count <= 5);
    }

    [Fact]
    public void 静止淡出开启时断流停发且池清空()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true, IdleFade = true };
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 3; i++)
            effect.Update(Frame()); // 第一帧点火、后续帧开始喷发
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
    public void 静止淡出关闭时断流仍持续爆燃()
    {
        var effect = new SparklerEffect(new Random(11)) { Enabled = true, IdleFade = false };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 210; i++) // 断流 ~3.4s
            effect.Update(Frame());

        Assert.True(effect.InputStalled);
        Assert.NotEmpty(effect.ActiveParticles); // 仍在爆发
        Assert.Equal(1, effect.CoreFade);        // 光核常亮
    }
}
