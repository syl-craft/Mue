using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

namespace Mue.Avalonia.Tests;

public sealed class CaptureTests
{
    [AvaloniaTheory]
    [InlineData(false, 440, 360)]
    [InlineData(false, 560, 480)]
    [InlineData(true, 440, 360)]
    [InlineData(true, 560, 480)]
    public async Task CaptureEveryState(bool dark, int width, int height)
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var previous = app.RequestedThemeVariant;
        var culture = CultureInfo.CurrentUICulture;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        UpdateTexts.Configure(null);
        try
        {
            foreach (var state in Enum.GetValues<UpdateState>())
            {
                var backend = Fixtures.Backend();
                var gate = new TaskCompletionSource();
                var preparing = new TaskCompletionSource();
                if (state == UpdateState.Unavailable) backend.IsInstalled = false;
                if (state == UpdateState.Idle) backend.Release = null;
                if (state == UpdateState.Failed) backend.CheckFailure = new IOException("Source indisponible");
                if (state == UpdateState.Checking) backend.WaitForCheck = _ => gate.Task;
                if (state == UpdateState.Downloading) backend.WaitForDownload = _ => gate.Task;
                using var service = new UpdateService(backend);
                var host = new TestHost();
                if (state == UpdateState.Preparing) host.PrepareOverride = async () => { preparing.SetResult(); await gate.Task; return false; };
                var dialog = new UpdatesDialog(service, host) { Width = width, Height = height };
                dialog.Show();
                Task? pending = null;
                if (state == UpdateState.Checking) pending = service.CheckAsync();
                else if (state != UpdateState.Unavailable) await service.CheckAsync();
                if (state == UpdateState.Downloading) pending = service.DownloadAsync();
                else if (state == UpdateState.Ready) await service.DownloadAsync();
                else if (state is UpdateState.Preparing or UpdateState.Installing)
                {
                    pending = dialog.InstallAsync();
                    if (state == UpdateState.Preparing) await preparing.Task;
                    else await pending;
                }
                Assert.Equal(state, service.State);
                Capture(dialog, state.ToString(), dark, width, height);
                Assert.Equal(service.Presentation.PrimaryKey == "Common_Close" ? "Fermer" : service.PrimaryLabel, Fixtures.Control<Button>(dialog, "InstallButton").Content);
                Assert.Equal(service.Presentation.SecondaryKey == "Common_Close" ? "Fermer" : service.SecondaryLabel, Fixtures.Control<Button>(dialog, "SecondaryButton").Content);
                Assert.Equal(service.PrimaryEnabled, Fixtures.Control<Button>(dialog, "InstallButton").IsEnabled);
                Assert.Equal(service.SecondaryEnabled, Fixtures.Control<Button>(dialog, "SecondaryButton").IsEnabled);
                gate.TrySetResult();
                if (pending != null) await pending;
                dialog.Close();
            }
            foreach (var incomplete in new[] { false, true })
            {
                var host = new TestHost { Blockers = [Fixtures.Blocker(), Fixtures.Blocker(pid: 91, selected: false, unavailable: incomplete)] };
                var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers,
                    host, UpdateShutdownBehavior.Kill, summary: host.Summary) { Width = width, Height = height };
                dialog.Show();
                Assert.Equal(incomplete, Fixtures.Control<TextBlock>(dialog, "DetectionWarning").IsVisible);
                Capture(dialog, incomplete ? "DetectionIncomplete" : "Blockers", dark, width, height);
                Fixtures.Control<ScrollViewer>(dialog, "DetailsScroll").ScrollToEnd();
                Capture(dialog, incomplete ? "DetectionIncomplete-details" : "Blockers-details", dark, width, height);
                dialog.Close();
            }
            foreach (var behavior in Enum.GetValues<UpdateShutdownBehavior>())
            {
                var host = new TestHost { Blockers = [Fixtures.Blocker(), Fixtures.Blocker(pid: 91, selected: false)] };
                var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers,
                    host, behavior, summary: host.Summary) { Width = width, Height = height };
                dialog.Show();
                if (behavior != UpdateShutdownBehavior.Kill) Capture(dialog, $"Blockers-{behavior}", dark, width, height);
                await dialog.CloseSelectedAsync();
                Capture(dialog, $"CooperativeFailure-{behavior}", dark, width, height);
                Fixtures.Control<ScrollViewer>(dialog, "DetailsScroll").ScrollToEnd();
                Capture(dialog, $"CooperativeFailure-{behavior}-details", dark, width, height);
                dialog.Close();
            }
            var results = Enum.GetValues<UpdateBlockerCloseResult>();
            var resultBlockers = results.Select((_, index) => Fixtures.Blocker(pid: index)).ToArray();
            var resultHost = new TestHost
            {
                Blockers = resultBlockers,
                CloseOverride = (blocker, _) => Task.FromResult(results[blocker.Pid]),
            };
            var resultDialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), resultBlockers,
                resultHost, UpdateShutdownBehavior.Kill) { Width = width, Height = height };
            resultDialog.Show();
            resultHost.Blockers = resultBlockers.Where(blocker => results[blocker.Pid] is UpdateBlockerCloseResult.StillRunning or UpdateBlockerCloseResult.AccessDenied).ToArray();
            await resultDialog.CloseSelectedAsync();
            Capture(resultDialog, "CloseResults", dark, width, height);
            var resultPanel = Fixtures.Control<StackPanel>(resultDialog, "CloseResults");
            foreach (var row in resultPanel.Children.OfType<TextBlock>())
            {
                row.BringIntoView();
                resultDialog.UpdateLayout();
                Capture(resultDialog, $"CloseResult-{results[resultPanel.Children.IndexOf(row)]}", dark, width, height);
            }
            resultDialog.Close();

        }
        finally { app.RequestedThemeVariant = previous; CultureInfo.CurrentUICulture = culture; UpdateTexts.Configure(null); }
    }

    private static void Capture(Window window, string state, bool dark, int width, int height)
    {
        var path = Path.Combine(Fixtures.Root, "artifacts", "avalonia", $"net{Environment.Version.Major}.0", $"{(dark ? "sombre" : "clair")}-{width}x{height}");
        Directory.CreateDirectory(path);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Aucune image rendue");
        Assert.Equal(width, frame.PixelSize.Width);
        Assert.Equal(height, frame.PixelSize.Height);
        frame.Save(Path.Combine(path, state + ".png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        var footer = Fixtures.Control<Border>(window, "Footer");
        Assert.True(footer.Bounds.Bottom <= height);
        Assert.True(footer.Bounds.Width <= width);
        foreach (var buttonName in window is UpdatesDialog ? new[] { "InstallButton", "SecondaryButton" } : new[] { "PostponeButton", "RetryButton", "CloseButton" })
        {
            var button = Fixtures.Control<Button>(window, buttonName);
            var origin = button.TranslatePoint(default, window) ?? throw new InvalidOperationException();
            Assert.InRange(origin.X, 0, width - button.Bounds.Width);
            Assert.InRange(origin.Y, 0, height - button.Bounds.Height);
        }
    }
}
