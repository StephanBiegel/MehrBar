using MehrBar.Core.Drop;

namespace MehrBar.Tests.Drop;

/// <summary>DND-01: the drop effect is link, else copy, else none, and never move.</summary>
public class DropEffectChooserTests
{
    [Fact]
    public void Link_wins_whenever_it_is_allowed()
    {
        Assert.Equal(DropEffects.Link, DropEffectChooser.Choose(DropEffects.Link));
        Assert.Equal(DropEffects.Link, DropEffectChooser.Choose(DropEffects.Copy | DropEffects.Move | DropEffects.Link));
    }

    [Fact]
    public void Copy_and_move_give_copy()
    {
        Assert.Equal(DropEffects.Copy, DropEffectChooser.Choose(DropEffects.Copy | DropEffects.Move));
    }

    [Fact]
    public void Move_alone_gives_none()
    {
        Assert.Equal(DropEffects.None, DropEffectChooser.Choose(DropEffects.Move));
    }

    [Fact]
    public void Nothing_allowed_gives_none()
    {
        Assert.Equal(DropEffects.None, DropEffectChooser.Choose(DropEffects.None));
    }
}
