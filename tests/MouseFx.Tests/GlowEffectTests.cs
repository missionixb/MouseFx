using System.Windows;
using MouseFx.Effects;
using Xunit;

namespace MouseFx.Tests;

public class GlowEffectTests
{
    [Fact]
    public void 鼠标移动设置目标位置()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(120, 80));

        Assert.Equal(120, effect.Target.X);
        Assert.Equal(80, effect.Target.Y);
    }

    [Fact]
    public void 位置按帧率无关指数平滑追赶()
    {
        var effect = new GlowEffect { FollowSpeed = 50 };
        effect.OnMouseMove(new Point(100, 0));
        // factor = 1 - e^(-50*0.02) = 1 - e^-1 ≈ 0.63212，位置 = 0 + 100*0.63212 = 63.212
        effect.Update(TimeSpan.FromMilliseconds(20));

        Assert.Equal(63.212, effect.Position.X, 3);
        Assert.Equal(0, effect.Position.Y, 3);
    }

    [Fact]
    public void 跟随速度越高收敛越快()
    {
        var slow = new GlowEffect { FollowSpeed = 20 };
        var fast = new GlowEffect { FollowSpeed = 100 };
        slow.OnMouseMove(new Point(100, 0));
        fast.OnMouseMove(new Point(100, 0));

        slow.Update(TimeSpan.FromMilliseconds(20)); // 1-e^-0.4 ≈ 0.3297 → 32.97
        fast.Update(TimeSpan.FromMilliseconds(20)); // 1-e^-2.0 ≈ 0.8647 → 86.47

        Assert.Equal(32.97, slow.Position.X, 2);
        Assert.Equal(86.47, fast.Position.X, 2);
    }

    [Fact]
    public void 相同总时长不同分帧方式收敛结果一致()
    {
        var a = new GlowEffect { FollowSpeed = 50 };
        a.OnMouseMove(new Point(100, 100));
        for (int i = 0; i < 60; i++) a.Update(TimeSpan.FromMilliseconds(16.67));

        var b = new GlowEffect { FollowSpeed = 50 };
        b.OnMouseMove(new Point(100, 100));
        b.Update(TimeSpan.FromMilliseconds(1000));

        Assert.InRange(Math.Abs(a.Position.X - b.Position.X), 0, 0.5);
        Assert.InRange(a.Position.X, 99, 101);
        Assert.InRange(a.Position.Y, 99, 101);
    }

    [Fact]
    public void 输入断流超过阈值后光晕淡出且一秒内淡到零()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(0, 0));
        effect.Update(TimeSpan.FromMilliseconds(16));
        Assert.Equal(1, effect.InputFade); // 有输入 → 不淡出

        for (int i = 0; i < 150; i++) // 断流 ~2.4s：超过 2s 阈值 0.4s，处于 0.5s 淡出中途
            effect.Update(TimeSpan.FromMilliseconds(16));

        Assert.True(effect.InputFade < 1, "断流超阈值后应开始淡出");
        Assert.True(effect.InputFade > 0, "刚超阈值 0.5s 内尚未淡完");

        effect.Update(TimeSpan.FromSeconds(1));
        Assert.Equal(0, effect.InputFade); // 继续断流 → 完全隐藏
    }

    [Fact]
    public void 恢复输入立即淡回不透明()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 200; i++) effect.Update(TimeSpan.FromMilliseconds(16)); // 断流淡出
        Assert.Equal(0, effect.InputFade);

        effect.OnMouseMove(new Point(50, 50)); // 输入恢复
        effect.Update(TimeSpan.FromMilliseconds(16));

        Assert.Equal(1, effect.InputFade);
    }

    [Fact]
    public void 点击也算输入会重置断流计时()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(0, 0));
        for (int i = 0; i < 150; i++) effect.Update(TimeSpan.FromMilliseconds(16)); // 断流 ~2.4s
        Assert.True(effect.InputFade < 1);

        effect.OnMouseDown(new Point(0, 0)); // 点击（如波纹触发）证明输入仍活跃
        effect.Update(TimeSpan.FromMilliseconds(16));

        Assert.Equal(1, effect.InputFade);
    }

    [Fact]
    public void 关闭静止淡出后光晕常亮不消失()
    {
        var effect = new GlowEffect { IdleFade = false };
        effect.OnMouseMove(new Point(0, 0));

        for (int i = 0; i < 250; i++) // 断流 4s，远超阈值
            effect.Update(TimeSpan.FromMilliseconds(16));

        Assert.Equal(1, effect.InputFade); // IdleFade 关闭 → 永不淡出
    }

    [Fact]
    public void HasVisual跟随鼠标出现并在淡出后消失()
    {
        var glow = new GlowEffect { Enabled = true };
        Assert.False(glow.HasVisual); // 从未收到鼠标位置

        glow.OnMouseMove(new Point(0, 0));
        Assert.True(glow.HasVisual);

        for (int i = 0; i < 200; i++) // 断流 3.2s：淡出完成
            glow.Update(TimeSpan.FromMilliseconds(16));
        Assert.False(glow.HasVisual);
    }
}
