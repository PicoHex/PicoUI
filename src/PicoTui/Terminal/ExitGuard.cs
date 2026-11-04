namespace PicoTui.Terminal;

/// <summary>
/// Subscription seam for <see cref="AppDomain.ProcessExit"/>.
/// <para>
/// The F5 guard had no seam here: it reached into the guard's private handler and
/// called it directly, so deleting the real subscription left the tests green
/// (R3, 04-H2 residual). Going through a source object means the guard can only
/// restore on process exit if it actually subscribed — and a test can substitute a
/// source that raises <em>only</em> when it was subscribed to.
/// </para>
/// </summary>
internal interface IProcessExitSource
{
    void Subscribe(EventHandler handler);

    void Unsubscribe(EventHandler handler);
}

/// <summary>The production source: <see cref="AppDomain.ProcessExit"/> itself.</summary>
internal sealed class AppDomainProcessExitSource : IProcessExitSource
{
    internal static readonly AppDomainProcessExitSource Instance = new();

    public void Subscribe(EventHandler handler) =>
        // F10b guard target: this is the ONLY place the guard can attach. Removing it
        // makes Regression_F10_H2_ExitGuardWiringTests.Register_ReallyAttaches... red.
        AppDomain.CurrentDomain.ProcessExit += handler;

    public void Unsubscribe(EventHandler handler) =>
        AppDomain.CurrentDomain.ProcessExit -= handler;
}

/// <summary>
/// Factory for one POSIX signal hook. Same reasoning as
/// <see cref="IProcessExitSource"/>: the guard must be able to record what it really
/// registered, and a test must be able to make <c>Create</c> throw.
/// </summary>
/// <returns>A handle for the hook, or null when nothing was registered.</returns>
internal delegate IDisposable? SignalRegistrar(
    PosixSignal signal,
    Action<PosixSignalContext> handler
);

/// <summary>
/// Last-resort terminal restore for exits that never run the normal TUI exit path
/// (04-H2: <c>kill</c>, <c>docker stop</c>, <c>Ctrl-\</c>/<c>Ctrl-Z</c>, host
/// watchdog, FailFast, AOT crash).
/// <para>
/// Signals: <c>SIGTERM</c>/<c>SIGINT</c>/<c>SIGHUP</c> plus — added by F10b (04-H2
/// residual) — <c>SIGQUIT</c> (Ctrl-\) and <c>SIGTSTP</c> (Ctrl-Z), both of which killed
/// the process with the tty still in raw mode. Availability is per platform:
/// SIGTERM/SIGINT/SIGHUP/SIGQUIT are registrable everywhere (Windows maps them to
/// console ctrl/close events), SIGTSTP only exists on Unix. Only the platform-available
/// subset (<see cref="RequiredSignals"/>) is required for the guard to count as armed;
/// requesting an unavailable signal must never break it — <see cref="FailedSignals"/>,
/// <see cref="RegisteredSignals"/> and <see cref="Defect"/> report the gap instead.
/// </para>
/// <para>
/// <see cref="IsRegistered"/> is derived from the recorded registrations, never from
/// intent: the F5 version set a bool before the registration loop, so "registered" was
/// true even when every <c>Create</c> threw.
/// </para>
/// <para>
/// Every hook funnels into one idempotent restore. <see cref="Dispose"/> unregisters
/// symmetrically with <see cref="Register"/> — the ordinary <c>Stop()</c> path keeps
/// ownership of the restore while it is alive.
/// </para>
/// </summary>
internal sealed class ExitGuard : IDisposable
{
    /// <summary>Signals the guard declares it will restore on, everywhere.
    /// <see cref="RequiredSignals"/> is the platform-available subset.</summary>
    // CA1416: naming SIGTSTP/SIGHUP is not calling a Windows-unsupported API. They are
    // *declared* here and armed only through RequiredSignals below (plus a
    // PlatformNotSupportedException that is recorded, never thrown), so no Windows
    // process ever reaches PosixSignalRegistration.Create with them.
#pragma warning disable CA1416
    internal static IReadOnlyList<PosixSignal> RequestedSignals { get; } =
    [
        PosixSignal.SIGTERM,
        PosixSignal.SIGINT,
        PosixSignal.SIGHUP,
        PosixSignal.SIGQUIT,
        PosixSignal.SIGTSTP,
    ];
#pragma warning restore CA1416

    /// <summary>Signals that actually exist on this OS. Windows has no SIGTSTP: the
    /// console has no job control, so <c>PosixSignalRegistration.Create</c> throws
    /// <see cref="PlatformNotSupportedException"/>. (SIGHUP *is* registrable on Windows
    /// — it maps to console close — so it belongs to the Windows set too; the F5 code
    /// skipped it there for no reason.) Requiring SIGTSTP on Windows would make every
    /// session report a broken guard, which is the shape of failure that hides a real
    /// defect (see <see cref="Defect"/>).</summary>
    internal static IReadOnlyList<PosixSignal> RequiredSignals { get; } =
        [.. RequestedSignals.Where(IsAvailableOnThisPlatform)];

