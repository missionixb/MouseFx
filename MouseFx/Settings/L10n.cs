using System.Windows;

namespace MouseFx.Settings;

/// <summary>
/// 界面语言（zh/en）的唯一入口。字符串资源放 Styles/Strings.{zh|en}.xaml，
/// 键名两本一致；运行时整本替换合并字典（模式与 Tokens 亮暗字典相同），
/// XAML 侧用 DynamicResource 自动刷新，代码侧经 <see cref="T"/>/<see cref="Fmt"/> 取值。
/// </summary>
public static class L10n
{
    public const string Zh = "zh";
    public const string En = "en";

    /// <summary>当前语言（归一化后；未知值一律回退中文）。</summary>
    public static string Current { get; private set; } = Zh;

    /// <summary>语言切换后触发。订阅方：托盘菜单（更新菜单文字）、设置窗口（刷新动态文本）。</summary>
    public static event Action? LanguageChanged;

    /// <summary>语言归一化（纯函数，可测）：只认 "en"，其余（含 null/未知）回退中文。</summary>
    public static string Normalize(string? lang) => lang == En ? En : Zh;

    /// <summary>取字符串资源；缺失时返回键名（不抛异常，漏翻一眼可见）。</summary>
    public static string T(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>取格式字符串并填充（如 "Str.Fmt.Count" + 250 → "250 颗"）。</summary>
    public static string Fmt(string key, object arg) => string.Format(T(key), arg);

    /// <summary>应用语言：整本替换 Strings 字典并广播变更。重复设置同一语言为空操作。</summary>
    public static void Apply(string? lang)
    {
        var target = Normalize(lang);
        if (target == Current) return;

        if (Application.Current is { } app)
        {
            var md = app.Resources.MergedDictionaries;
            var fresh = new ResourceDictionary
            {
                Source = new Uri($"Styles/Strings.{target}.xaml", UriKind.Relative)
            };
            var old = md.FirstOrDefault(d => d.Source?.OriginalString.Contains("Strings.") == true);
            int index = old != null ? md.IndexOf(old) : md.Count;
            if (old != null) md.Remove(old);
            md.Insert(index, fresh);
        }
        Current = target;
        LanguageChanged?.Invoke();
    }
}
