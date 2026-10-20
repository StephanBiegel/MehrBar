namespace MehrBar.Core.IO;

/// <summary>Existence checks, so Core decisions can be tested without real files.</summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);
}

/// <summary>The real file system.</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public static PhysicalFileSystem Instance { get; } = new();

    private PhysicalFileSystem()
    {
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);
}
