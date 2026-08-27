using Microsoft.Win32;

namespace MouseFx.Platform;

public sealed class AutoStartService : IAutoStartService
{
    public const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultAppName = "MouseFx";
    private const string ConfiguredValueName = "MouseFx_AutoStart_Configured";

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
            key.SetValue(ConfiguredValueName, "1");
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
            key?.DeleteValue(_valueName, false);
        }
        catch
        {
        }
    }
}
