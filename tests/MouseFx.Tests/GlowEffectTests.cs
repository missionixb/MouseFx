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
}
