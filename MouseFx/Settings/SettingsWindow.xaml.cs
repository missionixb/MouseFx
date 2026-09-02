using System.Windows;
using System.Windows.Media;
using MouseFx.Effects;
using MouseFx.Overlay;
using MouseFx.Platform;

namespace MouseFx.Settings;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppSettings _settings;
    private readonly GlowEffect _glow;
    private readonly RippleEffect _ripple;
    private readonly SparkEffect _spark;
    private readonly SparklerEffect _sparkler;
    private readonly OverlayWindow _overlay;
    private readonly SettingsService _service;
    private readonly DebouncedSaver _saver;

    public SettingsWindow(AppSettings settings, GlowEffect glow, RippleEffect ripple, SparkEffect spark,
        SparklerEffect sparkler, OverlayWindow overlay, IAutoStartService autoStart, SettingsService service)
    {
        _settings = settings;
        _glow = glow;
        _ripple = ripple;
        _spark = spark;
        _sparkler = sparkler;
        _overlay = overlay;
        _service = service;
        _saver = new DebouncedSaver(() => Dispatcher.BeginInvoke(SaveNow));
        InitializeComponent();

        // 先赋值（此时事件未挂接，不会触发），再挂事件，最后统一同步一次
        HueSlider.Value = settings.Hue;
        RadiusSlider.Value = settings.GlowRadius;
        OpacitySlider.Value = settings.GlowOpacity;
        RippleSlider.Value = settings.RippleRadius;
        SpeedSlider.Value = settings.FollowSpeed;
        SparkHueSlider.Value = settings.SparkHue;
        SparkCountSlider.Value = settings.SparkCount;
        SparkLifeSlider.Value = settings.SparkLife;
        SparklerCountSlider.Value = settings.SparklerCount;
        SparklerSizeSlider.Value = settings.SparklerSize;
        SparkBurstToggle.IsChecked = settings.SparkClickBurst;
        SparklerBurstToggle.IsChecked = settings.SparklerClickBurst;
        RippleClickToggle.IsChecked = settings.RippleClickEnabled;
        FpsSlider.Value = settings.RenderFps;
        // 分段选择器：项由 EffectModeRegistry 驱动（新增模式自动出现），初始选中在 Loaded 后设置
        ModeSelector.ItemsSource = EffectModeRegistry.Modes;
        IdleFadeToggle.IsChecked = settings.IdleFade;
        FullscreenToggle.IsChecked = overlay.FadeOnFullscreen;
        AutoStartToggle.IsChecked = autoStart.IsEnabled;
        RippleShapeBox.SelectedIndex = (int)settings.RippleShape;
        LanguageBox.SelectedIndex = settings.Language == L10n.En ? 1 : 0; // 先赋值后挂事件，不触发 SelectionChanged

        HueSlider.ValueChanged += (_, _) => ApplyAll();
        RadiusSlider.ValueChanged += (_, _) => ApplyAll();
        OpacitySlider.ValueChanged += (_, _) => ApplyAll();
        RippleSlider.ValueChanged += (_, _) => ApplyAll();
        SpeedSlider.ValueChanged += (_, _) => ApplyAll();
        RippleShapeBox.SelectionChanged += (_, _) => ApplyAll();
        SparkHueSlider.ValueChanged += (_, _) => ApplySparkAll();
        SparkCountSlider.ValueChanged += (_, _) => ApplySparkAll();
        SparkLifeSlider.ValueChanged += (_, _) => ApplySparkAll();
        SparklerCountSlider.ValueChanged += (_, _) => ApplySparklerAll();
        SparklerSizeSlider.ValueChanged += (_, _) => ApplySparklerAll();
        SparkBurstToggle.Click += (_, _) =>
        {
            _spark.ClickBurstEnabled = SparkBurstToggle.IsChecked == true;
            _settings.SparkClickBurst = _spark.ClickBurstEnabled;
            SaveSoon();
        };
        SparklerBurstToggle.Click += (_, _) =>
        {
            _sparkler.ClickBurstEnabled = SparklerBurstToggle.IsChecked == true;
            _settings.SparklerClickBurst = _sparkler.ClickBurstEnabled;
            SaveSoon();
        };
        RippleClickToggle.Click += (_, _) =>
        {
            _ripple.ClickEnabled = RippleClickToggle.IsChecked == true;
            _settings.RippleClickEnabled = _ripple.ClickEnabled;
            SaveSoon();
        };
        FpsSlider.ValueChanged += (_, _) =>
        {
            _overlay.TargetFps = (int)Math.Round(FpsSlider.Value);
            _settings.RenderFps = _overlay.TargetFps;
            UpdateFpsText();
            SaveSoon();
        };
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
            SparkLifeSlider.Value = d.SparkLife;
        };
        SparklerResetButton.Click += (_, _) =>
        {
            var d = AppSettings.CreateDefault();
            SparklerCountSlider.Value = d.SparklerCount;
            SparklerSizeSlider.Value = d.SparklerSize;
        };
        IdleFadeToggle.Click += (_, _) =>
        {
            _settings.IdleFade = IdleFadeToggle.IsChecked == true;
            EffectSettingsApplier.ApplyIdleFade(_settings, _glow, _spark, _sparkler);
            SaveSoon();
        };
        FullscreenToggle.Click += (_, _) =>
        {
            _overlay.FadeOnFullscreen = FullscreenToggle.IsChecked == true;
            _settings.HideOnFullscreen = _overlay.FadeOnFullscreen;
            SaveSoon();
        };
        AutoStartToggle.Click += (_, _) =>
        {
            if (AutoStartToggle.IsChecked == true) autoStart.Enable();
            else autoStart.Disable();
        };
        LanguageBox.SelectionChanged += (_, _) =>
        {
            var lang = LanguageBox.SelectedIndex == 1 ? L10n.En : L10n.Zh;
            _settings.Language = lang;
            SaveSoon();
            L10n.Apply(lang); // 触发 LanguageChanged → OnLanguageChanged 刷新动态文本
        };
        L10n.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            L10n.LanguageChanged -= OnLanguageChanged; // 窗口每次打开重建，必须退订防泄漏
            _saver.FlushNow();  // 关窗兜底：尚未落盘的变更立即保存
            _saver.Dispose();
        };
        ApplyAll();
        ApplySparkAll();
        ApplySparklerAll();
        ApplyModeUi(_settings.EffectMode); // 初始模式面板可见性
        UpdateFpsText();
    }

    /// <summary>渲染帧率数值文案（语言切换时随 OnLanguageChanged 刷新）。</summary>
    private void UpdateFpsText() => FpsValue.Text = L10n.Fmt("Str.Fmt.Fps", $"{_settings.RenderFps:0}");

    /// <summary>防抖落盘：滑块拖动的高频变更在窗口内合并为一次写盘，特效参数照旧实时生效。</summary>
    private void SaveSoon() => _saver.Schedule();

    private void SaveNow() => _service.Save(_settings);

    /// <summary>把光圈卡片滑块值同步到设置、特效并保存（改动即生效即保存）。
    /// 只写光圈自己的特效——绝不触碰火屑/烟花（历史串扰 bug 见 EffectSettingsApplier 注释）。</summary>
    private void ApplyAll()
    {
        _settings.Hue = HueSlider.Value;
        _settings.GlowRadius = RadiusSlider.Value;
        _settings.GlowOpacity = OpacitySlider.Value;
        _settings.RippleRadius = RippleSlider.Value;
        if (RippleShapeBox.SelectedIndex >= 0) // 防御：选项未就绪时不写入非法形状
            _settings.RippleShape = (RippleShape)RippleShapeBox.SelectedIndex;
        _settings.FollowSpeed = SpeedSlider.Value;

        EffectSettingsApplier.ApplyClassic(_settings, _glow, _ripple);

        ColorPreview.Fill = new SolidColorBrush(ColorUtils.FromHue(_settings.Hue));
        UpdateClassicTexts();

        SaveSoon();
    }

    /// <summary>光圈数值文案（随语言切换刷新；单位取自字符串字典）。</summary>
    private void UpdateClassicTexts()
    {
        HueValue.Text = L10n.Fmt("Str.Fmt.Deg", $"{HueSlider.Value:0}");
        RadiusValue.Text = L10n.Fmt("Str.Fmt.Px", $"{RadiusSlider.Value:0}");
        OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        RippleValue.Text = L10n.Fmt("Str.Fmt.Px", $"{RippleSlider.Value:0}");
        SpeedValue.Text = $"{SpeedSlider.Value:0}";
    }

    /// <summary>
    /// 切换特效模式：同一时刻只启用一种（光圈 / 火屑 / 烟花），
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
        SaveSoon();
    }

    /// <summary>分段选择器就绪后：设置等宽列数，并按当前设置选中对应模式。</summary>
    private void ModeSelector_Loaded(object sender, RoutedEventArgs e)
    {
        ModeSelector.Loaded -= ModeSelector_Loaded;
        RefreshModeSelector();
    }

    /// <summary>同步分段选择器：等宽列数 + 按当前设置选中。语言切换重建 ItemsSource 后也需调用。</summary>
    private void RefreshModeSelector()
    {
        if (FindVisualChild<System.Windows.Controls.Primitives.UniformGrid>(ModeSelector) is { } grid)
            grid.Columns = EffectModeRegistry.Modes.Count;
        foreach (var container in ModeSelector.Items.OfType<object>()
                     .Select(ModeSelector.ItemContainerGenerator.ContainerFromItem)
                     .OfType<System.Windows.Controls.RadioButton>())
        {
            if (container.Tag is EffectModeInfo info)
                container.IsChecked = info.Mode == _settings.EffectMode;
        }
    }

    /// <summary>
    /// 界面语言切换：XAML 静态文本由 DynamicResource 自动刷新，
    /// 这里负责动态生成的部分——模式分段选择器（数据绑定 DisplayName）与数值文案。
    /// </summary>
    private void OnLanguageChanged()
    {
        ModeSelector.ItemsSource = null;
        ModeSelector.ItemsSource = EffectModeRegistry.Modes;
        // 容器在下一个布局拍才生成，延迟同步选中态
        Dispatcher.BeginInvoke(RefreshModeSelector, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        UpdateFpsText();
        UpdateClassicTexts();
        UpdateSparkTexts();
        UpdateSparklerTexts();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>分段选择器点击：切换到目标模式（Tag 为 EffectModeInfo）。</summary>
    private void ModeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { Tag: EffectModeInfo info })
            ApplyMode(info.Mode);
    }

    /// <summary>
    /// 模式对应的界面状态：只显示当前模式的参数面板。
    /// 窗口高度为 SizeToContent，随面板显隐自动伸缩。
    /// </summary>
    private void ApplyModeUi(EffectMode mode)
    {
        ClassicPanel.Visibility = mode == EffectMode.Classic ? Visibility.Visible : Visibility.Collapsed;
        SparkPanel.Visibility = mode == EffectMode.Spark ? Visibility.Visible : Visibility.Collapsed;
        SparklerPanel.Visibility = mode == EffectMode.Sparkler ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>火屑参数：独立颜色 + 粒子上限 + 生命，同步到设置与特效并保存（只写火屑）。</summary>
    private void ApplySparkAll()
    {
        _settings.SparkHue = SparkHueSlider.Value;
        _settings.SparkCount = (int)Math.Round(SparkCountSlider.Value);
        _settings.SparkLife = Math.Round(SparkLifeSlider.Value, 2);
        EffectSettingsApplier.ApplySpark(_settings, _spark);

        SparkColorPreview.Fill = new SolidColorBrush(ColorUtils.FromHue(_settings.SparkHue));
        UpdateSparkTexts();

        SaveSoon();
    }

    /// <summary>火屑数值文案（随语言切换刷新）。</summary>
    private void UpdateSparkTexts()
    {
        SparkHueValue.Text = L10n.Fmt("Str.Fmt.Deg", $"{SparkHueSlider.Value:0}");
        SparkCountValue.Text = L10n.Fmt("Str.Fmt.Count", $"{SparkCountSlider.Value:0}");
        SparkLifeValue.Text = L10n.Fmt("Str.Fmt.Seconds", $"{SparkLifeSlider.Value:0.0}");
    }

    /// <summary>烟花参数：粒子上限 + 星芒直径（颜色固定不可调），同步到设置与特效并保存（只写烟花）。</summary>
    private void ApplySparklerAll()
    {
        _settings.SparklerCount = (int)Math.Round(SparklerCountSlider.Value);
        _settings.SparklerSize = Math.Round(SparklerSizeSlider.Value);
        EffectSettingsApplier.ApplySparkler(_settings, _sparkler);

        UpdateSparklerTexts();

        SaveSoon();
    }

    /// <summary>烟花数值文案（随语言切换刷新）。</summary>
    private void UpdateSparklerTexts()
    {
        SparklerCountValue.Text = L10n.Fmt("Str.Fmt.Count", $"{SparklerCountSlider.Value:0}");
        SparklerSizeValue.Text = L10n.Fmt("Str.Fmt.Px", $"{SparklerSizeSlider.Value:0}");
    }
}
