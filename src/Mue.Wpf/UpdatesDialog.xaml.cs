using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mue.Wpf;

public partial class UpdatesDialog : Window
{
    private readonly UpdateService _updates;
    private bool _preparing;
    private UpdateNotes? _renderedNotes;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly IUpdateDialogHost _host;
    private readonly Func<UpdateBlockersDialog, bool?> _showBlockers;
    private readonly bool _separateInstallStep;
    public UpdatesDialog(UpdateService updates, IUpdateDialogHost host, Func<UpdateBlockersDialog, bool?>? showBlockers = null)
    {
        InitializeComponent(); _updates = updates; _host = host; DataContext = updates;
        MueStyles.Attach(this);
        CreditLink.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"{UpdateTexts.T("Update_Credit")}, GitHub");
        Loaded += (_, _) => PaintCreditFeather();
        Activated += (_, _) => PaintCreditFeather();
        // Le style système de CheckBox impose sa couleur de texte : en sombre, le libellé resterait noir. Seulement quand
        // Mue gère le thème et que l'hôte ne fournit pas de style de CheckBox (DockPad garde son rendu au pixel).
        // Seules les ressources de l'application comptent : TryFindResource trouverait aussi le style de Windows.
        if (MueStyles.Theme != null && Application.Current?.Resources[typeof(CheckBox)] == null)
            Automatic.SetResourceReference(ForegroundProperty, "Brush.Text");
        _showBlockers = showBlockers ?? (dialog => dialog.ShowDialog());
        Automatic.IsChecked = _updates.Automatic;
        _updates.PropertyChanged += Updates_Changed;
        _clock.Tick += Clock_Tick;
        _clock.Start();
        Closed += (_, _) =>
        {
            _updates.PropertyChanged -= Updates_Changed;
            _clock.Stop();
            _clock.Tick -= Clock_Tick;
        };
        RefreshDisplay();
    }
    /// <summary>Surcharge de la 0.4.0 : le constructeur 0.3.0 reste inchangé.</summary>
    public UpdatesDialog(UpdateService updates, IUpdateDialogHost host, UpdatesDialogOptions options)
        : this(updates, host, options.ShowBlockers)
    {
        _separateInstallStep = options.SeparateInstallStep;
        CreditLink.Visibility = options.ShowCredit ? Visibility.Visible : Visibility.Collapsed;
        if (_separateInstallStep == false) return;
        UpdateTexts.LanguageChanged += LanguageChanged;
        Closed += (_, _) => UpdateTexts.LanguageChanged -= LanguageChanged;
        RefreshDisplay();
    }
    private void LanguageChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshDisplay);
    /// <summary>Ouverture du lien de crédit ; remplacée par les tests pour ne pas lancer de navigateur.</summary>
    internal static Action<string> OpenLink { get; set; } =
        url => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    private void Credit_Click(object sender, RoutedEventArgs e) => OpenLink(UpdateCredit.Url);

    /// <summary>
    /// Plume lisible sur le pied de l'hôte (WCAG 1.4.11, 3:1) : tons du logo sur fond sombre, tons foncés sur fond clair,
    /// couleur du texte système en contraste élevé.
    /// </summary>
    internal void PaintCreditFeather()
    {
        var (dark, light) = CreditFeatherColors(SystemParameters.HighContrast, TryFindResource("Brush.SurfaceCard") as SolidColorBrush);
        Resources["Mue.Credit.FeatherDark"] = new SolidColorBrush(dark);
        Resources["Mue.Credit.FeatherLight"] = new SolidColorBrush(light);
    }

    internal static (Color Dark, Color Light) CreditFeatherColors(bool highContrast, SolidColorBrush? footer)
    {
        if (highContrast) return (SystemColors.WindowTextColor, SystemColors.WindowTextColor);
        var background = footer?.Color ?? Colors.White;
        var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255;
        return luminance < 0.5
            ? (Color.FromRgb(0xD9, 0x77, 0x1A), Color.FromRgb(0xF0, 0xA2, 0x3A))
            : (Color.FromRgb(0xB3, 0x5C, 0x0A), Color.FromRgb(0xC9, 0x70, 0x0F));
    }

    private void Automatic_Click(object sender, RoutedEventArgs e) =>
        _updates.Automatic = Automatic.IsChecked == true;
    private void Clock_Tick(object? sender, EventArgs e) => LastChecked.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
    private void Updates_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(RefreshDisplay); return; }
        RefreshDisplay();
    }
    private void RefreshDisplay()
    {
        if (_updates.State == UpdateState.Checking) SetPreparationSummary(null);
        InstallButton.SetResourceReference(StyleProperty, _updates.PrimaryAccent ? "PrimaryButton" : "SecondaryButton");
        if (_separateInstallStep) ShowSeparateDownloadLabel();
        RenderNotes();
    }
    /// <summary>En deux étapes, une version disponible se télécharge d'abord : « Download » au lieu de « Download and install ».</summary>
    private void ShowSeparateDownloadLabel()
    {
        if (_updates.State == UpdateState.Available) InstallButton.Content = UpdateTexts.T("Update_Download");
        else InstallButton.SetBinding(ContentProperty, new System.Windows.Data.Binding(nameof(UpdateService.PrimaryLabel)));
    }
    private void SetPreparationSummary(string? summary)
    {
        PreparationSummary.Text = summary ?? "";
        PreparationSummary.Visibility = summary == null ? Visibility.Collapsed : Visibility.Visible;
    }
    private void RenderNotes()
    {
        if (ReferenceEquals(_renderedNotes, _updates.ParsedNotes)) return;
        _renderedNotes = _updates.ParsedNotes;
        NotesBlocks.Children.Clear();
        foreach (var block in _renderedNotes.Blocks)
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Margin = new Thickness(block.Kind == UpdateNoteKind.Bullet ? 18 + block.Indent * 6 : 0,
                    block.Kind is UpdateNoteKind.Heading or UpdateNoteKind.Subheading ? 10 : 0, 8, 6) };
            if (block.Kind is UpdateNoteKind.Heading or UpdateNoteKind.Subheading) text.FontWeight = FontWeights.SemiBold;
            if (block.Kind == UpdateNoteKind.Bullet) text.Inlines.Add(new Run("•  "));
            foreach (var inline in block.Inlines)
            {
                var run = new Run(inline.Text);
                if (inline.Kind == UpdateInlineKind.Bold) run.FontWeight = FontWeights.SemiBold;
                if (inline.Kind == UpdateInlineKind.Italic) run.FontStyle = FontStyles.Italic;
                if (inline.Kind == UpdateInlineKind.Code)
                {
                    run.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                    run.SetResourceReference(TextElement.BackgroundProperty, "Brush.SurfaceMuted");
                }
                text.Inlines.Add(run);
            }
            NotesBlocks.Children.Add(text);
        }
    }
    private async void Secondary_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(_updates.Presentation.Secondary, sender, e);
    private async void Primary_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(_updates.Presentation.Primary, sender, e);
    private async Task ExecuteAsync(UpdateAction action, object sender, RoutedEventArgs e)
    {
        switch (action)
        {
            case UpdateAction.Check: await _updates.CheckAsync(); break;
            case UpdateAction.Close: Close(); break;
            case UpdateAction.Stop: _updates.CancelDownload(); break;
            case UpdateAction.Install: Install_Click(sender, e); break;
            case UpdateAction.DownloadLatest: DownloadLatest_Click(sender, e); break;
        }
    }
    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        var version = (_updates.Release?.Version ?? _updates.CurrentVersion).TrimStart('v');
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            _host.ReleasePage(version)) { UseShellExecute = true });
    }
    private void DownloadLatest_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_host.LatestReleasePage) { UseShellExecute = true });
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_preparing && !_host.IsExiting) e.Cancel = true;
        if (!e.Cancel) _updates.CancelDownload();
        base.OnClosing(e);
    }
    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var downloadedBefore = _updates.State == UpdateState.Ready;
        if (_preparing || !await _updates.DownloadAsync()) return;
        // En deux étapes, le téléchargement s'arrête sur « New version ready » : l'installation est un second clic.
        if (_separateInstallStep && downloadedBefore == false) return;
        _preparing = true; InstallButton.IsEnabled = false;
        try
        {
            _updates.BeginPreparation();
            var check = await _host.CheckAsync(this, System.Threading.CancellationToken.None);
            SetPreparationSummary(check.Summary);
            if (!check.CanContinue) return;
            var scan = await Task.Run(() => _host.Finder.Find(_updates.BlockerQuery, _host.Classifier));
            IReadOnlySet<(int Pid, long Started)> ignored = new HashSet<(int Pid, long Started)>();
            if (scan.Blockers.Count > 0 || check.Summary != null)
            {
                var dialog = new UpdateBlockersDialog(_updates.BlockerQuery, scan, _host,
                    _updates.ShutdownBehavior, summary: check.Summary);
                if (IsVisible) dialog.Owner = this;
                if (_showBlockers(dialog) != true) return;
                ignored = dialog.IgnoredBlockers;
            }
            // Le contrôle final améliore le retour ; le moteur natif décide ensuite du remplacement.
            var finalScan = await Task.Run(() => _host.Finder.Find(_updates.BlockerQuery, _host.Classifier, enrich: false));
            if (UpdateBlockers.HasUnacceptedBlockers(finalScan.Blockers, _updates.ShutdownBehavior, ignored))
                throw new IOException(UpdateTexts.T("Update_LockError"));
            if (!await _host.PrepareAsync(this, System.Threading.CancellationToken.None)) return;
            _host.BeforeApply();
            try { _updates.Apply(); }
            catch { _host.ApplyFailed(); throw; }
            _host.ExitAfterApply();
        }
        catch (Exception ex) { _host.Log.Warn(ex, "Cannot apply update"); _host.ShowError(ex.Message, this); }
        finally
        {
            // Sans effet une fois l'outil de mise à jour lancé : l'état est alors Installing.
            _updates.CancelPreparation();
            _preparing = false; InstallButton.SetBinding(IsEnabledProperty, new System.Windows.Data.Binding(nameof(UpdateService.PrimaryEnabled)));
        }
    }
}
