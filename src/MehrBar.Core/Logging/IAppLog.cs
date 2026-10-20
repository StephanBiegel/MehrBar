namespace MehrBar.Core.Logging;

/// <summary>Application log. Implementations never throw.</summary>
public interface IAppLog
{
    void Debug(string message, Exception? ex = null);

    void Info(string message, Exception? ex = null);

    void Warn(string message, Exception? ex = null);

    void Error(string message, Exception? ex = null);
}

/// <summary>Log that drops every line (default before the file log is open, and in tests).</summary>
public sealed class NullAppLog : IAppLog
{
    public static NullAppLog Instance { get; } = new();

    private NullAppLog()
    {
    }

    public void Debug(string message, Exception? ex = null)
    {
    }

    public void Info(string message, Exception? ex = null)
    {
    }

    public void Warn(string message, Exception? ex = null)
    {
    }

    public void Error(string message, Exception? ex = null)
    {
    }
}

/// <summary>
/// Ambient log for the app layer, so the app plans need not pass the log through constructors; testable Core classes
/// still take an <see cref="IAppLog"/> explicitly.
/// </summary>
public static class AppLog
{
    public static IAppLog Current { get; set; } = NullAppLog.Instance;
}
