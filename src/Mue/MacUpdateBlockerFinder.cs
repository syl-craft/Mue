using System.Runtime.Versioning;

namespace Mue;

[SupportedOSPlatform("macos15.0")]
public sealed class MacUpdateBlockerFinder : IUpdateBlockerFinder
{
    private readonly MacUpdateBlockerOperations _operations;

    public MacUpdateBlockerFinder(IUpdateLog? log = null) : this(TimeSpan.FromSeconds(5), log) { }

    public MacUpdateBlockerFinder(TimeSpan exitTimeout, IUpdateLog? log = null)
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(15)) throw new PlatformNotSupportedException();
        _operations = new(new MacProcessAccess(), log ?? new SilentUpdateLog(), Environment.ProcessId,
            exitTimeout);
    }

    public IReadOnlyList<UpdateBlocker> Find(string directory, IUpdateBlockerClassifier classifier, bool enrich = true)
    {
        var bundle = MacUpdateBlockerRules.ResolveBundlePath(directory, MacProcessAccess.ResolvePath);
        return MacUpdateBlockerAdapter.ToLegacy(_operations.Scan(bundle, classifier, enrich));
    }

    public async Task<bool> CloseAsync(UpdateBlocker blocker, bool force) =>
        MacUpdateBlockerAdapter.ToLegacy(await _operations.CloseAsync(blocker, force).ConfigureAwait(false));
}
