using System.Threading.Tasks;

namespace Mue;

public interface IUpdateBlockerFinder
{
    UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true);
    Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default);
}

public enum UpdateBlockerCloseResult { Closed, StillRunning, AccessDenied, Gone, IdentityChanged }

public sealed record UpdateBlockerScan(IReadOnlyList<UpdateBlocker> Blockers)
{
    public bool Incomplete => Blockers.Where(b => b.Unavailable).Any();
}

public sealed record UpdateBlockerQuery(IReadOnlyList<string> Paths, bool IncludeCurrentProcess = false)
{
    public static UpdateBlockerQuery ForDirectory(string directory) => new([directory]);

    public bool Contains(string image) => Paths.Where(path =>
        string.Equals(Path.GetFullPath(image), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
        || UpdatePaths.IsWithin(image, path)).Any();
}
