using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MehrBar.Spikes.Common;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.WindowHost;

/// <summary>
/// A host variant (H1..H3, guards, render workloads). Variants are discovered by reflection,
/// so later spike plans only add files under Variants/ and never touch this registry.
/// </summary>
internal interface IHostVariant
{
    void Attach(HostContext ctx, string name);

    void Detach();
}

/// <summary>Names a variant may be selected by on the command line (<c>--variant</c>).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class HostVariantAttribute : Attribute
{
    public HostVariantAttribute(params string[] names)
    {
        Names = names;
    }

    public IReadOnlyList<string> Names { get; }
}

/// <summary>Everything a variant may use; created once the HWND exists and nothing has been drawn.</summary>
internal sealed class HostContext
{
    public HostContext(
        HostWindow window,
        HWND hwnd,
        IReadOnlyDictionary<string, string> options,
        RunLog log,
        Rect plate,
        RECT screenPlate,
        Canvas root,
        MessageCounters counters)
    {
        Window = window;
        Hwnd = hwnd;
        Options = options;
        Log = log;
        Plate = plate;
        ScreenPlate = screenPlate;
        Root = root;
        Counters = counters;
    }

    public HostWindow Window { get; }

    public HWND Hwnd { get; }

    /// <summary>All parsed <c>--key value</c> pairs (keys lower case, without the leading dashes).</summary>
    public IReadOnlyDictionary<string, string> Options { get; }

    public RunLog Log { get; }

    /// <summary>The plate in window-relative pixels.</summary>
    public Rect Plate { get; }

    /// <summary>The plate in screen pixels.</summary>
    public RECT ScreenPlate { get; }

    /// <summary>Canvas (sized in pixels) a variant may add children to.</summary>
    public Canvas Root { get; }

    public MessageCounters Counters { get; }

    public void AddHook(HwndSourceHook hook) => Window.AddHook(hook);
}

/// <summary>Raised for a <c>--variant</c> name that no <see cref="IHostVariant"/> declares.</summary>
internal sealed class UnknownVariantException : Exception
{
    public UnknownVariantException(string name)
        : base($"Unknown variant '{name}'. Known variants: {string.Join(", ", VariantRegistry.Names)}")
    {
        VariantName = name;
    }

    public string VariantName { get; }
}

internal static class VariantRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> Map = new(Discover);

    /// <summary>All variant names, sorted ordinally.</summary>
    public static IReadOnlyList<string> Names =>
        Map.Value.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Creates the variants named in a comma separated list.</summary>
    /// <exception cref="UnknownVariantException">A name is not registered.</exception>
    public static IReadOnlyList<(string Name, IHostVariant Variant)> Resolve(string csv)
    {
        var result = new List<(string, IHostVariant)>();
        foreach (string raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Map.Value.TryGetValue(raw, out Type? type))
            {
                throw new UnknownVariantException(raw);
            }

            result.Add((raw, (IHostVariant)Activator.CreateInstance(type)!));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, Type> Discover()
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || !typeof(IHostVariant).IsAssignableFrom(type))
            {
                continue;
            }

            var attribute = type.GetCustomAttribute<HostVariantAttribute>();
            if (attribute == null)
            {
                continue;
            }

            foreach (string name in attribute.Names)
            {
                map[name] = type;
            }
        }

        return map;
    }
}
