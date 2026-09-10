namespace PicoTui.Terminal;

public static class TerminalFactory
{
    public static ITerminal Create() =>
        OperatingSystem.IsWindows() ? new WindowsTerminal() : new UnixTerminal();
}
