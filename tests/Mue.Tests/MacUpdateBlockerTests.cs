namespace Mue.Tests;

public class MacUpdateBlockerTests
{
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
    public void OnlyExecutablesUnderTheBundleAreIncluded(string? image, bool expected)
    {
        Assert.Equal(expected, MacUpdateBlockerRules.IsWithinBundle(image, "/Applications/Test.app/"));
    }

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
        processes.Add(42, "/Applications/Test.app/Contents/MacOS/Test");
        processes.Add(43, "/Applications/Test.app/Contents/MacOS/Helper");
        processes.Add(44, "/Applications/Test.app.other/Contents/MacOS/Test");
        processes.Add(45, "/Applications/Test.app/Contents/MacOS/Ignored");
        var classifier = new Classifier();
        var blockers = Create(processes, currentPid: 42).Find("/Applications/Test.app", classifier, enrich: true);
        var blocker = Assert.Single(blockers);
        Assert.Equal(43, blocker.Pid);
        Assert.Equal(100, blocker.Started);
        Assert.True(blocker.IsRelay);
        Assert.True(blocker.Selected);
        Assert.Equal("Host", blocker.ParentName);
        Assert.Single(classifier.Contexts);
        Assert.Null(classifier.Contexts[0].CommandLine);
    }

    [Fact]
    public void UninspectableProcessesAreReportedAndKnownBundleMembersRemainVisible()
    {
        var processes = new FakeProcesses();
        processes.Add(42, null, state: MacProcessState.Unavailable);
        processes.Add(43, "/Applications/Test.app/Contents/MacOS/Helper", state: MacProcessState.Unavailable);
        processes.Add(44, null, state: MacProcessState.Gone);
        processes.Add(45, "/Applications/Test.app/Contents/MacOS/Test");
        var log = new Log();
        var classifier = new Classifier();
        var blockers = Create(processes, log: log).Find("/Applications/Test.app", classifier, enrich: false);
        Assert.Equal(new[] { 43, 45 }, blockers.Select(b => b.Pid).Order().ToArray());
        Assert.Equal(0, blockers.Where(b => b.Pid == 43).Single().Started);
        Assert.Equal(2, log.Messages.Count);
        Assert.Contains(log.Messages, message => message.Contains("42"));
        Assert.Contains(log.Messages, message => message.Contains("43"));
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
        Assert.False(await Create(processes).CloseAsync(new() { Pid = pid, Started = 100 }, force: true));
        Assert.Empty(processes.Signals);
        Assert.Equal(0, processes.IdentityReads);
    }

    [Theory]
    [InlineData(0, 100, (int)MacProcessState.Readable, false)]
    [InlineData(100, 0, (int)MacProcessState.Unavailable, false)]
    [InlineData(100, 0, (int)MacProcessState.Gone, true)]
    [InlineData(100, 101, (int)MacProcessState.Readable, true)]
    public async Task UnverifiedOrObsoleteIdentitiesNeverReceiveSignals(long expected, long actual,
        int state, bool closed)
    {
        var processes = new FakeProcesses { Identity = new(42, actual, (MacProcessState)state) };
        Assert.Equal(closed, await Create(processes).CloseAsync(new() { Pid = 42, Started = expected }, force: true));
        Assert.Empty(processes.Signals);
    }

    [Fact]
    public async Task DifferentPidInSnapshotCannotAuthorizeSignal()
    {
        var processes = new FakeProcesses { Identity = new(43, 100, MacProcessState.Readable) };
        Assert.False(await Create(processes).CloseAsync(new() { Pid = 42, Started = 100 }, force: true));
        Assert.Empty(processes.Signals);
    }

    [Theory]
    [InlineData(false, false, 15)]
    [InlineData(false, true, 15)]
    [InlineData(true, false, 9)]
    [InlineData(true, true, 9)]
    public async Task ForceAloneSelectsSignalAndSuccessRequiresExit(bool force, bool relay, int signal)
    {
        var processes = new FakeProcesses { AfterSignal = new(42, 0, MacProcessState.Gone) };
        Assert.True(await Create(processes).CloseAsync(new() { Pid = 42, Started = 100, IsRelay = relay }, force));
        Assert.Equal((42, signal), Assert.Single(processes.Signals));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(13, false)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    public async Task SignalErrorsAndStillRunningAreNotReportedAsClosed(int error, bool expected)
    {
        var processes = new FakeProcesses { SignalError = error };
        Assert.Equal(expected, await Create(processes).CloseAsync(new() { Pid = 42, Started = 100 }, force: false));
        Assert.Single(processes.Signals);
    }

    [Theory]
    [InlineData((int)MacProcessState.Unavailable, 0, false)]
    [InlineData((int)MacProcessState.Readable, 101, true)]
    public async Task IdentityAfterSignalDistinguishesUnreadableAndReusedPid(int state, long started, bool expected)
    {
        var processes = new FakeProcesses { AfterSignal = new(42, started, (MacProcessState)state) };
        Assert.Equal(expected, await Create(processes).CloseAsync(new() { Pid = 42, Started = 100 }, force: false));
        Assert.Single(processes.Signals);
    }

    private static MacUpdateBlockerOperations Create(FakeProcesses processes, int currentPid = 99, Log? log = null) =>
        new(processes, log ?? new Log(), currentPid, TimeSpan.Zero);

    private sealed class FakeProcesses : IMacProcessAccess
    {
        private readonly Dictionary<int, MacProcessSnapshot> _processes = [];
        public MacProcessIdentity Identity { get; set; } = new(42, 100, MacProcessState.Readable);
        public MacProcessIdentity? AfterSignal { get; init; }
        public int SignalError { get; init; }
        public int IdentityReads { get; private set; }
        public List<(int Pid, int Signal)> Signals { get; } = [];
        public void Add(int pid, string? image, MacProcessState state = MacProcessState.Readable) =>
            _processes[pid] = new(new(pid, state == MacProcessState.Readable ? 100 : 0, state), image);
        public IReadOnlyList<int> ListPids() => _processes.Keys.ToArray();
        public MacProcessSnapshot ReadProcess(int pid) => _processes[pid];
        public MacProcessIdentity ReadIdentity(int pid) { IdentityReads++; return Identity; }
        public int SendSignal(int pid, int signal)
        {
            Signals.Add((pid, signal));
            if (AfterSignal != null) Identity = AfterSignal;
            return SignalError;
        }
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
