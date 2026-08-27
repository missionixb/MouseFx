using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MouseFx.Effects;
using MouseFx.Platform;
// 注意：Color 已由全局别名指向 System.Windows.Media.Color，这里需要 Drawing 的 Color 时全限定

namespace MouseFx.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _rippleItem;
    private readonly ToolStripMenuItem _glowItem;
    private readonly ToolStripMenuItem _autoStartItem;

    public TrayIcon(EffectManager manager, RippleEffect ripple, GlowEffect glow, IAutoStartService autoStart, Action openSettings)
    {
        _rippleItem = new ToolStripMenuItem("点击波纹") { CheckOnClick = true, Checked = ripple.Enabled };
        _glowItem = new ToolStripMenuItem("常驻光晕") { CheckOnClick = true, Checked = glow.Enabled };
        _autoStartItem = new ToolStripMenuItem("开机自启动") { CheckOnClick = true, Checked = autoStart.IsEnabled };
        _rippleItem.CheckedChanged += (_, _) => ripple.Enabled = _rippleItem.Checked;
        _glowItem.CheckedChanged += (_, _) => glow.Enabled = _glowItem.Checked;
        _autoStartItem.CheckedChanged += (_, _) =>
        {
            if (_autoStartItem.Checked) autoStart.Enable();
            else autoStart.Disable();
        };

        var settingsItem = new ToolStripMenuItem("设置…");
        settingsItem.Click += (_, _) => openSettings();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_rippleItem);
        menu.Items.Add(_glowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
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
        // 双击托盘图标：全部特效开关取反（快速总开关）
        _icon.DoubleClick += (_, _) =>
        {
            bool anyEnabled = _rippleItem.Checked || _glowItem.Checked;
            _rippleItem.Checked = !anyEnabled;
            _glowItem.Checked = !anyEnabled;
        };
    }

    public void Show() { /* 已在构造函数中置 Visible=true */ }

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
