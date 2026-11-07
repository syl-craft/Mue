using System.Threading.Channels;

namespace Mue.Tests;

public class MacUpdateBlockerTests
{
    private const string Bundle = "/Applications/Test.app";

    [Theory]
    [InlineData("/Applications/Test.app/Contents/MacOS/Test", true)]
    [InlineData("/Applications/Test.app/Contents/Helpers/Child.app/Contents/MacOS/Child", true)]
    [InlineData("/Applications/Test.app2/Contents/MacOS/Test", false)]
    [InlineData("/Applications/Test.app", true)]
    [InlineData("/Applications/test.app/Contents/MacOS/Test", false)]
    [InlineData("/Applications/Test.app/../Other.app/Test", false)]
    [InlineData("/Applications//Test.app/Contents/./MacOS/Test", true)]
    [InlineData("relative/Test.app/Test", false)]
    [InlineData(null, false)]
    public void MatchingUsesExactPathsOrDescendantsWithOrdinalComparison(string? image, bool expected) =>
        Assert.Equal(expected, MacUpdateBlockerRules.MatchesPath(image, Bundle + "/"));

    [Fact]
    public void OriginalPathReachesRealpathWithoutLexicallyReducingParents()
    {
        const string original = "/tmp/alias/../Test.app/";
        var query = new UpdateBlockerQuery([original], IncludeCurrentProcess: true);
        var result = MacUpdateBlockerRules.ResolveQuery(query, path =>
        {
            Assert.Equal(original, path);
            return "/opt/releases/Test.app";
        });
        Assert.Equal("/opt/releases/Test.app", Assert.Single(result.Paths));
        Assert.True(result.IncludeCurrentProcess);
        Assert.False(MacUpdateBlockerRules.MatchesPath("/tmp/alias/../Test.app/Probe", "/tmp/Test.app"));
    }

    [Theory]
    [InlineData("relative/Test.app")]
    [InlineData("")]
    [InlineData("/tmp/Test\0.app")]
    public void InvalidPathsNeverReachRealpath(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => MacUpdateBlockerRules.ResolveQuery(new([path]),
            _ => throw new InvalidOperationException("Must not resolve")));

    [Fact]
    public void AllQueryPathsAreResolvedWithoutLosingFilesOrCaseDifferences()
    {
        var query = new UpdateBlockerQuery([Bundle, "/tmp/helper", "/tmp/Helper", Bundle]);
        var visited = new List<string>();
        var resolved = MacUpdateBlockerRules.ResolveQuery(query, path =>
        {
            visited.Add(path);
            return path;
        });
        Assert.Equal(query.Paths, visited);
        Assert.Equal(new[] { Bundle, "/tmp/helper", "/tmp/Helper" }, resolved.Paths);
        Assert.True(MacUpdateBlockerRules.MatchesPath("/tmp/helper", "/"));
    }

