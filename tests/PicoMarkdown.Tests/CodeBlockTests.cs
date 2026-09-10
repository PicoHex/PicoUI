namespace PicoMarkdown.Tests;

public sealed class CodeBlockTests
{
    [Test]
    public async Task Parse_FencedCode_ProducesFencedCode()
    {
        var doc = Markdown.Parse("```csharp\nint x = 1;\n```");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Lang).IsEqualTo("csharp");
        await Assert.That(code.Code).IsEqualTo("int x = 1;");
    }

    [Test]
    public async Task Parse_PartialClosingFence_IsTrimmedFromContent()
    {
        // While streaming, the closing ``` arrives as "`" then "``" then "```".
        var doc = Markdown.Parse("```\ncode\n``"); // only 2 backticks so far

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("code"); // partial fence NOT in content
    }
}
