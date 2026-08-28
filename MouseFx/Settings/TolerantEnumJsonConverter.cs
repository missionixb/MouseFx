using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseFx.Settings;

/// <summary>
/// 枚举的容错序列化：设置文件里出现无法识别的名字（如历史版本遗留值）
/// 时回退到指定默认值，而不是让整个文件反序列化失败导致全部设置被重置。
/// </summary>
public sealed class TolerantEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly T _fallback;

    public TolerantEnumJsonConverter(T fallback) => _fallback = fallback;

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            Enum.TryParse(reader.GetString(), true, out T value))
        {
            return value;
        }
        return _fallback; // 无法识别（含旧版本遗留名/非法值）→ 默认值
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
