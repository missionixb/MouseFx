using MouseFx.Effects;

namespace MouseFx.Settings;

/// <summary>
/// "AppSettings → 特效实例"的唯一接线点：每张参数卡片（光圈/火屑/烟花）各一个方法，
/// 只写自己模式的特效属性，卡片之间零交叉写入。
/// 历史 bug：设置窗口光圈卡片里遗留 "_spark.Hue = settings.Hue"，光圈颜色覆盖火屑独立颜色，
/// 切换模式后火屑显示光圈的颜色——根因即两处接线不一致，故收敛到此单点。
/// App 启动与设置窗口的所有应用路径都必须经由此类，不允许各自直写特效属性。
/// </summary>
public static class EffectSettingsApplier
{
    /// <summary>光圈卡片参数 → 光晕 + 点击涟漪（两者共用主题色 Hue）。</summary>
    public static void ApplyClassic(AppSettings s, GlowEffect glow, RippleEffect ripple)
    {
        glow.Hue = s.Hue;
        glow.GlowRadius = s.GlowRadius;
        glow.Opacity = s.GlowOpacity;
        glow.FollowSpeed = s.FollowSpeed;
        ripple.Hue = s.Hue;
        ripple.MaxRadius = s.RippleRadius;
        ripple.Shape = s.RippleShape;
        ripple.ClickEnabled = s.RippleClickEnabled;
    }

    /// <summary>火屑卡片参数 → 火屑（颜色独立 SparkHue，与光圈颜色互不影响）。</summary>
    public static void ApplySpark(AppSettings s, SparkEffect spark)
    {
        spark.Hue = s.SparkHue;
        spark.PoolLimit = s.SparkCount;
        spark.MaxLife = s.SparkLife;
        spark.ClickBurstEnabled = s.SparkClickBurst;
    }

    /// <summary>烟花卡片参数 → 烟花（颜色固定，不读用户颜色设置）。</summary>
    public static void ApplySparkler(AppSettings s, SparklerEffect sparkler)
    {
        sparkler.PoolLimit = s.SparklerCount;
        sparkler.Size = s.SparklerSize;
        sparkler.ClickBurstEnabled = s.SparklerClickBurst;
    }

    /// <summary>全局开关：静止淡出（作用于光圈/火屑/烟花）。</summary>
    public static void ApplyIdleFade(AppSettings s, GlowEffect glow, SparkEffect spark, SparklerEffect sparkler)
    {
        glow.IdleFade = s.IdleFade;
        spark.IdleFade = s.IdleFade;
        sparkler.IdleFade = s.IdleFade;
    }
}
