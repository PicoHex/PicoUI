namespace PicoTui.Tests.Sample;

/// <summary>
/// F10b guard for F6/F9 residual: <c>samples/PicoTui.Sample</c> still drove the loop
/// the old way (construct <see cref="UiLoop"/>, call <see cref="UiLoop.RunAsync"/>
/// directly) and therefore never called <see cref="TuiLifecycle.Enter(ITerminal, UiLoop)"/>
/// nor set <see cref="UiLoop.OnKey"/>. The sample is the documented "how do I use
/// this" surface, so it demonstrated a path with no terminal start, no input bridge
/// and no keystrokes at all — the real entry point (F9-2) was unreachable from the
/// code users copy.
/// <para>
/// The guard reads the sample source: it must use the production entry point, wire a
/// key handler and finish through <see cref="TuiLifecycle.ExitAsync"/> (which is what
/// releases the reader, see the F10b Stop() guard).
/// </para>
/// </summary>
public sealed class Regression_F10_SampleEntryTests
{
    private static string SampleSource()
    {
        var path = Path.Combine(RepoLayout.Root, "samples", "PicoTui.Sample", "Program.cs");
        if (!File.Exists(path))
            throw new InvalidOperationException($"sample source not found: {path}");
        return File.ReadAllText(path);
    }

    private static string SampleCsproj()
    {
        var path = Path.Combine(RepoLayout.Root, "samples", "PicoTui.Sample", "PicoTui.Sample.csproj");
        if (!File.Exists(path))
            throw new InvalidOperationException($"sample project not found: {path}");
        return File.ReadAllText(path);
    }

    [Test]
    public async Task Sample_UsesTheRealEntryPoint()
    {
        var source = SampleSource();

        await Assert
            .That(source)
            .Contains("TuiLifecycle.Enter(t, loop)")
            .Because("the sample must enter through TuiLifecycle.Enter(t, loop): it is "
                + "the only thing that calls Start(), writes the alt-screen and wires "
                + "the byte pump to the loop (F9-2)");
    }

    [Test]
    public async Task Sample_WiresAKeyHandler()
    {
        var source = SampleSource();

        await Assert
            .That(source)
            .Contains("loop.OnKey")
            .Because("OnKey is the handler a component keys off; a sample without it "
                + "shows a UI that cannot react to a keystroke");
    }

    [Test]
    public async Task Sample_ExitsThroughExitAsync()
    {
        var source = SampleSource();

        await Assert
            .That(source)
            .Contains("TuiLifecycle.ExitAsync")
            .Because("ExitAsync is the restore path (and, after F10b, the reader release)");
    }

    [Test]
    public async Task Sample_DoesNotBypassTheEntryPoint()
    {
        var source = SampleSource();

        await Assert
            .That(source.Contains("t.Start("))
            .IsFalse()
            .Because("calling Start() by hand skips the alt screen, the paste/bracketed "
                + "modes and the input bridge — that is the old path this sample used");
    }

    [Test]
    public async Task Sample_ProjectIsPartOfTheRepository()
    {
        var csproj = SampleCsproj();
        await Assert.That(csproj).Contains("PicoTui.csproj");
        await Assert.That(csproj).Contains("net10.0");
    }
}

/// <summary>Locates the repository root from the test assembly location.</summary>
internal static class RepoLayout
{
    internal static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PicoUI.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"repository root (PicoUI.slnx) not found above {AppContext.BaseDirectory}"
        );
    }
}