    [Fact]
    public void ResolutionFailureIsNotAnEmptyScan() =>
        Assert.Throws<IOException>(() => MacUpdateBlockerRules.ResolveQuery(new([Bundle]),
            _ => throw new IOException("Accès refusé")));

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
        var scan = Create(processes, currentPid: 42).Find(UpdateBlockerQuery.ForDirectory(Bundle), classifier, enrich: true);
        Assert.False(scan.Incomplete);
        var blocker = Assert.Single(scan.Blockers);
        Assert.Equal(43, blocker.Pid);
        Assert.Equal(100, blocker.Started);
        Assert.True(blocker.IsRelay);
        Assert.True(blocker.Selected);
        Assert.Equal("Host", blocker.ParentName);
        Assert.Single(classifier.Contexts);
        Assert.Null(classifier.Contexts[0].CommandLine);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultiplePathsAndOverlapsReturnEachProcessOnceAndPassTheQueryToTheClassifier(bool enrich)
    {
        var query = new UpdateBlockerQuery([Bundle, Bundle + "/Contents", "/tmp/helper"]);
        var processes = new FakeProcesses();
        processes.Add(42, Bundle + "/Contents/MacOS/Test");
        processes.Add(43, "/tmp/helper");
        processes.Add(44, "/tmp/helper2");
        processes.Add(45, "/tmp/Helper");
        var classifier = new Classifier();
        var scan = Create(processes).Find(query, classifier, enrich);
        Assert.Equal(new[] { 42, 43 }, scan.Blockers.Select(b => b.Pid).Order().ToArray());
        Assert.False(scan.Incomplete);
        Assert.Equal(enrich ? 2 : 0, classifier.Contexts.Count);
        Assert.All(classifier.Contexts, context => Assert.Same(query, context.Query));
        Assert.Equal(2, classifier.IgnoredQueries.Count);
        Assert.All(classifier.IgnoredQueries, actual => Assert.Same(query, actual));
        Assert.All(scan.Blockers, blocker => Assert.Equal(enrich, blocker.IsRelay));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncludingTheCurrentProcessOnlyAllowsDetection(bool unavailable)
    {
        var processes = new FakeProcesses();
        processes.Add(99, Bundle + "/Contents/MacOS/Test",
            unavailable ? MacProcessState.Unavailable : MacProcessState.Readable);
        var operations = Create(processes);
        Assert.Empty(operations.Find(new([Bundle]), new Classifier(), enrich: true).Blockers);
        var scan = operations.Find(new([Bundle], IncludeCurrentProcess: true), new Classifier(), enrich: true);
        var blocker = Assert.Single(scan.Blockers);
        Assert.Equal(99, blocker.Pid);
        Assert.Equal(unavailable, scan.Incomplete);
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
        Assert.Equal(0, processes.IdentityReads);
    }

    [Fact]
    public void EmptyQueryDoesNotEnumerateProcesses()
    {
        var processes = new FakeProcesses { ListError = new IOException("Ne pas énumérer") };
        var scan = Create(processes).Find(new([]), new Classifier(), enrich: true);
        Assert.Empty(scan.Blockers);
        Assert.False(scan.Incomplete);
    }

    [Fact]
    public void EnumerationFailureIsNotAnEmptyScan()
    {
        var processes = new FakeProcesses { ListError = new IOException("Accès refusé") };
        Assert.Throws<IOException>(() => Create(processes).Find(new([Bundle]), new Classifier(), enrich: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUnavailableBlockerNeverReachesNativeCallsEvenWithAKnownStartTime(bool force)
    {
        var processes = new FakeProcesses();
        var blocker = new UpdateBlocker { Pid = 42, Started = 100, Unavailable = true };
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied, await Create(processes).CloseAsync(blocker, force));
        Assert.Empty(processes.SignalAttempts);
        Assert.Equal(0, processes.IdentityReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessesWithoutPathAreSkippedAndUninspectableAppProcessesMakeTheScanIncomplete(bool enrich)
    {
        var processes = new FakeProcesses();
        processes.Add(42, null, state: MacProcessState.Unavailable);
        processes.Add(43, Bundle + "/Contents/MacOS/Helper", state: MacProcessState.Unavailable);
        processes.Add(44, null, state: MacProcessState.Gone);
        processes.Add(45, Bundle + "/Contents/MacOS/Test");
        var log = new Log();
        var classifier = new Classifier();
        var operations = Create(processes, log: log);
        var scan = operations.Find(UpdateBlockerQuery.ForDirectory(Bundle), classifier, enrich);
        Assert.True(scan.Incomplete);
        var blockers = scan.Blockers;
        Assert.Equal(new[] { 43, 45 }, blockers.Select(b => b.Pid).Order().ToArray());
        Assert.Equal(new[] { 43 }, blockers.Where(b => b.Unavailable).Select(b => b.Pid).Order().ToArray());
        Assert.All(blockers.Where(b => b.Unavailable), blocker =>
        {
            Assert.Equal(0, blocker.Started);
            Assert.False(blocker.Selected);
            Assert.False(blocker.IsRelay);
            Assert.Equal(UpdateTexts.F("Update_ProcessUnavailable", blocker.Pid), blocker.Detail);
        });
        Assert.Single(log.Messages);
        Assert.Equal(enrich ? 1 : 0, classifier.Contexts.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-42)]
    [InlineData(99)]
    public async Task InvalidPidsAndSelfNeverReachNativeCalls(int pid)
    {
        var processes = new FakeProcesses();
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied,
            await Create(processes).CloseAsync(new() { Pid = pid, Started = 100 }, force: true));
        Assert.Empty(processes.Signals);
        Assert.Equal(0, processes.IdentityReads);
    }

    [Theory]
    [InlineData(0, 100, (int)MacProcessState.Readable, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(100, 0, (int)MacProcessState.Unavailable, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(100, 0, (int)MacProcessState.Gone, (int)UpdateBlockerCloseResult.Gone)]
    [InlineData(100, 101, (int)MacProcessState.Readable, (int)UpdateBlockerCloseResult.IdentityChanged)]
    public async Task UnverifiedOrObsoleteIdentitiesNeverReceiveSignals(long expected, long actual,
        int state, int result)
    {
        var processes = new FakeProcesses { Identity = Identity(started: actual, state: (MacProcessState)state) };
        Assert.Equal((UpdateBlockerCloseResult)result,
            await Create(processes).CloseAsync(new() { Pid = 42, Started = expected }, force: true));
        Assert.Empty(processes.Signals);
    }

    [Fact]
    public async Task ABlockerWithoutADetectionTokenCannotAuthorizeSignal()
    {
        var processes = new FakeProcesses();
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied,
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
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
    }

    [Fact]
    public async Task MissingDetectionTokenRemainsUnavailableEvenIfInspectionLaterSucceeds()
    {
        var processes = new FakeProcesses { Identity = Identity() with { Token = null } };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal(0, blocker.Started);
        Assert.True(blocker.Unavailable);
        processes.Identity = Identity();
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied, await operations.CloseAsync(blocker, force: true));
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
        Assert.Equal(UpdateBlockerCloseResult.Closed, await operations.CloseAsync(blocker, force));
        Assert.Equal((Token(), signal), Assert.Single(processes.Signals));
    }

    [Theory]
    [InlineData(1, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(13, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(45, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(78, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(22, (int)UpdateBlockerCloseResult.StillRunning)]
    [InlineData(0, (int)UpdateBlockerCloseResult.StillRunning)]
    public async Task SignalErrorsAndStillRunningAreNotReportedAsClosed(int error, int expected)
    {
        var processes = new FakeProcesses { SignalError = error };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal((UpdateBlockerCloseResult)expected, await operations.CloseAsync(blocker, force: false));
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
        Assert.Equal(UpdateBlockerCloseResult.IdentityChanged, await operations.CloseAsync(blocker, force));
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
        Assert.Equal(UpdateBlockerCloseResult.IdentityChanged, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.SignalAttempts);
    }

    [Fact]
    public async Task ExitBetweenIdentityCheckAndSignalIsGone()
    {
        var processes = new FakeProcesses { BeforeSignal = Identity(state: MacProcessState.Gone) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal(UpdateBlockerCloseResult.Gone, await operations.CloseAsync(blocker, force: true));
        Assert.Empty(processes.Signals);
    }

    [Theory]
    [InlineData((int)MacProcessState.Unavailable, 0, (int)UpdateBlockerCloseResult.AccessDenied)]
    [InlineData((int)MacProcessState.Readable, 101, (int)UpdateBlockerCloseResult.IdentityChanged)]
    public async Task IdentityAfterSignalDistinguishesUnreadableAndReusedPid(int state, long started, int expected)
    {
        var processes = new FakeProcesses { AfterSignal = Identity(started: started, state: (MacProcessState)state) };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        Assert.Equal((UpdateBlockerCloseResult)expected, await operations.CloseAsync(blocker, force: false));
        Assert.Single(processes.Signals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeClosingNeverInspectsOrSignals(bool force)
    {
        var processes = new FakeProcesses();
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.CloseAsync(blocker, force, cancellation.Token));
        Assert.Equal(0, processes.IdentityReads);
        Assert.Empty(processes.SignalAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringIdentityInspectionPreventsTheSignal(bool force)
    {
        using var cancellation = new CancellationTokenSource();
        var processes = new FakeProcesses { OnReadIdentity = cancellation.Cancel };
        var operations = Create(processes);
        var blocker = Detect(operations, processes);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.CloseAsync(blocker, force, cancellation.Token));
        Assert.Equal(1, processes.IdentityReads);
        Assert.Empty(processes.SignalAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInterruptsExitWaitingWithoutAnotherSignal(bool force)
    {
        var time = new ObservedTime();
        var processes = new FakeProcesses();
        var operations = new MacUpdateBlockerOperations(processes, new Log(), 99, TimeSpan.FromSeconds(5), time);
        var blocker = Detect(operations, processes);
        using var cancellation = new CancellationTokenSource();
        var closing = operations.CloseAsync(blocker, force, cancellation.Token);
        await time.NextDelay();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal((Token(), force ? 9 : 15), Assert.Single(processes.Signals));
    }

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
        Assert.Equal(UpdateBlockerCloseResult.Closed, await closing.WaitAsync(TimeSpan.FromSeconds(5)));
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
        Assert.Equal(UpdateBlockerCloseResult.StillRunning, await closing.WaitAsync(TimeSpan.FromSeconds(5)));
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
        return Assert.Single(operations.Find(UpdateBlockerQuery.ForDirectory(Bundle), new Classifier(), enrich: true).Blockers);
    }

    private sealed class FakeProcesses : IMacProcessAccess
    {
        private readonly Dictionary<int, MacProcessSnapshot> _processes = [];
        public MacProcessIdentity Identity { get; set; } = MacUpdateBlockerTests.Identity();
        public MacProcessIdentity? BeforeSignal { get; init; }
        public MacProcessIdentity? AfterSignal { get; init; }
        public int SignalError { get; init; }
        public Exception? ListError { get; init; }
        public Action? OnReadIdentity { get; init; }
        public int IdentityReads { get; private set; }
        public List<MacAuditToken> SignalAttempts { get; } = [];
        public List<(MacAuditToken Token, int Signal)> Signals { get; } = [];
        public void Add(int pid, string? image, MacProcessState state = MacProcessState.Readable) =>
            _processes[pid] = new(Identity with { Pid = pid, State = state,
                Token = Identity.Token == null ? null : Token(pid, Identity.Token.Value.Version) }, image);
        public IReadOnlyList<int> ListPids() => ListError == null ? _processes.Keys.ToArray() : throw ListError;
        public MacProcessSnapshot ReadProcess(int pid) => _processes[pid];
        public MacProcessIdentity ReadIdentity(int pid) { IdentityReads++; OnReadIdentity?.Invoke(); return Identity; }
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
        public List<UpdateBlockerQuery> IgnoredQueries { get; } = [];
        public bool IsIgnored(string? image, UpdateBlockerQuery query)
        {
            IgnoredQueries.Add(query);
            return image?.EndsWith("/Ignored", StringComparison.Ordinal) == true;
        }
        public UpdateBlockerClassification Classify(UpdateBlockerContext context)
        {
            Contexts.Add(context);
            return new(true, "Host");
        }
    }
}
