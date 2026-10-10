namespace MehrBar.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Core_assembly_is_referenced()
    {
        var name = typeof(MehrBar.Core.AssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("MehrBar.Core", name);
    }
}
