namespace PicoTui.Tests.Layout;

/// <summary>
/// 05-H2 guard: the single-argument render must honour the scroll offset, and a
/// manual scroll must detach the follow-end pin (so history can be read) while
/// returning to the bottom re-arms it.
/// </summary>
public sealed class Regression_05_H2_ScrollViewOffsetTests
{
    private sealed class ManyLines(int n) : IComponent
    {
        public string[] Render(int width) =>
            Enumerable.Range(0, n).Select(i => $"line{i}").ToArray();

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    /// <summary>Content whose height changes, to observe whether the viewport
    /// still follows the end after a detach / re-arm.</summary>
    private sealed class GrowingLines : IComponent
    {
        public int Count { get; set; }

        public string[] Render(int width) =>
            Enumerable.Range(0, Count).Select(i => $"line{i}").ToArray();

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task UnboundedRender_StartsAtTheScrolledOffset()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());
        sv.ScrollBy(3);

        var view = sv.Render(5);

        await Assert.That(view[0]).IsEqualTo("line3");
        await Assert.That(view.Length).IsEqualTo(7);
    }

    [Test]
    public async Task UnboundedRender_WithoutScroll_StillReturnsEverything()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());

        var view = sv.Render(5);

        await Assert.That(view[0]).IsEqualTo("line0");
        await Assert.That(view.Length).IsEqualTo(10);
    }

    [Test]
    public async Task ManualScrollUp_DetachesFollowEnd()
    {
        var content = new GrowingLines { Count = 10 };
        var sv = new ScrollView(content, new ScrollOptions(FollowEnd: true));
        _ = sv.RenderViewport(5, 3); // pinned to the end → offset 7

        sv.ScrollBy(-2); // manual scroll up must win over the auto-pin
        var view = sv.RenderViewport(5, 3);
        await Assert.That(view[0]).IsEqualTo("line5");
        await Assert.That(view[2]).IsEqualTo("line7");

        content.Count = 20; // still detached: new content must not yank the viewport down
        var afterGrowth = sv.RenderViewport(5, 3);
        await Assert.That(afterGrowth[0]).IsEqualTo("line5");
    }

    [Test]
    public async Task ScrollingBackToTheBottom_ResumesFollowing()
    {
        var content = new GrowingLines { Count = 10 };
        var sv = new ScrollView(content, new ScrollOptions(FollowEnd: true));
        _ = sv.RenderViewport(5, 3);
        sv.ScrollBy(-4); // detach
        _ = sv.RenderViewport(5, 3);

        sv.ScrollBy(99); // back past the end → clamped to the last page
        var view = sv.RenderViewport(5, 3);
        await Assert.That(view[0]).IsEqualTo("line7");
        await Assert.That(view[2]).IsEqualTo("line9");

        content.Count = 20; // re-armed at the bottom → follows the new end
        var afterGrowth = sv.RenderViewport(5, 3);
        await Assert.That(afterGrowth[0]).IsEqualTo("line17");
        await Assert.That(afterGrowth[2]).IsEqualTo("line19");
    }

    [Test]
    public async Task ScrollToEnd_ReArmsFollowing()
    {
        var content = new GrowingLines { Count = 10 };
        var sv = new ScrollView(content, new ScrollOptions(FollowEnd: true));
        _ = sv.RenderViewport(5, 3);
        sv.ScrollBy(-3); // detach
        _ = sv.RenderViewport(5, 3);

        sv.ScrollToEnd(10, 3); // explicit "jump to end" re-arms the pin
        await Assert.That(sv.RenderViewport(5, 3)[2]).IsEqualTo("line9");

        content.Count = 20;
        await Assert.That(sv.RenderViewport(5, 3)[2]).IsEqualTo("line19");
    }

    [Test]
    public async Task FollowEndAndOffset_AreObservable()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions(FollowEnd: true));
        await Assert.That(sv.FollowEnd).IsTrue();

        sv.ScrollBy(-2); // clamped at the top: no offset, but the pin is released
        await Assert.That(sv.FollowEnd).IsFalse();
        await Assert.That(sv.Offset).IsEqualTo(0);

        _ = sv.RenderViewport(5, 3); // detached: the offset stays where the user left it
        await Assert.That(sv.Offset).IsEqualTo(0);
        await Assert.That(sv.RenderViewport(5, 3)[0]).IsEqualTo("line0");

        sv.ScrollToEnd(10, 3);
        await Assert.That(sv.FollowEnd).IsTrue();
        await Assert.That(sv.Offset).IsEqualTo(7);
    }
}
