using MouseFx.Effects;
using MouseFx.Settings;
using Xunit;

namespace MouseFx.Tests;

/// <summary>
/// "AppSettings → 特效实例"接线的回归测试：每张参数卡片只准写自己模式的特效属性。
/// 历史 bug：设置窗口光圈卡片 ApplyAll 里遗留 "_spark.Hue = settings.Hue"，
/// 光圈颜色覆盖火屑独立颜色（SparkHue），切到火屑模式后显示的是光圈的颜色。
/// </summary>
public class EffectSettingsApplierTests
{
    [Fact]
    public void 应用光圈参数不写入火屑与烟花()
    {
        var s = new AppSettings
        {
            Hue = 210, GlowRadius = 40, GlowOpacity = 0.5, FollowSpeed = 60,
            RippleRadius = 90, RippleShape = RippleShape.Heart, RippleClickEnabled = false,
        };
        var glow = new GlowEffect();
        var ripple = new RippleEffect();
        var spark = new SparkEffect { Hue = 30, PoolLimit = 111, MaxLife = 1.23, ClickBurstEnabled = false };
        var sparkler = new SparklerEffect { PoolLimit = 222, Size = 150, ClickBurstEnabled = false };

        EffectSettingsApplier.ApplyClassic(s, glow, ripple);

        Assert.Equal(210, glow.Hue);
        Assert.Equal(40, glow.GlowRadius);
        Assert.Equal(0.5, glow.Opacity, 3);
        Assert.Equal(60, glow.FollowSpeed);
        Assert.Equal(90, ripple.MaxRadius);
        Assert.Equal(RippleShape.Heart, ripple.Shape);
        Assert.False(ripple.ClickEnabled);
        // 串扰断言：光圈卡片绝不触碰火屑/烟花的任何属性
        Assert.Equal(30, spark.Hue);
        Assert.Equal(111, spark.PoolLimit);
        Assert.Equal(1.23, spark.MaxLife);
        Assert.False(spark.ClickBurstEnabled);
        Assert.Equal(222, sparkler.PoolLimit);
        Assert.Equal(150, sparkler.Size);
        Assert.False(sparkler.ClickBurstEnabled);
    }

    [Fact]
    public void 应用火屑参数不写入光圈与烟花()
    {
        var s = new AppSettings { SparkHue = 30, SparkCount = 120, SparkLife = 2.0, SparkClickBurst = true };
        var glow = new GlowEffect { Hue = 210 };
        var ripple = new RippleEffect { Hue = 210, MaxRadius = 90, ClickEnabled = false };
        var spark = new SparkEffect();
        var sparkler = new SparklerEffect { PoolLimit = 222, Size = 150, ClickBurstEnabled = false };

        EffectSettingsApplier.ApplySpark(s, spark);

        Assert.Equal(30, spark.Hue);
        Assert.Equal(120, spark.PoolLimit);
        Assert.Equal(2.0, spark.MaxLife);
        Assert.True(spark.ClickBurstEnabled);
        // 串扰断言：火屑卡片绝不触碰光圈/烟花的任何属性
        Assert.Equal(210, glow.Hue);
        Assert.Equal(210, ripple.Hue);
        Assert.Equal(90, ripple.MaxRadius);
        Assert.False(ripple.ClickEnabled);
        Assert.Equal(222, sparkler.PoolLimit);
        Assert.Equal(150, sparkler.Size);
        Assert.False(sparkler.ClickBurstEnabled);
    }

    [Fact]
    public void 应用烟花参数不写入光圈与火屑()
    {
        var s = new AppSettings { SparklerCount = 300, SparklerSize = 120, SparklerClickBurst = true };
        var glow = new GlowEffect { Hue = 210 };
        var ripple = new RippleEffect { Hue = 210, MaxRadius = 90 };
        var spark = new SparkEffect { Hue = 30, PoolLimit = 111, ClickBurstEnabled = false };
        var sparkler = new SparklerEffect();

        EffectSettingsApplier.ApplySparkler(s, sparkler);

        Assert.Equal(300, sparkler.PoolLimit);
        Assert.Equal(120, sparkler.Size);
        Assert.True(sparkler.ClickBurstEnabled);
        // 串扰断言：烟花卡片绝不触碰光圈/火屑的任何属性
        Assert.Equal(210, glow.Hue);
        Assert.Equal(210, ripple.Hue);
        Assert.Equal(90, ripple.MaxRadius);
        Assert.Equal(30, spark.Hue);
        Assert.Equal(111, spark.PoolLimit);
        Assert.False(spark.ClickBurstEnabled);
    }

    [Fact]
    public void 光圈与火屑颜色来回应用20次互不串扰()
    {
        // 用户场景复现：光圈蓝(210) ↔ 火屑橙金(30)，光圈卡片参数反复应用后火屑仍保持自己的颜色
        var s = new AppSettings { Hue = 210, SparkHue = 30 };
        var glow = new GlowEffect();
        var ripple = new RippleEffect();
        var spark = new SparkEffect();
        var sparkler = new SparklerEffect();

        for (int i = 0; i < 20; i++)
        {
            EffectSettingsApplier.ApplyClassic(s, glow, ripple); // 光圈卡片动任何滑块都走这里
            EffectSettingsApplier.ApplySpark(s, spark);
            EffectSettingsApplier.ApplySparkler(s, sparkler);
        }

        Assert.Equal(210, glow.Hue);
        Assert.Equal(210, ripple.Hue);
        Assert.Equal(30, spark.Hue); // 修复前：被光圈卡片覆盖成 210，火屑显示蓝色
    }

    [Fact]
    public void 静止淡出开关作用于三种特效()
    {
        var s = new AppSettings { IdleFade = false };
        var glow = new GlowEffect();
        var spark = new SparkEffect();
        var sparkler = new SparklerEffect();

        EffectSettingsApplier.ApplyIdleFade(s, glow, spark, sparkler);

        Assert.False(glow.IdleFade);
        Assert.False(spark.IdleFade);
        Assert.False(sparkler.IdleFade);
    }
}
