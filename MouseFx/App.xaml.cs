using System.Windows;
using MouseFx.Effects;
using MouseFx.Hooks;
using MouseFx.Overlay;
using MouseFx.Platform;
using MouseFx.Tray;

namespace MouseFx;

public partial class App : Application
{
    private readonly IAutoStartService _autoStart = new AutoStartService();
    private IMouseHookService? _hook;
    private EffectManager? _manager;
    private OverlayWindow? _overlay;
    private RippleEffect? _ripple;
    private GlowEffect? _glow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 首次运行默认开启开机自启动；之后菜单状态只反映注册表真实状态
        if (!_autoStart.IsConfigured)
            _autoStart.Enable();

        _manager = new EffectManager();
        _ripple = new RippleEffect { Enabled = true };
        _glow = new GlowEffect { Enabled = true };
        _manager.Register(_ripple);
        _manager.Register(_glow);

        _overlay = new OverlayWindow(_manager);
        _overlay.Show();

        _hook = new MouseHook();
        _hook.MouseMove += devicePoint => Dispatcher.BeginInvoke(
            () => _manager!.HandleMouseMove(_overlay!.ToLocal(devicePoint)));
        _hook.MouseDown += devicePoint => Dispatcher.BeginInvoke(
            () => _manager!.HandleMouseDown(_overlay!.ToLocal(devicePoint)));
        try
        {
            _hook.Start();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"鼠标钩子启动失败：{ex.Message}。特效将无法工作。",
                "鼠标特效", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var tray = new TrayIcon(_manager, _ripple, _glow, _autoStart);
        tray.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        base.OnExit(e);
    }
}
