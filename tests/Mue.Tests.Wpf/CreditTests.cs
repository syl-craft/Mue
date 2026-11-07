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
            Assert.Equal($"{UpdateTexts.T("Update_Credit")}, github.com/syl-craft/Mue", AutomationProperties.GetName(credit));

            // Clavier réel : Tab depuis la case automatique atteint le lien, Entrée l'ouvre.
            window.Show();
            var automatic = (CheckBox)window.FindName("Automatic");
            Assert.True(automatic.Focus());
            automatic.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            Flush();
            Assert.Same(credit, System.Windows.Input.Keyboard.FocusedElement);
            var source = PresentationSource.FromVisual(credit) ?? throw new InvalidOperationException("fenêtre non affichée");
            credit.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0,
                System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
            Flush();
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

    [Fact]
    public Task Credit_feather_follows_the_footer_while_the_window_is_open() => WpfTestHost.Run(() =>
    {
        var host = new Host();
        using var service = AvailableService(host);
        // Ouverte en sombre (tons du logo), puis bascule en clair sans réactivation : une plume figée tomberait à 2,1:1.
        MueStyles.SetTheme(Application.Current, MueTheme.Dark);
        var window = new UpdatesDialog(service, host);
        try
        {
            window.Show();
            Flush();
            AssertReadableFeather(window);
            MueStyles.SetTheme(Application.Current, MueTheme.Light);
            Flush();
            AssertReadableFeather(window);
        }
        finally
        {
            window.Close();
            MueStyles.SetTheme(Application.Current, null);
            MueStyles.AddMissingDefaults(Application.Current);
        }
    });

    /// <summary>Lit les brosses réellement posées dans la fenêtre et le fond réellement dessiné du pied.</summary>
    private static void AssertReadableFeather(UpdatesDialog window)
    {
        var footer = ((Border)window.FindName("Footer")).Background as SolidColorBrush
            ?? throw new InvalidOperationException("pied sans couleur unie");
        foreach (var key in new[] { "Mue.Credit.FeatherDark", "Mue.Credit.FeatherLight" })
        {
            var tone = ((SolidColorBrush)window.Resources[key]).Color;
            var ratio = ContrastPairs.Ratio((tone.R, tone.G, tone.B), (footer.Color.R, footer.Color.G, footer.Color.B));
            Assert.True(ratio >= 3, $"{key} {tone} sur {footer.Color} : {ratio:0.00}");
        }
    }

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
