using System.Windows;

namespace Mue.Sample.Wpf;

/// <summary>Hôte de démonstration : aucun programme réel n'est cherché, fermé ni installé.</summary>
internal sealed class DemoHost : IUpdateDialogHost, IUpdateBlockerFinder
{
    public static readonly UpdateBlocker[] Blockers =
    [
        new() { Pid = 4128, Started = DateTime.Today.AddHours(8.5).ToFileTime(), Name = "Sync Agent", Selected = false },
        new() { Pid = 9360, Started = DateTime.Today.AddHours(11.75).ToFileTime(), Name = "Preview", Selected = true },
    ];

    public IUpdateLog Log { get; } = new SilentUpdateLog();
    public IUpdateBlockerClassifier Classifier { get; } = new ProgramClassifier();
    public IUpdateBlockerFinder Finder => this;
    public bool IsExiting { get; private set; }
    public string LatestReleasePage => "https://example.invalid/notebook/releases/latest";
    public string ReleasePage(string version) => $"https://example.invalid/notebook/releases/{version}";

    public Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token) =>
        Task.FromResult(new UpdatePreparationCheck(true));
    public Task<bool> PrepareAsync(object owner, CancellationToken token) => Task.FromResult(true);
    public void BeforeApply() { }
    public void ApplyFailed() { }
    public void ExitAfterApply() { IsExiting = true; Application.Current.Shutdown(); }
    public void ShowError(string message, object owner) => MessageBox.Show(message, "Notebook");

    public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true) => new([]);
    public Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default) =>
        Task.FromResult(UpdateBlockerCloseResult.Closed);
}
