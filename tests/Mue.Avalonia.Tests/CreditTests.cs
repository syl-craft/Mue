using Avalonia;
using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
            var credit = Fixtures.Control<Button>(dialog, "CreditLink");
            Assert.True(credit.IsVisible);
            Assert.True(credit.IsTabStop);
            Assert.Equal(UpdateTexts.T("Update_Credit"), credit.Content);
            Assert.Equal($"{UpdateTexts.T("Update_Credit")}, GitHub", AutomationProperties.GetName(credit));
            Fixtures.Click(dialog, "CreditLink");
            Assert.Equal([UpdateCredit.Url], opened);
        }
        finally
        {
            UpdatesDialog.OpenLink = original;
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task Credit_can_be_hidden_by_the_host()
    {
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        var dialog = new UpdatesDialog(service, new TestHost(), new UpdatesDialogOptions { ShowCredit = false });
        dialog.Show();
        try { Assert.False(Fixtures.Control<Button>(dialog, "CreditLink").IsVisible); }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public async Task Footer_buttons_stay_inside_the_window_with_the_credit()
    {
        using var service = new UpdateService(Fixtures.Backend());
        await service.CheckAsync();
        var dialog = new UpdatesDialog(service, new TestHost(), new UpdatesDialogOptions { SeparateInstallStep = true });
        dialog.Show();
        try
        {
            dialog.UpdateLayout();
            var install = Fixtures.Control<Button>(dialog, "InstallButton");
            var credit = Fixtures.Control<Button>(dialog, "CreditLink");
            var installRight = install.TranslatePoint(new(install.Bounds.Width, 0), dialog)?.X ?? double.MaxValue;
            var creditRight = credit.TranslatePoint(new(credit.Bounds.Width, 0), dialog)?.X ?? double.MaxValue;
            var installLeft = install.TranslatePoint(new(0, 0), dialog)?.X ?? 0;
            Assert.True(installRight <= dialog.Bounds.Width, "le bouton principal sort de la fenêtre");
            Assert.True(creditRight <= installLeft, "le crédit chevauche les boutons");
        }
        finally { dialog.Close(); }
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
