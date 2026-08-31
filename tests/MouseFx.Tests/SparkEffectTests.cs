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
        var effect = new SparkEffect(new Random(99)) { Enabled = true, BurstReserve = 0 };
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

    [Fact]
    public void 输入断流后停止发射且存量火星自然烧尽()
    {
        var effect = new SparkEffect(new Random(11)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());
        Assert.NotEmpty(effect.ActiveSparks);
        Assert.False(effect.InputStalled);

        // 断流 ~3.4s：先跑到超阈值，确认进入断流态；存量火星最长 0.9s 也已烧尽。
        // 若停发生效，池必然清空；否则静止发射会持续补充，永远不会空。
        for (int i = 0; i < 130; i++)
            effect.Update(Frame()); // ~2.08s，刚超过 2s 阈值
        Assert.True(effect.InputStalled);

        for (int i = 0; i < 80; i++)
            effect.Update(Frame());

        Assert.Empty(effect.ActiveSparks);
    }

    [Fact]
    public void 关闭静止淡出后断流仍持续发射()
    {
        var effect = new SparkEffect(new Random(11)) { Enabled = true, IdleFade = false };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 210; i++) // 断流 ~3.4s：IdleFade 关闭 → 继续发射，池不空
            effect.Update(Frame());

        Assert.True(effect.InputStalled); // 断流态成立
        Assert.NotEmpty(effect.ActiveSparks); // 但仍在发射
    }

    [Fact]
    public void 火花寿命随生命设置延长()
    {
        // 默认 MaxLife 0.9：寿命 0.4~0.9s
        var effect = new SparkEffect(new Random(7)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());
        Assert.All(effect.ActiveSparks, s => Assert.InRange(s.Life, 0.4, 0.9 + 1e-9));

        // MaxLife 2.5：寿命延长到 0.4~2.5s，下坠窗口更长
        var longLife = new SparkEffect(new Random(7)) { Enabled = true, MaxLife = 2.5 };
        longLife.OnMouseMove(new Point(0, 0));
        longLife.Update(Frame());
        Assert.All(longLife.ActiveSparks, s => Assert.InRange(s.Life, 0.4, 2.5 + 1e-9));
        Assert.True(longLife.ActiveSparks.Max(s => s.Life) > effect.ActiveSparks.Max(s => s.Life));
    }

    [Fact]
    public void 左键点击爆发40到80颗两段式粒子()
    {
        var effect = new SparkEffect(new Random(7)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.OnMouseDown(new Point(500, 400)); // 爆发在按下瞬间直接入池（先于 Update，未经阻力衰减）

        var burst = effect.ActiveSparks.Where(s => s.IsBurst).ToList();
        Assert.InRange(burst.Count, 40, 80);
        Assert.All(burst, s =>
        {
            var speed = Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            Assert.InRange(speed, ClickBurst.MinSpeed, ClickBurst.MaxSpeed + 1e-9); // 常态的 2~3 倍
            Assert.InRange(s.Life, ClickBurst.MinLife, ClickBurst.MaxLife);         // 比常态(0.4~0.9)更长
            Assert.True(s.StartSize >= 1.6 * 1.5);                                  // 尺寸放大 1.5~2 倍
        });
    }

    [Fact]
    public void 鼠标移动不触发点击爆炸()
    {
        var effect = new SparkEffect(new Random(2026)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame());
        effect.OnMouseMove(new Point(300, 0));
        effect.Update(Frame());

        Assert.DoesNotContain(effect.ActiveSparks, s => s.IsBurst);
    }

    [Fact]
    public void 关闭点击爆裂后点击无爆发无闪光()
    {
        var effect = new SparkEffect(new Random(3)) { Enabled = true, ClickBurstEnabled = false };
        effect.OnMouseDown(new Point(10, 10));
        effect.Update(Frame());

        Assert.DoesNotContain(effect.ActiveSparks, s => s.IsBurst);
        Assert.Equal(0, effect.ClickBurstTotal);
        Assert.Equal(0, effect.FlashRemaining);
    }

    [Fact]
    public void 爆发粒子阻力衰减帧率无关()
    {
        // 指数衰减可分性：一次 0.1s 与两次 0.05s 的末速一致（水平方向无重力干扰；
        // Update 把单帧 dt 钳制到 0.1s，故用 0.1 为最大步长）
        var effect = new SparkEffect(new Random(5)) { Enabled = true };
        effect.OnMouseDown(new Point(0, 0)); // 爆发粒子先入池，位于列表最前
        double v0 = effect.ActiveSparks[0].VX;
        effect.Update(TimeSpan.FromSeconds(0.1));
        double v1 = effect.ActiveSparks[0].VX;

        var effect2 = new SparkEffect(new Random(5)) { Enabled = true };
        effect2.OnMouseDown(new Point(0, 0));
        effect2.Update(TimeSpan.FromSeconds(0.05));
        effect2.Update(TimeSpan.FromSeconds(0.05));
        double w1 = effect2.ActiveSparks[0].VX;

        // 两条路径末速一致，且都等于 v0 × f(0.1)（解析值）
        Assert.Equal(v0 * ClickBurst.DragFactor(0.1), w1, 6);
        Assert.Equal(v1, w1, 6);
        Assert.True(Math.Abs(v1) < Math.Abs(v0)); // 阻力确实在减速（VX 为负，比绝对值）
    }

    [Fact]
    public void 点击后中心闪光持续约100毫秒()
    {
        var effect = new SparkEffect(new Random(1)) { Enabled = true };
        effect.OnMouseDown(new Point(0, 0));
        Assert.InRange(effect.FlashRemaining, 0.09, 0.101);

        effect.Update(TimeSpan.FromSeconds(0.15));
        Assert.Equal(0, effect.FlashRemaining);
    }

    [Fact]
    public void 连续快速点击池有界且常态粒子不被挤掉()
    {
        var effect = new SparkEffect(new Random(11)) { Enabled = true, PoolLimit = 50 };
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 30; i++)
            effect.Update(Frame()); // 先积累常态火星（爆发窗口内停发，须在此之前产生）

        for (int i = 0; i < 10; i++) // 连点 10 次
        {
            effect.OnMouseDown(new Point(i * 10, 0));
            effect.Update(Frame());
        }

        Assert.InRange(effect.ActiveSparks.Count, 1, 50 + effect.BurstReserve); // 粒子数有界
        Assert.Contains(effect.ActiveSparks, s => !s.IsBurst);                  // 常态火星未被爆炸挤掉
    }

    [Fact]
    public void 点击瞬间原有火星被施加向外径向冲量()
    {
        var effect = new SparkEffect(new Random(9)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(Frame()); // 先有常态火星（位于 0,0 附近，即点击点左侧）
        var before = effect.ActiveSparks[0];

        effect.OnMouseDown(new Point(200, 0));
        var after = effect.ActiveSparks[0];

        Assert.True(after.VX < before.VX); // 被推离点击点（向 -X）
    }

    [Fact]
    public void 爆发窗口内常态发射暂停_窗口结束无痕恢复()
    {
        var effect = new SparkEffect(new Random(13)) { Enabled = true };
        effect.OnMouseMove(new Point(0, 0));
        effect.OnMouseDown(new Point(0, 0));
        Assert.InRange(effect.BurstWindowRemaining, 0.3, 0.36);
        Assert.Equal(0, effect.ActiveSparks.Count(s => !s.IsBurst)); // 点击前无常态火星

        for (int i = 0; i < 15; i++) // 窗口内 ~0.24s：无新增常态火星
            effect.Update(Frame());
        Assert.Equal(0, effect.ActiveSparks.Count(s => !s.IsBurst));

        effect.Update(TimeSpan.FromSeconds(0.4)); // 窗口结束
        Assert.Equal(0, effect.BurstWindowRemaining);
        Assert.True(effect.ActiveSparks.Count(s => !s.IsBurst) > 0); // 常态发射恢复
    }
}
