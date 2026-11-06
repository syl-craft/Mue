using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mue;

internal enum MacProcessState { Readable, Unavailable, Gone }
internal enum MacCloseResult { Closed, Gone, IdentityChanged, StillRunning, AccessDenied }

// audit_token_t : huit uint32_t, conservés tels que renvoyés par TASK_AUDIT_TOKEN.
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MacAuditToken(uint AuditUser, uint EffectiveUser, uint EffectiveGroup,
    uint RealUser, uint RealGroup, uint Pid, uint Session, uint Version);

internal sealed record MacProcessIdentity(int Pid, long Started, MacProcessState State, int Error = 0,
    MacAuditToken? Token = null);
internal sealed record MacProcessSnapshot(MacProcessIdentity Identity, string? Image);
internal sealed record MacBlockerEntry(MacProcessSnapshot Process, UpdateBlocker? Blocker);
internal sealed record MacBlockerScan(IReadOnlyList<MacBlockerEntry> Entries, bool Incomplete);

internal interface IMacProcessAccess
{
    IReadOnlyList<int> ListPids();
    MacProcessSnapshot ReadProcess(int pid);
    MacProcessIdentity ReadIdentity(int pid);
    int SendSignal(MacAuditToken token, int signal);
}

// Frontière temporaire 0.1.0 : les informations complètes restent disponibles en interne.
internal static class MacUpdateBlockerAdapter
{
    public static IReadOnlyList<UpdateBlocker> ToLegacy(MacBlockerScan scan) => scan.Entries
        .Select(entry => entry.Blocker).OfType<UpdateBlocker>()
        .OrderBy(blocker => blocker.Name).ThenBy(blocker => blocker.Pid).ToArray();

    public static bool ToLegacy(MacCloseResult result) =>
        result is MacCloseResult.Closed or MacCloseResult.Gone or MacCloseResult.IdentityChanged;
}

internal static class MacUpdateBlockerRules
{
    public static string ResolveBundlePath(string directory, Func<string, string> resolve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (directory[0] != '/' || directory.Contains('\0')
            || !directory.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An absolute .app bundle path is required.", nameof(directory));
        // realpath doit traverser les liens symboliques AVANT de traiter les segments « .. ».
        return resolve(directory);
    }

    public static bool IsWithinBundle(string? image, string directory)
    {
        var bundle = NormalizeResolvedPath(directory);
        var executable = NormalizeResolvedPath(image);
        return bundle != null && executable != null
            && executable.StartsWith(bundle + "/", StringComparison.Ordinal);
    }

    private static string? NormalizeResolvedPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Contains('\0')) return null;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Les chemins natifs sont résolus ; un parent restant ne doit jamais être réduit lexicalement.
        if (parts.Contains("..")) return null;
        return "/" + string.Join('/', parts.Where(part => part != "."));
    }

    public static long ToFileTime(ulong seconds, ulong microseconds)
    {
        if (microseconds >= 1_000_000) return 0;
        try
        {
            var ticks = checked((long)seconds * TimeSpan.TicksPerSecond + (long)microseconds * 10);
            return DateTime.UnixEpoch.AddTicks(ticks).ToFileTimeUtc();
        }
        catch (OverflowException) { return 0; }
        catch (ArgumentOutOfRangeException) { return 0; }
    }

    public static MacCloseResult? CheckIdentity(UpdateBlocker blocker, MacProcessIdentity identity,
        MacAuditToken? detectedToken)
    {
        if (identity.Pid != blocker.Pid) return MacCloseResult.AccessDenied;
        if (identity.State == MacProcessState.Gone) return MacCloseResult.Gone;
        if (identity.State != MacProcessState.Readable || identity.Started <= 0) return MacCloseResult.AccessDenied;
        if (!UpdateBlockers.MatchesIdentity(blocker, identity.Started)) return MacCloseResult.IdentityChanged;
        if (detectedToken != null && identity.Token != null
            && detectedToken.Value.Version != identity.Token.Value.Version) return MacCloseResult.IdentityChanged;
        if (identity.Token == null || identity.Token.Value.Pid != blocker.Pid) return MacCloseResult.AccessDenied;
        return null;
    }
}

internal sealed class MacUpdateBlockerOperations
{
    private readonly IMacProcessAccess _processes;
    private readonly IUpdateLog _log;
    private readonly int _currentPid;
    private readonly TimeSpan _exitTimeout;
    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<UpdateBlocker, MacProcessIdentity> _detectedIdentities = new();

