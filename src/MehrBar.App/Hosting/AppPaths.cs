using System.IO;

namespace MehrBar.App.Hosting;

/// <summary>
/// Data folder of the app: <c>%APPDATA%\MehrBar</c>, or <c>--data-dir {path}</c> for isolated hand runs. Never derived
/// from the assembly location (empty in single-file) or the current directory (System32 under autostart).
/// </summary>
internal sealed class AppPaths
{
    private AppPaths(string dataDir)
    {
        DataDir = dataDir;
    }

    public string DataDir { get; }

    public string LogsDir => Path.Combine(DataDir, "logs");

    public string ConfigPath => Path.Combine(DataDir, "config.json");

    public static AppPaths FromArgs(string[] args)
    {
        int index = Array.IndexOf(args, "--data-dir");
        if (index >= 0 && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]))
        {
            return new AppPaths(Path.GetFullPath(args[index + 1]));
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new AppPaths(Path.Combine(appData, "MehrBar"));
    }
}
