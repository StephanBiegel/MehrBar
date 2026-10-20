using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MehrBar.Core.Model;

namespace MehrBar.Core.Config;

/// <summary>
/// Polymorphic entry converter (CONF-02, STACK.md section 6). <c>"type"</c> may stand anywhere in the object; only the
/// fixed list app/folder/url/separator is ever instantiated. Unknown or missing types, and known types with a value of
/// the wrong kind, stay as <see cref="UnknownItem"/> with their raw JSON and are written back unchanged.
/// </summary>
public sealed class DockItemConverter : JsonConverter<DockItem>
{
    private const string TypeProperty = "type";

    private readonly Action<string> _onWarning;

    public DockItemConverter(Action<string> onWarning)
    {
        _onWarning = onWarning;
    }

    // Only the abstract base: the concrete types use their generated metadata, so there is no recursion.
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(DockItem);

    public override DockItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"An item must be a JSON object, not {element.ValueKind}.");
        }

        string typeName = TypeName(element);
        Type? known = typeName switch
        {
            "app" => typeof(AppItem),
            "folder" => typeof(FolderItem),
            "url" => typeof(UrlItem),
            "separator" => typeof(SeparatorItem),
            _ => null,
        };

        if (known is null)
        {
            return new UnknownItem(typeName, element.Clone());
        }

        try
        {
            DockItem item = (DockItem)element.Deserialize(options.GetTypeInfo(known))!;
            RemoveTypeFromExtra(item);
            return item;
        }
        catch (JsonException ex)
        {
            _onWarning($"item of type '{typeName}' kept unchanged: {ex.Message}");
            return new UnknownItem(typeName, element.Clone());
        }
    }

    public override void Write(Utf8JsonWriter writer, DockItem value, JsonSerializerOptions options)
    {
        if (value is UnknownItem unknown)
        {
            unknown.Raw.WriteTo(writer);
            return;
        }

        JsonTypeInfo info = options.GetTypeInfo(value.GetType());
        JsonElement body = JsonSerializer.SerializeToElement(value, info);

        writer.WriteStartObject();
        writer.WriteString(TypeProperty, value.Type);
        foreach (JsonProperty property in body.EnumerateObject())
        {
            if (property.NameEquals(TypeProperty))
            {
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static string TypeName(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(TypeProperty) && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    // "type" lands in the extension data of a known item after reading; it is written first again on save.
    private static void RemoveTypeFromExtra(DockItem item)
    {
        Dictionary<string, JsonElement>? extra = item switch
        {
            AppItem a => a.Extra,
            FolderItem f => f.Extra,
            UrlItem u => u.Extra,
            SeparatorItem s => s.Extra,
            _ => null,
        };
        extra?.Remove(TypeProperty);
    }
}
