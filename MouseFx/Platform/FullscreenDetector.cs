using System.Runtime.InteropServices;
using System.Windows;

namespace MouseFx.Platform;

/// <summary>
/// 前台"强制全屏"检测：无边框窗口铺满整台显示器（游戏全屏 / 无边框全屏窗口化）。
/// 与普通窗口最大化铺满的区分依据：
/// ① 最大化窗口 IsZoomed=true，直接排除；
/// ② 最大化只覆盖工作区，本判定要求矩形等于整个显示器矩形（含任务栏区域）；
/// ③ 要求窗口样式不含标题栏（WS_CAPTION）与可调边框（WS_THICKFRAME）；
/// ④ 排除被 DWM 遮蔽（cloaked）的窗口（如后台 UWP）。
/// </summary>
public sealed class FullscreenDetector
{
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint DWMWA_CLOAKED = 14;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    public bool IsForegroundFullscreen()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        // 桌面宿主窗口（Progman/WorkerW）恰好满足全部全屏条件：铺满整屏、
        // 无标题栏、无边框——左键点桌面会被误判为全屏游戏。按类名排除。
        var className = new System.Text.StringBuilder(256);
        if (GetClassName(fg, className, className.Capacity) > 0 && IsDesktopWindow(className.ToString()))
            return false;

        if (!GetWindowRect(fg, out RECT r)) return false;

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        IntPtr hMonitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        if (!GetMonitorInfo(hMonitor, ref monitorInfo)) return false;

        DwmGetWindowAttribute(fg, DWMWA_CLOAKED, out int cloaked, sizeof(int)); // 失败按 0 处理

        return IsFullscreen(
            new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
            new Rect(monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top,
                     monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                     monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top),
            GetWindowLong(fg, GWL_STYLE),
            IsZoomed(fg),
            cloaked != 0);
    }

    /// <summary>纯判定逻辑（可测）：铺满整屏 + 无标题栏/可调边框 + 非最大化 + 未被遮蔽。</summary>
    public static bool IsFullscreen(Rect windowRect, Rect monitorRect, int style, bool zoomed, bool cloaked)
        => !zoomed && !cloaked
           && windowRect == monitorRect
           && (style & WS_CAPTION) == 0
           && (style & WS_THICKFRAME) == 0;

    /// <summary>桌面宿主窗口类名（Progman 常规桌面；WorkerW 出现于壁纸引擎/幻灯片壁纸等场景）。</summary>
    public static bool IsDesktopWindow(string className)
        => className is "Progman" or "WorkerW";

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
