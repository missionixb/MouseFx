using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MouseFx.Effects;

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
    private Matrix? _transformFromDevice;
    private DateTime _lastFrame;
    private RenderMode _lastRenderMode = RenderMode.Default;

    public OverlayWindow(EffectManager manager)
    {
        _manager = manager;
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

        _manager.UpdateAll(delta);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        // 透明窗口每帧必须重画全部内容：先清屏再画特效
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        _manager.DrawAll(dc);
    }
}
