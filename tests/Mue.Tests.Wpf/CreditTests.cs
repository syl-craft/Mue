using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Mue.Wpf;

namespace Mue.Tests;

public partial class UpdateConsentTests
{
    [Fact]
    public Task Credit_is_shown_by_default_and_opens_the_Mue_page() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        var opened = new List<string>();
        var original = UpdatesDialog.OpenLink;
        UpdatesDialog.OpenLink = opened.Add;
        var window = new UpdatesDialog(service, host);
        try
        {
            var credit = Button(window, "CreditLink");
            Assert.Equal(Visibility.Visible, credit.Visibility);
            Assert.True(credit.IsTabStop);
            Assert.Equal(UpdateTexts.T("Update_Credit"), credit.Content);
            Assert.Equal($"{UpdateTexts.T("Update_Credit")}, GitHub", AutomationProperties.GetName(credit));
            Press(window, "CreditLink");
            Assert.Equal([UpdateCredit.Url], opened);
        }
        finally
        {
            UpdatesDialog.OpenLink = original;
            window.Close();
        }
    });

    [Fact]
    public Task Credit_can_be_hidden_by_the_host() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        var window = new UpdatesDialog(service, host, new UpdatesDialogOptions { ShowCredit = false });
        try { Assert.Equal(Visibility.Collapsed, Button(window, "CreditLink").Visibility); }
        finally { window.Close(); }
    });

    [Fact]
    public Task Credit_link_ignores_an_implicit_button_style_of_the_host() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        var hostStyle = new Style(typeof(Button));
        hostStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Red));
        Application.Current.Resources[typeof(Button)] = hostStyle;
        var window = new UpdatesDialog(service, host);
        try { Assert.NotSame(hostStyle, Button(window, "CreditLink").Style); }
        finally
        {
            Application.Current.Resources.Remove(typeof(Button));
            window.Close();
        }
    });

    [Fact]
    public Task Automatic_label_follows_the_Mue_text_brush_when_Mue_manages_the_theme() => WpfTestHost.Run(() =>
    {
        // Régression 0.4.0 : le style de CheckBox de Windows imposait un texte noir, illisible en sombre.
        var host = new Host();
        using var service = AvailableService(host);
        var window = new UpdatesDialog(service, host);
        try
        {
            var automatic = (CheckBox)window.FindName("Automatic");
            Assert.Same(Application.Current.TryFindResource("Brush.Text"), automatic.Foreground);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#F7F7F7")]
    [InlineData("#EEF0F3")]
    [InlineData("#292929")]
    [InlineData("#202020")]
    [InlineData("#0B0F14")]
    public void Credit_feather_stays_readable_on_light_and_dark_footers(string footer)
    {
        // WCAG 1.4.11 : 3:1 pour un élément graphique. Le logo d'origine (#F0A23A) tombe à 2,1:1 sur blanc.
        var background = (Color)ColorConverter.ConvertFromString(footer);
        var (dark, light) = UpdatesDialog.CreditFeatherColors(false, new SolidColorBrush(background));
        foreach (var tone in new[] { dark, light })
            Assert.True(ContrastPairs.Ratio((tone.R, tone.G, tone.B), (background.R, background.G, background.B)) >= 3,
                $"{tone} sur {footer}");
    }

    [Fact]
    public void Credit_feather_uses_the_system_text_color_in_high_contrast()
    {
        var (dark, light) = UpdatesDialog.CreditFeatherColors(true, null);
        Assert.Equal(SystemColors.WindowTextColor, dark);
        Assert.Equal(SystemColors.WindowTextColor, light);
    }
}
