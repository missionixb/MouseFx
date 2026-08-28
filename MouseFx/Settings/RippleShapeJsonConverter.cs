using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseFx.Settings;

/// <summary>
/// RippleShape 的容错序列化：设置文件里出现无法识别的形状名
/// （如历史版本已删除的 Clover、Note）时回退为 Circle，
/// 而不是让整个文件反序列化失败导致全部设置被重置。
/// </summary>
public sealed class RippleShapeJsonConverter : JsonConverter<RippleShape>
{
    public override RippleShape Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            Enum.TryParse(reader.GetString(), true, out RippleShape shape))
        {
            return shape;
        }
        return RippleShape.Circle; // 无法识别（含旧版本遗留名）→ 圆圈
    }

    public override void Write(Utf8JsonWriter writer, RippleShape value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
