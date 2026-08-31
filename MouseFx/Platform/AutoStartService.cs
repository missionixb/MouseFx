using Microsoft.Win32;

namespace MouseFx.Platform;

public sealed class AutoStartService : IAutoStartService
{
    public const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultAppName = "MouseFx";
    private const string ConfiguredValueName = "MouseFx_AutoStart_Configured";

    // 标志位状态：1 = 已启用；0 = 用户显式停用（自愈不得重新启用）。
    // 缺失 = 从未运行过。
    private const string StateEnabled = "1";
    private const string StateDisabledByUser = "0";

    private readonly string _runKey;
    private readonly string _valueName;

    public AutoStartService(string runKey = DefaultRunKey, string valueName = DefaultAppName)
    {
        _runKey = runKey;
        _valueName = valueName;
    }

    public bool IsConfigured
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKey, false);
                return key?.GetValue(ConfiguredValueName) is string;
            }
            catch { return false; }
        }
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKey, false);
                return key?.GetValue(_valueName) is string;
            }
            catch { return false; }
        }
    }

    public void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(_runKey);
            key.SetValue(ConfiguredValueName, StateEnabled);
            key.SetValue(_valueName, Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory);
        }
        catch
        {
            // spec §5：注册表异常不抛给 UI，保持当前状态不变，不影响主功能
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKey, true);
            key?.SetValue(ConfiguredValueName, StateDisabledByUser); // 记录"用户主动停用"，区别于启动项失效
            key?.DeleteValue(_valueName, false);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 启动时自愈：首次运行自动启用；已配置但启动项缺失或指向旧版本 exe 时自动重写；
    /// 用户显式停用（标志位 0）则尊重选择不启用。
    /// 背景：旧版逻辑"标志位在就不再写"导致 Run 值被清理工具删除后自启动静默失效。
    /// </summary>
    public void EnsureRegistered()
    {
        var state = ReadState();
        if (state == StateDisabledByUser) return;                // 用户显式停用
        if (state == null) { Enable(); return; }                 // 首次运行
        if (!IsEnabled || !PointsToCurrentExe()) Enable();       // 自愈：缺失或指向旧版本
    }

    private string? ReadState()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKey, false);
            return key?.GetValue(ConfiguredValueName) as string;
        }
        catch { return null; }
    }

    /// <summary>启动项是否指向当前正在运行的 exe（路径一致 = 未失效）。</summary>
    private bool PointsToCurrentExe()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKey, false);
            var registered = key?.GetValue(_valueName) as string;
            var current = Environment.ProcessPath;
            return registered != null && current != null
                && string.Equals(registered, current, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
