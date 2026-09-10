namespace PicoTui.Tests.Input;

public sealed class KeyTests
{
    [Test]
    public async Task CtrlC_Matches()
    {
        await Assert
            .That(KeySpec.Matches(new Key('c', Ctrl: true, Alt: false, Shift: false), "ctrl+c"))
            .IsTrue();
        await Assert
            .That(KeySpec.Matches(new Key('c', Ctrl: false, Alt: false, Shift: false), "ctrl+c"))
            .IsFalse();
    }

    [Test]
    public async Task ShiftTab_Matches()
    {
        await Assert
            .That(KeySpec.Matches(new Key('\t', Ctrl: false, Alt: false, Shift: true), "shift+tab"))
            .IsTrue();
    }

    [Test]
    public async Task AltLeft_Matches()
    {
        await Assert.That(KeySpec.Matches(Key.ArrowLeft with { Alt = true }, "alt+left")).IsTrue();
    }

    [Test]
    public async Task Enter_Matches()
    {
        await Assert.That(KeySpec.Matches(Key.Enter, "enter")).IsTrue();
    }

    [Test]
    public async Task PlainChar_MatchesItself()
    {
        await Assert.That(KeySpec.Matches(new Key('x', default, default, default), "x")).IsTrue();
    }
}
