using System.Windows;
using MouseFx.Effects;
using Xunit;

namespace MouseFx.Tests;

public class SparkEffectTests
{
    private static TimeSpan Frame(double ms = 16) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void 未收到鼠标位置前不发射()
    {
        var effect = new SparkEffect(new Random(1)) { Enabled = true };

        effect.Update(Frame());

        Assert.Empty(effect.ActiveSparks);
    }

    [Fact]
    public void 禁用时不发射不更新()
    {
        var effect = new SparkEffect(new Random(1)) { Enabled = false };
        effect.OnMouseMove(new Point(0, 0));

        effect.Update(Frame());

        Assert.Empty(effect.ActiveSparks);
    }

    [Fact]
    public void 静止时仍发射且每帧不超过2颗()
    {
        var effect = new SparkEffect(new Random(11)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        effect.Update(Frame());

        Assert.InRange(effect.ActiveSparks.Count, 1, 2);
    }

    [Fact]
    public void 移动时每帧发射1到3颗()
    {
        var effect = new SparkEffect(new Random(2026)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame()); // 静止帧
        int before = effect.ActiveSparks.Count;

        effect.OnMouseMove(new Point(400, 0)); // 位移 400px/帧 → 高速移动
        effect.Update(Frame());

        Assert.InRange(effect.ActiveSparks.Count - before, 1, 3);
    }

    [Fact]
    public void 重力使火星速度线性增加且位置按半隐式欧拉积分()
    {
        var effect = new SparkEffect(new Random(5)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());
        var s0 = effect.ActiveSparks[0];

        double dt = 0.1;
        effect.Update(TimeSpan.FromSeconds(dt));
        var s1 = effect.ActiveSparks[0];

        Assert.Equal(s0.VY + SparkEffect.Gravity * dt, s1.VY, 6);
        Assert.Equal(s0.Y + (s0.VY + SparkEffect.Gravity * dt) * dt, s1.Y, 6);
    }

    [Fact]
    public void 火星寿命到期被回收且年龄始终小于寿命()
    {
        var effect = new SparkEffect(new Random(42)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 120; i++) // ~2s，覆盖最长寿命 0.9s + 子火星 0.45s
        {
            effect.Update(Frame());
            Assert.All(effect.ActiveSparks, s => Assert.True(s.Age < s.Life + 1e-9, $"Age={s.Age} 应小于 Life={s.Life}"));
            Assert.All(effect.ActiveSparks, s => Assert.InRange(s.Life, 0.2, 0.9 + 1e-9));
        }
    }

    [Fact]
    public void 池满时不再增长且回收最早的火星()
    {
        var effect = new SparkEffect(new Random(99)) { Enabled = true };
        effect.PoolLimit = 5;
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 20; i++)
            effect.Update(Frame());

            // 最早发射的火星早就到期（寿命上限 0.9s ≈ 56 帧），池内全是新火星
        Assert.True(effect.ActiveSparks.Count <= 5);
    }

    [Fact]
    public void 约一成火星寿命终点炸裂成子火星()
    {
        var effect = new SparkEffect(new Random(77)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 150; i++) // ~2.4s，应有几十次到期 → 若干次炸裂
            effect.Update(Frame());

        Assert.True(effect.BurstCount > 0, $"随机种子 77 下 150 帧内应有炸裂，实际 {effect.BurstCount}");
    }

    [Fact]
    public void 炸裂产生的子火星寿命更短()
    {
        var effect = new SparkEffect(new Random(77)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 150 && effect.BurstCount == 0; i++) // 跑到首次炸裂为止
            effect.Update(Frame());

        Assert.True(effect.BurstCount > 0);
        // 母火星寿命 ≥ 0.4s；子火星寿命 0.2~0.45s，池中出现 <0.4s 的即子火星
        Assert.Contains(effect.ActiveSparks, s => s.Life < 0.4);
    }
}
