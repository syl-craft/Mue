using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Mue.Avalonia.Tests;

public sealed class LocalTextTests
{
    private static readonly (UpdateBlockerCloseResult Result, string Key, string French, string English)[] Results =
    [
        (UpdateBlockerCloseResult.Closed, "Update_CloseResult_Closed", "Fermé", "Closed"),
        (UpdateBlockerCloseResult.StillRunning, "Update_CloseResult_StillRunning", "Encore ouvert", "Still running"),
        (UpdateBlockerCloseResult.AccessDenied, "Update_CloseResult_AccessDenied", "Accès refusé", "Access denied"),
        (UpdateBlockerCloseResult.Gone, "Update_CloseResult_Gone", "Déjà terminé", "Already exited"),
        (UpdateBlockerCloseResult.IdentityChanged, "Update_CloseResult_IdentityChanged", "Identité du processus modifiée", "Process identity changed"),
    ];

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisplayedTextsUseCoreOrHostAndFollowLanguageChanges(bool hostTranslations)
    {
        var culture = CultureInfo.CurrentUICulture;
        var texts = new HostTexts(hostTranslations);
        UpdateTexts.Configure(texts);
        using var service = new UpdateService(Fixtures.Backend());
        var updates = new UpdatesDialog(service, new TestHost());
        var blockers = Results.Select((result, index) => Fixtures.Blocker(pid: index, unavailable: true)).ToArray();
        var host = new TestHost
        {
            Blockers = blockers,
            CloseOverride = (blocker, _) => Task.FromResult(Results[blocker.Pid].Result),
        };
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory(Path.GetTempPath()), new(blockers), host, UpdateShutdownBehavior.Kill);
        try
        {
            updates.Show();
            dialog.Show();
            // Les résultats restent lisibles même pour les processus disparus au nouveau scan.
            host.Blockers = [blockers[1], blockers[2]];
            await dialog.CloseSelectedAsync();
            foreach (var language in new[] { "fr-FR", "en-US" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                texts.ChangeLanguage();
                Dispatcher.UIThread.RunJobs();
                var french = language == "fr-FR";
                Assert.Equal(hostTranslations ? texts.T("Update_CloseWindow") : french ? "Fermer" : "Close",
                    Fixtures.Control<Button>(updates, "InstallButton").Content);
                Assert.Equal(UpdateTexts.T("Update_Check"), Fixtures.Control<Button>(updates, "SecondaryButton").Content);
                var detection = Fixtures.Control<TextBlock>(dialog, "DetectionWarning");
                Assert.True(detection.IsVisible);
                Assert.Equal(hostTranslations ? texts.T("Update_ScanIncomplete") : french
                    ? "Détection incomplète : certains processus n'ont pas pu être identifiés."
                    : "Detection incomplete: some processes could not be identified.", detection.Text);
                var displayed = Fixtures.Control<StackPanel>(dialog, "CloseResults");
                Assert.True(displayed.IsVisible);
                Assert.Equal(Results.Length, displayed.Children.Count);
                for (var index = 0; index < Results.Length; index++)
                {
                    var expected = hostTranslations ? texts.T(Results[index].Key) : french ? Results[index].French : Results[index].English;
                    Assert.Equal($"{blockers[index].Label} : {expected}", Assert.IsType<TextBlock>(displayed.Children[index]).Text);
                }
            }
        }
        finally
        {
            updates.Close();
            dialog.Close();
            CultureInfo.CurrentUICulture = culture;
            UpdateTexts.Configure(null);
        }
    }

    [AvaloniaTheory]
    [InlineData("fr-FR", "Fermer")]
    [InlineData("en-US", "Close")]
    public async Task CoreCloseTextAlsoAppearsOnSecondaryAction(string language, string expected)
    {
        var culture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        UpdateTexts.Configure(null);
        var backend = Fixtures.Backend();
        backend.CheckFailure = new IOException("Source indisponible");
        using var service = new UpdateService(backend);
        var dialog = new UpdatesDialog(service, new TestHost());
        try
        {
            dialog.Show();
            await service.CheckAsync();
            Assert.Equal(UpdateState.Failed, service.State);
            Assert.Equal(expected, Fixtures.Control<Button>(dialog, "SecondaryButton").Content);
        }
        finally { dialog.Close(); CultureInfo.CurrentUICulture = culture; UpdateTexts.Configure(null); }
    }

    private sealed class HostTexts(bool translations) : IUpdateTexts
    {
        public bool TryGet(string key, out string text)
        {
            text = T(key);
            return translations && (key is "Update_CloseWindow" or "Update_ScanIncomplete" || Results.Where(result => result.Key == key).Any());
        }
        public string T(string key) => $"Hôte {CultureInfo.CurrentUICulture.Name} : {key}";
        public string F(string key, params object?[] args) => T(key);
        public CultureInfo Formatting => CultureInfo.CurrentUICulture;
        public event EventHandler? LanguageChanged;
        public void ChangeLanguage() => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
