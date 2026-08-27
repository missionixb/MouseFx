using System.Windows;
using MouseFx.Effects;
using MouseFx.Hooks;
using MouseFx.Overlay;
using MouseFx.Platform;
using MouseFx.Settings;
using MouseFx.Tray;

namespace MouseFx;

public partial class App : Application
{
    private readonly IAutoStartService _autoStart = new AutoStartService();
    private readonly SettingsService _settingsService = new();
    private AppSettings? _settings;
    private IMouseHookService? _hook;
    private EffectManager? _manager;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private RippleEffect? _ripple;
    private GlowEffect? _glow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 首次运行默认开启开机自启动；之后菜单状态只反映注册表真实状态
        if (!_autoStart.IsConfigured)
            _autoStart.Enable();

        _settings = _settingsService.Load();

        _manager = new EffectManager();
        _ripple = new RippleEffect { Enabled = true };
        _glow = new GlowEffect { Enabled = true };
        ApplySettingsToEffects();
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

        var tray = new TrayIcon(OpenSettings);
        tray.Show();
    }

    private void ApplySettingsToEffects()
    {
        _glow!.Hue = _settings!.Hue;
        _glow.GlowRadius = _settings.GlowRadius;
        _glow.Opacity = _settings.GlowOpacity;
        _glow.FollowSpeed = _settings.FollowSpeed;
        _ripple!.Hue = _settings.Hue;
        _ripple.MaxRadius = _settings.RippleRadius;
    }

    /// <summary>打开设置窗口（单例，已开则激活；关闭后下次重建）。</summary>
    private void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(_settings!, _glow!, _ripple!, _autoStart, _settingsService);
            // WPF 窗口关闭后不能重新 Show()，关闭时释放引用以便下次重建
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        if (_settingsWindow.IsVisible)
            _settingsWindow.Activate();
        else
            _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        base.OnExit(e);
    }
}
