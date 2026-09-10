namespace PicoTui.Tests;

public sealed class AssemblySmokeTests
{
    [Test]
    public async Task Assembly_IsLoadable()
    {
        var asm = typeof(PicoTui.AssemblyMarker).Assembly;
        await Assert.That(asm.GetName().Name).IsEqualTo("PicoTui");
    }
}
