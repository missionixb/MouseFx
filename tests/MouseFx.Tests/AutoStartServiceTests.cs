using Microsoft.Win32;
using MouseFx.Platform;
using Xunit;

namespace MouseFx.Tests;

public class AutoStartServiceTests
{
    private const string TestRoot = @"Software\MouseFx.Tests";
    private const string TestKey = @"Software\MouseFx.Tests\AutoStart";
    private const string TestAppName = "MouseFx.Tests";

    [Fact]
    public void 从未设置时IsConfigured和IsEnabled均为false()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        Assert.False(service.IsConfigured);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void 启用后写入注册表标记已配置且可取消()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        try
        {
            service.Enable();
            Assert.True(service.IsConfigured);
            Assert.True(service.IsEnabled);

            service.Disable();
            Assert.True(service.IsConfigured);   // 配置过就是配置过
            Assert.False(service.IsEnabled);
        }
        finally
        {
            using var root = Registry.CurrentUser.OpenSubKey(TestRoot, true);
            root?.DeleteSubKey("AutoStart", false);
        }
    }
}
