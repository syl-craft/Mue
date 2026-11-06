using System.Threading.Channels;

namespace Mue.Tests;

public class MacUpdateBlockerTests
{
    private const string Bundle = "/Applications/Test.app";

    [Theory]
    [InlineData("/Applications/Test.app/Contents/MacOS/Test", true)]
    [InlineData("/Applications/Test.app/Contents/Helpers/Child.app/Contents/MacOS/Child", true)]
    [InlineData("/Applications/Test.app2/Contents/MacOS/Test", false)]
    [InlineData("/Applications/Test.app", false)]
    [InlineData("/Applications/test.app/Contents/MacOS/Test", false)]
    [InlineData("/Applications/Test.app/../Other.app/Test", false)]
    [InlineData("/Applications//Test.app/Contents/./MacOS/Test", true)]
    [InlineData("relative/Test.app/Test", false)]
    [InlineData(null, false)]
    public void OnlyExecutablesUnderTheBundleAreIncluded(string? image, bool expected) =>
        Assert.Equal(expected, MacUpdateBlockerRules.IsWithinBundle(image, Bundle + "/"));

    [Fact]
    public void OriginalPathReachesRealpathWithoutLexicallyReducingParents()
    {
        const string original = "/tmp/alias/../Test.app/";
        var result = MacUpdateBlockerRules.ResolveBundlePath(original, path =>
        {
            Assert.Equal(original, path);
            return "/opt/releases/Test.app";
        });
        Assert.Equal("/opt/releases/Test.app", result);
        Assert.False(MacUpdateBlockerRules.IsWithinBundle("/tmp/alias/../Test.app/Probe", "/tmp/Test.app"));
    }

    [Theory]
    [InlineData("relative/Test.app")]
    [InlineData("/tmp/Test")]
    [InlineData("/tmp/Test\0.app")]
    public void InvalidBundlePathsNeverReachRealpath(string path) =>
        Assert.Throws<ArgumentException>(() => MacUpdateBlockerRules.ResolveBundlePath(path,
            _ => throw new InvalidOperationException("Must not resolve")));

    [Fact]
    public void StartTimeKeepsMicrosecondsInTheExistingFileTimeFormat()
    {
        var expected = DateTime.UnixEpoch.AddSeconds(1_700_000_000).AddTicks(123_456 * 10).ToFileTimeUtc();
        Assert.Equal(expected, MacUpdateBlockerRules.ToFileTime(1_700_000_000, 123_456));
        Assert.Equal(0, MacUpdateBlockerRules.ToFileTime(ulong.MaxValue, 0));
        Assert.Equal(0, MacUpdateBlockerRules.ToFileTime(1, 1_000_000));
    }

    [Fact]
    public void DetectionExcludesSelfOtherBundlesAndIgnoredProcesses()
    {
        var processes = new FakeProcesses();
        processes.Add(42, Bundle + "/Contents/MacOS/Test");
        processes.Add(43, Bundle + "/Contents/MacOS/Helper");
        processes.Add(44, Bundle + ".other/Contents/MacOS/Test");
        processes.Add(45, Bundle + "/Contents/MacOS/Ignored");
        var classifier = new Classifier();
        var scan = Create(processes, currentPid: 42).Scan(Bundle, classifier, enrich: true);
        Assert.False(scan.Incomplete);
        var blocker = Assert.Single(MacUpdateBlockerAdapter.ToLegacy(scan));
        Assert.Equal(43, blocker.Pid);
        Assert.Equal(100, blocker.Started);
        Assert.True(blocker.IsRelay);
        Assert.True(blocker.Selected);
        Assert.Equal("Host", blocker.ParentName);
        Assert.Single(classifier.Contexts);
        Assert.Null(classifier.Contexts[0].CommandLine);
    }

