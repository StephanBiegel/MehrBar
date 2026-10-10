using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MehrBar.Spikes.Common;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.Driver;

/// <summary>One parsed row of a host CSV (columns ts_ms,case,variant,guard,event,detail).</summary>
internal sealed record HostRow(long TsMs, string Case, string Variant, string Guard, string Event, string Detail);

/// <summary>
/// A launched WindowHost process (bar or witness). Start waits for the READY handshake line on stdout
/// (<c>READY hwnd=0x… pid=… role=…</c>, printed once the first content is rendered), then exposes the
/// window handle and the host CSV, which the Driver tails as its source of host-side truth.
/// Kill and Dispose end the process (and its children).
/// </summary>
internal sealed partial class HostProcess : IDisposable
{
    private static readonly TimeSpan s_readyTimeout = TimeSpan.FromSeconds(10);
    private static int s_sequence;

    private readonly Process _process;
    private readonly object _gate = new();
    private readonly List<string> _stdout = [];
    private readonly List<string> _stderr = [];
    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _readOffset;
    private bool _disposed;

    private HostProcess(Process process, string logPath, string arguments)
    {
        _process = process;
        LogPath = logPath;
        Arguments = arguments;
    }

    public HWND Hwnd { get; private set; }

    public int Pid { get; private set; }

    /// <summary>"bar" or "witness", as announced in the READY line.</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Host CSV written by the process (always set; <c>--log</c> is added when the caller gave none).</summary>
    public string LogPath { get; }

    /// <summary>The argument string the process was started with, including the log argument.</summary>
    public string Arguments { get; }

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    /// <summary>Lines the process wrote to stdout (READY and anything else).</summary>
    public IReadOnlyList<string> StdoutLines
    {
        get
        {
            lock (_gate)
            {
                return _stdout.ToArray();
            }
        }
    }

    /// <summary>
    /// Starts WindowHost.exe with <paramref name="arguments"/> and waits at most 10 s for READY.
    /// A <c>--log</c> argument is appended under <see cref="RepoPaths.DryResults"/> unless the caller passed one.
    /// </summary>
    /// <exception cref="FileNotFoundException">The host has not been built.</exception>
    /// <exception cref="InvalidOperationException">The process exited before READY.</exception>
    /// <exception cref="TimeoutException">No READY line within 10 s (the process is killed).</exception>
    public static HostProcess Start(string arguments)
    {
        string exe = RepoPaths.HostExe;
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException(
                "WindowHost.exe not found; build it first: dotnet build spikes/WindowHost -c Release", exe);
        }

