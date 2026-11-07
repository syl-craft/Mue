using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Mue.Wpf;

namespace Mue.Tests;

public class UpdateConsentTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("Session will close", false)]
    [InlineData("Session will close", true)]
    [InlineData("", false)]
    public Task PreparationFollowsAgreementAndFinalScan(string? summary, bool hasBlockers) => WpfTestHost.Run(() =>
    {
        var host = new Host { Summary = summary, Blockers = hasBlockers ? [Blocker(1, selected: true)] : [] };
        using var service = ReadyService(host);
        var shown = 0;
        var window = new UpdatesDialog(service, host, dialog =>
        {
            shown++;
            Assert.DoesNotContain("prepare", host.Calls);
            return WpfTestHost.Show(dialog, current =>
            {
                if (hasBlockers)
                {
                    Press(current, "CloseButton");
                    WpfTestHost.Pump(host.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
                    WaitUntil(() => !current.IsVisible || Button(current, "CloseButton").IsEnabled);
                }
                if (summary != null)
                {
                    Assert.True(current.IsVisible);
                    Assert.Equal(UpdateTexts.T("Update_ConfirmTitle"), current.Title);
                    Assert.Equal(UpdateTexts.T("Update_ConfirmInstall"), Button(current, "CloseButton").Content);
                    Assert.Equal(summary, ((TextBlock)current.FindName("PreparationSummary")).Text);
                    Assert.DoesNotContain("prepare", host.Calls);
                    Press(current, "CloseButton");
                }
            });
        });
        try
        {
            Press(window, "InstallButton");
            WpfTestHost.Pump(host.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(hasBlockers || summary != null ? 1 : 0, shown);
            Assert.Equal(new[] { "final", "prepare", "before", "apply", "exit" }, host.Calls.TakeLast(5));
            var preparations = host.Calls.Where(call => call == "prepare").ToArray();
            Assert.Single(preparations);
            Assert.Empty(host.Errors);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task PostponeOrWindowCloseNeverPrepares(bool hasBlockers, bool postpone) => WpfTestHost.Run(() =>
    {
        var host = new Host { Summary = "Session will close", Blockers = hasBlockers ? [Blocker(1)] : [] };
        using var service = ReadyService(host);
        var window = new UpdatesDialog(service, host, dialog => WpfTestHost.Show(dialog, current =>
        {
            Assert.True(Button(current, "PostponeButton").IsCancel);
            if (postpone)
                typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(Button(current, "PostponeButton"), null);
            else current.Close();
        }));
        try
        {
            Press(window, "InstallButton");
            WaitUntil(() => service.State != UpdateState.Preparing);
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.DoesNotContain("apply", host.Calls);
            Assert.DoesNotContain("final", host.Calls);
            Assert.Equal(UpdateState.Ready, service.State);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(null)]
    [InlineData("Session will close")]
    public Task RetryWithAnEmptyListOnlyAgreesWithoutSummary(string? summary) => WpfTestHost.Run(() =>
    {
        var host = new Host();
        var dialog = Dialog(host, [Blocker(1)], summary: summary, refresh: () => Task.FromResult(new UpdateBlockerScan([])));
        var result = WpfTestHost.Show(dialog, current =>
        {
            Press(current, "RetryButton");
            Assert.Equal(summary != null, current.IsVisible);
            if (summary != null)
            {
                Assert.Equal(UpdateTexts.T("Update_ConfirmInstall"), Button(current, "CloseButton").Content);
                current.Close();
            }
        });
        Assert.Equal(summary == null, result == true);
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill, false, null)]
    [InlineData(UpdateShutdownBehavior.Kill, true, null)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, false, null)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, true, null)]
    [InlineData(UpdateShutdownBehavior.Kill, false, "Session will close")]
    [InlineData(UpdateShutdownBehavior.AskToQuit, true, "Session will close")]
    public Task IgnoredGroupIsAcceptedWithoutClosingIt(UpdateShutdownBehavior behavior, bool unavailable, string? summary) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior, Summary = summary, Blockers = [Blocker(1, unavailable: unavailable)] };
        using var service = ReadyService(host);
        var window = new UpdatesDialog(service, host, dialog => WpfTestHost.Show(dialog, current =>
        {
            Assert.Equal(UpdateTexts.T("Update_InstallIgnoring"), Button(current, "CloseButton").Content);
            Assert.Equal(unavailable ? Visibility.Visible : Visibility.Collapsed, ((TextBlock)current.FindName("ScanIncomplete")).Visibility);
            Press(current, "CloseButton");
            Assert.Contains((1, 100L), current.IgnoredBlockers);
        }));
        try
        {
            Press(window, "InstallButton");
            WpfTestHost.Pump(host.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(host.CloseAttempts);
            Assert.Empty(host.Errors);
            Assert.Contains("apply", host.Calls);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill)]
    [InlineData(UpdateShutdownBehavior.AskToQuit)]
    public Task ABlockerWithoutStartTimeCannotBeIgnored(UpdateShutdownBehavior behavior) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior };
        var dialog = Dialog(host, [new UpdateBlocker { Pid = 1, Name = "Unknown", Started = 0, Unavailable = true }]);
        WpfTestHost.Show(dialog, current =>
        {
            Assert.False(Button(current, "CloseButton").IsEnabled);
            Press(current, "CloseButton");
            Assert.Empty(current.IgnoredBlockers);
            Assert.True(current.IsVisible);
            current.Close();
        });
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill)]
    [InlineData(UpdateShutdownBehavior.AskToQuit)]
    public Task ANewUncheckedProcessAfterClosingIsNotAutomaticallyIgnored(UpdateShutdownBehavior behavior) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior };
        var dialog = Dialog(host, [Blocker(1, selected: true), Blocker(2)],
            refresh: () => Task.FromResult(new UpdateBlockerScan([Blocker(2), Blocker(3)])));
        Assert.False(WpfTestHost.Show(dialog, current =>
        {
            Press(current, "CloseButton");
            Assert.True(current.IsVisible);
            Assert.Contains((2, 100L), current.IgnoredBlockers);
            Assert.DoesNotContain((3, 100L), current.IgnoredBlockers);
        }));
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill)]
    [InlineData(UpdateShutdownBehavior.AskToQuit)]
    public Task CheckedGroupClosesBeforeContinuingWithIgnoredGroup(UpdateShutdownBehavior behavior) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior, Blockers = [Blocker(1, selected: true), Blocker(2)] };
        var dialog = Dialog(host, host.Blockers);
        Assert.True(WpfTestHost.Show(dialog, current =>
        {
            Assert.Equal(UpdateTexts.F("Update_CloseCount", 1), Button(current, "CloseButton").Content);
            Press(current, "CloseButton");
            WaitUntil(() => !current.IsVisible);
        }));
        Assert.Equal(new[] { (1, false) }, host.CloseAttempts);
        Assert.Equal(new[] { (2, 100L) }, dialog.IgnoredBlockers);
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Refuse)]
    [InlineData(UpdateShutdownBehavior.Kill)]
    [InlineData(UpdateShutdownBehavior.AskToQuit)]
    public Task SelectedProgramsThatStayOpenRequireAnExplicitForce(UpdateShutdownBehavior behavior) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior, KeepOpen = true, Blockers = [Blocker(1, selected: true), Blocker(2)] };
        var dialog = Dialog(host, host.Blockers);
        Assert.False(WpfTestHost.Show(dialog, current =>
        {
            Press(current, "CloseButton");
            WpfTestHost.Pump(host.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            WaitUntil(() => Button(current, "CloseButton").IsEnabled);
            Assert.True(current.IsVisible);
            Assert.Equal(UpdateTexts.T("Update_ForceInstall"), Button(current, "CloseButton").Content);
            Press(current, "CloseButton");
            WaitUntil(() => Button(current, "CloseButton").IsEnabled);
            Assert.True(current.IsVisible);
        }));
        Assert.Equal(new[] { (1, false), (1, true) }, host.CloseAttempts);
    });

    [Fact]
    public Task RefuseCannotIgnoreAnUncheckedGroup() => WpfTestHost.Run(() =>
    {
        var host = new Host { Blockers = [Blocker(1)] };
        var dialog = Dialog(host, host.Blockers);
        Assert.False(WpfTestHost.Show(dialog, current =>
        {
            Assert.False(Button(current, "CloseButton").IsEnabled);
            Press(current, "CloseButton");
            Assert.True(current.IsVisible);
            Assert.Empty(current.IgnoredBlockers);
        }));
        Assert.Empty(host.CloseAttempts);
    });

    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill, 2, 100)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, 2, 100)]
    [InlineData(UpdateShutdownBehavior.Kill, 1, 200)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, 1, 200)]
    public Task FinalScanRejectsAnIdentityNotAccepted(UpdateShutdownBehavior behavior, int pid, long started) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior, Blockers = [Blocker(1)], FinalBlockers = [new() { Pid = pid, Started = started }] };
        using var service = ReadyService(host);
        var window = new UpdatesDialog(service, host, dialog => WpfTestHost.Show(dialog, current => Press(current, "CloseButton")));
        try
        {
            Press(window, "InstallButton");
            WpfTestHost.Pump(host.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Single(host.Errors);
            Assert.Equal(UpdateTexts.T("Update_LockError"), host.Errors[0]);
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.DoesNotContain("apply", host.Calls);
        }
        finally { window.Close(); }
    });

    private static UpdateBlocker Blocker(int pid, bool selected = false, bool unavailable = false) =>
        new() { Pid = pid, Started = 100, Name = "Program " + pid, Selected = selected, Unavailable = unavailable };
    private static UpdateBlockersDialog Dialog(Host host, IReadOnlyList<UpdateBlocker> blockers, string? summary = null,
        Func<Task<UpdateBlockerScan>>? refresh = null) => new(UpdateBlockerQuery.ForDirectory("unused"), new(blockers), host, host.Behavior, refresh: refresh, summary: summary);
    private static UpdateService ReadyService(Host host)
    {
        var service = new UpdateService(new Backend(host));
        WpfTestHost.Pump(service.CheckAsync());
        WpfTestHost.Pump(service.DownloadAsync());
        return service;
    }
    private static Button Button(Window window, string name) => (Button)window.FindName(name);
    private static void Press(Window window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    private static void Flush() => WpfTestHost.Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task);
    private static void WaitUntil(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < limit) Flush();
        Assert.True(condition());
    }

    private sealed class Host : IUpdateDialogHost, IUpdateBlockerFinder
    {
        public UpdateShutdownBehavior Behavior { get; init; } = UpdateShutdownBehavior.Refuse;
        public string? Summary { get; init; }
        public IReadOnlyList<UpdateBlocker> Blockers { get; set; } = [];
        public IReadOnlyList<UpdateBlocker>? FinalBlockers { get; init; }
        public bool KeepOpen { get; init; }
        public List<string> Calls { get; } = [];
        public List<string> Errors { get; } = [];
        public List<(int Pid, bool Force)> CloseAttempts { get; } = [];
        public TaskCompletionSource Completed { get; } = new();
        public TaskCompletionSource Closed { get; } = new();
        public IUpdateLog Log { get; } = new SilentUpdateLog();
        public IUpdateBlockerClassifier Classifier { get; } = new ProgramClassifier();
        public IUpdateBlockerFinder Finder => this;
        public bool IsExiting => false;
        public string LatestReleasePage => "unused";
        public string ReleasePage(string version) => "unused";
        public Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token)
        { Calls.Add("check"); return Task.FromResult(new UpdatePreparationCheck(true, Summary)); }
        public Task<bool> PrepareAsync(object owner, CancellationToken token) { Calls.Add("prepare"); return Task.FromResult(true); }
        public void BeforeApply() => Calls.Add("before");
        public void ApplyFailed() => Calls.Add("failed");
        public void ExitAfterApply() { Calls.Add("exit"); Completed.TrySetResult(); }
        public void ShowError(string message, object owner) { Errors.Add(message); Completed.TrySetResult(); }
        public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true)
        { Calls.Add(enrich ? "find" : "final"); return new(!enrich && FinalBlockers != null ? FinalBlockers : Blockers); }
        public Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default)
        {
            CloseAttempts.Add((blocker.Pid, force));
            if (!KeepOpen) Blockers = Blockers.Where(b => b.Pid != blocker.Pid).ToArray();
            Closed.TrySetResult();
            return Task.FromResult(KeepOpen ? UpdateBlockerCloseResult.StillRunning : UpdateBlockerCloseResult.Closed);
        }
    }

    private sealed class Backend(Host host) : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string CurrentVersion => "1.0.0";
        public string ContentDirectory => "unused";
        public UpdateShutdownBehavior ShutdownBehavior => host.Behavior;
        public Task<UpdateRelease?> CheckAsync(CancellationToken token) => Task.FromResult<UpdateRelease?>(new("2.0.0", "", new object()));
        public Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token) => Task.CompletedTask;
        public void Apply(UpdateRelease release) => host.Calls.Add("apply");
    }
}