    [Fact]
    public void UninspectableProcessesRemainInTheInternalScanBeforeLegacyReduction()
    {
        var processes = new FakeProcesses();
        processes.Add(42, null, state: MacProcessState.Unavailable);
        processes.Add(43, Bundle + "/Contents/MacOS/Helper", state: MacProcessState.Unavailable);
        processes.Add(44, null, state: MacProcessState.Gone);
        processes.Add(45, Bundle + "/Contents/MacOS/Test");
        var log = new Log();
        var classifier = new Classifier();
        var scan = Create(processes, log: log).Scan(Bundle, classifier, enrich: false);
        Assert.True(scan.Incomplete);
        Assert.Equal(new[] { 42, 43, 45 }, scan.Entries.Select(entry => entry.Process.Identity.Pid));
        Assert.Equal(2, scan.Entries.Where(entry => entry.Process.Identity.State == MacProcessState.Unavailable).Count());
        var blockers = MacUpdateBlockerAdapter.ToLegacy(scan);
        Assert.Equal(new[] { 43, 45 }, blockers.Select(b => b.Pid).Order().ToArray());
        Assert.Equal(0, blockers.Where(b => b.Pid == 43).Single().Started);
        Assert.Equal(2, log.Messages.Count);
        Assert.Empty(classifier.Contexts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-42)]
    [InlineData(99)]
    public async Task InvalidPidsAndSelfNeverReachNativeCalls(int pid)
    {
        var processes = new FakeProcesses();
        Assert.Equal(MacCloseResult.AccessDenied,
            await Create(processes).CloseAsync(new() { Pid = pid, Started = 100 }, force: true));
        Assert.Empty(processes.Signals);
        Assert.Equal(0, processes.IdentityReads);
    }

    [Theory]
    [InlineData(0, 100, (int)MacProcessState.Readable, (int)MacCloseResult.AccessDenied)]
    [InlineData(100, 0, (int)MacProcessState.Unavailable, (int)MacCloseResult.AccessDenied)]
    [InlineData(100, 0, (int)MacProcessState.Gone, (int)MacCloseResult.Gone)]
    [InlineData(100, 101, (int)MacProcessState.Readable, (int)MacCloseResult.IdentityChanged)]
    public async Task UnverifiedOrObsoleteIdentitiesNeverReceiveSignals(long expected, long actual,
        int state, int result)
    {
        var processes = new FakeProcesses { Identity = Identity(started: actual, state: (MacProcessState)state) };
        Assert.Equal((MacCloseResult)result,
            await Create(processes).CloseAsync(new() { Pid = 42, Started = expected }, force: true));
        Assert.Empty(processes.Signals);
    }

    [Fact]
    public async Task ABlockerWithoutADetectionTokenCannotAuthorizeSignal()
    {
        var processes = new FakeProcesses();
        Assert.Equal(MacCloseResult.AccessDenied,
            await Create(processes).CloseAsync(new() { Pid = 42, Started = 100 }, force: true));
        Assert.Empty(processes.Signals);
    }

