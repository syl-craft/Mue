using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Mue.Avalonia.Tests;

public sealed class UpdateConsentTests
{
    [AvaloniaTheory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("Session fermée", false)]
    [InlineData("Session fermée", true)]
    [InlineData("", false)]
    public async Task PreparationFollowsAgreementAndFinalScan(string? summary, bool hasBlockers)
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Summary = summary, Blockers = hasBlockers ? [Fixtures.Blocker()] : [] };
        host.CloseOverride = (_, _) => { host.Blockers = []; return Task.FromResult(UpdateBlockerCloseResult.Closed); };
        var shown = 0;
        var window = new UpdatesDialog(service, host, async dialog =>
        {
            shown++;
            dialog.Show();
            Assert.DoesNotContain("prepare", host.Calls);
            if (hasBlockers)
            {
                Assert.Equal(UpdateTexts.T("Update_BlockedTitle"), dialog.Title);
                Assert.Equal(UpdateTexts.F("Update_CloseCount", 1), Fixtures.Control<Button>(dialog, "CloseButton").Content);
                await dialog.CloseSelectedAsync();
            }
            if (summary != null)
            {
                Assert.True(dialog.IsVisible);
                Assert.Null(dialog.Accepted);
                Assert.Equal(UpdateTexts.T("Update_ConfirmTitle"), dialog.Title);
                Assert.Equal(UpdateTexts.T("Update_ConfirmInstall"), Fixtures.Control<Button>(dialog, "CloseButton").Content);
                Assert.Equal(summary, Fixtures.Control<TextBlock>(dialog, "PreparationSummary").Text);
                Assert.False(Fixtures.Control<TextBlock>(dialog, "HelpText").IsVisible);
                Assert.DoesNotContain("prepare", host.Calls);
                await dialog.CloseSelectedAsync();
            }
            return dialog.Accepted;
        });
        window.Show();
        try
        {
            await window.InstallAsync();
            Assert.Equal(hasBlockers || summary != null ? 1 : 0, shown);
            Assert.Equal(new[] { "final", "prepare", "before", "exit" }, host.Calls.TakeLast(4));
            Assert.Single(host.Calls.Where(call => call == "prepare").ToArray());
            Assert.Equal(1, backend.Applies);
            Assert.Null(host.Error);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, "postpone")]
    [InlineData(true, "postpone")]
    [InlineData(false, "close")]
    [InlineData(true, "close")]
    [InlineData(false, "escape")]
    [InlineData(true, "escape")]
    public async Task PostponeWindowCloseAndEscapeNeverPrepare(bool hasBlockers, string dismissal)
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = hasBlockers ? [Fixtures.Blocker()] : [] };
        var shown = new TaskCompletionSource<UpdateBlockersDialog>();
        UpdatesDialog? owner = null;
        owner = new UpdatesDialog(service, host, dialog =>
        {
            var result = dialog.ShowDialog<bool?>(owner ?? throw new InvalidOperationException());
            shown.SetResult(dialog);
            return result;
        });
        owner.Show();
        var install = owner.InstallAsync();
        var dialog = await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.True(Fixtures.Control<Button>(dialog, "PostponeButton").IsCancel);
            if (dismissal == "postpone") Fixtures.Click(dialog, "PostponeButton");
            else if (dismissal == "close") dialog.Close();
            else
            {
                dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            }
            Assert.False(dialog.IsVisible);
            Assert.NotEqual(true, dialog.Accepted);
            await install;
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.DoesNotContain("final", host.Calls);
            Assert.Equal(0, backend.Applies);
            Assert.Equal(UpdateState.Ready, service.State);
            Assert.Null(host.Error);
        }
        finally { dialog.Close(); await install; owner.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("Session fermée")]
    public async Task RetryWithEmptyListOnlyAgreesWithoutSummary(string? summary)
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker()] };
        var dialog = Dialog(host, UpdateShutdownBehavior.Kill, summary);
        dialog.Show();
        try
        {
            host.Blockers = [];
            await dialog.RetryAsync();
            Assert.Equal(summary != null, dialog.IsVisible);
            Assert.Equal(summary == null, dialog.Accepted == true);
            if (summary != null)
            {
                Assert.Equal(UpdateTexts.T("Update_ConfirmTitle"), dialog.Title);
                Assert.Equal(UpdateTexts.T("Update_ConfirmInstall"), Fixtures.Control<Button>(dialog, "CloseButton").Content);
            }
            Assert.DoesNotContain("close", host.Calls);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Kill, false)]
    [InlineData(UpdateShutdownBehavior.Kill, true)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, false)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, true)]
    public async Task IgnoredGroupIsAcceptedWithoutClosingIt(UpdateShutdownBehavior behavior, bool unavailable)
    {
        var backend = Fixtures.Backend();
        backend.ShutdownBehavior = behavior;
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = [Fixtures.Blocker(started: 100, unavailable: unavailable)] };
        var window = new UpdatesDialog(service, host, async dialog =>
        {
            dialog.Show();
            var selection = Assert.Single(dialog.GetVisualDescendants().OfType<CheckBox>());
            // La liaison ne tient sa source que faiblement : un ramasse-miettes ne doit pas perdre le groupe coché.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            selection.IsChecked = false;
            Assert.False(host.Blockers[0].Selected);
            Assert.True(Fixtures.Control<Button>(dialog, "CloseButton").IsEnabled);
            Assert.Equal(UpdateTexts.T("Update_InstallIgnoring"), Fixtures.Control<Button>(dialog, "CloseButton").Content);
            Assert.Equal(unavailable, Fixtures.Control<TextBlock>(dialog, "DetectionWarning").IsVisible);
            await dialog.CloseSelectedAsync();
            Assert.Equal(new[] { (42, 100L) }, dialog.IgnoredBlockers);
            return dialog.Accepted;
        });
        window.Show();
        try
        {
            await window.InstallAsync();
            Assert.DoesNotContain("close", host.Calls);
            Assert.DoesNotContain("force", host.Calls);
            Assert.Equal(1, backend.Applies);
            Assert.Null(host.Error);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Kill, false)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, false)]
    [InlineData(UpdateShutdownBehavior.Kill, true)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, true)]
    public async Task ClosingSelectedOnlyAcceptsAlreadyIgnoredRemainingProcesses(UpdateShutdownBehavior behavior, bool newProcess)
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker(pid: 1), Fixtures.Blocker(pid: 2, selected: false, started: 100)] };
        var closed = new List<int>();
        host.CloseOverride = (blocker, _) =>
        {
            closed.Add(blocker.Pid);
            host.Blockers = newProcess
                ? [Fixtures.Blocker(pid: 2, selected: false, started: 100), Fixtures.Blocker(pid: 3, selected: false)]
                : [Fixtures.Blocker(pid: 2, selected: false, started: 100)];
            return Task.FromResult(UpdateBlockerCloseResult.Closed);
        };
        var dialog = Dialog(host, behavior, host.Summary);
        dialog.Show();
        try
        {
            await dialog.CloseSelectedAsync();
            Assert.Equal(new[] { 1 }, closed);
            Assert.Equal(new[] { (2, 100L) }, dialog.IgnoredBlockers);
            Assert.Equal(!newProcess, dialog.Accepted == true);
            Assert.Equal(newProcess, dialog.IsVisible);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Kill, 43, 100)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, 43, 100)]
    [InlineData(UpdateShutdownBehavior.Kill, 42, 200)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, 42, 200)]
    [InlineData(UpdateShutdownBehavior.Kill, 42, 0)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, 42, 0)]
    public async Task FinalScanRejectsAnIdentityNotAccepted(UpdateShutdownBehavior behavior, int pid, long started)
    {
        var backend = Fixtures.Backend();
        backend.ShutdownBehavior = behavior;
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = [Fixtures.Blocker(selected: false, started: 100)] };
        host.FindOverride = enrich => new(enrich ? host.Blockers : [Fixtures.Blocker(pid: pid, started: started)]);
        var window = new UpdatesDialog(service, host, async dialog =>
        {
            dialog.Show();
            await dialog.CloseSelectedAsync();
            return dialog.Accepted;
        });
        window.Show();
        try
        {
            await window.InstallAsync();
            Assert.Equal(UpdateTexts.T("Update_LockError"), host.Error);
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.Equal(0, backend.Applies);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RefuseCannotIgnoreAnUncheckedGroup()
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker(selected: false)] };
        var dialog = Dialog(host, UpdateShutdownBehavior.Refuse);
        dialog.Show();
        try
        {
            Assert.False(Fixtures.Control<Button>(dialog, "CloseButton").IsEnabled);
            await dialog.CloseSelectedAsync();
            Assert.True(dialog.IsVisible);
            Assert.Null(dialog.Accepted);
            Assert.Empty(dialog.IgnoredBlockers);
            Assert.Empty(host.Calls);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Kill, false)]
    [InlineData(UpdateShutdownBehavior.Kill, true)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, false)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, true)]
    public async Task UnknownIdentityCannotBeIgnoredAcrossScans(UpdateShutdownBehavior behavior, bool closeOther)
    {
        var backend = Fixtures.Backend();
        backend.ShutdownBehavior = behavior;
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var unknown = Fixtures.Blocker(pid: 2, selected: false, unavailable: true, started: 0);
        var host = new TestHost { Blockers = closeOther ? [Fixtures.Blocker(pid: 1, started: 100), unknown] : [unknown] };
        host.CloseOverride = (_, _) =>
        {
            host.Blockers = [Fixtures.Blocker(pid: 2, selected: false, unavailable: true, started: 0)];
            return Task.FromResult(UpdateBlockerCloseResult.Closed);
        };
        var shown = new TaskCompletionSource<UpdateBlockersDialog>();
        UpdatesDialog? window = null;
        window = new UpdatesDialog(service, host, dialog =>
        {
            var result = dialog.ShowDialog<bool?>(window ?? throw new InvalidOperationException());
            shown.SetResult(dialog);
            return result;
        });
        window.Show();
        var install = window.InstallAsync();
        var dialog = await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(closeOther, Fixtures.Control<Button>(dialog, "CloseButton").IsEnabled);
            await dialog.CloseSelectedAsync();
            Assert.Empty(dialog.IgnoredBlockers);
            Assert.Null(dialog.Accepted);
            host.Blockers = [Fixtures.Blocker(pid: 2, selected: false, unavailable: true, started: 0)];
            await dialog.RetryAsync();
            await dialog.CloseSelectedAsync();
            Assert.False(Fixtures.Control<Button>(dialog, "CloseButton").IsEnabled);
            Assert.Empty(dialog.IgnoredBlockers);
            Assert.Null(dialog.Accepted);
            Assert.True(dialog.IsVisible);
            Fixtures.Click(dialog, "PostponeButton");
            await install;
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.DoesNotContain("final", host.Calls);
            Assert.Equal(closeOther ? 1 : 0, host.Calls.Where(call => call == "close").Count());
            Assert.Equal(0, backend.Applies);
            Assert.Null(host.Error);
        }
        finally { dialog.Close(); await install; window.Close(); }
    }

    private static UpdateBlockersDialog Dialog(TestHost host, UpdateShutdownBehavior behavior, string? summary = null) =>
        new(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host, behavior, summary: summary);
}
