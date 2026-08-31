using System.IO;
using MouseFx.Settings;

namespace MouseFx.Tests;

public class SettingsServiceTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"mfx_test_{Guid.NewGuid():N}.json");

    [Fact]
    public void 文件不存在时返回默认值()
    {
        var service = new SettingsService(TempPath());
        var s = service.Load();

        Assert.Equal(210, s.Hue);
        Assert.Equal(28, s.GlowRadius);
        Assert.Equal(0.35, s.GlowOpacity, 3);
        Assert.Equal(60, s.RippleRadius);
        Assert.Equal(50, s.FollowSpeed);
        Assert.True(s.RippleEnabled);   // 开关缺省：经典组合开，静止淡出开
        Assert.True(s.GlowEnabled);
        Assert.False(s.SparkEnabled);
        Assert.True(s.IdleFade);
        Assert.True(s.HideOnFullscreen);
        Assert.Equal(EffectMode.Classic, s.EffectMode);
        Assert.Equal(30, s.SparkHue);       // 火花默认橙金，与经典颜色（210 蓝）独立
        Assert.Equal(250, s.SparkCount);
        Assert.Equal(0.9, s.SparkLife);     // 火花默认寿命 = 现状
        Assert.Equal(200, s.SparklerCount);
        Assert.Equal(80, s.SparklerSize);
        Assert.True(s.SparkClickBurst);     // 点击爆裂默认开
        Assert.True(s.SparklerClickBurst);
    }

    [Fact]
    public void 保存后能读回相同值()
    {
        var path = TempPath();
        try
        {
            var service = new SettingsService(path);
            var s = new AppSettings
            {
                Hue = 120,
                GlowRadius = 40,
                GlowOpacity = 0.8,
                RippleRadius = 100,
                RippleShape = RippleShape.Star,
                RippleEnabled = false,
                GlowEnabled = false,
                SparkEnabled = true,
                IdleFade = false,
                HideOnFullscreen = false,
                EffectMode = EffectMode.Sparkler,
                SparkHue = 45,
                SparkCount = 120,
                SparkLife = 2.0,
                SparklerCount = 300,
                FollowSpeed = 80,
                SparkClickBurst = false,
                SparklerClickBurst = false,
            };
            service.Save(s);

            var loaded = new SettingsService(path).Load();
            Assert.Equal(120, loaded.Hue);
            Assert.Equal(40, loaded.GlowRadius);
            Assert.Equal(0.8, loaded.GlowOpacity, 3);
            Assert.Equal(100, loaded.RippleRadius);
            Assert.Equal(RippleShape.Star, loaded.RippleShape);
            Assert.False(loaded.RippleEnabled);
            Assert.False(loaded.GlowEnabled);
            Assert.True(loaded.SparkEnabled);
            Assert.False(loaded.IdleFade);
            Assert.False(loaded.HideOnFullscreen);
            Assert.Equal(EffectMode.Sparkler, loaded.EffectMode);
            Assert.Equal(45, loaded.SparkHue);
            Assert.Equal(120, loaded.SparkCount);
            Assert.Equal(2.0, loaded.SparkLife);
            Assert.Equal(300, loaded.SparklerCount);
            Assert.Equal(80, loaded.FollowSpeed);
            Assert.False(loaded.SparkClickBurst);       // 开关持久化
            Assert.False(loaded.SparklerClickBurst);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 损坏文件返回默认值不抛异常()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{invalid json!!!");
            var service = new SettingsService(path);
            var s = service.Load();

            Assert.Equal(210, s.Hue); // 解析失败 → 默认值
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 含已删除形状名的旧设置文件形状回退圆圈且其余设置保留()
    {
        var path = TempPath();
        try
        {
            // 旧版本写入的 Note/Clover 在新枚举中已不存在
            File.WriteAllText(path, "{ \"Hue\": 120, \"GlowRadius\": 40, \"RippleShape\": \"Note\" }");
            var s = new SettingsService(path).Load();

            Assert.Equal(RippleShape.Circle, s.RippleShape); // 未知形状 → 圆圈
            Assert.Equal(120, s.Hue);                        // 其余设置不受影响
            Assert.Equal(40, s.GlowRadius);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 旧设置文件无模式字段时按旧开关字段推导()
    {
        var path = TempPath();
        try
        {
            // 旧版程序写的文件：没有 EffectMode，只有三个开关
            File.WriteAllText(path, "{ \"RippleEnabled\": false, \"GlowEnabled\": false, \"SparkEnabled\": true }");
            var s = new SettingsService(path).Load();

            Assert.Equal(EffectMode.Spark, s.EffectMode); // 火花开 → 火花模式

            File.WriteAllText(path, "{ \"RippleEnabled\": true, \"GlowEnabled\": true, \"SparkEnabled\": false }");
            var classic = new SettingsService(path).Load();

            Assert.Equal(EffectMode.Classic, classic.EffectMode); // 经典组合 → Classic
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 新设置文件的模式字段优先生效()
    {
        var path = TempPath();
        try
        {
            // 新文件同时含模式与旧开关：以模式为准
            File.WriteAllText(path,
                "{ \"EffectMode\": \"Classic\", \"RippleEnabled\": false, \"GlowEnabled\": false, \"SparkEnabled\": true }");
            var s = new SettingsService(path).Load();

            Assert.Equal(EffectMode.Classic, s.EffectMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 非法模式值回退经典模式()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ \"EffectMode\": \"Bogus\" }");
            var s = new SettingsService(path).Load();

            Assert.Equal(EffectMode.Classic, s.EffectMode);
            Assert.True(s.RippleEnabled); // 其余字段不受影响
        }
        finally
        {
            File.Delete(path);
        }
    }
}
