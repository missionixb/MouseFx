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
    public void 位置按平滑系数向目标追赶()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(100, 0)); // 目标 (100, 0)，当前位置 (0, 0)

        effect.Update(TimeSpan.FromMilliseconds(16)); // 0 + (100-0)*0.3 = 30
        Assert.Equal(30, effect.Position.X, 3);

        effect.Update(TimeSpan.FromMilliseconds(16)); // 30 + (100-30)*0.3 = 51
        Assert.Equal(51, effect.Position.X, 3);
    }

    [Fact]
    public void 多次更新后收敛到目标不振荡()
    {
        var effect = new GlowEffect();
        effect.OnMouseMove(new Point(100, 100));

        for (int i = 0; i < 200; i++)
            effect.Update(TimeSpan.FromMilliseconds(16));

        Assert.InRange(effect.Position.X, 99.5, 100.5);
        Assert.InRange(effect.Position.Y, 99.5, 100.5);
    }
}
