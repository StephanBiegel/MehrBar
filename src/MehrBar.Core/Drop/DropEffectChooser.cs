namespace MehrBar.Core.Drop;

/// <summary>Drop effects with the same values as OLE and WPF, so the app can map them bit for bit.</summary>
[Flags]
public enum DropEffects
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}

/// <summary>
/// Picks the effect the bar reports for a drop: link if the source allows it, else copy, else none. Never move: the
/// source would delete the originals (RESEARCH Pitfall 7).
/// </summary>
public static class DropEffectChooser
{
    public static DropEffects Choose(DropEffects allowed)
    {
        if (allowed.HasFlag(DropEffects.Link))
        {
            return DropEffects.Link;
        }

        return allowed.HasFlag(DropEffects.Copy) ? DropEffects.Copy : DropEffects.None;
    }
}
