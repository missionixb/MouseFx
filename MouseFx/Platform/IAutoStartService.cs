namespace MouseFx.Platform;

public interface IAutoStartService
{
    bool IsConfigured { get; }
    bool IsEnabled { get; }
    void Enable();
    void Disable();

    /// <summary>启动时自愈：首次运行启用；启动项缺失或指向旧版本时重写；用户显式停用则不动。</summary>
    void EnsureRegistered();
}
