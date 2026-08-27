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
