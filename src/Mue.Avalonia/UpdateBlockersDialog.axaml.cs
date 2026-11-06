using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Mue.Avalonia;

public partial class UpdateBlockersDialog : Window
{
    private readonly Func<Task<IReadOnlyList<UpdateBlocker>>> _refresh;
    private readonly Func<UpdateBlocker, bool, Task<UpdateBlockerCloseResult>> _close;
    private readonly IUpdateLog _log;
    private readonly UpdateShutdownBehavior _shutdownBehavior;
    private readonly HashSet<(int Pid, long Started)> _remaining = [];
    private readonly List<(UpdateBlocker Blocker, UpdateBlockerCloseResult Result)> _results = [];
    private IReadOnlyList<UpdateBlocker> _blockers = [];
    private bool _working;
    private bool _closed;
    internal bool? Accepted { get; private set; }

    public UpdateBlockersDialog(UpdateBlockerQuery query, IReadOnlyList<UpdateBlocker> blockers,
        IUpdateDialogHost host, UpdateShutdownBehavior shutdownBehavior,
        Func<Task<IReadOnlyList<UpdateBlocker>>>? refresh = null,
        Func<UpdateBlocker, bool, Task<UpdateBlockerCloseResult>>? close = null,
        bool incomplete = false, string? summary = null)
    {
        InitializeComponent();
        _log = host.Log;
        _shutdownBehavior = shutdownBehavior;
        _refresh = refresh ?? (() => Task.Run(() => host.Finder.Find(query, host.Classifier).Blockers));
        _close = close ?? ((blocker, force) => host.Finder.CloseAsync(blocker, force));
        PreparationSummary.Text = summary;
        PreparationSummary.IsVisible = summary != null;
        if (incomplete)
            foreach (var blocker in blockers.Where(b => b.Selected)) _remaining.Add((blocker.Pid, blocker.Started));
        ShowBlockers(blockers);
        UpdateTexts.LanguageChanged += LanguageChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            UpdateTexts.LanguageChanged -= LanguageChanged;
            foreach (var blocker in _blockers) blocker.PropertyChanged -= Blocker_Changed;
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_working) e.Cancel = true;
        base.OnClosing(e);
    }

    private void LanguageChanged(object? sender, EventArgs e) => ShowBlockers(_blockers);
    private void Blocker_Changed(object? sender, PropertyChangedEventArgs e) => UpdateLabels();

    private void ShowBlockers(IReadOnlyList<UpdateBlocker> blockers)
    {
        foreach (var blocker in _blockers) blocker.PropertyChanged -= Blocker_Changed;
        _blockers = blockers;
        foreach (var blocker in blockers) blocker.PropertyChanged += Blocker_Changed;
        _remaining.IntersectWith(blockers.Select(b => (b.Pid, b.Started)));
        RemainingProcesses.Children.Clear();
        foreach (var blocker in blockers.Where(b => _remaining.Contains((b.Pid, b.Started))))
            RemainingProcesses.Children.Add(ProcessRow(blocker));
        Processes.Children.Clear();
        var groups = UpdateBlockerGroups.Group(blockers.Where(b => !_remaining.Contains((b.Pid, b.Started))).ToArray());
        foreach (var group in groups)
        {
            var selection = new CheckBox
            {
                Content = new TextBlock { Text = $"{group.Name} · {group.Count}", TextWrapping = TextWrapping.Wrap },
                FontWeight = FontWeight.SemiBold,
            };
            selection.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(UpdateBlockerGroup.Selected)) { Source = group, Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(selection, group.Name);
            var help = new TextBlock { Text = group.Help, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
            help.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(group.IsRelay ? "Brush.TextLabel" : "Brush.WarnTextAlt"));
            var rows = new StackPanel();
            foreach (var blocker in group.Processes) rows.Children.Add(ProcessRow(blocker));
            var chevron = new global::Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M 0,4 L 4,0 L 8,4"), StrokeThickness = 1.4,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            };
            chevron.Bind(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, new DynamicResourceExtension("Brush.AccentText"));
            var disclosure = new ToggleButton { Content = chevron, IsChecked = true, Width = 28, Height = 28, MinHeight = 28, Padding = new Thickness(6) };
            disclosure.Bind(ThemeProperty, new DynamicResourceExtension("SecondaryButton"));
            AutomationProperties.SetName(disclosure, UpdateTexts.T("Update_ExpandGroup"));
            disclosure.IsCheckedChanged += (_, _) =>
            {
                rows.IsVisible = disclosure.IsChecked == true;
                chevron.Data = Geometry.Parse(rows.IsVisible ? "M 0,4 L 4,0 L 8,4" : "M 0,0 L 4,4 L 8,0");
            };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            header.Children.Add(new StackPanel { Spacing = 4, Children = { selection, help } });
            Grid.SetColumn(disclosure, 1);
            header.Children.Add(disclosure);
            var card = new Border
            {
                CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Padding = new Thickness(12),
                Child = new StackPanel { Spacing = 8, Children = { header, rows } },
            };
            card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Brush.SurfaceCard"));
            card.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Brush.Border"));
            Processes.Children.Add(card);
        }
        UpdateLabels();
    }

    private static Control ProcessRow(UpdateBlocker blocker)
    {
        var name = new TextBlock { Text = blocker.DisplayName, TextWrapping = TextWrapping.Wrap };
        var detail = new TextBlock { Text = blocker.Detail, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        detail.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Brush.TextDetail"));
        var path = new TextBlock { Text = blocker.File, FontSize = 11, TextWrapping = TextWrapping.Wrap, IsVisible = blocker.File.Length > 0 };
        path.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Brush.TextSubtle"));
        ToolTip.SetTip(path, blocker.File);
        var row = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6),
            Child = new StackPanel { Spacing = 3, Children = { name, detail, path } },
        };
        row.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Brush.Separator"));
        return row;
    }

    private void UpdateLabels()
    {
        var incomplete = _remaining.Count > 0;
        StateLabel.Text = UpdateTexts.T(incomplete ? "Update_IncompleteState" : "Update_StatePreparing").ToUpper(UpdateTexts.Formatting);
        Heading.Text = incomplete ? UpdateTexts.F("Update_IncompleteTitle", _remaining.Count) : UpdateTexts.T("Update_BlockedTitle");
        HelpText.Text = UpdateTexts.T(_shutdownBehavior switch
        {
            UpdateShutdownBehavior.Kill => incomplete ? "Update_IncompleteHelpForced" : "Update_BlockedHelpForced",
            UpdateShutdownBehavior.AskToQuit => incomplete ? "Update_IncompleteHelpAskToQuit" : "Update_BlockedHelpAskToQuit",
            _ => incomplete ? "Update_IncompleteHelp" : "Update_BlockedHelp",
        });
        DetectionWarning.IsVisible = _blockers.Where(b => b.Unavailable).Any();
        DetectionWarning.Text = BlockerTexts.DetectionIncomplete;
        ForceWarning.IsVisible = RemainingCard.IsVisible = incomplete;
        OtherHeading.IsVisible = incomplete && Processes.Children.Count > 0;
        CloseButton.Content = incomplete ? UpdateTexts.T("Update_ForceInstall") : UpdateTexts.F("Update_CloseCount", _blockers.Where(b => b.Selected).Count());
        CloseButton.Bind(ThemeProperty, new DynamicResourceExtension(incomplete ? "DangerButton" : "PrimaryButton"));
        CloseButton.IsEnabled = !_working && (incomplete || _blockers.Where(b => b.Selected).Any());
        StateLabel.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(incomplete ? "Brush.Danger" : "Brush.TextLabel"));
        CloseResults.Children.Clear();
        foreach (var (blocker, result) in _results)
            CloseResults.Children.Add(new TextBlock { Text = $"{blocker.Label} : {BlockerTexts.CloseResult(result)}", TextWrapping = TextWrapping.Wrap });
        CloseResults.IsVisible = _results.Count > 0;
    }

    internal Task RetryAsync() => RunAsync(RefreshAsync);

    private async Task RefreshAsync()
    {
        var blockers = await _refresh();
        var choices = _blockers.ToDictionary(b => (b.Pid, b.Started), b => b.Selected);
        foreach (var blocker in blockers)
            if (choices.TryGetValue((blocker.Pid, blocker.Started), out var selected)) blocker.Selected = selected;
        ShowBlockers(blockers);
        if (blockers.Count != 0) return;
        _working = false;
        Accepted = true;
        Close(true);
    }

    internal Task CloseSelectedAsync()
    {
        if (_working || _closed) return Task.CompletedTask;
        var force = _remaining.Count > 0;
        var selected = _blockers.Where(b => force ? _remaining.Contains((b.Pid, b.Started)) : b.Selected).ToArray();
        if (selected.Length == 0) return Task.CompletedTask;
        return RunAsync(async () =>
        {
            _results.Clear();
            foreach (var blocker in selected)
            {
                // Le finder Windows arrête les relais de force même si force vaut false.
                var result = blocker.IsRelay && !force ? UpdateBlockerCloseResult.StillRunning : await _close(blocker, force);
                _results.Add((blocker, result));
                _remaining.Add((blocker.Pid, blocker.Started));
            }
            await RefreshAsync();
        });
    }

    private async Task RunAsync(Func<Task> work)
    {
        if (_working || _closed) return;
        _working = true;
        RetryButton.IsEnabled = CloseButton.IsEnabled = PostponeButton.IsEnabled = Processes.IsEnabled = false;
        ErrorText.IsVisible = false;
        try { await work(); }
        catch (Exception ex)
        {
            _log.Warn(ex, "Cannot resolve update locks");
            ErrorText.Text = UpdateTexts.T("Update_LockError");
            ErrorText.IsVisible = true;
        }
        finally
        {
            _working = false;
            RetryButton.IsEnabled = PostponeButton.IsEnabled = Processes.IsEnabled = true;
            UpdateLabels();
        }
    }

    private async void Retry_Click(object? sender, RoutedEventArgs e) => await RetryAsync();
    private async void CloseSelected_Click(object? sender, RoutedEventArgs e) => await CloseSelectedAsync();
    private void Postpone_Click(object? sender, RoutedEventArgs e) { Accepted = false; Close(false); }
}
