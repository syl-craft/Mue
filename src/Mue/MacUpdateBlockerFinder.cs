using System.Runtime.Versioning;

namespace Mue;

[SupportedOSPlatform("macos")]
public sealed class MacUpdateBlockerFinder : IUpdateBlockerFinder
{
    private readonly MacUpdateBlockerOperations _operations;

    public MacUpdateBlockerFinder(IUpdateLog? log = null)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        _operations = new(new MacProcessAccess(), log ?? new SilentUpdateLog(), Environment.ProcessId,
            TimeSpan.FromSeconds(2));
    }

    public IReadOnlyList<UpdateBlocker> Find(string directory, IUpdateBlockerClassifier classifier, bool enrich = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var bundle = MacUpdateBlockerRules.NormalizePath(directory);
        if (bundle == null || !bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An absolute .app bundle path is required.", nameof(directory));
        return _operations.Find(MacProcessAccess.ResolvePath(bundle), classifier, enrich);
    }

    public Task<bool> CloseAsync(UpdateBlocker blocker, bool force) => _operations.CloseAsync(blocker, force);
}
