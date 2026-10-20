using System.Globalization;
using System.Text;

namespace MehrBar.Core.Logging;

/// <summary>
/// Hand-written rolling file log (STACK.md section 8, APP-05): <c>mehrbar.log</c> rolls over at <c>maxBytes</c> into
/// <c>mehrbar.1.log</c> … <c>mehrbar.{keep}.log</c>; older files are dropped. Line breaks and control characters in
/// messages never start a new line. Never throws: if the file cannot be opened, lines are dropped silently.
/// </summary>
public sealed class RollingFileLog : IAppLog, IDisposable
{
    private const string BaseName = "mehrbar";

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly int _keep;
    private readonly TimeProvider _time;
    private StreamWriter? _writer;

    public RollingFileLog(string directory, long maxBytes = 1_048_576, int keep = 4, TimeProvider? time = null)
    {
        _directory = directory;
        _maxBytes = maxBytes;
        _keep = keep;
        _time = time ?? TimeProvider.System;
        try
        {
            Directory.CreateDirectory(directory);
            RollIfNeeded();
            Open();
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            _writer = null;
        }
    }

    private string CurrentPath => Path.Combine(_directory, BaseName + ".log");

    public void Debug(string message, Exception? ex = null) => Write("DEBUG", message, ex);

    public void Info(string message, Exception? ex = null) => Write("INFO", message, ex);

    public void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                // Nothing left to do; the log never throws.
            }

            _writer = null;
        }
    }

    /// <summary>One log line: CR/LF become " | ", other control characters become spaces.</summary>
    public static string Sanitize(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                builder.Append(" | ");
            }
            else
            {
                builder.Append(char.IsControl(c) ? ' ' : c);
            }
        }

        return builder.ToString();
    }

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            string text = message;
            if (ex is not null)
            {
                text += $" ex={ex.GetType().FullName}: {ex.Message} | {ex.StackTrace}";
            }

            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{_time.GetLocalNow():yyyy-MM-ddTHH:mm:ss.fffzzz} {level} {Sanitize(text)}");

            lock (_gate)
            {
                if (_writer is null)
                {
                    return;
                }

                if (_writer.BaseStream.Length >= _maxBytes)
                {
                    _writer.Dispose();
                    _writer = null;
                    RollIfNeeded();
                    Open();
                }

                _writer?.WriteLine(line);
            }
        }
        catch (Exception failure) when (IsIoFailure(failure) || failure is ObjectDisposedException)
        {
            // The dock keeps running even if the log cannot be written.
        }
    }

    private void Open()
    {
        var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    private void RollIfNeeded()
    {
        var current = new FileInfo(CurrentPath);
        if (!current.Exists || current.Length < _maxBytes)
        {
            return;
        }

        string Numbered(int n) => Path.Combine(_directory, $"{BaseName}.{n}.log");

        File.Delete(Numbered(_keep));
        for (int n = _keep - 1; n >= 1; n--)
        {
            if (File.Exists(Numbered(n)))
            {
                File.Move(Numbered(n), Numbered(n + 1));
            }
        }

        File.Move(CurrentPath, Numbered(1));
    }

    private static bool IsIoFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or System.Security.SecurityException;
}
