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

    public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        var resolved = MacUpdateBlockerRules.ResolveQuery(query, MacProcessAccess.ResolvePath);
        return _operations.Find(resolved, classifier, enrich);
    }

    public Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default) =>
        _operations.CloseAsync(blocker, force, token);
}
