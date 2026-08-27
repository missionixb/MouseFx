using System.Diagnostics;
using System.Runtime.InteropServices;
using MouseFx.Platform;

namespace MouseFx.Hooks;

public sealed class MouseHook : IMouseHookService
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int MaxRetries = 3;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private LowLevelMouseProc _proc = null!;
    private IntPtr _hookHandle = IntPtr.Zero;

    public event Action<Point>? MouseMove;
    public event Action<Point>? MouseDown;
    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning) return;

        _proc = Callback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        IntPtr moduleHandle = GetModuleHandle(module!.ModuleName!);

        for (int attempt = 1; ; attempt++)
        {
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);
            if (_hookHandle != IntPtr.Zero) return;
            if (attempt >= MaxRetries)
                throw new InvalidOperationException($"全局鼠标钩子安装失败（错误码 {Marshal.GetLastWin32Error()}）");
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var point = new Point(data.pt.X, data.pt.Y);
                switch ((int)wParam)
                {
                    case WM_MOUSEMOVE: MouseMove?.Invoke(point); break;
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                    case WM_XBUTTONDOWN: MouseDown?.Invoke(point); break;
                }
            }
        }
        catch
        {
            // 回调异常绝不允许逃逸（会崩溃宿主进程）；事件订阅方自行容错
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
