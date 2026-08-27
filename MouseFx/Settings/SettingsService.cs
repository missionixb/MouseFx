using System.IO;
using System.Text.Json;

namespace MouseFx.Settings;

/// <summary>设置持久化：JSON 文件存取。任何异常都不抛出（保持程序可用）。</summary>
public sealed class SettingsService
{
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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateDefault();
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
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 保存失败不崩溃，不影响主功能
        }
    }
}
