namespace MouseFx.Settings;

/// <summary>一种特效模式的展示元数据。</summary>
/// <param name="Mode">模式枚举值（持久化键）。</param>
/// <param name="NameKey">显示名字符串资源键（Strings.zh/en.xaml，随界面语言切换）。</param>
/// <param name="DescriptionKey">说明字符串资源键（保留元数据；当前界面不再展示）。</param>
public sealed record EffectModeInfo(EffectMode Mode, string NameKey, string DescriptionKey)
{
    /// <summary>设置界面显示名（经 L10n 实时解析；切换语言后需重建 ItemsSource 刷新）。</summary>
    public string DisplayName => L10n.T(NameKey);

    /// <summary>模式说明（经 L10n 实时解析）。</summary>
    public string Description => L10n.T(DescriptionKey);
}

/// <summary>
/// 特效模式注册表——模式名称与描述的单一来源，设置界面（分段选择器）由此驱动。
/// 新增模式步骤：
/// ① AppSettings.EffectMode 加枚举值；② 在 <see cref="Modes"/> 注册一条元数据；
/// ③ Strings.zh/en.xaml 加对应名称键；④ SettingsWindow.ApplyModeUi 加对应参数面板可见性映射；
/// ⑤ App.ApplyEffectMode 加启用映射。
/// </summary>
public static class EffectModeRegistry
{
    public static readonly IReadOnlyList<EffectModeInfo> Modes = new[]
    {
        new EffectModeInfo(EffectMode.Classic, "Str.Mode.Halo", "Str.Mode.Halo.Desc"),
        new EffectModeInfo(EffectMode.Spark, "Str.Mode.Spark", "Str.Mode.Spark.Desc"),
        new EffectModeInfo(EffectMode.Sparkler, "Str.Mode.Sparkler", "Str.Mode.Sparkler.Desc"),
    };

    public static string DescriptionOf(EffectMode mode)
        => Modes.FirstOrDefault(m => m.Mode == mode)?.Description ?? string.Empty;

    public static string DisplayNameOf(EffectMode mode)
        => Modes.FirstOrDefault(m => m.Mode == mode)?.DisplayName ?? mode.ToString();
}