    private readonly Action _restore;
    private readonly IProcessExitSource _processExit;
    private readonly SignalRegistrar _registrar;
    private readonly List<IDisposable> _handles = [];
    private readonly List<PosixSignal> _registered = [];
    private readonly List<PosixSignal> _failed = [];
    private readonly EventHandler _onProcessExit;
    private int _restored;
    private bool _processExitSubscribed;

    internal ExitGuard(Action restore)
        : this(restore, AppDomainProcessExitSource.Instance, DefaultRegistrar) { }

    internal ExitGuard(
        Action restore,
        IProcessExitSource processExit,
        SignalRegistrar? registrar = null
    )
    {
        _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        _processExit = processExit ?? throw new ArgumentNullException(nameof(processExit));
        _registrar = registrar ?? DefaultRegistrar;
        _onProcessExit = (_, _) => TryRestore();
    }

    /// <summary>Signals whose registration succeeded, in registration order.</summary>
    internal IReadOnlyList<PosixSignal> RegisteredSignals => _registered;

    /// <summary>Signals that could not be registered on this platform/runtime.</summary>
    internal IReadOnlyList<PosixSignal> FailedSignals => _failed;

    /// <summary>Live hooks; one per registered signal. Empty before
    /// <see cref="Register"/> and after <see cref="Dispose"/>.</summary>
    internal IReadOnlyList<IDisposable> Handles => _handles;

    /// <summary>True once the process-exit handler is attached and not yet removed.</summary>
    internal bool ProcessExitSubscribed => _processExitSubscribed;

    /// <summary>
    /// True only when the guard is actually armed: every platform-available signal has
    /// a live handle and the process-exit hook is attached. Derived from the recorded
    /// registrations — never from intent.
    /// </summary>
    internal bool IsRegistered =>
        _handles.Count > 0
        && _processExitSubscribed
        && RequiredSignals.All(_registered.Contains)
        && _handles.All(h => h is not null);

    /// <summary>Why the guard is not armed (null when it is). Names the missing
    /// signals so a broken guard is diagnosable instead of silently useless.</summary>
    internal string? Defect
    {
        get
        {
            if (IsRegistered)
                return null;
            var missing = RequiredSignals.Where(s => !_registered.Contains(s)).ToArray();
            var parts = new List<string>();
            if (missing.Length > 0)
                parts.Add($"no hook for {string.Join("/", missing.Select(s => s.ToString()))}");
            if (!_processExitSubscribed)
                parts.Add("ProcessExit not subscribed");
            if (_handles.Count == 0)
                parts.Add("no registration handle");
            return parts.Count == 0 ? "guard not armed" : string.Join("; ", parts);
        }
    }

    /// <summary>Installs the OS hooks. Safe to call twice; safe on every OS.</summary>
    internal void Register()
    {
        if (_processExitSubscribed)
            return;

        foreach (var signal in RequestedSignals)
        {
            IDisposable? handle;
            try
            {
                handle = _registrar(signal, OnSignal);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
            {
                // Signal not available on this OS — ProcessExit still covers us.
                _failed.Add(signal);
                continue;
            }

            // A factory that returns nothing did not register anything: counting it
            // would recreate the "green but useless" guard.
            if (handle is null)
            {
                _failed.Add(signal);
                continue;
            }
            _handles.Add(handle);
            _registered.Add(signal);
        }

        _processExit.Subscribe(_onProcessExit);
        _processExitSubscribed = true;
    }

    /// <summary>Restores once; subsequent calls are no-ops. Returns true if it restored.</summary>
    internal bool TryRestore()
    {
        if (Interlocked.Exchange(ref _restored, 1) != 0)
            return false;
        _restore();
        return true;
    }

    private void OnSignal(PosixSignalContext context)
    {
        // Restore first, then let the default action terminate the process.
        TryRestore();
        context.Cancel = false;
    }

    private static IDisposable? DefaultRegistrar(
        PosixSignal signal,
        Action<PosixSignalContext> handler
    ) => PosixSignalRegistration.Create(signal, handler);

    // CA1416: see RequestedSignals — this is the filter that keeps the unix-only
    // signal out of the Windows registration loop.
#pragma warning disable CA1416
    private static bool IsAvailableOnThisPlatform(PosixSignal signal) =>
        signal switch
        {
            // Ctrl-Z / job control does not exist on the Windows console. Windows does
            // accept SIGTERM, SIGINT, SIGHUP and SIGQUIT (CTRL_C/CTRL_BREAK/close).
            PosixSignal.SIGTSTP => !OperatingSystem.IsWindows(),
            _ => true,
        };
#pragma warning restore CA1416

    public void Dispose()
    {
        foreach (var handle in _handles)
        {
            try
            {
                handle?.Dispose();
            }
            catch
            {
                /* shutdown must not throw */
            }
        }
        _handles.Clear();
        _registered.Clear();

        if (_processExitSubscribed)
        {
            _processExit.Unsubscribe(_onProcessExit);
            _processExitSubscribed = false;
        }
    }
}
