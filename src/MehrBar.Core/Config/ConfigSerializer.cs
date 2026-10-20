using System.Text.Json;
using System.Text.Json.Serialization;
using MehrBar.Core.Layout;
using MehrBar.Core.Model;

namespace MehrBar.Core.Config;

/// <summary>Source-generated metadata for the configuration model.</summary>
[JsonSerializable(typeof(DockConfig))]
[JsonSerializable(typeof(AppItem))]
[JsonSerializable(typeof(FolderItem))]
[JsonSerializable(typeof(UrlItem))]
[JsonSerializable(typeof(SeparatorItem))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Reads and writes <c>config.json</c>. The converters go into the options passed to the context; converters named in
/// <c>JsonSourceGenerationOptions</c> would be ignored (STACK.md, "What NOT to Use").
/// </summary>
public static class ConfigSerializer
{
    /// <summary>Indented JSON; known items start with <c>"type"</c>, unknown items and fields are kept.</summary>
    public static string Serialize(DockConfig config) =>
        JsonSerializer.Serialize(config, CreateContext(_ => { }).DockConfig);

    /// <summary>
    /// Parses <paramref name="json"/>. Unknown enum values and broken single items are reported in
    /// <paramref name="warnings"/>; errors at the root (for example <c>"bars": 5</c>) throw <see cref="JsonException"/>.
    /// </summary>
    public static DockConfig Deserialize(string json, ICollection<string> warnings)
    {
        DockConfig? config = JsonSerializer.Deserialize(json, CreateContext(warnings.Add).DockConfig);
        return config ?? throw new JsonException("The configuration is empty (null).");
    }

    private static ConfigJsonContext CreateContext(Action<string> warn)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = 64,
        };
        options.Converters.Add(new DockItemConverter(warn));
        options.Converters.Add(new TolerantEnumConverter<BarEdge>(BarEdge.Top, warn));
        options.Converters.Add(new TolerantEnumConverter<BarAlign>(BarAlign.Center, warn));
        options.Converters.Add(new TolerantEnumConverter<BarLayer>(BarLayer.Bottom, warn));
        options.Converters.Add(new TolerantEnumConverter<ZoomAnchorMode>(ZoomAnchorMode.Cursor, warn));
        return new ConfigJsonContext(options);
    }
}
