using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Mue.Tests;

namespace Mue.Avalonia.Tests;

/// <summary>Contrat de la 0.4.0 côté Avalonia : jeux par défaut, contraste, confirmation, téléchargement séparé, thème.</summary>
public sealed class Release040Tests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Default_sets_define_the_shared_keys_and_meet_WCAG_AA(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var defaults = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://Mue.Avalonia/Themes/Defaults.axaml"));

        Assert.All(Mue.MueStyleKeys.Required, key => Assert.True(defaults.TryGetResource(key, variant, out _), key));
        var brushes = (ResourceDictionary)defaults.ThemeDictionaries[variant];
        Assert.Equal(Mue.MueStyleKeys.Brushes.Order(), brushes.Keys.Cast<string>().Order());
        Assert.Empty(ContrastPairs.Failures(key =>
        {
            defaults.TryGetResource(key, variant, out var value);
            var color = ((ISolidColorBrush)(value ?? throw new InvalidOperationException(key))).Color;
            return (color.R, color.G, color.B);
        }));
    }

    [AvaloniaTheory]
    [InlineData(UpdateShutdownBehavior.Kill, "Update_ConfirmHelpForced")]
    [InlineData(UpdateShutdownBehavior.AskToQuit, "Update_ConfirmHelpAskToQuit")]
    [InlineData(UpdateShutdownBehavior.Refuse, null)]
    public void Confirmation_warns_when_the_installer_may_stop_new_programs(UpdateShutdownBehavior behavior, string? helpKey)
    {
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory("unused"), new([]), new TestHost(), behavior,
            summary: "2 profils seront migrés.");
        dialog.Show();
        try
        {
            var help = Fixtures.Control<TextBlock>(dialog, "HelpText");
            Assert.Equal(UpdateTexts.T("Update_ConfirmTitle"), dialog.Title);
            Assert.Equal(helpKey != null, help.IsVisible);
            if (helpKey != null) Assert.Equal(UpdateTexts.T(helpKey), help.Text);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task Separate_install_step_stops_on_the_downloaded_version()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Summary = null };
        var dialog = new UpdatesDialog(service, host, new UpdatesDialogOptions { SeparateInstallStep = true });
        dialog.Show();
        try
        {
            Assert.Equal(UpdateTexts.T("Update_Download"), Fixtures.Control<Button>(dialog, "InstallButton").Content);
            await dialog.InstallAsync();
            Assert.Equal(UpdateState.Ready, service.State);
            Assert.Empty(host.Calls);
            Assert.Equal(0, backend.Applies);

            await dialog.InstallAsync();
            Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "exit" }, host.Calls);
            Assert.Equal(1, backend.Applies);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task Without_options_one_click_still_downloads_and_installs()
    {
        var backend = Fixtures.Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var host = new TestHost { Summary = null };
        var dialog = new UpdatesDialog(service, host);
        dialog.Show();
        try
        {
            await dialog.InstallAsync();
            Assert.Equal(new[] { "check", "find", "final", "prepare", "before", "exit" }, host.Calls);
            Assert.Equal(1, backend.Applies);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public void Mue_windows_follow_the_application_theme_variant()
    {
        // Sous Windows, Avalonia peint la barre de titre (mode sombre DWM) d'après ActualThemeVariant :
        // il suffit que les fenêtres de Mue héritent du thème de l'application.
        var app = Application.Current ?? throw new InvalidOperationException();
        var requested = app.RequestedThemeVariant;
        app.RequestedThemeVariant = ThemeVariant.Dark;
        var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory("unused"), new([]), new TestHost(),
            UpdateShutdownBehavior.Kill, summary: "x");
        dialog.Show();
        try
        {
            Assert.Equal(ThemeVariant.Dark, dialog.ActualThemeVariant);
            app.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal(ThemeVariant.Light, dialog.ActualThemeVariant);
        }
        finally
        {
            dialog.Close();
            app.RequestedThemeVariant = requested;
        }
    }
}