    public MacUpdateBlockerOperations(IMacProcessAccess processes, IUpdateLog log, int currentPid,
        TimeSpan exitTimeout, TimeProvider? time = null)
    {
        if (exitTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(exitTimeout));
        _processes = processes;
        _log = log;
        _currentPid = currentPid;
        _exitTimeout = exitTimeout;
        _time = time ?? TimeProvider.System;
    }

    public MacBlockerScan Scan(string directory, IUpdateBlockerClassifier classifier, bool enrich)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        var entries = new List<MacBlockerEntry>();
        var incomplete = false;
        foreach (var pid in _processes.ListPids().Distinct())
        {
            if (pid <= 0 || pid == _currentPid) continue;
            var process = _processes.ReadProcess(pid);
            if (process.Identity.State == MacProcessState.Gone) continue;
            var unavailable = process.Identity.State != MacProcessState.Readable || process.Image == null
                || process.Identity.Started <= 0 || process.Identity.Token == null;
            if (unavailable)
            {
                incomplete = true;
                process = process with { Identity = process.Identity with { State = MacProcessState.Unavailable } };
                ReportUnavailable(pid, process.Identity.Error);
            }
            var image = process.Image;
            if (!MacUpdateBlockerRules.IsWithinBundle(image, directory) || image == null
                || classifier.IsIgnored(image, directory))
            {
                if (unavailable) entries.Add(new(process, null));
                continue;
            }
            var classification = enrich
                ? classifier.Classify(new(image, directory, null, null, null))
                : new UpdateBlockerClassification(false, null);
            var blocker = new UpdateBlocker
            {
                Pid = pid,
                Started = unavailable ? 0 : process.Identity.Started,
                Name = image[(image.LastIndexOf('/') + 1)..],
                File = image,
                IsRelay = classification.IsRelay,
                ParentName = classification.IsRelay ? classification.Program : null,
            };
            _detectedIdentities.Add(blocker, process.Identity);
            entries.Add(new(process, blocker));
        }
        return new(entries, incomplete);
    }

    public async Task<MacCloseResult> CloseAsync(UpdateBlocker blocker, bool force)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        if (blocker.Pid <= 0 || blocker.Pid == _currentPid || blocker.Started <= 0) return MacCloseResult.AccessDenied;
        _detectedIdentities.TryGetValue(blocker, out var detected);
        var token = detected?.Token;
        var identity = _processes.ReadIdentity(blocker.Pid);
        var result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, token);
        if (result != null) return ReportResult(result.Value, identity);
        // Ne jamais fabriquer une autorisation à la fermeture à partir d'un PID seul ou d'une nouvelle lecture.
        if (token == null || token.Value.Pid != blocker.Pid) return MacCloseResult.AccessDenied;

        var error = _processes.SendSignal(token.Value, force ? 9 : 15);
        if (error == 3) // ESRCH : disparu ou génération changée pendant l'appel natif.
        {
            identity = _processes.ReadIdentity(blocker.Pid);
            result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, token);
            return ReportResult(result ?? MacCloseResult.StillRunning, identity);
        }
        if (error != 0)
        {
            _log.Warn(new Win32Exception(error), $"Cannot close an update lock owner (PID {blocker.Pid})");
            return error is 1 or 13 or 45 or 78 ? MacCloseResult.AccessDenied : MacCloseResult.StillRunning;
        }

        var started = _time.GetTimestamp();
        while (true)
        {
            identity = _processes.ReadIdentity(blocker.Pid);
            result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, token);
            if (result == MacCloseResult.Gone) return MacCloseResult.Closed;
            if (result != null) return ReportResult(result.Value, identity);
            var remaining = _exitTimeout - _time.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) return MacCloseResult.StillRunning;
            var delay = remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50);
            await Task.Delay(delay, _time).ConfigureAwait(false);
        }
    }

    private MacCloseResult ReportResult(MacCloseResult result, MacProcessIdentity identity)
    {
        if (result == MacCloseResult.AccessDenied) ReportUnavailable(identity.Pid, identity.Error);
        return result;
    }

    private void ReportUnavailable(int pid, int error) =>
        _log.Warn(new Win32Exception(error), $"Cannot inspect process (PID {pid}); update blocker detection may be incomplete");
}
