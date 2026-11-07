using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mue.Avalonia.Tests;

internal sealed class TestHost : IUpdateDialogHost, IUpdateBlockerFinder
{
    public List<string> Calls { get; } = [];
    public IReadOnlyList<UpdateBlocker> Blockers { get; set; } = [];
    public Func<bool, UpdateBlockerScan>? FindOverride { get; set; }
    public Func<UpdateBlocker, bool, Task<UpdateBlockerCloseResult>>? CloseOverride { get; set; }
    public Func<Task<UpdatePreparationCheck>>? CheckOverride { get; set; }
    public Func<Task<bool>>? PrepareOverride { get; set; }
    public string? Summary { get; set; } = "La session avec serveur-exemple sera fermée.";
    public bool CanContinue { get; set; } = true;
    public bool CanPrepare { get; set; } = true;
    public bool IsExiting { get; private set; }
    public string? Error { get; private set; }
    public object? CheckOwner { get; private set; }
    public IUpdateLog Log { get; } = new SilentUpdateLog();
    public IUpdateBlockerClassifier Classifier { get; } = new ProgramClassifier();
    public IUpdateBlockerFinder? FinderOverride { get; set; }
    public IUpdateBlockerFinder Finder => FinderOverride ?? this;
    public string LatestReleasePage => "https://example.invalid/releases/latest";
    public string ReleasePage(string version) => $"https://example.invalid/releases/{version}";
    public Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token)
    {
        CheckOwner = owner;
        Calls.Add("check");
        return CheckOverride?.Invoke() ?? Task.FromResult(new UpdatePreparationCheck(CanContinue, Summary));
    }
    public Task<bool> PrepareAsync(object owner, CancellationToken token)
    {
        Calls.Add("prepare");
        return PrepareOverride?.Invoke() ?? Task.FromResult(CanPrepare);
    }
    public void BeforeApply() => Calls.Add("before");
    public void ApplyFailed() => Calls.Add("failed");
    public void ExitAfterApply() { Calls.Add("exit"); IsExiting = true; }
    public void ShowError(string message, object owner) { Calls.Add("error"); Error = message; }
    public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true)
    {
        Calls.Add(enrich ? "find" : "final");
        return FindOverride?.Invoke(enrich) ?? new(Blockers);
    }
    public Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default)
    {
        Calls.Add(force ? "force" : "close");
        return CloseOverride?.Invoke(blocker, force) ?? Task.FromResult(UpdateBlockerCloseResult.StillRunning);
    }
}

internal static class Fixtures
{
    public const string LongNotes = "## 1.1.0 — 2026-11-06\n### Nouveautés\n- **Connexion** : reprise de la session après une interruption réseau, avec conservation des favoris et des préférences.\n- Prise en charge des chemins `C:/Applications/Mon application/Versions/1.1.0/configuration.json`.\n\n";
    public static FakeUpdateBackend Backend() => new()
    {
        Release = new("1.1.0", string.Concat(Enumerable.Repeat(LongNotes, 8)), new object(), 42_000_000),
        ContentDirectory = Path.GetTempPath(),
    };
    public static UpdateBlocker Blocker(int pid = 42, bool selected = true, bool unavailable = false, long started = 0) => new()
    {
        Pid = pid, Started = started, Name = "Éditeur de configuration", Selected = selected, Unavailable = unavailable,
        File = "C:/Utilisateurs/Exemple/Documents/Applications/Un dossier au nom particulièrement long/Versions/1.1.0/outils/editeur-de-configuration.exe",
    };
    public static T Control<T>(Window window, string name) where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException(name);
    public static void Click(Window window, string name) => Control<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Mue.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Mue.sln introuvable");
        }
    }
}
