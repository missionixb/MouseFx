using System.Drawing;
using System.Reflection;
using System.Windows.Forms; // NotifyIcon：WPF 没有托盘组件，用 WinForms 只做"图标宿主"，菜单本身是 WPF 的
using MouseFx.Settings;
using Wpf.Ui.Controls;

// 注意：Color 已由全局别名指向 System.Windows.Media.Color，这里需要 Drawing 的 Color 时全限定

namespace MouseFx.Tray;

/// <summary>
/// 系统托盘图标。左键/右键都弹出同一菜单；菜单只含「设置」与「退出」。
/// 菜单为 WPF ContextMenu，由 WPF-UI 的 Fluent 隐式样式渲染，跟随应用亮暗主题；
/// 文本经 L10n 取值，界面语言切换时即时更新。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Controls.ContextMenu _menu;
    private readonly System.Windows.Controls.MenuItem _settingsItem;
    private readonly System.Windows.Controls.MenuItem _exitItem;

    public TrayIcon(Action openSettings)
    {
        _settingsItem = new System.Windows.Controls.MenuItem
        {
            Header = L10n.T("Str.Tray.Settings"),
            Icon = new SymbolIcon { Symbol = SymbolRegular.Settings20 },
        };
        _settingsItem.Click += (_, _) => openSettings();

        _exitItem = new System.Windows.Controls.MenuItem
        {
            Header = L10n.T("Str.Tray.Exit"),
            Icon = new SymbolIcon { Symbol = SymbolRegular.Power20 },
        };
        _exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        _menu = new System.Windows.Controls.ContextMenu();
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(new System.Windows.Controls.Separator());
        _menu.Items.Add(_exitItem);

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = L10n.T("Str.AppName"),
            Visible = true,
        };
        // 左键与右键一致：在光标处弹出同一 Fluent 菜单
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
                ShowContextMenu();
        };
        L10n.LanguageChanged += UpdateTexts;
    }

    /// <summary>界面语言切换后刷新托盘菜单与悬停提示文字。</summary>
    private void UpdateTexts()
    {
        _settingsItem.Header = L10n.T("Str.Tray.Settings");
        _exitItem.Header = L10n.T("Str.Tray.Exit");
        _icon.Text = L10n.T("Str.AppName");
    }

    public void Show() { /* 已在构造函数中置 Visible=true */ }

    /// <summary>在光标处弹出菜单（托盘没有 WPF 定位锚点，用 MousePoint）。</summary>
    private void ShowContextMenu()
    {
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    /// <summary>加载内嵌的应用图标（app.ico，随 csproj 以 Resource 打包）。</summary>
    private static Icon CreateIcon()
    {
        using var stream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/app.ico")).Stream;
        return new Icon(stream);
    }

    public void Dispose()
    {
        L10n.LanguageChanged -= UpdateTexts;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.IsOpen = false;
    }
}
