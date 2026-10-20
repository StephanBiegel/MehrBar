using System.Text.Json;
using System.Text.Json.Serialization;

namespace MehrBar.Core.Config;

/// <summary>
/// Reads an enum from its name (case-insensitive) and writes it in camelCase. An unknown value, a number or a
/// non-string falls back to <c>fallback</c> and is reported, instead of making the whole file unreadable.
/// </summary>
public sealed class TolerantEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private readonly T _fallback;
    private readonly Action<string> _onUnknown;

    public TolerantEnumConverter(T fallback, Action<string> onUnknown)
    {
        _fallback = fallback;
        _onUnknown = onUnknown;
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? text = null;
        if (reader.TokenType == JsonTokenType.String)
        {
            text = reader.GetString();
            if (text is not null
                && !text.Any(c => char.IsDigit(c) || c == '-' || c == ',')
                && Enum.TryParse(text, ignoreCase: true, out T value)
                && Enum.IsDefined(value))
            {
                return value;
            }
        }
        else
        {
            using JsonDocument skipped = JsonDocument.ParseValue(ref reader);
            text = skipped.RootElement.GetRawText();
        }

        _onUnknown($"unknown {typeof(T).Name} value '{text}', using '{Name(_fallback)}'");
        return _fallback;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Name(value));

    private static string Name(T value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
