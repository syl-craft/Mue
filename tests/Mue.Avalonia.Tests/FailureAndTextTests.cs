using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Mue.Avalonia.Tests;

public sealed class FailureAndTextTests
{
    [AvaloniaTheory]
    [InlineData("check")]
    [InlineData("prepare")]
    [InlineData("cancel")]
    [InlineData("final")]
    public async Task RefusalCancellationAndNewBlockerPreventApplication(string failure)
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost
        {
            CanContinue = failure != "check", CanPrepare = failure != "prepare", Summary = null,
        };
        if (failure == "cancel") host.PrepareOverride = () => throw new OperationCanceledException();
        if (failure == "final") host.FindOverride = enrich => new(enrich ? [] : [Fixtures.Blocker()]);
        var dialog = new UpdatesDialog(service, host);
        dialog.Show();
        await dialog.InstallAsync();
        Assert.False(Fixtures.Control<TextBlock>(dialog, "PreparationSummary").IsVisible);
        Assert.Equal(0, backend.Applies);
        Assert.DoesNotContain("before", host.Calls);
        Assert.DoesNotContain("exit", host.Calls);
        Assert.Equal(UpdateState.Ready, service.State);
        if (failure is "check" or "final") Assert.DoesNotContain("prepare", host.Calls);
        if (failure == "cancel") Assert.Null(host.Error);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task ApplyFailureResumesHostAndKeepsDownloadedRelease()
    {
        var backend = Fixtures.Backend();
        backend.ApplyFailure = new IOException("Application refusée");
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost();
        var dialog = new UpdatesDialog(service, host);
        await dialog.InstallAsync();
        Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "failed", "error" }, host.Calls);
        Assert.Equal("Application refusée", host.Error);
        Assert.Equal(UpdateState.Ready, service.State);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task DownloadAndConstructionNeverApplyWithoutClick()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        await service.DownloadAsync();
        var host = new TestHost();
        var dialog = new UpdatesDialog(service, host);
        dialog.Show();
        Assert.Equal(UpdateState.Ready, service.State);
        Assert.Empty(host.Calls);
        Assert.Equal(0, backend.Applies);
        dialog.Close();
        Assert.Empty(host.Calls);
    }

    [AvaloniaFact]
    public async Task CloseDoubleClickDoesNotRepeatOperationsAndIdentityChangeDropsOldSelection()
    {
        var old = Fixtures.Blocker(started: 1);
        var replacement = Fixtures.Blocker(selected: false, started: 2);
        var gate = new TaskCompletionSource<UpdateBlockerCloseResult>();
        var host = new TestHost { Blockers = [old], CloseOverride = (_, _) => gate.Task };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, UpdateShutdownBehavior.Kill);
        dialog.Show();
        var closing = dialog.CloseSelectedAsync();
        await dialog.CloseSelectedAsync();
        dialog.Close();
        Assert.True(dialog.IsVisible);
        Assert.Equal(new[] { "close" }, host.Calls);
        host.Blockers = [replacement];
        gate.SetResult(UpdateBlockerCloseResult.IdentityChanged);
        await closing;
        Assert.False(replacement.Selected);
        Assert.False(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        Assert.False(Fixtures.Control<Button>(dialog, "CloseButton").IsEnabled);
        dialog.Close();
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Refuse, "Update_BlockedHelp", "Update_IncompleteHelp")]
    [InlineData(UpdateShutdownBehavior.AskToQuit, "Update_BlockedHelpAskToQuit", "Update_IncompleteHelpAskToQuit")]
    [InlineData(UpdateShutdownBehavior.Kill, "Update_BlockedHelpForced", "Update_IncompleteHelpForced")]
    public async Task ShutdownBehaviorSelectsD9Texts(UpdateShutdownBehavior behavior, string initial, string remaining)
    {
        var host = new TestHost { Blockers = [Fixtures.Blocker(unavailable: true)] };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), host.Blockers, host, behavior);
        dialog.Show();
        Assert.Equal(UpdateTexts.T(initial), Fixtures.Control<TextBlock>(dialog, "HelpText").Text);
        Assert.True(Fixtures.Control<TextBlock>(dialog, "DetectionWarning").IsVisible);
        Assert.False(Fixtures.Control<Border>(dialog, "ForceWarning").IsVisible);
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == host.Blockers[0].Detail);
        await dialog.CloseSelectedAsync();
        Assert.Equal(UpdateTexts.T(remaining), Fixtures.Control<TextBlock>(dialog, "HelpText").Text);
        dialog.Close();
    }

    [AvaloniaFact]
    public void TextsUseMueFrenchAndEnglishFallbackAndFollowHostLanguageChange()
    {
        var previous = CultureInfo.CurrentUICulture;
        using var service = new UpdateService(Fixtures.Backend());
        var dialog = new UpdatesDialog(service, new TestHost());
        dialog.Show();
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var texts = new PartialTexts();
            UpdateTexts.Configure(texts);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Mises à jour de l’hôte", dialog.Title);
            Assert.Equal("Rechercher", Fixtures.Control<Button>(dialog, "SecondaryButton").Content);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            texts.ChangeLanguage();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(UpdateTexts.T("Update_Check"), Fixtures.Control<Button>(dialog, "SecondaryButton").Content);
            Assert.NotEqual("Rechercher", Fixtures.Control<Button>(dialog, "SecondaryButton").Content);
            UpdateTexts.Configure(null);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(UpdateTexts.T("Update_Title"), dialog.Title);
            Assert.NotEqual("Mises à jour de l’hôte", dialog.Title);
        }
        finally { dialog.Close(); CultureInfo.CurrentUICulture = previous; UpdateTexts.Configure(null); }
    }

    private sealed class PartialTexts : IUpdateTexts
    {
        public bool TryGet(string key, out string text) { text = "Mises à jour de l’hôte"; return key == "Update_Title"; }
        public string T(string key) => "Mises à jour de l’hôte";
        public string F(string key, params object?[] args) => T(key);
        public CultureInfo Formatting => CultureInfo.CurrentUICulture;
        public event EventHandler? LanguageChanged;
        public void ChangeLanguage() => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
