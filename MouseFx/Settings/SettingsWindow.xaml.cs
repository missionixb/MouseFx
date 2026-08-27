using System.Windows;
using System.Windows.Media;
using MouseFx.Effects;

namespace MouseFx.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly GlowEffect _glow;
    private readonly RippleEffect _ripple;
    private readonly SettingsService _service;

    public SettingsWindow(AppSettings settings, GlowEffect glow, RippleEffect ripple, SettingsService service)
    {
        _settings = settings;
        _glow = glow;
        _ripple = ripple;
        _service = service;
        InitializeComponent();

        // 先赋值（此时事件未挂接，不会触发 ApplyAll），再挂事件，最后统一同步一次
        HueSlider.Value = settings.Hue;
        RadiusSlider.Value = settings.GlowRadius;
        OpacitySlider.Value = settings.GlowOpacity;
        RippleSlider.Value = settings.RippleRadius;
        SpeedSlider.Value = settings.FollowSpeed;

        HueSlider.ValueChanged += (_, _) => ApplyAll();
        RadiusSlider.ValueChanged += (_, _) => ApplyAll();
        OpacitySlider.ValueChanged += (_, _) => ApplyAll();
        RippleSlider.ValueChanged += (_, _) => ApplyAll();
        SpeedSlider.ValueChanged += (_, _) => ApplyAll();
        ApplyAll();
    }

    /// <summary>把滑块值同步到设置、特效并保存（改动即生效即保存）。</summary>
    private void ApplyAll()
    {
        _settings.Hue = HueSlider.Value;
        _settings.GlowRadius = RadiusSlider.Value;
        _settings.GlowOpacity = OpacitySlider.Value;
        _settings.RippleRadius = RippleSlider.Value;
        _settings.FollowSpeed = SpeedSlider.Value;

        _glow.Hue = _settings.Hue;
        _glow.GlowRadius = _settings.GlowRadius;
        _glow.Opacity = _settings.GlowOpacity;
        _glow.FollowSpeed = _settings.FollowSpeed;
        _ripple.Hue = _settings.Hue;
        _ripple.MaxRadius = _settings.RippleRadius;

        ColorPreview.Fill = new SolidColorBrush(ColorUtils.FromHue(_settings.Hue));
        HueValue.Text = $"{HueSlider.Value:0}°";
        RadiusValue.Text = $"{RadiusSlider.Value:0} px";
        OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        RippleValue.Text = $"{RippleSlider.Value:0} px";
        SpeedValue.Text = $"{SpeedSlider.Value:0}";

        _service.Save(_settings);
    }
}
