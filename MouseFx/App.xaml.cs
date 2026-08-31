using System.Windows;
using System.Windows.Media;
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
    private SparkEffect? _spark;
    private SparklerEffect? _sparkler;

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
        _spark = new SparkEffect();
        _sparkler = new SparklerEffect();
        ApplySettingsToEffects();
        if (RenderCapability.Tier < 2)
            ApplySoftwareDensity(); // 软件渲染时限制粒子密度
        _manager.Register(_ripple);
        _manager.Register(_glow);
        _manager.Register(_spark);
        _manager.Register(_sparkler); // 最后注册 → 绘制在最上层

        // 渲染降级/恢复时同步调整粒子特效密度
        _overlay = new OverlayWindow(_manager, software =>
        {
            if (software) ApplySoftwareDensity();
            else
            {
                _spark!.PoolLimit = _settings!.SparkCount;
                _sparkler!.PoolLimit = _settings!.SparklerCount;
            }
        });
        _overlay.FadeOnFullscreen = _settings!.HideOnFullscreen;
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
        _ripple.Shape = _settings.RippleShape;
        _spark!.Hue = _settings.SparkHue;   // 火花颜色与经典特效分开
        _spark.PoolLimit = _settings.SparkCount;
        _spark.MaxLife = _settings.SparkLife;
        _sparkler!.PoolLimit = _settings.SparklerCount;
        _sparkler.Size = _settings.SparklerSize;
        _glow.IdleFade = _settings.IdleFade;
        _spark.IdleFade = _settings.IdleFade;
        _sparkler.IdleFade = _settings.IdleFade;
        ApplyEffectMode(_settings.EffectMode);
    }

    /// <summary>软件渲染时限制粒子特效密度（避免 CPU 光栅化过载）。</summary>
    private void ApplySoftwareDensity()
    {
        _spark!.PoolLimit = Math.Min(_settings!.SparkCount, 120);
        _sparkler!.PoolLimit = Math.Min(_settings!.SparklerCount, 200);
    }

    /// <summary>应用特效模式：同一时刻只启用一种（经典组合 / 火花 / 仙女棒），并同步旧开关字段。</summary>
    private void ApplyEffectMode(EffectMode mode)
    {
        _settings!.EffectMode = mode;
        _settings.RippleEnabled = mode == EffectMode.Classic;
        _settings.GlowEnabled = mode == EffectMode.Classic;
        _settings.SparkEnabled = mode == EffectMode.Spark;
        _ripple!.Enabled = mode == EffectMode.Classic;
        _glow!.Enabled = mode == EffectMode.Classic;
        _spark!.Enabled = mode == EffectMode.Spark;
        _sparkler!.Enabled = mode == EffectMode.Sparkler;
    }

    /// <summary>打开设置窗口（单例，已开则激活；关闭后下次重建）。</summary>
    private void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(_settings!, _glow!, _ripple!, _spark!, _sparkler!,
                _overlay!, _autoStart, _settingsService);
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
