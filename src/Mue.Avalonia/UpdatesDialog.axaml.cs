using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;

namespace Mue.Avalonia;

public partial class UpdatesDialog : Window
{
    private readonly UpdateService _updates;
    private readonly IUpdateDialogHost _host;
    private readonly Func<UpdateBlockersDialog, Task<bool?>> _showBlockers;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly CancellationTokenSource _lifetime = new();
    private UpdateNotes? _renderedNotes;
    private bool _preparing;
    private bool _installing;
    private bool _closed;
    private readonly bool _separateInstallStep;

    public UpdatesDialog(UpdateService updates, IUpdateDialogHost host,
        Func<UpdateBlockersDialog, Task<bool?>>? showBlockers = null)
    {
        InitializeComponent();
        _updates = updates;
        _host = host;
        _showBlockers = showBlockers ?? (dialog => dialog.ShowDialog<bool?>(this));
        DataContext = updates;
        _updates.PropertyChanged += Updates_Changed;
        _clock.Tick += Clock_Tick;
        _clock.Start();
        Closed += (_, _) =>
        {
            _closed = true;
            _lifetime.Cancel();
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
        RefreshDisplay();
    }

    private void Clock_Tick(object? sender, EventArgs e) => LastChecked.Text = _updates.LastChecked;
    private void Updates_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(RefreshDisplay); return; }
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_closed) return;
        InstallButton.Content = UpdateTexts.T(_updates.Presentation.PrimaryKey == "Common_Close" ? "Update_CloseWindow" : _updates.Presentation.PrimaryKey);
        // En deux étapes, une version disponible se télécharge d'abord : « Download » au lieu de « Download and install ».
        if (_separateInstallStep && _updates.State == UpdateState.Available) InstallButton.Content = UpdateTexts.T("Update_Download");
        SecondaryButton.Content = UpdateTexts.T(_updates.Presentation.SecondaryKey == "Common_Close" ? "Update_CloseWindow" : _updates.Presentation.SecondaryKey);
        if (_updates.State == UpdateState.Checking) SetPreparationSummary(null);
        InstallButton.Bind(ThemeProperty, new DynamicResourceExtension(_updates.PrimaryAccent ? "PrimaryButton" : "SecondaryButton"));
        StateLabel.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(_updates.State == UpdateState.Failed ? "Brush.Danger" : "Brush.TextLabel"));
        StatusBanner.Bind(Border.BackgroundProperty, new DynamicResourceExtension(_updates.State switch
        {
            UpdateState.Ready => "Brush.AccentTint", UpdateState.Failed => "Brush.DangerSurface", _ => "Brush.SurfaceCard",
        }));
        StatusBanner.Bind(Border.BorderBrushProperty, new DynamicResourceExtension(_updates.State switch
        {
            UpdateState.Ready => "Brush.AccentBorderSoft", UpdateState.Failed => "Brush.DangerBorder", _ => "Brush.Border",
        }));
        RenderNotes();
    }

    private void SetPreparationSummary(string? summary)
    {
        PreparationSummary.Text = summary;
        PreparationSummary.IsVisible = summary != null;
    }

    private void RenderNotes()
    {
        if (ReferenceEquals(_renderedNotes, _updates.ParsedNotes)) return;
        _renderedNotes = _updates.ParsedNotes;
        NotesBlocks.Children.Clear();
        foreach (var block in _renderedNotes.Blocks)
        {
            var text = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Margin = new Thickness(block.Kind == UpdateNoteKind.Bullet ? 18 + block.Indent * 6 : 0,
                    block.Kind is UpdateNoteKind.Heading or UpdateNoteKind.Subheading ? 10 : 0, 8, 6),
            };
            if (block.Kind is UpdateNoteKind.Heading or UpdateNoteKind.Subheading) text.FontWeight = FontWeight.SemiBold;
            var inlines = new InlineCollection();
            if (block.Kind == UpdateNoteKind.Bullet) inlines.Add(new Run("•  "));
            foreach (var inline in block.Inlines)
            {
                var run = new Run(inline.Text);
                if (inline.Kind == UpdateInlineKind.Bold) run.FontWeight = FontWeight.SemiBold;
                if (inline.Kind == UpdateInlineKind.Italic) run.FontStyle = FontStyle.Italic;
                if (inline.Kind == UpdateInlineKind.Code) run.FontFamily = new FontFamily("Consolas");
                inlines.Add(run);
            }
            text.Inlines = inlines;
            NotesBlocks.Children.Add(text);
        }
    }

    private async void Secondary_Click(object? sender, RoutedEventArgs e) => await ExecuteAsync(_updates.Presentation.Secondary);
    private async void Primary_Click(object? sender, RoutedEventArgs e) => await ExecuteAsync(_updates.Presentation.Primary);
    private async Task ExecuteAsync(UpdateAction action)
    {
        switch (action)
        {
            case UpdateAction.Check: await _updates.CheckAsync(); break;
            case UpdateAction.Close: Close(); break;
            case UpdateAction.Stop: _updates.CancelDownload(); break;
            case UpdateAction.Install: await InstallAsync(); break;
            case UpdateAction.DownloadLatest: OpenReleasePage(_host.LatestReleasePage); break;
        }
    }

    private void GitHub_Click(object? sender, RoutedEventArgs e) =>
        OpenReleasePage(_host.ReleasePage((_updates.Release?.Version ?? _updates.CurrentVersion).TrimStart('v')));

    private void OpenReleasePage(string page)
    {
        try { Process.Start(new ProcessStartInfo(page) { UseShellExecute = true }); }
        catch (Exception ex) { _host.Log.Warn(ex, "Cannot open release page"); _host.ShowError(ex.Message, this); }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_preparing && !_host.IsExiting) e.Cancel = true;
        if (!e.Cancel) _updates.CancelDownload();
        base.OnClosing(e);
    }

    internal async Task InstallAsync()
    {
        if (_installing || _closed) return;
        _installing = true;
        try
        {
            var downloadedBefore = _updates.State == UpdateState.Ready;
            if (!await _updates.DownloadAsync() || _closed) return;
            // En deux étapes, le téléchargement s'arrête sur « New version ready » : l'installation est un second clic.
            if (_separateInstallStep && downloadedBefore == false) return;
            _preparing = true;
            _updates.BeginPreparation();
            var check = await _host.CheckAsync(this, _lifetime.Token);
            SetPreparationSummary(check.Summary);
            if (!check.CanContinue) return;
            var scan = await Task.Run(() => _host.Finder.Find(_updates.BlockerQuery, _host.Classifier), _lifetime.Token);
            IReadOnlySet<(int Pid, long Started)> ignored = new HashSet<(int Pid, long Started)>();
            if (scan.Blockers.Count > 0 || check.Summary != null)
            {
                var dialog = new UpdateBlockersDialog(_updates.BlockerQuery, scan, _host,
                    _updates.ShutdownBehavior, summary: check.Summary);
                if (await _showBlockers(dialog) != true) return;
                ignored = dialog.IgnoredBlockers;
            }
            // Le contrôle final reste sans enrichissement ; l'installeur arbitre ensuite le remplacement.
            var finalScan = await Task.Run(() => _host.Finder.Find(_updates.BlockerQuery, _host.Classifier, enrich: false), _lifetime.Token);
            if (UpdateBlockers.HasUnacceptedBlockers(finalScan.Blockers, _updates.ShutdownBehavior, ignored)) throw new IOException(UpdateTexts.T("Update_LockError"));
            if (!await _host.PrepareAsync(this, _lifetime.Token)) return;
            _lifetime.Token.ThrowIfCancellationRequested();
            _host.BeforeApply();
            try { _updates.Apply(); }
            catch { _host.ApplyFailed(); throw; }
            _host.ExitAfterApply();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _host.Log.Warn(ex, "Cannot apply update"); _host.ShowError(ex.Message, this); }
        finally
        {
            _updates.CancelPreparation();
            _preparing = false;
            _installing = false;
        }
    }
}
