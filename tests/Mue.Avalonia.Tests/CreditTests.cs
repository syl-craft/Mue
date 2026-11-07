using Avalonia;
using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Media;
using Mue.Tests;

namespace Mue.Avalonia.Tests;

public sealed class CreditTests
{
    [AvaloniaFact]
    public async Task Credit_is_shown_by_default_and_opens_the_Mue_page()
    {
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        var opened = new List<string>();
        var original = UpdatesDialog.OpenLink;
        UpdatesDialog.OpenLink = opened.Add;
        var dialog = new UpdatesDialog(service, new TestHost());
        dialog.Show();
        try
        {
            var credit = Fixtures.Control<CreditLinkButton>(dialog, "CreditLink");
            Assert.True(credit.IsVisible);
            Assert.Equal(UpdateTexts.T("Update_Credit"), credit.Content);
            Assert.Equal($"{UpdateTexts.T("Update_Credit")}, github.com/syl-craft/Mue", AutomationProperties.GetName(credit));

            // Clavier réel : Tab depuis la case automatique atteint le lien, Entrée l'ouvre.
            Assert.True(Fixtures.Control<CheckBox>(dialog, "Automatic").Focus());
            dialog.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.Same(credit, dialog.FocusManager?.GetFocusedElement());
            dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.Equal([UpdateCredit.Url], opened);
        }
        finally
        {
            UpdatesDialog.OpenLink = original;
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_global_button_style_of_the_host_does_not_reach_the_credit_link()
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var hostStyle = new Style(selector => selector.OfType<Button>())
        {
            Setters = { new Setter(InputElement.IsTabStopProperty, false), new Setter(TemplatedControl.BackgroundProperty, Brushes.Red) },
        };
        app.Styles.Add(hostStyle);
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        var dialog = new UpdatesDialog(service, new TestHost());
        dialog.Show();
        try
        {
            var credit = Fixtures.Control<CreditLinkButton>(dialog, "CreditLink");
            Assert.True(credit.IsTabStop);
            Assert.NotEqual(Brushes.Red, credit.Background);
            // Le style de l'hôte s'applique bien aux autres boutons : le test n'est pas vide.
            Assert.False(Fixtures.Control<Button>(dialog, "InstallButton").IsTabStop);
        }
        finally
        {
            dialog.Close();
            app.Styles.Remove(hostStyle);
        }
    }

    [AvaloniaFact]
    public async Task Credit_can_be_hidden_by_the_host()
    {
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        var dialog = new UpdatesDialog(service, new TestHost(), new UpdatesDialogOptions { ShowCredit = false });
        dialog.Show();
        try { Assert.False(Fixtures.Control<CreditLinkButton>(dialog, "CreditLink").IsVisible); }
        finally { dialog.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("available")]
    [InlineData("downloading")]
    [InlineData("ready")]
    [InlineData("failed")]
    public async Task Footer_buttons_stay_inside_the_window_with_the_credit(string state)
    {
        var backend = Fixtures.Backend();
        if (state == "failed") backend.CheckFailure = new IOException("network down");
        var release = new TaskCompletionSource();
        if (state == "downloading") backend.WaitForDownload = _ => release.Task;
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        Task? download = null;
        if (state == "ready") await service.DownloadAsync();
        if (state == "downloading") download = service.DownloadAsync();
        // Largeur minimale de la fenêtre : le cas le plus serré pour le pied.
        var dialog = new UpdatesDialog(service, new TestHost(), new UpdatesDialogOptions { SeparateInstallStep = true }) { Width = 440 };
        dialog.Show();
        try
        {
            dialog.UpdateLayout();
            var credit = Fixtures.Control<CreditLinkButton>(dialog, "CreditLink");
            var creditRight = credit.TranslatePoint(new(credit.Bounds.Width, 0), dialog)?.X ?? double.MaxValue;
            foreach (var name in new[] { "SecondaryButton", "InstallButton" })
            {
                var button = Fixtures.Control<Button>(dialog, name);
                var left = button.TranslatePoint(new(0, 0), dialog)?.X ?? 0;
                var right = button.TranslatePoint(new(button.Bounds.Width, 0), dialog)?.X ?? double.MaxValue;
                Assert.True(right <= dialog.Bounds.Width, $"{name} sort de la fenêtre ({state})");
                Assert.True(creditRight <= left, $"le crédit chevauche {name} ({state})");
            }
        }
        finally
        {
            release.TrySetResult();
            if (download != null) await download;
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task Credit_feather_follows_the_footer_while_the_window_is_open()
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var requested = app.RequestedThemeVariant;
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        // Ouverte en sombre (tons du logo), puis bascule en clair : une plume figée tomberait à 2,1:1.
        app.RequestedThemeVariant = ThemeVariant.Dark;
        var dialog = new UpdatesDialog(service, new TestHost());
        dialog.Show();
        try
        {
            AssertReadableFeather(dialog);
            app.RequestedThemeVariant = ThemeVariant.Light;
            AssertReadableFeather(dialog);
        }
        finally
        {
            dialog.Close();
            app.RequestedThemeVariant = requested;
        }
    }

    private static void AssertReadableFeather(UpdatesDialog dialog)
    {
        var footer = (Fixtures.Control<Border>(dialog, "Footer").Background as ISolidColorBrush
            ?? throw new InvalidOperationException("pied sans couleur unie")).Color;
        foreach (var key in new[] { "Mue.Credit.FeatherDark", "Mue.Credit.FeatherLight" })
        {
            var tone = ((ISolidColorBrush)dialog.Resources[key]!).Color;
            var ratio = ContrastPairs.Ratio((tone.R, tone.G, tone.B), (footer.R, footer.G, footer.B));
            Assert.True(ratio >= 3, $"{key} {tone} sur {footer} : {ratio:0.00}");
        }
    }

    [AvaloniaTheory]
    [InlineData("#FFFFFF")]
    [InlineData("#F7F7F7")]
    [InlineData("#EEF0F3")]
    [InlineData("#292929")]
    [InlineData("#0B0F14")]
    public void Credit_feather_stays_readable_on_light_and_dark_footers(string footer)
    {
        var background = Color.Parse(footer);
        var (dark, light) = UpdatesDialog.CreditFeatherColors(background);
        foreach (var tone in new[] { dark, light })
            Assert.True(ContrastPairs.Ratio((tone.R, tone.G, tone.B), (background.R, background.G, background.B)) >= 3,
                $"{tone} sur {footer}");
    }
}
