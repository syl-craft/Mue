using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Mue;

/// <summary>
/// Restart Manager identifie les verrous ; RmShutdown n'est jamais appelé car il peut forcer la fermeture.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateBlockerFinder(IUpdateLog? log = null) : IUpdateBlockerFinder
{
    private readonly IUpdateLog _log = log ?? new SilentUpdateLog();
    public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true)
    {
        var results = new Dictionary<int, UpdateBlocker>();
        var sessionKey = new StringBuilder(33);
        Check(RmStartSession(out var session, 0, sessionKey));
        try
        {
            var files = query.Paths.SelectMany(path => Directory.Exists(path)
                ? Directory.GetFiles(path, "*", SearchOption.AllDirectories) : new[] { Path.GetFullPath(path) }).Distinct().ToArray();
            foreach (var batch in files.Chunk(128))
                Check(RmRegisterResources(session, (uint)batch.Length, batch, 0, null, 0, null));
            uint count = 0, needed = 0, reasons = 0;
            var status = RmGetList(session, out needed, ref count, null, ref reasons);
            for (var attempt = 0; status == 234 && attempt < 5; attempt++)
            {
                var infos = new ProcessInfo[needed];
                count = needed;
                status = RmGetList(session, out needed, ref count, infos, ref reasons);
                if (status != 0) continue;
                foreach (var info in infos.Take((int)count))
                {
                    if (!query.IncludeCurrentProcess && info.Process.Pid == Environment.ProcessId) continue;
                    results[info.Process.Pid] = new UpdateBlocker
                    {
                        Pid = info.Process.Pid, Name = info.Name,
                        Started = info.Process.Started.ToLong(), Unavailable = true,
                    };
                }
            }
            Check(status);
        }
        finally { RmEndSession(session); }
        // Les processus console peuvent retenir une image sans être signalés par Restart Manager.
        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                if (!query.IncludeCurrentProcess && process.Id == Environment.ProcessId) continue;
                results.TryGetValue(process.Id, out var blocker);
                using var inspection = new ProcessInspection(process);
                var inspected = InspectProcess(inspection, query, classifier, blocker);
                if (inspected == null) results.Remove(process.Id);
                else results[process.Id] = inspected;
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        // Le contrôle final de verrouillage n'a besoin d'aucun enrichissement WMI.
        if (!enrich) return new(results.Values.ToArray());
        var metadata = ReadProcessMetadata();
        return new(results.Values.Select(blocker =>
        {
            if (blocker.Unavailable) return blocker;
            metadata.TryGetValue(blocker.Pid, out var process);
            ProcessMetadata? parent = null;
            if (process != null) metadata.TryGetValue(process.ParentPid, out parent);
            ProcessMetadata? grandparent = null;
            if (parent != null) metadata.TryGetValue(parent.ParentPid, out grandparent);
            var image = blocker.File.Length > 0 ? blocker.File : process?.Image ?? "";
            var classification = classifier.Classify(new(image, query, process?.CommandLine,
                parent?.Name, parent?.Image, grandparent?.Name, grandparent?.Image));
            return new UpdateBlocker
            {
                Pid = blocker.Pid, Started = blocker.Started, Name = blocker.Name, File = image,
                IsRelay = classification.IsRelay, ParentName = classification.IsRelay ? classification.Program : null,
                Selected = classification.IsRelay,
            };
        }).OrderBy(p => p.Name).ThenBy(p => p.Pid).ToArray());
    }

    internal static UpdateBlocker? InspectProcess(IProcessInspection process, UpdateBlockerQuery query,
        IUpdateBlockerClassifier classifier, UpdateBlocker? blocker)
    {
        UpdateBlocker? Unavailable() => blocker == null ? null : new()
        {
            Pid = blocker.Pid, Name = blocker.Name, Started = blocker.Started, Unavailable = true,
        };
        try
        {
            var image = process.FileName ?? throw new InvalidOperationException("Process image is unavailable");
            if (classifier.IsIgnored(image, query)) return null;
            if (blocker == null && !query.Contains(image)) return null;
            var started = process.Started;
            if (blocker != null && !UpdateBlockers.MatchesIdentity(blocker, started)) return Unavailable();
            return new() { Pid = process.Id, Name = blocker?.Name ?? process.Name, Started = started, File = image };
        }
        catch (UnauthorizedAccessException) { return Unavailable(); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode is 5 or 87) { return Unavailable(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            bool exited;
            try { exited = process.HasExited; }
            catch { return Unavailable(); }
            if (exited) return null;
            throw new InvalidOperationException($"Cannot inspect process {process.Id}", ex);
        }
    }

    internal interface IProcessInspection
    {
        int Id { get; }
        string? FileName { get; }
        long Started { get; }
        string Name { get; }
        bool HasExited { get; }
    }

    private sealed class ProcessInspection(Process process) : IProcessInspection, IDisposable
    {
        private IntPtr _handle;
        private IntPtr Handle
        {
            get
            {
                if (_handle != IntPtr.Zero) return _handle;
                _handle = OpenProcess(0x1000, false, process.Id);
                if (_handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                return _handle;
            }
        }
        public int Id => process.Id;
        public string Name => Path.GetFileNameWithoutExtension(FileName);
        public bool HasExited => process.HasExited;
        public string FileName
        {
            get
            {
                var buffer = new StringBuilder(32767);
                var size = (uint)buffer.Capacity;
                if (!QueryFullProcessImageName(Handle, 0, buffer, ref size))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                return buffer.ToString(0, (int)size);
            }
        }
        public long Started
        {
            get
            {
                if (!GetProcessTimes(Handle, out var started, out _, out _, out _))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
                return started.ToLong();
            }
        }
        public void Dispose() { if (_handle != IntPtr.Zero) CloseHandle(_handle); }
    }

    internal sealed record ProcessMetadata(int ParentPid, string? Name, string? Image, string? CommandLine);
    internal Dictionary<int, ProcessMetadata> ReadProcessMetadata(Action<Dictionary<int, ProcessMetadata>>? read = null)
    {
        var metadata = new Dictionary<int, ProcessMetadata>();
        try
        {
            if (read != null) { read(metadata); return metadata; }
            using var search = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process");
            using var processes = search.Get();
            foreach (ManagementObject process in processes)
            {
                using (process)
                    metadata[Convert.ToInt32(process["ProcessId"])] = new(Convert.ToInt32(process["ParentProcessId"]),
                        process["Name"] as string, process["ExecutablePath"] as string, process["CommandLine"] as string);
            }
        }
        // L'enrichissement d'affichage ne doit jamais empêcher une installation, même si WMI/COM tombe.
        catch (Exception ex)
        {
            _log.Warn(ex, "Cannot identify update lock owners");
            metadata.Clear();
        }
        return metadata;
    }

    public async Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default)
    {
        if (blocker.Unavailable) return UpdateBlockerCloseResult.AccessDenied;
        token.ThrowIfCancellationRequested();
        try
        {
            using var process = Process.GetProcessById(blocker.Pid);
            using var inspection = new ProcessInspection(process);
            if (!UpdateBlockers.MatchesIdentity(blocker, inspection.Started)) return UpdateBlockerCloseResult.IdentityChanged;
            token.ThrowIfCancellationRequested();
            if (force || blocker.IsRelay) process.Kill(entireProcessTree: false);
            else if (!process.CloseMainWindow()) return UpdateBlockerCloseResult.StillRunning;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            return process.HasExited ? UpdateBlockerCloseResult.Closed : UpdateBlockerCloseResult.StillRunning;
        }
        catch (ArgumentException) { return UpdateBlockerCloseResult.Gone; }
        catch (InvalidOperationException) { return UpdateBlockerCloseResult.Gone; }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _log.Warn(ex, "Cannot close an update lock owner");
            if (ex.NativeErrorCode == 5) return UpdateBlockerCloseResult.AccessDenied;
            if (ex.NativeErrorCode == 87) return UpdateBlockerCloseResult.Gone;
            throw;
        }
    }
    private static void Check(int result)
    {
        if (result != 0) throw new System.ComponentModel.Win32Exception(result);
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileTime
    {
        public uint Low, High;
        public readonly long ToLong() => ((long)High << 32) | Low;
    }
    [StructLayout(LayoutKind.Sequential)] private struct UniqueProcess { public int Pid; public FileTime Started; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out FileTime creation, out FileTime exit, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Service;
        public uint Type, Status, Session;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmStartSession(out uint session, uint flags, StringBuilder key);
    [DllImport("rstrtmgr.dll")] private static extern int RmEndSession(uint session);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmRegisterResources(uint session, uint fileCount,
        string[] files, uint appCount, UniqueProcess[]? apps, uint serviceCount, string[]? services);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] private static extern int RmGetList(uint session, out uint needed,
        ref uint count, [In, Out] ProcessInfo[]? info, ref uint reasons);
}
