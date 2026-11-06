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
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Kill)
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
