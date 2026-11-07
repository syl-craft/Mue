using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mue;

internal enum MacProcessState { Readable, Unavailable, Gone }

// audit_token_t : huit uint32_t, conservés tels que renvoyés par TASK_AUDIT_TOKEN.
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MacAuditToken(uint AuditUser, uint EffectiveUser, uint EffectiveGroup,
    uint RealUser, uint RealGroup, uint Pid, uint Session, uint Version);

internal sealed record MacProcessIdentity(int Pid, long Started, MacProcessState State, int Error = 0,
    MacAuditToken? Token = null);
internal sealed record MacProcessSnapshot(MacProcessIdentity Identity, string? Image);

internal interface IMacProcessAccess
{
    IReadOnlyList<int> ListPids();
    MacProcessSnapshot ReadProcess(int pid);
    MacProcessIdentity ReadIdentity(int pid);
    int SendSignal(MacAuditToken token, int signal);
}

internal static class MacUpdateBlockerRules
{
    public static UpdateBlockerQuery ResolveQuery(UpdateBlockerQuery query, Func<string, string> resolve)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Paths);
        foreach (var path in query.Paths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (path[0] != '/' || path.Contains('\0'))
                throw new ArgumentException("Un chemin macOS absolu est requis.", nameof(query));
        }
        // realpath doit traverser les liens symboliques AVANT de traiter les segments « .. ».
        return query with { Paths = query.Paths.Select(resolve).Distinct(StringComparer.Ordinal).ToArray() };
    }

    public static bool MatchesPath(string? image, string path)
    {
        var target = NormalizeResolvedPath(path);
        var executable = NormalizeResolvedPath(image);
        return target != null && executable != null
            && (executable == target || executable.StartsWith(target.TrimEnd('/') + "/", StringComparison.Ordinal));
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

    public static UpdateBlockerCloseResult? CheckIdentity(UpdateBlocker blocker, MacProcessIdentity identity,
        MacAuditToken? detectedToken)
    {
        if (identity.Pid != blocker.Pid) return UpdateBlockerCloseResult.AccessDenied;
        if (identity.State == MacProcessState.Gone) return UpdateBlockerCloseResult.Gone;
        if (identity.State != MacProcessState.Readable || identity.Started <= 0) return UpdateBlockerCloseResult.AccessDenied;
        if (!UpdateBlockers.MatchesIdentity(blocker, identity.Started)) return UpdateBlockerCloseResult.IdentityChanged;
        if (detectedToken != null && identity.Token != null
            && detectedToken.Value.Version != identity.Token.Value.Version) return UpdateBlockerCloseResult.IdentityChanged;
        if (identity.Token == null || identity.Token.Value.Pid != blocker.Pid) return UpdateBlockerCloseResult.AccessDenied;
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

    public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(classifier);
        if (query.Paths.Count == 0) return new([]);
        var blockers = new List<UpdateBlocker>();
        foreach (var pid in _processes.ListPids().Distinct())
        {
            if (pid <= 0 || (!query.IncludeCurrentProcess && pid == _currentPid)) continue;
            var process = _processes.ReadProcess(pid);
            if (process.Identity.State == MacProcessState.Gone) continue;
            var image = process.Image;
            // Sans chemin, rien ne rattache le processus à l'appli : l'ignorer, comme Windows ignore un processus
            // illisible que Restart Manager ne signale pas. Il ne pourrait ni être ignoré ni fermé, et bloquerait tout.
            if (image == null) continue;
            if (!query.Paths.Where(path => MacUpdateBlockerRules.MatchesPath(image, path)).Any()
                || classifier.IsIgnored(image, query)) continue;
            var unavailable = process.Identity.State != MacProcessState.Readable
                || process.Identity.Started <= 0 || process.Identity.Token == null;
            if (unavailable) ReportUnavailable(pid, process.Identity.Error);
            var classification = enrich && !unavailable
                ? classifier.Classify(new(image, query, null, null, null))
                : new UpdateBlockerClassification(false, null);
            var blocker = new UpdateBlocker
            {
                Pid = pid,
                Started = unavailable ? 0 : process.Identity.Started,
                Name = image[(image.LastIndexOf('/') + 1)..],
                File = image,
                Unavailable = unavailable,
                IsRelay = classification.IsRelay,
                ParentName = classification.IsRelay ? classification.Program : null,
            };
            _detectedIdentities.Add(blocker, process.Identity);
            blockers.Add(blocker);
        }
        return new(blockers.OrderBy(blocker => blocker.Name).ThenBy(blocker => blocker.Pid).ToArray());
    }

    public async Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        token.ThrowIfCancellationRequested();
        if (blocker.Unavailable || blocker.Pid <= 0 || blocker.Pid == _currentPid || blocker.Started <= 0)
            return UpdateBlockerCloseResult.AccessDenied;
        _detectedIdentities.TryGetValue(blocker, out var detected);
        var auditToken = detected?.Token;
        var identity = _processes.ReadIdentity(blocker.Pid);
        var result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, auditToken);
        if (result != null) return ReportResult(result.Value, identity);
        // Ne jamais fabriquer une autorisation à la fermeture à partir d'un PID seul ou d'une nouvelle lecture.
        if (auditToken == null || auditToken.Value.Pid != blocker.Pid) return UpdateBlockerCloseResult.AccessDenied;

        token.ThrowIfCancellationRequested();
        var error = _processes.SendSignal(auditToken.Value, force ? 9 : 15);
        if (error == 3) // ESRCH : disparu ou génération changée pendant l'appel natif.
        {
            identity = _processes.ReadIdentity(blocker.Pid);
            result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, auditToken);
            return ReportResult(result ?? UpdateBlockerCloseResult.StillRunning, identity);
        }
        if (error != 0)
        {
            _log.Warn(new Win32Exception(error), $"Cannot close an update lock owner (PID {blocker.Pid})");
            return error is 1 or 13 or 45 or 78 ? UpdateBlockerCloseResult.AccessDenied : UpdateBlockerCloseResult.StillRunning;
        }

        var started = _time.GetTimestamp();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            identity = _processes.ReadIdentity(blocker.Pid);
            result = MacUpdateBlockerRules.CheckIdentity(blocker, identity, auditToken);
            if (result == UpdateBlockerCloseResult.Gone) return UpdateBlockerCloseResult.Closed;
            if (result != null) return ReportResult(result.Value, identity);
            var remaining = _exitTimeout - _time.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) return UpdateBlockerCloseResult.StillRunning;
            var delay = remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50);
            await Task.Delay(delay, _time, token).ConfigureAwait(false);
        }
    }

    private UpdateBlockerCloseResult ReportResult(UpdateBlockerCloseResult result, MacProcessIdentity identity)
    {
        if (result == UpdateBlockerCloseResult.AccessDenied) ReportUnavailable(identity.Pid, identity.Error);
        return result;
    }

    private void ReportUnavailable(int pid, int error) =>
        _log.Warn(new Win32Exception(error), $"Cannot inspect process (PID {pid}); update blocker detection may be incomplete");
}
