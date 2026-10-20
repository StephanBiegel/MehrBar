using MehrBar.Core.Drop;
using MehrBar.Core.IO;
using MehrBar.Core.Model;

namespace MehrBar.Tests.Drop;

/// <summary>DND-01, D-12: dropped paths become entries in order, duplicates kept, invalid ones skipped.</summary>
public class DropClassifierTests
{
    private readonly FakeFileSystem _fs = new(
        files: [@"C:\Tools\Firefox.lnk", @"C:\Program Files\App\app.exe", @"C:\Docs\Plan.pdf"],
        directories: [@"C:\Users\me\Downloads", @"D:\"]);

    [Fact]
    public void Folder_becomes_a_folder_item_named_after_the_folder()
    {
        var item = Assert.IsType<FolderItem>(Assert.Single(DropClassifier.Classify([@"C:\Users\me\Downloads"], _fs)));

        Assert.Equal("Downloads", item.Name);
        Assert.Equal(@"C:\Users\me\Downloads", item.Path);
    }

    [Fact]
    public void Shortcut_keeps_its_lnk_path()
    {
        var item = Assert.IsType<AppItem>(Assert.Single(DropClassifier.Classify([@"C:\Tools\Firefox.lnk"], _fs)));

        Assert.Equal(@"C:\Tools\Firefox.lnk", item.Path);
        Assert.Equal("Firefox", item.Name);
    }

    [Fact]
    public void Exe_becomes_an_app_item_named_without_extension()
    {
        var item = Assert.IsType<AppItem>(Assert.Single(DropClassifier.Classify([@"C:\Program Files\App\app.exe"], _fs)));

        Assert.Equal("app", item.Name);
    }

    [Fact]
    public void Order_and_duplicates_are_kept()
    {
        IReadOnlyList<DockItem> items = DropClassifier.Classify(
            [@"C:\Docs\Plan.pdf", @"C:\Tools\Firefox.lnk", @"C:\Docs\Plan.pdf"],
            _fs);

        Assert.Equal(["Plan", "Firefox", "Plan"], items.Cast<AppItem>().Select(i => i.Name));
    }

    [Fact]
    public void Empty_and_missing_paths_are_skipped()
    {
        IReadOnlyList<DockItem> items = DropClassifier.Classify(["", "   ", @"C:\gone.exe", @"C:\Docs\Plan.pdf"], _fs);

        Assert.Equal("Plan", Assert.IsType<AppItem>(Assert.Single(items)).Name);
    }

    [Fact]
    public void Drive_root_is_named_after_the_drive()
    {
        var item = Assert.IsType<FolderItem>(Assert.Single(DropClassifier.Classify([@"D:\"], _fs)));

        Assert.Equal("D:", item.Name);
    }

    /// <summary>Known files and folders, nothing else (also used by the launch resolver tests).</summary>
    internal sealed class FakeFileSystem(IEnumerable<string> files, IEnumerable<string> directories) : IFileSystem
    {
        private readonly HashSet<string> _files = new(files, StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directories = new(directories, StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => _files.Contains(path);

        public bool DirectoryExists(string path) => _directories.Contains(path);
    }
}
