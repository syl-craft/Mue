using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace Mue.Avalonia.Tests;

public sealed class ConfirmationTests
{
    [AvaloniaFact]
    public async Task SummaryIsVisibleInBothWindowsBeforeAnyPreparationAndPostponeHasNoEffect()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = [Fixtures.Blocker()] };
        UpdatesDialog? updates = null;
        updates = new UpdatesDialog(service, host, blockers =>
        {
            var window = updates ?? throw new InvalidOperationException();
            var summary = Fixtures.Control<TextBlock>(window, "PreparationSummary");
            Assert.True(summary.IsVisible);
            Assert.Equal(host.Summary, summary.Text);
            window.UpdateLayout();
            Assert.True(summary.Bounds.Height > 0);
            Assert.True(summary.Bounds.Bottom <= Fixtures.Control<Border>(window, "Footer").Bounds.Top);
            Assert.Equal(host.Summary, Fixtures.Control<TextBlock>(blockers, "PreparationSummary").Text);
            Assert.True(Fixtures.Control<TextBlock>(blockers, "PreparationSummary").IsVisible);
            Assert.Equal(new[] { "check", "find" }, host.Calls);
            Assert.Equal(0, backend.Applies);
            blockers.Show();
            Fixtures.Click(blockers, "PostponeButton");
            Assert.False(blockers.Accepted);
            return Task.FromResult<bool?>(false);
        });
        updates.Show();
        await updates.InstallAsync();
        Assert.Equal(UpdateState.Ready, service.State);
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.DoesNotContain("close", host.Calls);
        Assert.Equal(0, backend.Applies);
        Assert.Same(updates, host.CheckOwner);
        updates.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalAgreementPrecedesSinglePreparationAndFinalScan(bool blockersPresent)
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = blockersPresent ? [Fixtures.Blocker()] : [] };
        var agreement = new TaskCompletionSource<bool?>();
        var shown = new TaskCompletionSource();
        UpdateBlockersDialog? child = null;
        var dialog = new UpdatesDialog(service, host, blockers =>
        {
            child = blockers;
            shown.SetResult();
            return agreement.Task;
        });
        dialog.Show();
        var install = dialog.InstallAsync();
        if (blockersPresent)
        {
            await shown.Task;
            Assert.DoesNotContain("prepare", host.Calls);
            await dialog.InstallAsync();
            host.Blockers = [];
            agreement.SetResult(true);
        }
        await install;
        Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "exit" }, host.Calls);
        Assert.Equal(1, backend.Applies);
        Assert.Equal(UpdateState.Installing, service.State);
        child?.Close();
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task ClosingBlockersDoesNotPrepare()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = [Fixtures.Blocker()] };
        var dialog = new UpdatesDialog(service, host, blockers =>
        {
            blockers.Show();
            blockers.Close();
            Assert.Null(blockers.Accepted);
            return Task.FromResult<bool?>(null);
        });
        await dialog.InstallAsync();
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(0, backend.Applies);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task ForceRequiresASecondClickAfterCooperativeFailure()
    {
        var blocker = Fixtures.Blocker();
        var host = new TestHost { Blockers = [blocker] };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Kill);
        dialog.Show();
        Assert.False(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        Assert.DoesNotContain("force", host.Calls);
        await dialog.CloseSelectedAsync();
        Assert.Equal(new[] { "close", "find" }, host.Calls);
        Assert.True(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        Assert.Equal(UpdateTexts.T("Update_ForceInstall"), Fixtures.Control<Button>(dialog, "CloseButton").Content);
        Assert.Null(dialog.Accepted);
        await dialog.CloseSelectedAsync();
        Assert.Equal(new[] { "close", "find", "force", "find" }, host.Calls);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task RelayIsNeverPassedToFinderBeforeForceConfirmation()
    {
        var relay = new UpdateBlocker { Pid = 42, Name = "Relais", IsRelay = true };
        var host = new TestHost { Blockers = [relay] };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Kill);
        dialog.Show();
        await dialog.CloseSelectedAsync();
        Assert.Equal(new[] { "find" }, host.Calls);
        Assert.True(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        await dialog.CloseSelectedAsync();
        Assert.Equal(new[] { "find", "force", "find" }, host.Calls);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task DetectionFailureIsNotAnEmptySuccessfulScan()
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker()], FindOverride = _ => throw new IOException("détection refusée") };
        var blockers = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Refuse);
        blockers.Show();
        await blockers.RetryAsync();
        Assert.Null(blockers.Accepted);
        Assert.True(blockers.IsVisible);
        Assert.True(Fixtures.Control<TextBlock>(blockers, "ErrorText").IsVisible);
        Assert.NotEmpty(Fixtures.Control<StackPanel>(blockers, "Processes").Children);
        host.FindOverride = _ => new([]);
        await blockers.RetryAsync();
        Assert.True(blockers.Accepted);
        Assert.False(blockers.IsVisible);
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        host.FindOverride = _ => throw new IOException("détection refusée");
        var updates = new UpdatesDialog(service, host);
        await updates.InstallAsync();
        Assert.Equal("détection refusée", host.Error);
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(0, backend.Applies);
        updates.Close();
    }

    [AvaloniaTheory]
    [InlineData(UpdateBlockerCloseResult.Closed)]
    [InlineData(UpdateBlockerCloseResult.StillRunning)]
    [InlineData(UpdateBlockerCloseResult.AccessDenied)]
    [InlineData(UpdateBlockerCloseResult.Gone)]
    [InlineData(UpdateBlockerCloseResult.IdentityChanged)]
    public async Task EveryCloseResultIsDisplayed(UpdateBlockerCloseResult result)
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker()], CloseOverride = (_, _) => Task.FromResult(result) };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Refuse);
        dialog.Show();
        await dialog.CloseSelectedAsync();
        var results = Fixtures.Control<StackPanel>(dialog, "CloseResults");
        Assert.True(results.IsVisible);
        Assert.Contains(BlockerTexts.CloseResult(result), Assert.IsType<TextBlock>(Assert.Single(results.Children)).Text);
        Assert.Equal(5, Enum.GetValues<UpdateBlockerCloseResult>().Select(BlockerTexts.CloseResult).Distinct().Count());
        dialog.Close();
    }
}
