using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseFx.Settings;

/// <summary>设置持久化：JSON 文件存取。任何异常都不抛出（保持程序可用）。</summary>
public sealed class SettingsService
{
    /// <summary>读写共用配置：缩进 + 枚举存字符串（设置文件可读）；形状用容错转换器兼容历史遗留名。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new TolerantEnumJsonConverter<RippleShape>(RippleShape.Circle),
            new TolerantEnumJsonConverter<EffectMode>(EffectMode.Classic),
        },
    };

    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MouseFx", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return AppSettings.CreateDefault();
            var json = File.ReadAllText(_filePath);
            using var doc = JsonDocument.Parse(json);
            var settings = doc.RootElement.Deserialize<AppSettings>(JsonOptions) ?? AppSettings.CreateDefault();

            // 旧版设置文件没有 EffectMode 字段：按旧开关字段推导（火花开 → Spark，否则 Classic）
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                !doc.RootElement.TryGetProperty("EffectMode", out _))
            {
                settings.EffectMode = settings.SparkEnabled ? EffectMode.Spark : EffectMode.Classic;
            }
            return settings;
        }
        catch
        {
            return AppSettings.CreateDefault(); // 文件损坏 → 默认值，不抛异常
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 保存失败不崩溃，不影响主功能
        }
    }
}
