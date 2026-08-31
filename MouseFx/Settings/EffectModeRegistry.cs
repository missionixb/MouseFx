namespace MouseFx.Settings;

/// <summary>一种特效模式的展示元数据。</summary>
/// <param name="Mode">模式枚举值（持久化键）。</param>
/// <param name="DisplayName">设置界面显示名。</param>
/// <param name="Description">模式说明（设置界面动态展示）。</param>
public sealed record EffectModeInfo(EffectMode Mode, string DisplayName, string Description);

/// <summary>
/// 特效模式注册表——模式名称与描述的单一来源，设置界面（分段选择器/说明文字）由此驱动。
/// 新增模式步骤：
/// ① AppSettings.EffectMode 加枚举值；② 在 <see cref="Modes"/> 注册一条元数据；
/// ③ SettingsWindow.ApplyModeUi 加对应参数卡片可见性映射；④ App.ApplyEffectMode 加启用映射。
/// </summary>
public static class EffectModeRegistry
{
    public static readonly IReadOnlyList<EffectModeInfo> Modes = new[]
    {
        new EffectModeInfo(EffectMode.Classic, "光圈", "柔和光晕跟随光标，点击时荡开涟漪"),
        new EffectModeInfo(EffectMode.Spark, "火屑", "细小的火星沿轨迹迸落，带着重力下坠"),
        new EffectModeInfo(EffectMode.Sparkler, "烟花", "星芒自光标向四周绽放，像手持烟花"),
    };

    public static string DescriptionOf(EffectMode mode)
        => Modes.FirstOrDefault(m => m.Mode == mode)?.Description ?? string.Empty;

    public static string DisplayNameOf(EffectMode mode)
        => Modes.FirstOrDefault(m => m.Mode == mode)?.DisplayName ?? mode.ToString();
}
