using System.Text.RegularExpressions;
using MehrBar.Core.Logging;

namespace MehrBar.Tests.Logging;

/// <summary>APP-05: the log rolls over, keeps four old files, keeps every message on one line and never throws.</summary>
public sealed class RollingFileLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mehrbar-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Exceeding_the_limit_rolls_over_into_the_first_old_file()
    {
        using (var log = new RollingFileLog(_dir, maxBytes: 200))
        {
            for (int i = 0; i < 10; i++)
            {
                log.Info("line " + i + " " + new string('x', 30));
            }
        }

        Assert.True(File.Exists(Path.Combine(_dir, "mehrbar.1.log")));
        Assert.True(new FileInfo(Path.Combine(_dir, "mehrbar.log")).Length < 200 + 100);
    }

    [Fact]
    public void Only_four_old_files_are_kept()
    {
        using (var log = new RollingFileLog(_dir, maxBytes: 100))
        {
            // Each line is about 80 bytes, so every second line rolls over: well over six rotations.
            for (int i = 0; i < 20; i++)
            {
                log.Info(new string('y', 40));
            }
        }

        string[] files = Directory.GetFiles(_dir).Select(Path.GetFileName).Order().ToArray()!;
        Assert.Equal(["mehrbar.1.log", "mehrbar.2.log", "mehrbar.3.log", "mehrbar.4.log", "mehrbar.log"], files);
    }

    [Fact]
    public void Line_breaks_and_control_characters_stay_on_one_line()
    {
        using (var log = new RollingFileLog(_dir))
        {
            log.Warn("drop C:\\a.txt\r\nINFO forged\u0007end");
        }

        string[] lines = File.ReadAllLines(Path.Combine(_dir, "mehrbar.log"));
        string line = Assert.Single(lines);
        Assert.DoesNotContain('\u0007', line);
        Assert.Contains(" | INFO forged end", line);
    }

    [Fact]
    public void Locked_file_or_invalid_directory_never_throws()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "mehrbar.log");
        using (new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            using var locked = new RollingFileLog(_dir);
            locked.Error("still running");
        }

        using var invalid = new RollingFileLog("\0invalid<>|dir");
        invalid.Error("still running");
    }

    [Fact]
    public void Every_line_starts_with_an_iso_timestamp_and_the_level()
    {
        using (var log = new RollingFileLog(_dir))
        {
            log.Debug("d");
            log.Info("i");
            log.Warn("w");
            log.Error("e");
        }

        string[] lines = File.ReadAllLines(Path.Combine(_dir, "mehrbar.log"));
        var pattern = new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} (DEBUG|INFO|WARN|ERROR) ");
        Assert.Equal(4, lines.Length);
        Assert.All(lines, l => Assert.Matches(pattern, l));
        Assert.Equal(["DEBUG", "INFO", "WARN", "ERROR"], lines.Select(l => l.Split(' ')[1]));
    }

    [Fact]
    public void Exception_is_appended_on_the_same_line()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException caught)
        {
            ex = caught;
        }

        using (var log = new RollingFileLog(_dir))
        {
            log.Error("launch-failed", ex);
        }

        string line = Assert.Single(File.ReadAllLines(Path.Combine(_dir, "mehrbar.log")));
        Assert.Contains("launch-failed ex=System.InvalidOperationException: boom", line);
    }
}
