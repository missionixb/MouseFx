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

    [Fact]
    public void 首次运行时EnsureRegistered自动启用()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        try
        {
            service.EnsureRegistered();

            Assert.True(service.IsEnabled);
            Assert.True(service.IsConfigured);
        }
        finally
        {
            using var root = Registry.CurrentUser.OpenSubKey(TestRoot, true);
            root?.DeleteSubKey("AutoStart", false);
        }
    }

    [Fact]
    public void 用户显式停用后EnsureRegistered不再重新启用()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        try
        {
            service.Enable();
            service.Disable();

            service.EnsureRegistered(); // 用户选择必须被尊重

            Assert.False(service.IsEnabled);
        }
        finally
        {
            using var root = Registry.CurrentUser.OpenSubKey(TestRoot, true);
            root?.DeleteSubKey("AutoStart", false);
        }
    }

    [Fact]
    public void 启动项被删后EnsureRegistered自动重写()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        try
        {
            service.Enable();
            using (var key = Registry.CurrentUser.OpenSubKey(TestKey, true))
                key?.DeleteValue(TestAppName, false); // 模拟被清理工具删掉启动项
            Assert.False(service.IsEnabled);

            service.EnsureRegistered();

            Assert.True(service.IsEnabled); // 自愈重写
        }
        finally
        {
            using var root = Registry.CurrentUser.OpenSubKey(TestRoot, true);
            root?.DeleteSubKey("AutoStart", false);
        }
    }

    [Fact]
    public void 启动项指向旧版本exe时自动重写为当前程序()
    {
        var service = new AutoStartService(TestKey, TestAppName);
        try
        {
            service.Enable();
            using (var key = Registry.CurrentUser.OpenSubKey(TestKey, true))
                key?.SetValue(TestAppName, @"C:\not-exist\old\MouseFx.exe"); // 模拟指向旧版本

            service.EnsureRegistered();

            Assert.True(service.IsEnabled);
            using var check = Registry.CurrentUser.OpenSubKey(TestKey);
            Assert.Equal(Environment.ProcessPath, check?.GetValue(TestAppName)); // 已指向当前 exe
        }
        finally
        {
            using var root = Registry.CurrentUser.OpenSubKey(TestRoot, true);
            root?.DeleteSubKey("AutoStart", false);
        }
    }
}
