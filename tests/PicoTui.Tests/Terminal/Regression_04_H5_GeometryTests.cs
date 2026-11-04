namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-H5 (High): geometry was read once in the constructor and
/// <c>onResize</c> never fired, so a resized window kept the startup dimensions.
/// Injecting a changing fake size must move <c>Columns</c>/<c>Rows</c>.
/// </summary>
public sealed class Regression_04_H5_GeometryTests
{
    [Test]
    public async Task Geometry_RefreshReportsChange()
    {
        var probe = new FakeWinsizeProbe(100, 30);
        var geometry = new TerminalGeometry(probe);

        await Assert.That(geometry.Refresh()).IsTrue(); // 80x24 default → 100x30
        await Assert.That(geometry.Columns).IsEqualTo(100);
        await Assert.That(geometry.Rows).IsEqualTo(30);

        probe.Columns = 120;
        probe.Rows = 40;
        await Assert.That(geometry.Refresh()).IsTrue();
        await Assert.That(geometry.Columns).IsEqualTo(120);
        await Assert.That(geometry.Rows).IsEqualTo(40);

        await Assert.That(geometry.Refresh()).IsFalse(); // unchanged
    }

    [Test]
    public async Task Geometry_UnavailableProbe_KeepsPreviousSize()
    {
        var probe = new FakeWinsizeProbe(100, 30);
        var geometry = new TerminalGeometry(probe);
        geometry.Refresh();

        probe.Available = false;
        await Assert.That(geometry.Refresh()).IsFalse();
        await Assert.That(geometry.Columns).IsEqualTo(100);
        await Assert.That(geometry.Rows).IsEqualTo(30);
    }

    [Test]
    public async Task UnixTerminal_ResizeRefreshesColumnsRowsAndCallback()
    {
        var probe = new FakeWinsizeProbe(80, 24);
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            probe,
            runSystemHooks: false
        );

        var resizes = 0;
        terminal.Start(_ => { }, () => resizes++);

        probe.Columns = 132;
        probe.Rows = 43;
        await Assert.That(terminal.RefreshGeometry()).IsTrue();

        await Assert.That(terminal.Columns).IsEqualTo(132);
        await Assert.That(terminal.Rows).IsEqualTo(43);
        await Assert.That(resizes).IsEqualTo(1);

        await Assert.That(terminal.RefreshGeometry()).IsFalse(); // same size → no callback
        await Assert.That(resizes).IsEqualTo(1);

        terminal.Stop();
    }

    [Test]
    public async Task WindowsTerminal_ResizeRefreshesColumnsRowsAndCallback()
    {
        var probe = new FakeWinsizeProbe(80, 24);
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            probe,
            runSystemHooks: false
        );

        var resizes = 0;
        terminal.Start(_ => { }, () => resizes++);

        probe.Columns = 200;
        probe.Rows = 50;
        await Assert.That(terminal.RefreshGeometry()).IsTrue();

        await Assert.That(terminal.Columns).IsEqualTo(200);
        await Assert.That(terminal.Rows).IsEqualTo(50);
        await Assert.That(resizes).IsEqualTo(1);

        terminal.Stop();
    }

    [Test]
    public async Task UnixTerminal_ResizeHookIsWiredOnStarting()
    {
        // SIGWINCH wiring is asserted, not exercised: sending a real signal to the
        // test runner is unsafe (SIGINT/SIGTERM would kill the run) and there is no
        // Linux runner here — see the fixes report for the platform limitation.
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.ResizeSignalRegistered).IsEqualTo(OperatingSystem.IsLinux());
        terminal.Stop();
        await Assert.That(terminal.ResizeSignalRegistered).IsFalse();
    }
}
