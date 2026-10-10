using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Driver;

// Partial of CaseContext owned by plan 01-04; the main part lives in Program.cs.
public sealed partial class CaseContext
{
    /// <summary>
    /// Opens the safety session every input-sending case must run inside:
    /// <c>using var safety = ctx.BeginSafety(TimeSpan.FromSeconds(60));</c>.
    /// Safety log lines go to the case output (stdout), prefixed with the case name by the session.
    /// </summary>
    public SafetySession BeginSafety(TimeSpan timeout, bool watchRealInput = true) =>
        Safety.Begin(Name, timeout, watchRealInput, Console.WriteLine);
}
