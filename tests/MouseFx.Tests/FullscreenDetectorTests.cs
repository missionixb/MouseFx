using System.Windows;
using MouseFx.Platform;
using Xunit;

namespace MouseFx.Tests;

public class FullscreenDetectorTests
{
    private const int MonitorWidth = 1920;
    private const int MonitorHeight = 1080;
    private static readonly Rect MonitorRect = new(0, 0, MonitorWidth, MonitorHeight);
    private static readonly Rect WorkAreaRect = new(0, 0, MonitorWidth, MonitorHeight - 48); // 任务栏占底部
    private const int StyleBorderless = 0x10000000; // WS_POPUP，无边框
    private const int StyleWithCaption = 0x00C00000 | 0x00040000; // WS_CAPTION | WS_THICKFRAME

    [Fact]
    public void 无边框铺满整台显示器且非最大化_判定为强制全屏()
    {
        Assert.True(FullscreenDetector.IsFullscreen(MonitorRect, MonitorRect, StyleBorderless, zoomed: false, cloaked: false));
    }

    [Fact]
    public void 最大化窗口不算强制全屏()
    {
        // 最大化窗口即使矩形铺满整屏（任务栏自动隐藏的场景）也通过 IsZoomed 排除
        Assert.False(FullscreenDetector.IsFullscreen(MonitorRect, MonitorRect, StyleBorderless, zoomed: true, cloaked: false));
    }

    [Fact]
    public void 带标题栏或可调边框的窗口不算强制全屏()
    {
        Assert.False(FullscreenDetector.IsFullscreen(MonitorRect, MonitorRect, StyleWithCaption, zoomed: false, cloaked: false));
    }

    [Fact]
    public void 只覆盖工作区的普通窗口不算强制全屏()
    {
        // 普通窗口最大化铺满的是工作区（任务栏以外），不是整台显示器
        Assert.False(FullscreenDetector.IsFullscreen(WorkAreaRect, MonitorRect, StyleBorderless, zoomed: false, cloaked: false));
    }

    [Fact]
    public void 被遮蔽的窗口不算强制全屏()
    {
        Assert.False(FullscreenDetector.IsFullscreen(MonitorRect, MonitorRect, StyleBorderless, zoomed: false, cloaked: true));
    }

    [Fact]
    public void 跨屏偏移的窗口不算强制全屏()
    {
        // 窗口在副屏但坐标不是副屏原点（手动拖到半跨两屏的位置）
        var offset = new Rect(100, 0, MonitorWidth, MonitorHeight);
        Assert.False(FullscreenDetector.IsFullscreen(offset, MonitorRect, StyleBorderless, zoomed: false, cloaked: false));
    }

    [Fact]
    public void 桌面宿主窗口类名被识别()
    {
        // Progman 常规桌面；WorkerW 出现于壁纸引擎/幻灯片壁纸场景。
        // 两者恰好满足全部全屏条件，必须在 Win32 查询处短路排除。
        Assert.True(FullscreenDetector.IsDesktopWindow("Progman"));
        Assert.True(FullscreenDetector.IsDesktopWindow("WorkerW"));
    }

    [Fact]
    public void 非桌面窗口类名不误伤()
    {
        Assert.False(FullscreenDetector.IsDesktopWindow("AcGameWnd")); // 全屏游戏
        Assert.False(FullscreenDetector.IsDesktopWindow("Chrome_WidgetWin_1"));
        Assert.False(FullscreenDetector.IsDesktopWindow(""));
    }
}
