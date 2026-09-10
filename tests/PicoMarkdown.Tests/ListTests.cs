namespace PicoMarkdown.Tests;

public sealed class ListTests
{
    [Test]
    public async Task Parse_UnorderedList_ProducesList()
    {
        var doc = Markdown.Parse("- a\n- b");

        var list = (List)doc.Blocks.Single();
        await Assert.That(list.Ordered).IsFalse();
        await Assert.That(list.Items).Count().IsEqualTo(2);
        await Assert.That(list.Items[0].Blocks.Single()).IsTypeOf<Paragraph>();
    }

    [Test]
    public async Task Parse_OrderedList_RecordsStartNumber()
    {
        var doc = Markdown.Parse("3. a\n4. b");

        var list = (List)doc.Blocks.Single();
        await Assert.That(list.Ordered).IsTrue();
        await Assert.That(list.StartNumber).IsEqualTo(3);
    }

    [Test]
    public async Task Parse_NestedList_ProducesNestedList()
    {
        var doc = Markdown.Parse("- a\n  - a1");

        var outer = (List)doc.Blocks.Single();
        var outerItem = (ListItem)outer.Items[0];
        var inner = (List)outerItem.Blocks[1];
        await Assert.That(inner.Items).Count().IsEqualTo(1);
    }
}
