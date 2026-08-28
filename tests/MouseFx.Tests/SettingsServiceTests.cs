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
                FollowSpeed = 80,
            };
            service.Save(s);

            var loaded = new SettingsService(path).Load();
            Assert.Equal(120, loaded.Hue);
            Assert.Equal(40, loaded.GlowRadius);
            Assert.Equal(0.8, loaded.GlowOpacity, 3);
            Assert.Equal(100, loaded.RippleRadius);
            Assert.Equal(RippleShape.Star, loaded.RippleShape);
            Assert.Equal(80, loaded.FollowSpeed);
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
}
