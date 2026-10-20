using MehrBar.Core.IO;
using MehrBar.Core.Model;

namespace MehrBar.Core.Drop;

/// <summary>
/// Turns the path list of a drop into entries (DND-01): folders become <see cref="FolderItem"/>, every other existing
/// file an <see cref="AppItem"/> with the path as given (a shortcut keeps its .lnk path). Order and duplicates are kept
/// (D-12); empty paths and paths that are neither file nor folder are skipped. Nothing is opened or run.
/// </summary>
public static class DropClassifier
{
    public static IReadOnlyList<DockItem> Classify(IReadOnlyList<string> paths, IFileSystem fs)
    {
        var items = new List<DockItem>(paths.Count);
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (fs.DirectoryExists(path))
            {
                items.Add(new FolderItem { Name = DisplayNameFor(path), Path = path });
            }
            else if (fs.FileExists(path))
            {
                items.Add(new AppItem { Name = DisplayNameFor(path), Path = path });
            }
        }

        return items;
    }

    /// <summary>File name without extension; for a folder its last part; for a drive root "D:".</summary>
    public static string DisplayNameFor(string path)
    {
        string trimmed = path.TrimEnd('\\', '/');
        string? root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && string.Equals(root.TrimEnd('\\', '/'), trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        string name = Path.GetFileNameWithoutExtension(trimmed);
        return string.IsNullOrEmpty(name) ? Path.GetFileName(trimmed) : name;
    }
}
