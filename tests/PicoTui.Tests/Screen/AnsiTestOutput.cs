namespace PicoTui.Tests.Screen;

/// <summary>Shared helpers for asserting on raw terminal output.</summary>
public static class AnsiTestOutput
{
    /// <summary>Removes CSI/OSC escape sequences, leaving the visible text.</summary>
    public static string Strip(string output)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < output.Length; i++)
        {
            if (output[i] != '\x1b')
            {
                sb.Append(output[i]);
                continue;
            }
            if (i + 1 >= output.Length)
                break;
            if (output[i + 1] == '[')
            {
                i += 2;
                while (i < output.Length && !(output[i] >= '@' && output[i] <= '~'))
                    i++;
            }
            else if (output[i + 1] == ']')
            {
                i += 2;
                while (i < output.Length && output[i] != '\x07')
                    i++;
            }
        }
        return sb.ToString();
    }

    /// <summary>Display width of the visible text.</summary>
    public static int VisibleWidth(string output) => WidthTable.VisibleWidth(Strip(output));
}
