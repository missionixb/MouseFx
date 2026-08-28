using System.Windows;
using System.Windows.Media;
using MouseFx.Effects;
using MouseFx.Platform;

namespace MouseFx.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly GlowEffect _glow;
    private readonly RippleEffect _ripple;
    private readonly SparkEffect _spark;
    private readonly SparklerEffect _sparkler;
    private readonly SettingsService _service;

    public SettingsWindow(AppSettings settings, GlowEffect glow, RippleEffect ripple, SparkEffect spark,
        SparklerEffect sparkler, IAutoStartService autoStart, SettingsService service)
    {
        _settings = settings;
        _glow = glow;
        _ripple = ripple;
        _spark = spark;
        _sparkler = sparkler;
        _service = service;
        InitializeComponent();

        // 先赋值（此时事件未挂接，不会触发），再挂事件，最后统一同步一次
        HueSlider.Value = settings.Hue;
        RadiusSlider.Value = settings.GlowRadius;
        OpacitySlider.Value = settings.GlowOpacity;
        RippleSlider.Value = settings.RippleRadius;
        SpeedSlider.Value = settings.FollowSpeed;
        SparkHueSlider.Value = settings.SparkHue;
        SparkCountSlider.Value = settings.SparkCount;
        SparklerCountSlider.Value = settings.SparklerCount;
        EffectModeBox.SelectedIndex = (int)settings.EffectMode;
        IdleFadeToggle.IsChecked = settings.IdleFade;
        AutoStartToggle.IsChecked = autoStart.IsEnabled;
        RippleShapeBox.SelectedIndex = (int)settings.RippleShape;

        HueSlider.ValueChanged += (_, _) => ApplyAll();
        RadiusSlider.ValueChanged += (_, _) => ApplyAll();
        OpacitySlider.ValueChanged += (_, _) => ApplyAll();
        RippleSlider.ValueChanged += (_, _) => ApplyAll();
        SpeedSlider.ValueChanged += (_, _) => ApplyAll();
        RippleShapeBox.SelectionChanged += (_, _) => ApplyAll();
        SparkHueSlider.ValueChanged += (_, _) => ApplySparkAll();
        SparkCountSlider.ValueChanged += (_, _) => ApplySparkAll();
        SparklerCountSlider.ValueChanged += (_, _) => ApplySparklerAll();
        ClassicResetButton.Click += (_, _) =>
        {
            // 默认值取自 CreateDefault()（初版值：色相 210° 蓝、光圈 28px）；滑块赋值
            // 会触发 ValueChanged → ApplyAll，即时生效并保存
            var d = AppSettings.CreateDefault();
            HueSlider.Value = d.Hue;
            RadiusSlider.Value = d.GlowRadius;
            OpacitySlider.Value = d.GlowOpacity;
            RippleSlider.Value = d.RippleRadius;
            RippleShapeBox.SelectedIndex = (int)d.RippleShape;
            SpeedSlider.Value = d.FollowSpeed;
        };
        SparkResetButton.Click += (_, _) =>
        {
            var d = AppSettings.CreateDefault();
            SparkHueSlider.Value = d.SparkHue; // 火焰色（橙金 30°）
            SparkCountSlider.Value = d.SparkCount;
        };
        SparklerResetButton.Click += (_, _) =>
            SparklerCountSlider.Value = AppSettings.CreateDefault().SparklerCount;
        EffectModeBox.SelectionChanged += (_, _) =>
            ApplyMode((EffectMode)EffectModeBox.SelectedIndex);
        IdleFadeToggle.Click += (_, _) =>
        {
            _glow.IdleFade = IdleFadeToggle.IsChecked == true;
            _spark.IdleFade = _glow.IdleFade;
            _sparkler.IdleFade = _glow.IdleFade;
            _settings.IdleFade = _glow.IdleFade;
            _service.Save(_settings);
        };
        AutoStartToggle.Click += (_, _) =>
        {
            if (AutoStartToggle.IsChecked == true) autoStart.Enable();
            else autoStart.Disable();
        };
        ApplyAll();
        ApplySparkAll();
        ApplySparklerAll();
    }

    /// <summary>把滑块值同步到设置、特效并保存（改动即生效即保存）。</summary>
    private void ApplyAll()
    {
        _settings.Hue = HueSlider.Value;
        _settings.GlowRadius = RadiusSlider.Value;
        _settings.GlowOpacity = OpacitySlider.Value;
        _settings.RippleRadius = RippleSlider.Value;
        _settings.RippleShape = (RippleShape)RippleShapeBox.SelectedIndex;
        _settings.FollowSpeed = SpeedSlider.Value;

        _glow.Hue = _settings.Hue;
        _glow.GlowRadius = _settings.GlowRadius;
        _glow.Opacity = _settings.GlowOpacity;
        _glow.FollowSpeed = _settings.FollowSpeed;
        _ripple.Hue = _settings.Hue;
        _ripple.MaxRadius = _settings.RippleRadius;
        _ripple.Shape = _settings.RippleShape;
        _spark.Hue = _settings.Hue;

        ColorPreview.Fill = new SolidColorBrush(ColorUtils.FromHue(_settings.Hue));
        HueValue.Text = $"{HueSlider.Value:0}°";
        RadiusValue.Text = $"{RadiusSlider.Value:0} px";
        OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        RippleValue.Text = $"{RippleSlider.Value:0} px";
        SpeedValue.Text = $"{SpeedSlider.Value:0}";

        _service.Save(_settings);
    }

    /// <summary>
    /// 切换特效模式：同一时刻只启用一种（经典组合 / 火花 / 仙女棒），
    /// 同步旧开关字段（旧版程序仍能正确读取）并保存。
    /// </summary>
    private void ApplyMode(EffectMode mode)
    {
        _settings.EffectMode = mode;
        _ripple.Enabled = mode == EffectMode.Classic;
        _glow.Enabled = mode == EffectMode.Classic;
        _spark.Enabled = mode == EffectMode.Spark;
        _sparkler.Enabled = mode == EffectMode.Sparkler;
        _settings.RippleEnabled = _ripple.Enabled;
        _settings.GlowEnabled = _glow.Enabled;
        _settings.SparkEnabled = _spark.Enabled;
        ApplyModeUi(mode);
        _service.Save(_settings);
    }

    /// <summary>
    /// 模式对应的界面状态：只显示当前模式的参数面板，窗口高度随之收缩，
    /// 避免大片空白。
    /// </summary>
    private void ApplyModeUi(EffectMode mode)
    {
        ClassicPanel.Visibility = mode == EffectMode.Classic ? Visibility.Visible : Visibility.Collapsed;
        SparkPanel.Visibility = mode == EffectMode.Spark ? Visibility.Visible : Visibility.Collapsed;
        SparklerPanel.Visibility = mode == EffectMode.Sparkler ? Visibility.Visible : Visibility.Collapsed;
        Height = mode switch
        {
            EffectMode.Classic => 548,
            EffectMode.Spark => 365,
            _ => 330,
        };
    }

    /// <summary>火花参数：独立颜色 + 粒子上限，同步到设置与特效并保存。</summary>
    private void ApplySparkAll()
    {
        _settings.SparkHue = SparkHueSlider.Value;
        _settings.SparkCount = (int)Math.Round(SparkCountSlider.Value);
        _spark.Hue = _settings.SparkHue;
        _spark.PoolLimit = _settings.SparkCount;

        SparkColorPreview.Fill = new SolidColorBrush(ColorUtils.FromHue(_settings.SparkHue));
        SparkHueValue.Text = $"{SparkHueSlider.Value:0}°";
        SparkCountValue.Text = $"{SparkCountSlider.Value:0} 颗";

        _service.Save(_settings);
    }

    /// <summary>仙女棒参数：粒子上限（颜色固定不可调），同步到设置与特效并保存。</summary>
    private void ApplySparklerAll()
    {
        _settings.SparklerCount = (int)Math.Round(SparklerCountSlider.Value);
        _sparkler.PoolLimit = _settings.SparklerCount;

        SparklerCountValue.Text = $"{SparklerCountSlider.Value:0} 颗";

        _service.Save(_settings);
    }
}