        string logPath;
        Match explicitLog = LogArgument().Match(arguments);
        if (explicitLog.Success)
        {
            logPath = Path.GetFullPath(
                explicitLog.Groups["quoted"].Success ? explicitLog.Groups["quoted"].Value : explicitLog.Groups["plain"].Value);
        }
        else
        {
            int sequence = Interlocked.Increment(ref s_sequence);
            logPath = Path.Combine(
                RepoPaths.DryResults,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"host-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{sequence}.csv"));
            arguments = $"{arguments} --log \"{logPath}\"".Trim();
        }

        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var host = new HostProcess(process, logPath, arguments);
        process.OutputDataReceived += (_, e) => host.OnStdout(e.Data);
        process.ErrorDataReceived += (_, e) => host.OnStderr(e.Data);
        process.Exited += (_, _) => host.OnExited();

        if (!process.Start())
        {
            throw new InvalidOperationException("WindowHost.exe did not start");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            if (!host._ready.Task.Wait(s_readyTimeout))
            {
                throw new TimeoutException(
                    $"no READY line from WindowHost within {s_readyTimeout.TotalSeconds:0} s (args: {arguments})");
            }
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    /// <summary>
    /// Host CSV rows written since the previous call (or since the start). Only complete lines are
    /// returned; a line the host is still writing is picked up by the next call.
    /// </summary>
    public IReadOnlyList<HostRow> ReadNewRows()
    {
        lock (_gate)
        {
            (List<HostRow> rows, long consumed) = ReadFrom(_readOffset);
            _readOffset += consumed;
            return rows;
        }
    }

    /// <summary>All rows written so far, independent of <see cref="ReadNewRows"/>.</summary>
    public IReadOnlyList<HostRow> ReadAllRows() => ReadFrom(0).Rows;

    /// <summary>Polls the host CSV until a row with the given event exists; null after <paramref name="timeoutMs"/>.</summary>
    public HostRow? WaitForRow(string evt, int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            HostRow? row = ReadAllRows().FirstOrDefault(r => r.Event == evt);
            if (row != null)
            {
                return row;
            }

            Thread.Sleep(25);
        }
        while (clock.ElapsedMilliseconds < timeoutMs);

        return ReadAllRows().FirstOrDefault(r => r.Event == evt);
    }

    /// <summary>Ends the process tree; safe to call repeatedly and on an already exited process.</summary>
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            _process.WaitForExit(2000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Kill();
        _process.Dispose();
    }

    private (List<HostRow> Rows, long Consumed) ReadFrom(long offset)
    {
        var rows = new List<HostRow>();
        if (!File.Exists(LogPath))
        {
            return (rows, 0);
        }

        byte[] bytes;
        using (var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length <= offset)
            {
                return (rows, 0);
            }

            stream.Seek(offset, SeekOrigin.Begin);
            bytes = new byte[stream.Length - offset];
            int read = 0;
            while (read < bytes.Length)
            {
                int n = stream.Read(bytes, read, bytes.Length - read);
                if (n <= 0)
                {
                    break;
                }

                read += n;
            }

            if (read < bytes.Length)
            {
                Array.Resize(ref bytes, read);
            }
        }

        int lastNewline = Array.LastIndexOf(bytes, (byte)'\n');
        if (lastNewline < 0)
        {
            return (rows, 0);
        }

        string text = new UTF8Encoding(false).GetString(bytes, 0, lastNewline + 1);
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0 || (offset == 0 && trimmed == RunLog.Header))
            {
                continue;
            }

            string[] fields = SplitCsv(trimmed);
            if (fields.Length < 6 || !long.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ts))
            {
                continue; // the header, or a malformed line
            }

            rows.Add(new HostRow(ts, fields[1], fields[2], fields[3], fields[4], fields[5]));
        }

        return (rows, lastNewline + 1);
    }

    private static string[] SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private void OnStdout(string? line)
    {
        if (line == null)
        {
            return;
        }

        lock (_gate)
        {
            _stdout.Add(line);
        }

        Match ready = ReadyLine().Match(line);
        if (!ready.Success)
        {
            return;
        }

        Hwnd = new HWND((nint)Convert.ToInt64(ready.Groups["hwnd"].Value, 16));
        Pid = int.Parse(ready.Groups["pid"].Value, CultureInfo.InvariantCulture);
        Role = ready.Groups["role"].Value;
        _ready.TrySetResult(true);
    }

    private void OnStderr(string? line)
    {
        if (line == null)
        {
            return;
        }

        lock (_gate)
        {
            _stderr.Add(line);
        }
    }

    private void OnExited()
    {
        if (_ready.Task.IsCompleted)
        {
            return;
        }

        string stderr;
        lock (_gate)
        {
            stderr = string.Join(" | ", _stderr);
        }

        _ready.TrySetException(new InvalidOperationException(
            $"WindowHost exited before READY (args: {Arguments}) stderr: {stderr}"));
    }

    [GeneratedRegex(@"^READY hwnd=0x(?<hwnd>[0-9A-Fa-f]+) pid=(?<pid>\d+) role=(?<role>\w+)\s*$")]
    private static partial Regex ReadyLine();

    [GeneratedRegex("--log\\s+(?:\"(?<quoted>[^\"]+)\"|(?<plain>\\S+))")]
    private static partial Regex LogArgument();
}
