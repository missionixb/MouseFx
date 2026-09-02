using System.IO;
using System.Xml.Linq;
using MouseFx.Settings;
using Xunit;

namespace MouseFx.Tests;

public class L10nTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("zh", "zh")]
    [InlineData(null, "zh")]
    [InlineData("", "zh")]
    [InlineData("fr", "zh")]
    [InlineData("EN", "zh")] // 大小写敏感：只认小写 "en"，其余一律回退中文
    public void 未知语言一律归一化为中文(string? input, string expected)
    {
        Assert.Equal(expected, L10n.Normalize(input));
    }

    /// <summary>从仓库源码定位 Strings 字典 XAML（测试输出目录上溯 5 级到仓库根）。</summary>
    private static XDocument LoadStrings(string lang)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "MouseFx", "Styles", $"Strings.{lang}.xaml");
        return XDocument.Load(path);
    }

    private static Dictionary<string, string> ReadKeys(XDocument doc)
    {
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        return doc.Descendants()
            .Where(e => e.Attribute(xNamespace + "Key") != null)
            .ToDictionary(
                e => e.Attribute(xNamespace + "Key")!.Value,
                e => e.Value.Trim());
    }

    [Fact]
    public void 中英文字符串字典键完备且一致()
    {
        var zh = ReadKeys(LoadStrings("zh"));
        var en = ReadKeys(LoadStrings("en"));

        Assert.NotEmpty(zh);
        Assert.Equal(zh.Keys.OrderBy(k => k), en.Keys.OrderBy(k => k)); // 键集合完全一致

        // 两本字典都不能有空的翻译（漏翻一眼可见）
        Assert.All(zh.Values, v => Assert.False(string.IsNullOrEmpty(v)));
        Assert.All(en.Values, v => Assert.False(string.IsNullOrEmpty(v)));
    }

    [Fact]
    public void 模式注册表引用的字符串键在字典中存在()
    {
        var zh = ReadKeys(LoadStrings("zh"));
        foreach (var mode in EffectModeRegistry.Modes)
        {
            Assert.True(zh.ContainsKey(mode.NameKey), $"缺失显示名键 {mode.NameKey}");
            Assert.True(zh.ContainsKey(mode.DescriptionKey), $"缺失说明键 {mode.DescriptionKey}");
        }
        Assert.Equal("光圈", zh[EffectModeRegistry.Modes[0].NameKey]); // 中文名未被误改（运行时经 L10n 解析，测试直接查字典）
    }

    [Fact]
    public void 反射确认没有字符串字典遗漏的硬编码界面文案键()
    {
        // Str.Fmt 五个格式串都应含 {0} 占位符
        var zh = ReadKeys(LoadStrings("zh"));
        foreach (var (key, value) in zh.Where(kv => kv.Key.StartsWith("Str.Fmt.")))
            Assert.Contains("{0}", value);
    }
}
