namespace MouseFx.Settings;

/// <summary>点击波纹扩散的形状。枚举顺序与设置界面下拉框一致（Circle=0…）。</summary>
public enum RippleShape
{
    Circle,
    Heart,
    Star,
}

/// <summary>用户可配置参数（可序列化，全部带默认值）。</summary>
public sealed class AppSettings
{
    /// <summary>主题色色相（0-360），光晕与波纹共用。</summary>
    public double Hue { get; set; } = 210;

    /// <summary>静止光晕半径（px）。</summary>
    public double GlowRadius { get; set; } = 28;

    /// <summary>光晕中心不透明度（0-1）。</summary>
    public double GlowOpacity { get; set; } = 0.35;

    /// <summary>点击波纹最大扩散半径（px）。</summary>
    public double RippleRadius { get; set; } = 60;

    /// <summary>点击波纹扩散形状。</summary>
    public RippleShape RippleShape { get; set; } = RippleShape.Circle;

    /// <summary>光晕跟随指数系数 k（/s），越大越跟手。</summary>
    public double FollowSpeed { get; set; } = 50;

    public static AppSettings CreateDefault() => new();
}
