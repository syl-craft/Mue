using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

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
        var shown = new TaskCompletionSource<UpdateBlockersDialog>();
        UpdatesDialog? updates = null;
        updates = new UpdatesDialog(service, host, blockers =>
        {
            var result = blockers.ShowDialog<bool?>(updates ?? throw new InvalidOperationException());
            shown.SetResult(blockers);
            return result;
        });
        updates.Show();
        var install = updates.InstallAsync();
        var blockers = await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var summary = Fixtures.Control<TextBlock>(updates, "PreparationSummary");
            Assert.True(summary.IsVisible);
            Assert.Equal(host.Summary, summary.Text);
            updates.UpdateLayout();
            Assert.True(summary.Bounds.Height > 0);
            Assert.True(summary.Bounds.Bottom <= Fixtures.Control<Border>(updates, "Footer").Bounds.Top);
            Assert.Equal(host.Summary, Fixtures.Control<TextBlock>(blockers, "PreparationSummary").Text);
            Assert.True(Fixtures.Control<TextBlock>(blockers, "PreparationSummary").IsVisible);
            Assert.Equal(new[] { "check", "find" }, host.Calls);
            Assert.Equal(0, backend.Applies);
            Fixtures.Click(blockers, "PostponeButton");
            Assert.False(blockers.Accepted);
            await install;
            Assert.Equal(UpdateState.Ready, service.State);
            Assert.DoesNotContain("prepare", host.Calls);
            Assert.DoesNotContain("close", host.Calls);
            Assert.Equal(0, backend.Applies);
            Assert.Same(updates, host.CheckOwner);
            Assert.Null(host.Error);
        }
        finally { blockers.Close(); await install; updates.Close(); }
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
        var shown = new TaskCompletionSource<UpdateBlockersDialog>();
        UpdatesDialog? dialog = null;
        dialog = new UpdatesDialog(service, host, blockers =>
        {
            var result = blockers.ShowDialog<bool?>(dialog ?? throw new InvalidOperationException());
            shown.SetResult(blockers);
            return result;
        });
        dialog.Show();
        var install = dialog.InstallAsync();
        var child = await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.DoesNotContain("prepare", host.Calls);
            await dialog.InstallAsync();
            host.Blockers = [];
            if (blockersPresent) await child.RetryAsync();
            Assert.True(child.IsVisible);
            Assert.DoesNotContain("prepare", host.Calls);
            Fixtures.Click(child, "CloseButton");
            await install;
            Assert.True(child.Accepted);
            Assert.False(child.IsVisible);
            Assert.Equal(blockersPresent ? new[] { "check", "find", "find", "final", "prepare", "before", "exit" }
                : new[] { "check", "find", "final", "prepare", "before", "exit" }, host.Calls);
            Assert.Equal(1, backend.Applies);
            Assert.Equal(UpdateState.Installing, service.State);
            Assert.Null(host.Error);
        }
        finally { child.Close(); await install; dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingBlockersDoesNotPrepare()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Blockers = [Fixtures.Blocker()] };
        UpdateBlockersDialog? child = null;
        var dialog = new UpdatesDialog(service, host, blockers =>
        {
            child = blockers;
            blockers.Show();
            blockers.Close();
            return Task.FromResult<bool?>(null);
        });
        await dialog.InstallAsync();
        Assert.NotNull(child);
        Assert.Null(child.Accepted);
        Assert.Null(host.Error);
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(0, backend.Applies);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task ForceRequiresASecondClickAfterCooperativeFailure()
    {
        var blocker = Fixtures.Blocker();
        var host = new TestHost { Blockers = [blocker] };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host, UpdateShutdownBehavior.Kill);
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

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelayRequestsCooperativeCloseUnlessFinderForcesIt(bool windowsFinder)
    {
        if (windowsFinder && !OperatingSystem.IsWindows()) return;
        var relay = new UpdateBlocker { Pid = 42, Name = "Relais", IsRelay = true };
        var host = new TestHost { Blockers = [relay] };
        if (windowsFinder && OperatingSystem.IsWindows()) host.FinderOverride = new WindowsUpdateBlockerFinder();
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host,
            UpdateShutdownBehavior.Kill, refresh: () => Task.FromResult(host.Find(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Classifier)),
            close: (blocker, force) => host.CloseAsync(blocker, force));
        dialog.Show();
        await dialog.CloseSelectedAsync();
        Assert.Equal(windowsFinder ? new[] { "find" } : new[] { "close", "find" }, host.Calls);
        Assert.True(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        await dialog.CloseSelectedAsync();
        Assert.Equal(windowsFinder ? new[] { "find", "force", "find" } : new[] { "close", "find", "force", "find" }, host.Calls);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task AllSelectedProcessesStartClosingBeforeAnyCompletes()
    {
        var pending = Enumerable.Range(1, 20).ToDictionary(pid => pid, _ => new TaskCompletionSource<UpdateBlockerCloseResult>());
        var started = new List<int>();
        var host = new TestHost { Blockers = pending.Keys.Select(pid => Fixtures.Blocker(pid: pid, started: 100)).ToArray() };
        host.CloseOverride = (blocker, _) =>
        {
            started.Add(blocker.Pid);
            return pending[blocker.Pid].Task;
        };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host, UpdateShutdownBehavior.Kill);
        dialog.Show();
        var closing = dialog.CloseSelectedAsync();
        try
        {
            Assert.Equal(pending.Keys.ToArray(), started);
            Assert.False(closing.IsCompleted);
            pending[20].SetResult(UpdateBlockerCloseResult.AccessDenied);
            Assert.False(closing.IsCompleted);
            foreach (var completion in pending.Values) completion.TrySetResult(UpdateBlockerCloseResult.StillRunning);
            await closing;
            Assert.Equal(new[] { "find" }, host.Calls.TakeLast(1));
            Assert.Equal(20, Fixtures.Control<StackPanel>(dialog, "CloseResults").Children.Count);
            var last = Assert.IsType<TextBlock>(Fixtures.Control<StackPanel>(dialog, "CloseResults").Children.Last());
            Assert.Equal($"{host.Blockers[19].Label} : {UpdatePresentation.CloseResultText(UpdateBlockerCloseResult.AccessDenied)}", last.Text);
            Assert.True(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
            Assert.True(Fixtures.Control<Button>(dialog, "PostponeButton").IsEnabled);
        }
        finally
        {
            foreach (var completion in pending.Values) completion.TrySetResult(UpdateBlockerCloseResult.StillRunning);
            await closing;
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task DetectionFailureIsNotAnEmptySuccessfulScan()
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker()], FindOverride = _ => throw new IOException("détection refusée") };
        var blockers = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host, UpdateShutdownBehavior.Refuse);
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
    [InlineData("fr-FR", 440, 360)]
    [InlineData("en-US", 440, 360)]
    [InlineData("fr-FR", 560, 480)]
    [InlineData("en-US", 560, 480)]
    public async Task ForceWarningStaysInsideWindowAtBothScrollExtremes(string language, int width, int height)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(language);
        UpdateTexts.Configure(null);
        var host = new TestHost { Blockers = Enumerable.Range(1, 20).Select(pid => Fixtures.Blocker(pid: pid)).ToArray() };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(host.Blockers), host, UpdateShutdownBehavior.Kill)
        { Width = width, Height = height };
        try
        {
            dialog.Show();
            await dialog.CloseSelectedAsync();
            var warning = Fixtures.Control<Border>(dialog, "ForceWarning");
            var scroll = Fixtures.Control<ScrollViewer>(dialog, "DetailsScroll");
            var button = Fixtures.Control<Button>(dialog, "CloseButton");
            Assert.True(warning.IsVisible);
            Assert.True(button.IsEnabled);
            Assert.Equal(UpdateTexts.T("Update_ForceInstall"), button.Content);
            Assert.Equal(UpdateTexts.T("Update_ForceRisk"), Assert.IsType<TextBlock>(warning.Child).Text);
            Assert.DoesNotContain(warning.GetVisualAncestors(), ancestor => ancestor is ScrollViewer);
            dialog.UpdateLayout();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            global::Avalonia.Point? firstPosition = null;
            foreach (var atEnd in new[] { false, true })
            {
                if (atEnd) scroll.ScrollToEnd();
                else scroll.ScrollToHome();
                dialog.UpdateLayout();
                var origin = warning.TranslatePoint(default, dialog) ?? throw new InvalidOperationException();
                var buttonOrigin = button.TranslatePoint(default, dialog) ?? throw new InvalidOperationException();
                Assert.True(warning.Bounds.Height > 0);
                Assert.InRange(origin.X, 0, width - warning.Bounds.Width);
                Assert.InRange(origin.Y, 0, height - warning.Bounds.Height);
                Assert.True(origin.Y + warning.Bounds.Height <= buttonOrigin.Y);
                Assert.True(scroll.Bounds.Bottom <= origin.Y);
                if (firstPosition != null) Assert.Equal(firstPosition.Value, origin);
                firstPosition = origin;
            }
        }
        finally
        {
            dialog.Close();
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            UpdateTexts.Configure(null);
        }
    }
}
