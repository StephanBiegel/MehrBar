using System.Text.Json;
using System.Text.Json.Serialization;

namespace MehrBar.Core.Model;

/// <summary>
/// An entry of a bar. The type is resolved from a fixed list by <see cref="Config.DockItemConverter"/>; any other type
/// name is kept as <see cref="UnknownItem"/> with its raw JSON and written back unchanged (CONF-02).
/// </summary>
public abstract record DockItem
{
    /// <summary>Discriminator written as the first property <c>"type"</c>.</summary>
    [JsonIgnore]
    public abstract string Type { get; }
}

/// <summary>Program, file or shortcut; <see cref="Path"/> keeps the .lnk path and unexpanded variables.</summary>
public sealed record AppItem : DockItem
{
    [JsonIgnore]
    public override string Type => "app";

    public string? Name { get; init; }

    public string Path { get; init; } = string.Empty;

    public string? Args { get; init; }

    public string? WorkingDir { get; init; }

    public string? Icon { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Folder opened in Explorer.</summary>
public sealed record FolderItem : DockItem
{
    [JsonIgnore]
    public override string Type => "folder";

    public string? Name { get; init; }

    public string Path { get; init; } = string.Empty;

    public string? Icon { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Web link (shown from a later phase on; kept unchanged until then).</summary>
public sealed record UrlItem : DockItem
{
    [JsonIgnore]
    public override string Type => "url";

    public string? Name { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? Icon { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Separator line (drawn from phase 4 on).</summary>
public sealed record SeparatorItem : DockItem
{
    [JsonIgnore]
    public override string Type => "separator";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>An entry this build does not understand (newer type, or a value of the wrong kind), kept verbatim.</summary>
/// <param name="TypeName">The <c>"type"</c> value as read, or an empty string when it was missing.</param>
/// <param name="Raw">The whole entry as read.</param>
public sealed record UnknownItem(string TypeName, JsonElement Raw) : DockItem
{
    [JsonIgnore]
    public override string Type => TypeName;
}
