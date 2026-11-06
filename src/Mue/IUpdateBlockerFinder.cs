using System.Threading.Tasks;

namespace Mue;

public interface IUpdateBlockerFinder
{
    IReadOnlyList<UpdateBlocker> Find(string directory, IUpdateBlockerClassifier classifier, bool enrich = true);
    Task<bool> CloseAsync(UpdateBlocker blocker, bool force);
}
