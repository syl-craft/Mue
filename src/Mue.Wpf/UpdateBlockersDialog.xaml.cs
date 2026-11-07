using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace Mue.Wpf;

public partial class UpdateBlockersDialog : Window
{
    private readonly Func<Task<UpdateBlockerScan>> _refresh;
    private readonly Func<UpdateBlocker, bool, Task<UpdateBlockerCloseResult>> _close;
    private readonly IUpdateLog _log;
    private readonly UpdateShutdownBehavior _shutdownBehavior;
    private readonly bool _requiresConfirmation;
    private bool _scanIncomplete;
    private readonly HashSet<(int Pid, long Started)> _ignored = [];
    public IReadOnlySet<(int Pid, long Started)> IgnoredBlockers => _ignored;
    private bool CanIgnore => _shutdownBehavior != UpdateShutdownBehavior.Refuse;
    private bool Confirming => _requiresConfirmation && _blockers.Count == 0;
    private bool _working;
    private bool _incomplete;
    private readonly HashSet<(int Pid, long Started)> _remaining = [];
    private IReadOnlyList<UpdateBlocker> _blockers = [];

    // Les captures injectent aussi les opérations : aucun clic de fixture ne consulte les vrais processus.
    public UpdateBlockersDialog(UpdateBlockerQuery query, UpdateBlockerScan scan, IUpdateDialogHost host, UpdateShutdownBehavior shutdownBehavior,
        Func<Task<UpdateBlockerScan>>? refresh = null,
        Func<UpdateBlocker, bool, Task<UpdateBlockerCloseResult>>? close = null, bool incomplete = false, string? summary = null)
    {
        InitializeComponent();
        _log = host.Log;
        _shutdownBehavior = shutdownBehavior;
        _requiresConfirmation = summary != null;
        PreparationSummary.Text = summary ?? "";
        PreparationSummary.Visibility = summary == null ? Visibility.Collapsed : Visibility.Visible;
        _refresh = refresh ?? (() => Task.Run(() => host.Finder.Find(query, host.Classifier)));
        _close = close ?? ((blocker, force) => host.Finder.CloseAsync(blocker, force));
        if (incomplete)
            foreach (var blocker in scan.Blockers.Where(b => b.Selected)) _remaining.Add((blocker.Pid, blocker.Started));
        ShowBlockers(scan);
        UpdateTexts.LanguageChanged += LanguageChanged;
        Closed += (_, _) =>
        {
            UpdateTexts.LanguageChanged -= LanguageChanged;
            foreach (var blocker in _blockers) blocker.PropertyChanged -= Blocker_PropertyChanged;
        };
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_working) e.Cancel = true;
        base.OnClosing(e);
    }
    private void LanguageChanged(object? sender, EventArgs e) => ShowBlockers(new(_blockers));
    private void ShowBlockers(UpdateBlockerScan scan)
    {
        var blockers = scan.Blockers;
        _scanIncomplete = scan.Incomplete;
        foreach (var blocker in _blockers) blocker.PropertyChanged -= Blocker_PropertyChanged;
        _blockers = blockers;
        foreach (var blocker in _blockers) blocker.PropertyChanged += Blocker_PropertyChanged;
        _remaining.IntersectWith(blockers.Select(b => (b.Pid, b.Started)));
        _incomplete = _remaining.Count > 0;
        Processes.ItemsSource = UpdateBlockerGroups.Group(blockers.Where(b => !_remaining.Contains((b.Pid, b.Started))).ToArray());
        RemainingProcesses.ItemsSource = blockers.Where(b => _remaining.Contains((b.Pid, b.Started))).ToArray();
        UpdateLabels();
    }
    private void Blocker_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateLabels();
    private void UpdateLabels()
    {
        Title = UpdateTexts.T(Confirming ? "Update_ConfirmTitle" : "Update_BlockedTitle");
        StateLabel.Text = UpdateTexts.T(_incomplete ? "Update_IncompleteState" : "Update_StatePreparing").ToUpper(UpdateTexts.Formatting);
        Heading.Text = Confirming ? UpdateTexts.T("Update_ConfirmTitle") : _incomplete ? UpdateTexts.F("Update_IncompleteTitle", _remaining.Count) : UpdateTexts.T("Update_BlockedTitle");
        HelpText.Visibility = Confirming ? Visibility.Collapsed : Visibility.Visible;
        ScanIncomplete.Visibility = _scanIncomplete ? Visibility.Visible : Visibility.Collapsed;
        HelpText.Text = UpdateTexts.T(_shutdownBehavior switch
        {
            UpdateShutdownBehavior.Kill => _incomplete ? "Update_IncompleteHelpForced" : "Update_BlockedHelpForced",
            UpdateShutdownBehavior.AskToQuit => _incomplete ? "Update_IncompleteHelpAskToQuit" : "Update_BlockedHelpAskToQuit",
            _ => _incomplete ? "Update_IncompleteHelp" : "Update_BlockedHelp",
        });
        ForceWarning.Visibility = _incomplete ? Visibility.Visible : Visibility.Collapsed;
        RemainingCard.Visibility = _incomplete ? Visibility.Visible : Visibility.Collapsed;
        // Les programmes encore ouverts sans qu'on ait tenté de les fermer passent sous les restants, avec leur titre.
        OtherHeading.Visibility = _incomplete && Processes.HasItems ? Visibility.Visible : Visibility.Collapsed;
        Processes.Visibility = Visibility.Visible;
        var selectedCount = _blockers.Where(b => b.Selected).Count();
        CloseButton.Content = Confirming ? UpdateTexts.T("Update_ConfirmInstall") : _incomplete ? UpdateTexts.T("Update_ForceInstall")
            : CanIgnore && selectedCount == 0 ? UpdateTexts.T("Update_InstallIgnoring") : UpdateTexts.F("Update_CloseCount", selectedCount);
        CloseButton.SetResourceReference(StyleProperty, _incomplete ? "DangerButton" : "PrimaryButton");
        CloseButton.IsEnabled = !_working && (Confirming || CanIgnore || _incomplete || selectedCount > 0);
        StateLabel.SetResourceReference(TextBlock.ForegroundProperty, _incomplete ? "Brush.Danger" : "Brush.TextLabel");
    }
    private async Task RefreshAsync()
    {
        var scan = await _refresh();
        var blockers = scan.Blockers;
        // Une actualisation préserve le choix pour une même identité, pas pour un PID réutilisé.
        var choices = _blockers.ToDictionary(b => (b.Pid, b.Started), b => b.Selected);
        foreach (var blocker in blockers)
            if (choices.TryGetValue((blocker.Pid, blocker.Started), out var selected)) blocker.Selected = selected;
        ShowBlockers(scan);
        if (blockers.Count == 0 && !_requiresConfirmation) Accept();
    }
    private void Accept()
    {
        _working = false;
        DialogResult = true;
    }
    private async void Retry_Click(object sender, RoutedEventArgs e) => await RunAsync(RefreshAsync);
    private async void CloseSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_working) return;
        if (Confirming) { Accept(); return; }
        var selected = _blockers.Where(b => _incomplete ? _remaining.Contains((b.Pid, b.Started)) : b.Selected).ToArray();
        if (!CanIgnore && selected.Length == 0) return;
        _ignored.Clear();
        if (CanIgnore)
            foreach (var blocker in _blockers.Where(b => !b.Selected && !_remaining.Contains((b.Pid, b.Started))))
                _ignored.Add((blocker.Pid, blocker.Started));
        if (CanIgnore && selected.Length == 0) { Accept(); return; }
        await RunAsync(async () =>
        {
            // Le contrôle d'identité est refait par CloseAsync pour CHAQUE arrêt, y compris forcé.
            await Task.WhenAll(selected.Select(item => _close(item, _incomplete)));
            foreach (var blocker in selected) _remaining.Add((blocker.Pid, blocker.Started));
            await RefreshAsync();
            if (CanIgnore && _blockers.Count > 0 && !UpdateBlockers.HasUnacceptedBlockers(_blockers, _shutdownBehavior, _ignored)) Accept();
        });
    }
    private async Task RunAsync(Func<Task> work)
    {
        if (_working) return;
        _working = true;
        RetryButton.IsEnabled = CloseButton.IsEnabled = PostponeButton.IsEnabled = Processes.IsEnabled = false;
        ErrorText.Text = "";
        try { await work(); }
        catch (Exception ex) { _log.Warn(ex, "Cannot resolve update locks"); ErrorText.Text = UpdateTexts.T("Update_LockError"); }
        finally
        {
            _working = false;
            RetryButton.IsEnabled = PostponeButton.IsEnabled = Processes.IsEnabled = true;
            UpdateLabels();
        }
    }
}
