using System.Windows;
using MouseFx.Effects;
using MouseFx.Settings;
using Xunit;

namespace MouseFx.Tests;

public class RippleEffectTests
{
    [Fact]
    public void 默认波纹形状为圆圈()
    {
        Assert.Equal(RippleShape.Circle, new RippleEffect().Shape);
    }

    [Theory]
    [InlineData(RippleShape.Circle)]
    [InlineData(RippleShape.Heart)]
    [InlineData(RippleShape.Clover)]
    [InlineData(RippleShape.Note)]
    public void 每种形状几何都有正尺寸(RippleShape shape)
    {
        var g = RippleShapes.For(shape);
        Assert.True(g.Bounds.Width > 0, $"{shape} 宽度应为正");
        Assert.True(g.Bounds.Height > 0, $"{shape} 高度应为正");
    }

    [Fact]
    public void 按下后立即创建一个半径为零的波纹()
    {
        var effect = new RippleEffect();
        effect.OnMouseDown(new Point(50, 60));

        var ripple = Assert.Single(effect.ActiveRipples);
        Assert.Equal(0, ripple.Radius, 3);
        Assert.Equal(0.9, ripple.Opacity, 3);
        Assert.Equal(50, ripple.Position.X);
        Assert.Equal(60, ripple.Position.Y);
    }

    [Fact]
    public void 半程时半径按EaseOutQuad增长且透明度衰减()
    {
        var effect = new RippleEffect();
        effect.OnMouseDown(new Point(0, 0));

        effect.Update(RippleEffect.Duration / 2);

        var ripple = Assert.Single(effect.ActiveRipples);
        // progress=0.5, eased=1-(1-0.5)^2=0.75, radius=60*0.75=45, opacity=0.9*0.5=0.45
        Assert.Equal(45, ripple.Radius, 3);
        Assert.Equal(0.45, ripple.Opacity, 3);
        Assert.Equal(0.5, ripple.Progress, 3);
    }

    [Fact]
    public void 达到时长后波纹被回收()
    {
        var effect = new RippleEffect();
        effect.OnMouseDown(new Point(0, 0));

        effect.Update(RippleEffect.Duration + TimeSpan.FromMilliseconds(1));

        Assert.Empty(effect.ActiveRipples);
    }

    [Fact]
    public void 连续快速点击只保留最近30个波纹()
    {
        var effect = new RippleEffect();
        for (int i = 0; i < 35; i++)
            effect.OnMouseDown(new Point(i, i));

        Assert.Equal(30, effect.ActiveRipples.Count);
        // 丢弃了最早的 5 个，保留的是 i=5..34
        Assert.Equal(5, effect.ActiveRipples[0].Position.X);
        Assert.Equal(34, effect.ActiveRipples[^1].Position.X);
    }
}
