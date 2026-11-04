namespace PicoTui.Tests.Layout;

/// <summary>
/// F9-4 guard (03-H2 residual): <see cref="PicoMermaid.Mermaid.Render"/> reports
/// what it cannot draw in <c>MermaidArt.Warnings</c>, but the only UI consumer —
/// <see cref="MermaidComponent"/> — threw them away, so a diagram with an
/// unsupported edge silently rendered as a wrong picture (the official sample's
/// own 4-line graph was one of them: <c>A -- yes --> B</c> drew
/// <c>[A -- yes] ──▶ [B]</c> with zero feedback).
/// <para>
/// The component must therefore (a) expose the diagnostics on a property and
/// (b) render them as a visible degraded-mode row, and a diagram that is fully
/// supported must produce neither.
/// </para>
/// </summary>
public sealed class Regression_F9_MermaidWarningsTests
{
    private const string WarningMark = "\u26a0"; // ⚠

    private static bool HasWarningRow(string[] rows) =>
        rows.Any(r => r.Contains(WarningMark, StringComparison.Ordinal));

    [Test]
    public async Task EdgeLabelSyntax_IsVisibleInTheRenderedOutput()
    {
        // mermaid accepts "A -- label --> B"; the subset renderer prints the
        // label as part of the node name, so it must say the line is degraded
        var m = new MermaidComponent("flowchart LR\n  A -- yes --> B");

        var rows = m.Render(40);

        await Assert
            .That(HasWarningRow(rows))
            .IsTrue()
            .Because($"degraded diagram rendered without a warning row: [{string.Join(" | ", rows)}]");
        await Assert.That(m.Warnings.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BidirectionalArrow_IsVisibleInTheRenderedOutput()
    {
        // "A <--> B" splits into "A <" + "B": the '<' would be printed as node text
        var m = new MermaidComponent("flowchart LR\n  A <--> B");

        var rows = m.Render(40);

        await Assert.That(HasWarningRow(rows)).IsTrue();
        await Assert.That(m.Warnings.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ParserLevelWarning_IsVisibleInTheRenderedOutput()
    {
        // PicoMermaid already warns about a single-node line; the component must
        // relay that warning instead of dropping it
        var m = new MermaidComponent("flowchart LR\n  A --> B\n  C[Forgotten]");

        var rows = m.Render(40);

        await Assert.That(HasWarningRow(rows)).IsTrue();
        await Assert.That(m.Warnings.Any(w => w.Contains("Forgotten"))).IsTrue();
    }

    [Test]
    public async Task FullySupportedDiagram_HasNoWarning()
    {
        var m = new MermaidComponent("flowchart LR\n  A --> B\n  B --> C");

        var rows = m.Render(40);

        await Assert
            .That(HasWarningRow(rows))
            .IsFalse()
            .Because($"a supported diagram must not claim degradation: [{string.Join(" | ", rows)}]");
        await Assert.That(m.Warnings.Count).IsEqualTo(0);
        await Assert.That(rows.Length).IsEqualTo(2);
    }

    [Test]
    public async Task SequenceDiagramWithoutProblems_HasNoWarning()
    {
        var m = new MermaidComponent("sequenceDiagram\n  A->>B: hi");

        var rows = m.Render(40);

        await Assert.That(HasWarningRow(rows)).IsFalse();
        await Assert.That(m.Warnings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task WarningRow_FitsTheAvailableWidth()
    {
        var m = new MermaidComponent("flowchart LR\n  A -- very long label --> B");

        var rows = m.Render(14);

        foreach (var row in rows)
            await Assert
                .That(WidthTable.VisibleWidth(row))
                .IsLessThanOrEqualTo(14)
                .Because($"row '{row}' exceeds the 14-column budget");
        await Assert.That(HasWarningRow(rows)).IsTrue();
    }

    [Test]
    public async Task Warnings_SurviveRepeatedRendersWithoutDuplicating()
    {
        var m = new MermaidComponent("flowchart LR\n  A -- yes --> B");
        _ = m.Render(40);
        var first = m.Warnings.Count;
        _ = m.Render(40);
        _ = m.Render(40);

        await Assert.That(first).IsGreaterThan(0);
        await Assert.That(m.Warnings.Count).IsEqualTo(first);
        // memoized: the art (and its diagnostics) is not recomputed per frame
        await Assert.That(m.ArtRenderCount).IsEqualTo(1);
    }

    [Test]
    public async Task SetSource_ClearsTheOldWarning()
    {
        var m = new MermaidComponent("flowchart LR\n  A -- yes --> B");
        _ = m.Render(40);
        await Assert.That(m.Warnings.Count).IsGreaterThan(0);

        m.SetSource("flowchart LR\n  A --> B");
        var rows = m.Render(40);

        await Assert.That(m.Warnings.Count).IsEqualTo(0);
        await Assert.That(HasWarningRow(rows)).IsFalse();
    }
}
