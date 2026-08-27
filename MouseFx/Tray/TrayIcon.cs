using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
// 注意：Color 已由全局别名指向 System.Windows.Media.Color，这里需要 Drawing 的 Color 时全限定

namespace MouseFx.Tray;

/// <summary>
/// 系统托盘图标。左键/右键都弹出同一菜单；菜单只含「设置…」与「退出」。
/// 特效开关与开机自启动均已收纳到设置窗口。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action openSettings)
    {
        var settingsItem = new ToolStripMenuItem("设置…");
        settingsItem.Click += (_, _) => openSettings();

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "鼠标特效",
            ContextMenuStrip = menu,
            Visible = true,
        };
        // 左键单击与右键一致：弹出同一菜单
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowContextMenu();
        };
    }

    public void Show() { /* 已在构造函数中置 Visible=true */ }

    /// <summary>弹出托盘菜单（NotifyIcon.ShowContextMenu 是私有方法，经反射调用）。</summary>
    private void ShowContextMenu()
    {
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_icon, null);
    }

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, 16, 16), System.Drawing.Color.SkyBlue, System.Drawing.Color.DodgerBlue, 45f);
            g.FillEllipse(brush, 1, 1, 14, 14);
        }
        IntPtr hIcon = bmp.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        var icon = (Icon)temp.Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
