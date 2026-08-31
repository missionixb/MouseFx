namespace MouseFx.Settings;

/// <summary>点击波纹扩散的形状。枚举顺序与设置界面下拉框一致（Circle=0…）。</summary>
public enum RippleShape
{
    Circle,
    Heart,
    Star,
}

/// <summary>特效模式（三选一，互斥；后续新特效在此扩展）。枚举顺序与设置界面下拉框一致。</summary>
public enum EffectMode
{
    /// <summary>经典组合：常驻光晕 + 点击波纹。</summary>
    Classic,
    /// <summary>火花：跟随鼠标运动方向的拖尾迸射，颜色可调。</summary>
    Spark,
    /// <summary>仙女棒：以鼠标为中心 360° 爆发，颜色固定。</summary>
    Sparkler,
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

    /// <summary>特效模式（三选一；旧设置文件缺省时按旧开关字段推导，推导不出则 Classic）。</summary>
    public EffectMode EffectMode { get; set; } = EffectMode.Classic;

    /// <summary>旧版开关字段，现由 EffectMode 统一驱动，仅作旧文件读取兼容与新文件回写（旧版程序可读）。</summary>
    public bool RippleEnabled { get; set; } = true;

    /// <summary>旧版开关字段，同上。</summary>
    public bool GlowEnabled { get; set; } = true;

    /// <summary>旧版开关字段，同上。</summary>
    public bool SparkEnabled { get; set; }

    /// <summary>鼠标静止（或输入断流）2 秒后特效是否淡出消失（持久化；作用于光晕/火花/仙女棒）。</summary>
    public bool IdleFade { get; set; } = true;

    /// <summary>前台强制全屏（游戏）时特效是否自动淡出隐藏，退出全屏后恢复（持久化）。</summary>
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>火花主色色相（0-360），与经典特效（光圈+点击）的颜色分开保存。默认橙金。</summary>
    public double SparkHue { get; set; } = 30;

    /// <summary>火花粒子上限（颗，50~600），超出回收最早发射的。</summary>
    public int SparkCount { get; set; } = 250;

    /// <summary>火花最长寿命（秒，0.4~2.5）；实际寿命在 0.4 秒与该值之间随机。默认 0.9（现状）。</summary>
    public double SparkLife { get; set; } = 0.9;

    /// <summary>仙女棒粒子上限（颗，100~800），超出回收最早发射的。</summary>
    public int SparklerCount { get; set; } = 200;

    /// <summary>仙女棒星芒直径（px，80~500），火星初速与线长随之缩放。</summary>
    public double SparklerSize { get; set; } = 150;

    /// <summary>光晕跟随指数系数 k（/s），越大越跟手。</summary>
    public double FollowSpeed { get; set; } = 50;

    public static AppSettings CreateDefault() => new();
}
