namespace MouseFx.Platform;

public interface IAutoStartService
{
    bool IsConfigured { get; }
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
