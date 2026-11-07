using System.Windows;
using System.Windows.Controls;
using Mue.Wpf;

namespace Mue.Tests;

public partial class UpdateConsentTests
{
    [Theory]
    [InlineData(UpdateShutdownBehavior.Kill, "Update_ConfirmHelpForced")]
    [InlineData(UpdateShutdownBehavior.AskToQuit, "Update_ConfirmHelpAskToQuit")]
    [InlineData(UpdateShutdownBehavior.Refuse, null)]
    public Task ConfirmationWarnsWhenTheInstallerMayStopNewPrograms(UpdateShutdownBehavior behavior, string? helpKey) => WpfTestHost.Run(() =>
    {
        var host = new Host { Behavior = behavior };
        var dialog = Dialog(host, [], summary: "2 profiles will be migrated.");
        WpfTestHost.Show(dialog, current =>
        {
            var help = (TextBlock)current.FindName("HelpText");
            Assert.Equal(UpdateTexts.T("Update_ConfirmTitle"), current.Title);
            if (helpKey == null) Assert.Equal(Visibility.Collapsed, help.Visibility);
            else
            {
                Assert.Equal(Visibility.Visible, help.Visibility);
                Assert.Equal(UpdateTexts.T(helpKey), help.Text);
            }
        });
    });

    [Fact]
    public Task WithoutOptionsOneClickStillDownloadsAndInstalls() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        var window = new UpdatesDialog(service, host);
        try
        {
            Press(window, "InstallButton");
            WpfTestHost.Pump(host.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "apply", "exit" }, host.Calls);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task SeparateInstallStepStopsOnTheDownloadedVersion() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        var window = new UpdatesDialog(service, host, new UpdatesDialogOptions { SeparateInstallStep = true });
        try
        {
            Assert.Equal(UpdateTexts.T("Update_Download"), Button(window, "InstallButton").Content);
            Press(window, "InstallButton");
            WaitUntil(() => service.State == UpdateState.Ready);
            Flush();
            Assert.Empty(host.Calls);
            Assert.Equal(UpdateTexts.T("Update_RestartInstall"), Button(window, "InstallButton").Content);

            Press(window, "InstallButton");
            WpfTestHost.Pump(host.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "apply", "exit" }, host.Calls);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task OptionsCarryTheBlockersDisplay() => WpfTestHost.Run(() =>
    {
        var host = new Host { Summary = "Session will close" };
        using var service = ReadyService(host);
        var shown = 0;
        var window = new UpdatesDialog(service, host, new UpdatesDialogOptions
        {
            ShowBlockers = dialog => { shown++; return false; },
        });
        try
        {
            Press(window, "InstallButton");
            WaitUntil(() => shown == 1 && service.State == UpdateState.Ready);
            Assert.DoesNotContain("prepare", host.Calls);
        }
        finally { window.Close(); }
    });

    private static UpdateService AvailableService(Host host)
    {
        var service = new UpdateService(new Backend(host));
        WpfTestHost.Pump(service.CheckAsync());
        return service;
    }
}
