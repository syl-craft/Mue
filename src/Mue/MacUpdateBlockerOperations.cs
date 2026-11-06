using System.ComponentModel;
using System.Diagnostics;

namespace Mue;

internal enum MacProcessState { Readable, Unavailable, Gone }
internal enum MacCloseDecision { Signal, Closed, Refuse }
internal sealed record MacProcessIdentity(int Pid, long Started, MacProcessState State, int Error = 0);
internal sealed record MacProcessSnapshot(MacProcessIdentity Identity, string? Image);

internal interface IMacProcessAccess
{
    IReadOnlyList<int> ListPids();
    MacProcessSnapshot ReadProcess(int pid);
    MacProcessIdentity ReadIdentity(int pid);
    int SendSignal(int pid, int signal);
}

internal static class MacUpdateBlockerRules
{
    public static bool IsWithinBundle(string? image, string directory)
    {
        var bundle = NormalizePath(directory);
        var executable = NormalizePath(image);
        return bundle != null && executable != null
            && executable.StartsWith(bundle + "/", StringComparison.Ordinal);
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Contains('\0')) return null;
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        return "/" + string.Join('/', parts);
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

    public static MacCloseDecision DecideClose(UpdateBlocker blocker, MacProcessIdentity identity, int currentPid)
    {
        if (blocker.Pid <= 0 || blocker.Pid == currentPid || blocker.Started <= 0 || identity.Pid != blocker.Pid)
            return MacCloseDecision.Refuse;
        if (identity.State == MacProcessState.Gone) return MacCloseDecision.Closed;
        if (identity.State != MacProcessState.Readable || identity.Started <= 0) return MacCloseDecision.Refuse;
        return UpdateBlockers.MatchesIdentity(blocker, identity.Started) ? MacCloseDecision.Signal : MacCloseDecision.Closed;
    }
}

internal sealed class MacUpdateBlockerOperations(IMacProcessAccess processes, IUpdateLog log,
    int currentPid, TimeSpan exitTimeout)
{
    public IReadOnlyList<UpdateBlocker> Find(string directory, IUpdateBlockerClassifier classifier, bool enrich)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        var results = new List<UpdateBlocker>();
        foreach (var pid in processes.ListPids().Distinct())
        {
            if (pid <= 0 || pid == currentPid) continue;
            var process = processes.ReadProcess(pid);
            if (process.Identity.State == MacProcessState.Gone) continue;
            if (process.Identity.State == MacProcessState.Unavailable || process.Image == null)
                ReportUnavailable(pid, process.Identity.Error);
            if (!MacUpdateBlockerRules.IsWithinBundle(process.Image, directory)) continue;
            var image = process.Image;
            if (image == null || classifier.IsIgnored(image, directory)) continue;
            var classification = enrich
                ? classifier.Classify(new(image, directory, null, null, null))
                : new UpdateBlockerClassification(false, null);
            results.Add(new UpdateBlocker
            {
                Pid = pid,
                Started = process.Identity.State == MacProcessState.Readable ? process.Identity.Started : 0,
                Name = image[(image.LastIndexOf('/') + 1)..],
                File = image,
                IsRelay = classification.IsRelay,
                ParentName = classification.IsRelay ? classification.Program : null,
            });
        }
        return results.OrderBy(blocker => blocker.Name).ThenBy(blocker => blocker.Pid).ToArray();
    }

    public async Task<bool> CloseAsync(UpdateBlocker blocker, bool force)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        if (blocker.Pid <= 0 || blocker.Pid == currentPid || blocker.Started <= 0) return false;
        var identity = processes.ReadIdentity(blocker.Pid);
        var decision = MacUpdateBlockerRules.DecideClose(blocker, identity, currentPid);
        if (decision == MacCloseDecision.Closed) return true;
        if (decision == MacCloseDecision.Refuse)
        {
            ReportUnavailable(blocker.Pid, identity.Error);
            return false;
        }

        // kill ne permet pas de rendre atomiques le contrôle d'identité et l'envoi du signal.
        var error = processes.SendSignal(blocker.Pid, force ? 9 : 15);
        if (error == 3) return true; // ESRCH : le processus a disparu entre les deux appels.
        if (error != 0)
        {
            log.Warn(new Win32Exception(error), $"Cannot close an update lock owner (PID {blocker.Pid})");
            return false;
        }

        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            identity = processes.ReadIdentity(blocker.Pid);
            decision = MacUpdateBlockerRules.DecideClose(blocker, identity, currentPid);
            if (decision == MacCloseDecision.Closed) return true;
            if (decision == MacCloseDecision.Refuse)
            {
                ReportUnavailable(blocker.Pid, identity.Error);
                return false;
            }
            if (elapsed.Elapsed >= exitTimeout) return false;
            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
    }

    private void ReportUnavailable(int pid, int error) =>
        log.Warn(new Win32Exception(error), $"Cannot inspect process (PID {pid}); update blocker detection may be incomplete");
}
