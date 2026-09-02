using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
    private TrayIcon? _tray;
    private RippleEffect? _ripple;
    private GlowEffect? _glow;
    private SparkEffect? _spark;
    private SparklerEffect? _sparkler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplySystemTheme(); // 跟随系统亮暗主题（含 token 字典整本替换）
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += (theme, _) => ApplyThemeTokens(theme);

        // 首次运行启用自启动；之后自愈（启动项被删/指向旧 exe 时自动重写；用户显式停用则不动）
        _autoStart.EnsureRegistered();

        _settings = _settingsService.Load();
        L10n.Apply(_settings.Language); // 界面语言：托盘/设置窗口/对话框文本的字典在此之前必须就位

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
                _spark.BurstScale = 1.0;
                _sparkler.BurstScale = 1.0;
            }
        });
        _overlay.FadeOnFullscreen = _settings!.HideOnFullscreen;
        _overlay.TargetFps = _settings.RenderFps;
        _overlay.Show();
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(_overlay); // 系统切换亮暗时自动应用（触发上面的 token 替换）

        _hook = new MouseHook();
        // 移动事件合并：钩子线程只写最新坐标，每个 UI 拍最多消费一次（此前每条
        // 钩子事件一次 BeginInvoke，高轮询鼠标下每秒数千次调度 + 堆分配，且
        // UI 忙时处理的全是过期位置）
        var movePump = new MoveCoalescer(devicePoint => _manager!.HandleMouseMove(_overlay!.ToLocal(devicePoint)));
        var drainMove = new Action(movePump.Drain);
        _hook.MouseMove += devicePoint =>
        {
            if (movePump.Push(devicePoint))
                Dispatcher.BeginInvoke(drainMove);
        };
        _hook.MouseDown += devicePoint => Dispatcher.BeginInvoke(
            () => _manager!.HandleMouseDown(_overlay!.ToLocal(devicePoint)));
        try
        {
            _hook.Start();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(L10n.Fmt("Str.HookFailed", ex.Message),
                L10n.T("Str.AppName"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _tray = new TrayIcon(OpenSettings);
        _tray.Show();
    }

    /// <summary>按系统亮暗应用 Fluent 主题（Mica 底）。</summary>
    private void ApplySystemTheme()
    {
        var sys = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme();
        var theme = sys == Wpf.Ui.Appearance.SystemTheme.Dark
            ? Wpf.Ui.Appearance.ApplicationTheme.Dark
            : Wpf.Ui.Appearance.ApplicationTheme.Light;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica);
        ApplyThemeTokens(theme);
    }

    /// <summary>主题变化时整本替换项目 token 字典（Tokens.Light/Dark 键名一致，DynamicResource 自动刷新）。</summary>
    private void ApplyThemeTokens(Wpf.Ui.Appearance.ApplicationTheme theme)
    {
        var md = Resources.MergedDictionaries;
        var uri = new Uri($"Styles/Tokens.{(theme == Wpf.Ui.Appearance.ApplicationTheme.Dark ? "Dark" : "Light")}.xaml", UriKind.Relative);
        var fresh = new ResourceDictionary { Source = uri };
        var old = md.FirstOrDefault(d => d.Source?.OriginalString.Contains("Tokens.") == true);
        int index = old != null ? md.IndexOf(old) : md.Count;
        if (old != null) md.Remove(old);
        md.Insert(index, fresh);
    }

    private void ApplySettingsToEffects()
    {
        var s = _settings!; // 唯一接线点：每张参数卡片只写自己的特效（串扰历史 bug 见 EffectSettingsApplier 注释）
        EffectSettingsApplier.ApplyClassic(s, _glow!, _ripple!);
        EffectSettingsApplier.ApplySpark(s, _spark!);
        EffectSettingsApplier.ApplySparkler(s, _sparkler!);
        EffectSettingsApplier.ApplyIdleFade(s, _glow!, _spark!, _sparkler!);
        ApplyEffectMode(s.EffectMode);
    }

    /// <summary>软件渲染时限制粒子特效密度（避免 CPU 光栅化过载），爆炸同步降密度。</summary>
    private void ApplySoftwareDensity()
    {
        _spark!.PoolLimit = Math.Min(_settings!.SparkCount, 120);
        _sparkler!.PoolLimit = Math.Min(_settings!.SparklerCount, 200);
        _spark.BurstScale = 0.5;
        _sparkler.BurstScale = 0.5;
    }

    /// <summary>应用特效模式：同一时刻只启用一种（光圈 / 火屑 / 烟花），并同步旧开关字段。</summary>
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

    /// <summary>打开设置窗口（单例，已开则激活；关闭后下次重建）。保证弹到前台最上层。</summary>
    private void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(_settings!, _glow!, _ripple!, _spark!, _sparkler!,
                _overlay!, _autoStart, _settingsService);
            // WPF 窗口关闭后不能重新 Show()，关闭时释放引用以便下次重建
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        if (_settingsWindow.WindowState == WindowState.Minimized)
            _settingsWindow.WindowState = WindowState.Normal;
        if (_settingsWindow.IsVisible)
            _settingsWindow.Activate();
        else
            _settingsWindow.Show();
        BringToForeground(_settingsWindow);
    }

    /// <summary>
    /// 强制窗口到前台。托盘菜单点击后，本进程常已不是前台进程，
    /// WPF 的 Activate() 会被 Windows 前台锁（foreground lock）偶尔拒绝——
    /// 窗口就开在了其他程序下层。先模拟一次 Alt 释放前台锁再 SetForegroundWindow（社区标准做法）。
    /// </summary>
    private static void BringToForeground(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || GetForegroundWindow() == hwnd) return;

        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const int VK_MENU = 0x12;        // Alt
    private const int KEYEVENTF_KEYUP = 0x0002;

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose(); // 退订 L10n 语言事件并移除托盘图标（此前靠进程退出兜底）
        _hook?.Dispose();
        base.OnExit(e);
    }
}
