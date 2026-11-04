namespace PicoMermaid.Tests;

/// <summary>
/// Regression guards for the PicoUI review findings 03-H1 (display width used to
/// be UTF-16 length), 03-H2 (non-edge flowchart lines were dropped silently),
/// M2 (control characters leaked into the public output) and M3 (warnings echoed
/// raw unbounded input).
/// </summary>
public sealed class Regression_03_H
{
    // ---- 03-H1: width must be measured in display COLUMNS, per rune ----------

    [Test]
    public async Task H1_CjkDiagram_FitsColumns_AndWarns()
    {
        var art = Mermaid.Render("flowchart LR\n开始 --> 结束", 16);

        foreach (var row in art.Rows)
            await Assert.That(Cols(row)).IsLessThanOrEqualTo(16);
        await Assert.That(art.Width).IsLessThanOrEqualTo(16);
        // the box art is 17 columns wide, so it cannot be drawn in 16: it must
        // degrade and say so instead of being silently clipped by the terminal.
        await Assert.That(art.Warnings.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task H1_ArrowTemplateConstantIsNineColumns()
    {
        // "A" + "B" in "[A] ──▶ [B]" occupies 9 fixed columns; a width of
        // 1 + 1 + 9 = 11 must fit, 10 must not. The old "+5" under-counted by 4.
        var fits = Mermaid.Render("flowchart LR\nA --> B", 11);
        await Assert.That(fits.Warnings).IsEmpty();
        await Assert.That(fits.Rows[0]).IsEqualTo("[A] ──▶ [B]");

        var tooNarrow = Mermaid.Render("flowchart LR\nA --> B", 10);
        await Assert.That(tooNarrow.Warnings.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task H1_AsciiRows_AreUnchanged()
    {
        var art = Mermaid.Render("flowchart LR\n  A --> B", 40);

        await Assert.That(art.Warnings).IsEmpty();
        await Assert.That(art.Rows.Length).IsEqualTo(1);
        await Assert.That(art.Rows[0]).IsEqualTo("[A] ──▶ [B]");
        await Assert.That(art.Width).IsEqualTo(11);
    }

    [Test]
    public async Task H1_EveryRowFitsColumnsOrWarns()
    {
        string[] sources =
        [
            "flowchart LR\n开始 --> 结束",
            "flowchart LR\nA[长标签节点名称] --> B[另一个长标签]",
            "flowchart LR\nA --> 😀😀😀",
            "sequenceDiagram\n参与者甲->>参与者乙: 你好世界",
        ];
        int[] widths = [8, 16, 21, 30];

        foreach (var source in sources)
        foreach (var width in widths)
        {
            var art = Mermaid.Render(source, width);
            if (art.Warnings.Count > 0)
                continue; // degraded: caller gets the fallback plus a warning
            foreach (var row in art.Rows)
                await Assert.That(Cols(row)).IsLessThanOrEqualTo(width);
        }
    }

    // ---- 03-H2: unsupported flowchart lines must never be dropped silently ---

    [Test]
    public async Task H2_SingleNodeLine_Warns()
    {
        var art = Mermaid.Render("flowchart TD\nA[Start]", 40);

        await Assert.That(art.Warnings.Count).IsGreaterThan(0);
        await Assert.That(art.Rows.Length).IsGreaterThan(0); // fallback, not a blank screen
    }

    [Test]
    public async Task H2_SubgraphLines_WarnButKeepEdges()
    {
        var art = Mermaid.Render("flowchart TD\nsubgraph S\nA --> B\nend", 40);

        await Assert.That(art.Warnings.Count).IsGreaterThan(0);
        await Assert.That(art.Rows.Any(r => r.Contains("──▶"))).IsTrue();
    }

    [Test]
    public async Task H2_OtherArrowSyntax_Warns()
    {
        string[] sources =
        [
            "flowchart TD\nA ==> B",
            "flowchart TD\nA -.-> B",
            "flowchart TD\nA --> B --> C",
            "flowchart TD\nclick A call cb()",
            "flowchart TD\nstyle A fill:#fff",
        ];

        foreach (var source in sources)
        {
            var art = Mermaid.Render(source, 40);
            await Assert.That(art.Warnings.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task H2_ForgottenNode_IsNotDroppedSilently()
    {
        // A --> B renders, C[Forgotten] cannot: the user must be told, and the
        // warning must name the line that was dropped (line 3 of the source).
        var art = Mermaid.Render("flowchart TD\nA --> B\nC[Forgotten]", 40);

        await Assert.That(art.Rows.Any(r => r.Contains("A") && r.Contains("B"))).IsTrue();
        await Assert.That(art.Warnings.Count).IsGreaterThan(0);
        await Assert.That(art.Warnings.Any(w => w.Contains("Forgotten"))).IsTrue();
        await Assert.That(art.Warnings.Any(w => w.Contains('3'))).IsTrue();
    }

    [Test]
    public async Task H2_EdgeLabel_IsNotPrintedAsNodeName()
    {
        var art = Mermaid.Render("flowchart LR\nA -->|yes| B", 40);

        await Assert.That(art.Rows.Any(r => r.Contains("|yes|"))).IsFalse();
    }

    [Test]
    public async Task H2_EmptyDiagram_Degrades()
    {
        // nothing was drawn: the fallback contract must be "rows.Length == 0",
        // not "rows empty AND a warning happened to exist".
        var art = Mermaid.Render("flowchart TD", 40);

        await Assert.That(art.Rows.Length).IsGreaterThan(0);
        await Assert.That(art.Rows.Any(r => r.Contains("flowchart TD"))).IsTrue();
    }

    // ---- M2: control characters must never reach the public output ----------

    [Test]
    public async Task M2_FallbackRows_HaveNoControlCharacters()
    {
        var art = Mermaid.Render("classDiagram\nA\u001b[31mB\u001b]0;PWNED\u0007", 40);

        foreach (var row in art.Rows)
            await Assert.That(HasControlChar(row)).IsFalse();
    }

    [Test]
    public async Task M2_FlowchartLabels_HaveNoControlCharacters()
    {
        var art = Mermaid.Render("flowchart LR\nA\u001b[31m --> B\u0007", 40);

        foreach (var row in art.Rows)
            await Assert.That(HasControlChar(row)).IsFalse();
    }

    [Test]
    public async Task M2_CrlfSource_DoesNotKeepCarriageReturns()
    {
        var art = Mermaid.Render("classDiagram\r\nA --> B\r\n", 40);

        foreach (var row in art.Rows)
            await Assert.That(HasControlChar(row)).IsFalse();
    }

    // ---- M3: warnings must not echo unbounded raw input ---------------------

    [Test]
    public async Task M3_Warnings_AreLengthBounded()
    {
        var longLine = new string('x', 5000);
        string[] sources =
        [
            "classDiagram\n" + longLine,
            "flowchart TD\n" + longLine,
            "sequenceDiagram\n" + longLine,
            longLine,
        ];

        foreach (var source in sources)
        {
            var art = Mermaid.Render(source, 40);
            await Assert.That(art.Warnings.Count).IsGreaterThan(0);
            foreach (var warning in art.Warnings)
                await Assert.That(warning.Length).IsLessThanOrEqualTo(200);
        }
    }

    [Test]
    public async Task M3_Warnings_HaveNoControlCharacters()
    {
        var art = Mermaid.Render("sequenceDiagram\nA\u001b[31m->>", 40);

        foreach (var warning in art.Warnings)
            await Assert.That(HasControlChar(warning)).IsFalse();
    }

    // ---- helpers -----------------------------------------------------------

    private static bool HasControlChar(string s)
    {
        foreach (var c in s)
            if (char.IsControl(c) && c is not '\n' and not '\t')
                return true;
        return false;
    }

    /// <summary>Independent column count: wide runes (CJK/fullwidth/emoji) take 2.</summary>
    private static int Cols(string s)
    {
        var w = 0;
        foreach (var r in s.EnumerateRunes())
            w += IsWide(r.Value) ? 2 : 1;
        return w;
    }

    private static bool IsWide(int c) =>
        c
            is >= 0x1100
                and <= 0x115f
                or >= 0x2e80
                and <= 0x303e
                or >= 0x3041
                and <= 0x33ff
                or >= 0x3400
                and <= 0x4dbf
                or >= 0x4e00
                and <= 0x9fff
                or >= 0xa000
                and <= 0xa4cf
                or >= 0xac00
                and <= 0xd7a3
                or >= 0xf900
                and <= 0xfaff
                or >= 0xfe30
                and <= 0xfe4f
                or >= 0xff00
                and <= 0xff60
                or >= 0xffe0
                and <= 0xffe6
                or >= 0x1f300
                and <= 0x1faff;
}