    [Fact]
    public async Task DifferentPidInSnapshotCannotAuthorizeSignal()
    {
        var processes = new FakeProcesses();
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        processes.Identity = Identity(pid: 43);
        Assert.Equal(MacCloseResult.AccessDenied, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
    }

    [Fact]
    public async Task MissingDetectionTokenRemainsUnavailableEvenIfInspectionLaterSucceeds()
    {
        var processes = new FakeProcesses { Identity = Identity() with { Token = null } };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal(0, blocker.Started);
        processes.Identity = Identity();
        Assert.Equal(MacCloseResult.AccessDenied, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
    }

    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 9)]
    public async Task ForceAloneSelectsSignalAndSuccessRequiresExit(bool force, int signal)
    {
        var processes = new FakeProcesses { AfterSignal = Identity(state: MacProcessState.Gone) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.True(blocker.IsRelay);
        Assert.Equal(MacCloseResult.Closed, await operations.CloseAsync(blocker, force));
        Assert.Equal((Token(), signal), Assert.Single(processes.Signals));
    }

    [Theory]
    [InlineData(1, (int)MacCloseResult.AccessDenied)]
    [InlineData(13, (int)MacCloseResult.AccessDenied)]
    [InlineData(45, (int)MacCloseResult.AccessDenied)]
    [InlineData(78, (int)MacCloseResult.AccessDenied)]
    [InlineData(22, (int)MacCloseResult.StillRunning)]
    [InlineData(0, (int)MacCloseResult.StillRunning)]
    public async Task SignalErrorsAndStillRunningAreNotReportedAsClosed(int error, int expected)
    {
        var processes = new FakeProcesses { SignalError = error };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal((MacCloseResult)expected, await operations.CloseAsync(blocker, force: false));
        Assert.Single(processes.Signals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReuseBetweenIdentityCheckAndSignalCannotSignalTheReplacement(bool force)
    {
        var processes = new FakeProcesses { BeforeSignal = Identity(started: 101, version: 2) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal(MacCloseResult.IdentityChanged, await operations.CloseAsync(blocker, force));
        Assert.Empty(processes.Signals);
        Assert.Equal(Token(), Assert.Single(processes.SignalAttempts));
    }

    [Fact]
    public async Task ExecWithTheSameStartTimeCannotRefreshTheDetectionToken()
    {
        var processes = new FakeProcesses();
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        processes.Identity = Identity(version: 2);
        // Un nouveau scan ne remplace pas le token attaché à l'ancien objet.
        Detect(operations, processes);
        Assert.Equal(MacCloseResult.IdentityChanged, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
    }

    [Fact]
    public async Task ExitBetweenIdentityCheckAndSignalIsGone()
    {
        var processes = new FakeProcesses { BeforeSignal = Identity(state: MacProcessState.Gone) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal(MacCloseResult.Gone, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.Signals);
    }

    [Theory]
    [InlineData((int)MacProcessState.Unavailable, 0, (int)MacCloseResult.AccessDenied)]
    [InlineData((int)MacProcessState.Readable, 101, (int)MacCloseResult.IdentityChanged)]
    public async Task IdentityAfterSignalDistinguishesUnreadableAndReusedPid(int state, long started, int expected)
    {
        var processes = new FakeProcesses { AfterSignal = Identity(started: started, state: (MacProcessState)state) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal((MacCloseResult)expected, await operations.CloseAsync(blocker, force: false));
        Assert.Single(processes.Signals);
    }

    [Theory]
    [InlineData((int)MacCloseResult.Closed, true)]
    [InlineData((int)MacCloseResult.Gone, true)]
    [InlineData((int)MacCloseResult.IdentityChanged, true)]
    [InlineData((int)MacCloseResult.StillRunning, false)]
    [InlineData((int)MacCloseResult.AccessDenied, false)]
    public void LegacyAdapterExplicitlyReducesAllFiveResults(int result, bool expected) =>
        Assert.Equal(expected, MacUpdateBlockerAdapter.ToLegacy((MacCloseResult)result));

    [Fact]
    public async Task SoftCloseAllowsFourSecondsWithTheInjectedClock()
    {
        var time = new ObservedTime();
        var processes = new FakeProcesses();
        var operations = new MacUpdateBlockerOperations(processes, new Log(), 99, TimeSpan.FromSeconds(5), time);
        var blocker = Detect(operations, processes);
        var closing = operations.CloseAsync(blocker, force: false);
        await time.NextDelay();
        time.Advance(TimeSpan.FromSeconds(2));
        await time.NextDelay();
        Assert.False(closing.IsCompleted);
        processes.Identity = Identity(state: MacProcessState.Gone);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(MacCloseResult.Closed, await closing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal((Token(), 15), Assert.Single(processes.Signals));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public async Task TimeoutUsesTheConfiguredDurationWithoutEscalation(int seconds)
    {
        var time = new ObservedTime();
        var processes = new FakeProcesses();
        var operations = new MacUpdateBlockerOperations(processes, new Log(), 99, TimeSpan.FromSeconds(seconds), time);
        var blocker = Detect(operations, processes);
        var closing = operations.CloseAsync(blocker, force: false);
        await time.NextDelay();
        time.Advance(TimeSpan.FromSeconds(seconds - 1));
        await time.NextDelay();
        Assert.False(closing.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(MacCloseResult.StillRunning, await closing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal((Token(), 15), Assert.Single(processes.Signals));
    }

    private static MacAuditToken Token(int pid = 42, uint version = 1) => new(0, 501, 20, 501, 20, (uint)pid, 1, version);
    private static MacProcessIdentity Identity(int pid = 42, long started = 100,
        MacProcessState state = MacProcessState.Readable, uint version = 1) => new(pid, started, state, Token: Token(pid, version));
    private static MacUpdateBlockerOperations Create(FakeProcesses processes, int currentPid = 99, Log? log = null) =>
        new(processes, log ?? new Log(), currentPid, TimeSpan.Zero);
    private static UpdateBlocker Detect(MacUpdateBlockerOperations operations, FakeProcesses processes)
    {
        processes.Add(42, Bundle + "/Contents/MacOS/Test");
        return Assert.Single(MacUpdateBlockerAdapter.ToLegacy(operations.Scan(Bundle, new Classifier(), enrich: true)));
    }

    private sealed class FakeProcesses : IMacProcessAccess
    {
        private readonly Dictionary<int, MacProcessSnapshot> _processes = [];
        public MacProcessIdentity Identity { get; set; } = MacUpdateBlockerTests.Identity();
        public MacProcessIdentity? BeforeSignal { get; init; }
        public MacProcessIdentity? AfterSignal { get; init; }
        public int SignalError { get; init; }
        public int IdentityReads { get; private set; }
        public List<MacAuditToken> SignalAttempts { get; } = [];
        public List<(MacAuditToken Token, int Signal)> Signals { get; } = [];
        public void Add(int pid, string? image, MacProcessState state = MacProcessState.Readable) =>
            _processes[pid] = new(Identity with { Pid = pid, State = state,
                Token = Identity.Token == null ? null : Token(pid, Identity.Token.Value.Version) }, image);
        public IReadOnlyList<int> ListPids() => _processes.Keys.ToArray();
        public MacProcessSnapshot ReadProcess(int pid) => _processes[pid];
        public MacProcessIdentity ReadIdentity(int pid) { IdentityReads++; return Identity; }
        public int SendSignal(MacAuditToken token, int signal)
        {
            SignalAttempts.Add(token);
            if (BeforeSignal != null) Identity = BeforeSignal;
            // Simule le contrôle de génération effectué par le noyau AVANT la remise du signal.
            if (Identity.State == MacProcessState.Gone || Identity.Token?.Version != token.Version) return 3;
            Signals.Add((token, signal));
            if (AfterSignal != null) Identity = AfterSignal;
            return SignalError;
        }
    }

    private sealed class ObservedTime : TimeProvider
    {
        private readonly ManualTimeProvider _time = new();
        private readonly Channel<bool> _delays = Channel.CreateUnbounded<bool>();
        public override long TimestampFrequency => _time.TimestampFrequency;
        public override long GetTimestamp() => _time.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _time.CreateTimer(callback, state, dueTime, period);
            _delays.Writer.TryWrite(true);
            return timer;
        }
        public async Task NextDelay() => await _delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        public void Advance(TimeSpan elapsed) => _time.Advance(elapsed);
    }

    private sealed class Log : IUpdateLog
    {
        public List<string> Messages { get; } = [];
        public void Warn(Exception ex, string context) => Messages.Add(context);
    }

    private sealed class Classifier : IUpdateBlockerClassifier
    {
        public List<UpdateBlockerContext> Contexts { get; } = [];
        public bool IsIgnored(string? image, string directory) => image?.EndsWith("/Ignored", StringComparison.Ordinal) == true;
        public UpdateBlockerClassification Classify(UpdateBlockerContext context)
        {
            Contexts.Add(context);
            return new(true, "Host");
        }
    }
}
