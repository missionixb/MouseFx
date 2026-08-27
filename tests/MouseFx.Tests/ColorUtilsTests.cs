using MouseFx.Settings;

namespace MouseFx.Tests;

public class ColorUtilsTests
{
    [Fact]
    public void 色相0为纯红()
    {
        var c = ColorUtils.FromHue(0);
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
    }

    [Fact]
    public void 色相120为纯绿()
    {
        var c = ColorUtils.FromHue(120);
        Assert.Equal(0, c.R);
        Assert.Equal(255, c.G);
        Assert.Equal(0, c.B);
    }

    [Fact]
    public void 色相240为纯蓝()
    {
        var c = ColorUtils.FromHue(240);
        Assert.Equal(0, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(255, c.B);
    }

    [Fact]
    public void 色相210为蓝偏青()
    {
        var c = ColorUtils.FromHue(210);
        Assert.Equal(0, c.R);
        Assert.Equal(128, c.G);
        Assert.Equal(255, c.B);
    }

    [Fact]
    public void 色相360归一化回红色()
    {
        var c = ColorUtils.FromHue(360);
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
    }

    [Fact]
    public void 带透明度版本输出指定alpha()
    {
        var c = ColorUtils.FromHue(240, 0.5);
        Assert.Equal(128, c.A);
        Assert.Equal(0, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(255, c.B);
    }
}
