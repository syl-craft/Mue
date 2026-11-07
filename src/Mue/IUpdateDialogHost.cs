using System.Threading;
using System.Threading.Tasks;

namespace Mue;

public sealed record UpdatePreparationCheck(bool CanContinue, string? Summary = null);

public interface IUpdateDialogHost
{
    Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token);
    Task<bool> PrepareAsync(object owner, CancellationToken token);
    void BeforeApply();
    void ApplyFailed();
    void ExitAfterApply();
    bool IsExiting { get; }
    void ShowError(string message, object owner);
    IUpdateLog Log { get; }
    IUpdateBlockerClassifier Classifier { get; }
    IUpdateBlockerFinder Finder { get; }
    string ReleasePage(string version);
    string LatestReleasePage { get; }
}
