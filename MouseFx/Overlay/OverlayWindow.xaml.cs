using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MouseFx.Effects;
using MouseFx.Platform;

namespace MouseFx.Overlay;

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly EffectManager _manager;
    private readonly Action<bool>? _onRenderModeChanged;
    private readonly FullscreenDetector _fullscreenDetector = new();
    private Matrix? _transformFromDevice;
    private DateTime _lastFrame;
    private DateTime _lastFullscreenCheck = DateTime.MinValue;
    private bool _isForegroundFullscreen;
    private double _fullscreenFade = 1;   // 全屏淡出系数（1=正常，0=完全隐藏）
    private RenderMode _lastRenderMode = RenderMode.Default;

    private static readonly TimeSpan FullscreenCheckInterval = TimeSpan.FromMilliseconds(200);
    private const double FullscreenFadeSeconds = 0.4;

    /// <summary>前台强制全屏（游戏）时自动隐藏特效；退出全屏后恢复。</summary>
    public bool FadeOnFullscreen { get; set; } = true;

    /// <param name="manager">特效管理器。</param>
    /// <param name="onRenderModeChanged">渲染模式切换回调（参数：是否软件渲染），供特效调整密度。</param>
    public OverlayWindow(EffectManager manager, Action<bool>? onRenderModeChanged = null)
    {
        _manager = manager;
        _onRenderModeChanged = onRenderModeChanged;
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => MakeClickThrough();
        CompositionTarget.Rendering += OnRendering;
        RenderCapability.TierChanged += OnTierChanged;
    }

    /// <summary>
    /// 渲染降级监控：硬件加速丢失（Tier &lt; 2）时自动切软件渲染并记录日志。
    /// 软件渲染的透明窗口合成更稳定，可规避与其他 GPU 应用切换时的灰色合成异常。
    /// </summary>
    private void OnTierChanged(object sender, EventArgs e)
    {
        bool software = RenderCapability.Tier < 2;
        var mode = software ? RenderMode.SoftwareOnly : RenderMode.Default;
        if (mode == _lastRenderMode) return;
        _lastRenderMode = mode;
        RenderOptions.ProcessRenderMode = mode;
        LogRenderMode($"TierChanged → Tier={RenderCapability.Tier}，切换为{(software ? "软件渲染" : "硬件加速")}");
        _onRenderModeChanged?.Invoke(software);
    }

    private static void LogRenderMode(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseFx");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "render.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}\n");
        }
        catch
        {
            // 日志失败不影响功能
        }
    }

    private void MakeClickThrough()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE);
    }

    /// <summary>把钩子的屏幕物理像素坐标转换为窗口本地 DIP 坐标（多显示器 + DPI 缩放正确）。</summary>
    public Point ToLocal(Point devicePoint)
    {
        _transformFromDevice ??= PresentationSource.FromVisual(this)!.CompositionTarget.TransformFromDevice;
        return _transformFromDevice.Value.Transform(devicePoint);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var delta = _lastFrame == default ? TimeSpan.Zero : now - _lastFrame;
        _lastFrame = now;

        // 强制全屏检测（节流 200ms）：全屏游戏时特效整体淡出，退出后恢复
        if (FadeOnFullscreen && now - _lastFullscreenCheck >= FullscreenCheckInterval)
        {
            _lastFullscreenCheck = now;
            _isForegroundFullscreen = _fullscreenDetector.IsForegroundFullscreen();
        }
        double target = !FadeOnFullscreen || !_isForegroundFullscreen ? 1 : 0;
        double maxStep = delta.TotalSeconds / FullscreenFadeSeconds;
        _fullscreenFade += Math.Clamp(target - _fullscreenFade, -maxStep, maxStep);

        _manager.UpdateAll(delta);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        // 透明窗口每帧必须重画全部内容：先清屏再画特效
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (_fullscreenFade <= 0) return; // 全屏隐藏中，只清屏
        if (_fullscreenFade < 1) dc.PushOpacity(_fullscreenFade);
        _manager.DrawAll(dc);
        if (_fullscreenFade < 1) dc.Pop();
    }
}
