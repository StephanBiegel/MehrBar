using System.Globalization;
using MehrBar.Core.Model;

namespace MehrBar.Core.Config;

/// <summary>
/// Brings a loaded configuration into valid limits and repairs missing parts. Every correction is reported as
/// <c>config-clamped …</c> or <c>config-repaired …</c>. Entries the app cannot show yet (url, separator, unknown) stay.
/// </summary>
public static class ConfigNormalizer
{
    public static DockConfig Normalize(DockConfig config, ICollection<string> warnings)
    {
        int version = config.Version;
        if (version < 1)
        {
            warnings.Add($"config-repaired field=version from={version} to=1");
            version = 1;
        }

        GeneralConfig general = config.General;
        if (general is null)
        {
            warnings.Add("config-repaired field=general from=null to=default");
            general = new GeneralConfig();
        }
        else if (general.SkinFolder is null)
        {
            warnings.Add("config-repaired field=skinFolder from=null to=default");
            general = general with { SkinFolder = Defaults.SkinFolder };
        }

        IReadOnlyList<BarConfig> bars = config.Bars;
        var normalized = new List<BarConfig>();
        if (bars is null || bars.Count == 0)
        {
            warnings.Add("config-repaired field=bars from=empty to=default bar");
            normalized.Add(Defaults.CreateBar(Defaults.FirstBarId));
        }
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BarConfig? bar in bars)
            {
                if (bar is null)
                {
                    warnings.Add("config-repaired field=bars from=null entry to=removed");
                    continue;
                }

                normalized.Add(NormalizeBar(bar, ids, warnings));
            }

            if (normalized.Count == 0)
            {
                normalized.Add(Defaults.CreateBar(Defaults.FirstBarId));
            }
        }

        return config with { Version = version, General = general, Bars = normalized };
    }

    private static BarConfig NormalizeBar(BarConfig bar, HashSet<string> ids, ICollection<string> warnings)
    {
        string id = bar.Id;
        if (string.IsNullOrWhiteSpace(id) || ids.Contains(id))
        {
            int n = 1;
            while (ids.Contains($"b{n}"))
            {
                n++;
            }

            string unique = $"b{n}";
            warnings.Add($"config-repaired bar={id} field=id from={id} to={unique}");
            id = unique;
        }

        ids.Add(id);

        string name = Repaired(bar.Name, Defaults.BarName, id, "name", warnings);
        string monitor = Repaired(bar.Monitor, Defaults.Monitor, id, "monitor", warnings);
        string skin = Repaired(bar.Skin, Defaults.Skin, id, "skin", warnings);

        int iconSize = Clamp(bar.IconSize, 16, 256, id, "iconSize", warnings);
        int zoomSize = Clamp(bar.ZoomSize, iconSize, 512, id, "zoomSize", warnings);
        double zoomWidth = Clamp(bar.ZoomWidth, 1, 16, id, "zoomWidth", warnings);
        int zoomDuration = Clamp(bar.ZoomDurationMs, 0, 2000, id, "zoomDurationMs", warnings);
        int spacing = Clamp(bar.Spacing, 0, 64, id, "spacing", warnings);
        double edgeOffset = Finite(bar.EdgeOffset, id, "edgeOffset", warnings);
        edgeOffset = Clamp(edgeOffset, -256, 1024, id, "edgeOffset", warnings);
        double offset = Finite(bar.Offset, id, "offset", warnings);

        LabelConfig label = bar.Label;
        if (label is null)
        {
            warnings.Add($"config-repaired bar={id} field=label from=null to=default");
            label = new LabelConfig();
        }
        else
        {
            double fontSize = Clamp(label.FontSize, 6, 72, id, "label.fontSize", warnings);
            string family = string.IsNullOrWhiteSpace(label.FontFamily) ? Defaults.LabelFontFamily : label.FontFamily;
            if (!ReferenceEquals(family, label.FontFamily))
            {
                warnings.Add($"config-repaired bar={id} field=label.fontFamily from=empty to={family}");
            }

            label = label with { FontSize = fontSize, FontFamily = family };
        }

        IReadOnlyList<DockItem> items = bar.Items;
        if (items is null)
        {
            warnings.Add($"config-repaired bar={id} field=items from=null to=empty");
            items = [];
        }
        else if (items.Any(i => i is null))
        {
            warnings.Add($"config-repaired bar={id} field=items from=null entries to=removed");
            items = items.Where(i => i is not null).ToList();
        }

        return bar with
        {
            Id = id,
            Name = name,
            Monitor = monitor,
            Skin = skin,
            IconSize = iconSize,
            ZoomSize = zoomSize,
            ZoomWidth = zoomWidth,
            ZoomDurationMs = zoomDuration,
            Spacing = spacing,
            EdgeOffset = edgeOffset,
            Offset = offset,
            Label = label,
            Items = items,
        };
    }

    private static string Repaired(string? value, string fallback, string id, string field, ICollection<string> warnings)
    {
        if (value is not null)
        {
            return value;
        }

        warnings.Add($"config-repaired bar={id} field={field} from=null to={fallback}");
        return fallback;
    }

    private static int Clamp(int value, int min, int max, string id, string field, ICollection<string> warnings)
    {
        int clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            warnings.Add($"config-clamped bar={id} field={field} from={value} to={clamped}");
        }

        return clamped;
    }

    private static double Clamp(double value, double min, double max, string id, string field, ICollection<string> warnings)
    {
        double clamped = double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (clamped != value)
        {
            warnings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"config-clamped bar={id} field={field} from={value} to={clamped}"));
        }

        return clamped;
    }

    private static double Finite(double value, string id, string field, ICollection<string> warnings)
    {
        if (double.IsFinite(value))
        {
            return value;
        }

        warnings.Add(string.Create(CultureInfo.InvariantCulture, $"config-repaired bar={id} field={field} from={value} to=0"));
        return 0;
    }
}
