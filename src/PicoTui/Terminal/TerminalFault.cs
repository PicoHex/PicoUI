namespace PicoTui.Terminal;

/// <summary>
/// A terminal capability that could not be applied — recorded instead of thrown so a
/// session degrades rather than dies (F10b, 04-C2 residual).
/// <para>
/// <c>Start()</c> used to discard the result of the raw-mode/console-mode switch. A
/// tty that refused it (no <c>/dev/tty</c>, non-Linux runtime, failing
/// <c>tcsetattr</c>/<c>SetConsoleMode</c>) then looked healthy while every keystroke
/// was echoed and line-buffered by the line discipline: the UI drew, but nothing it
/// received was a keystroke, and nobody could tell why.
/// </para>
/// </summary>
/// <param name="Operation">The capability that failed, e.g.
/// <c>"raw mode (termios)"</c> or <c>"console mode (SetConsoleMode)"</c>.</param>
/// <param name="Reason">What the OS surface reported.</param>
public sealed record TerminalFault(string Operation, string Reason)
{
    public override string ToString() => $"{Operation} unavailable: {Reason}";
}
