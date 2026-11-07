using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Mue;

[SupportedOSPlatform("macos15.0")]
internal sealed class MacProcessAccess : IMacProcessAccess
{
    private const string LibProc = "/usr/lib/libproc.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";

    public IReadOnlyList<int> ListPids()
    {
        var count = proc_listallpids(null, 0);
        if (count <= 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot enumerate processes");
        var capacity = checked(count + 64);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var pids = new int[capacity];
            count = proc_listallpids(pids, checked(pids.Length * sizeof(int)));
            if (count <= 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot enumerate processes");
            if (count < pids.Length) return pids.Take(count).Where(pid => pid > 0).ToArray();
            capacity = checked(capacity * 2);
        }
        throw new InvalidOperationException("The process list kept growing during update blocker detection.");
    }

    public MacProcessSnapshot ReadProcess(int pid)
    {
        var before = ReadIdentity(pid);
        if (before.State == MacProcessState.Gone) return new(before, null);
        var buffer = new byte[4096];
        var token = before.Token;
        var length = token != null
            ? proc_pidpath_audittoken(token.Value, buffer, (uint)buffer.Length)
            : proc_pidpath(pid, buffer, (uint)buffer.Length);
        var error = Marshal.GetLastPInvokeError();
        if (length <= 0)
            return new(new(pid, 0, error == 3 ? MacProcessState.Gone : MacProcessState.Unavailable, error), null);
        var terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator <= 0) return new(new(pid, 0, MacProcessState.Unavailable), null);
        var image = Encoding.UTF8.GetString(buffer, 0, terminator);
        var after = ReadIdentity(pid);
        if (after.State == MacProcessState.Gone) return new(after, null);
        // Une identité instable ne doit jamais autoriser la fermeture à partir d'un ancien chemin.
        if (before.State != MacProcessState.Readable || after.State != MacProcessState.Readable
            || before.Started != after.Started || before.Token?.Version != after.Token?.Version)
            return new(new(pid, 0, MacProcessState.Unavailable, after.Error != 0 ? after.Error : before.Error), image);
        return new(after, image);
    }

    public MacProcessIdentity ReadIdentity(int pid)
    {
        var before = ReadBsdIdentity(pid);
        if (before.State != MacProcessState.Readable) return before;
        var self = GetCurrentTask();
        // Un port « name » suffit : aucun droit de débogage/task_for_pid n'est demandé.
        var error = task_name_for_pid(self, pid, out var task);
        if (error != 0)
        {
            var after = ReadBsdIdentity(pid);
            return after.State == MacProcessState.Gone ? after
                : after with { State = MacProcessState.Unavailable, Error = 13 };
        }
        try
        {
            uint count = 8;
            error = task_info(task, 15, out var token, ref count); // TASK_AUDIT_TOKEN
            var after = ReadBsdIdentity(pid);
            if (after.State != MacProcessState.Readable) return after;
            // Les erreurs Mach ne sont pas des errno ; les refus sont adaptés en EACCES.
            if (error != 0 || count != 8 || token.Pid != pid || before.Started != after.Started)
                return after with { State = MacProcessState.Unavailable, Error = error != 0 ? 13 : 0 };
            return after with { Token = token };
        }
        finally { mach_port_deallocate(self, task); }
    }

    private static MacProcessIdentity ReadBsdIdentity(int pid)
    {
        var size = Marshal.SizeOf<BsdInfo>();
        var length = proc_pidinfo(pid, 3, 0, out var info, size);
        var error = Marshal.GetLastPInvokeError();
        if (length != size)
            return new(pid, 0, length == 0 && error == 3 ? MacProcessState.Gone : MacProcessState.Unavailable, error);
        if (info.Pid != (uint)pid) return new(pid, 0, MacProcessState.Unavailable);
        if (info.Status == 5) return new(pid, 0, MacProcessState.Gone); // SZOMB : l'exécutable n'est plus actif.
        var started = MacUpdateBlockerRules.ToFileTime(info.StartSeconds, info.StartMicroseconds);
        return new(pid, started, started > 0 ? MacProcessState.Readable : MacProcessState.Unavailable);
    }

    public int SendSignal(MacAuditToken token, int signal)
    {
        if (token.Pid == 0 || token.Pid > int.MaxValue || token.Pid == Environment.ProcessId) return 22;
        // Le noyau lie le signal au pidversion du token capturé, même si le PID vient d'être réutilisé.
        // Cette API retourne directement errno ; aucun repli vers kill(pid, ...) n'est autorisé.
        try { return proc_signal_with_audittoken(in token, signal); }
        catch (EntryPointNotFoundException) { return 45; } // ENOTSUP : refus conservateur.
    }

    public static string ResolvePath(string path)
    {
        var resolved = realpath(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot resolve the app bundle");
        try { return Marshal.PtrToStringUTF8(resolved) ?? throw new InvalidOperationException("Empty bundle path"); }
        finally { free(resolved); }
    }

    private static uint GetCurrentTask()
    {
        // mach_task_self() est une macro vers cette variable publique, sans acquérir de droit Mach.
        var library = NativeLibrary.Load(LibSystem);
        try { return unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library, "mach_task_self_"))); }
        finally { NativeLibrary.Free(library); }
    }

    // proc_bsdinfo (Darwin) : 136 octets sur arm64 et x64, dates en secondes et microsecondes Unix.
    [StructLayout(LayoutKind.Explicit, Size = 136)]
    internal struct BsdInfo
    {
        [FieldOffset(4)] public uint Status;
        [FieldOffset(12)] public uint Pid;
        [FieldOffset(120)] public ulong StartSeconds;
        [FieldOffset(128)] public ulong StartMicroseconds;
    }

    [DllImport(LibProc, SetLastError = true)]
    private static extern int proc_listallpids([Out] int[]? buffer, int bufferSize);
    [DllImport(LibProc, SetLastError = true)]
    private static extern int proc_pidpath(int pid, [Out] byte[] buffer, uint bufferSize);
    [DllImport(LibProc, SetLastError = true)]
    private static extern int proc_pidpath_audittoken(in MacAuditToken token, [Out] byte[] buffer, uint bufferSize);
    [DllImport(LibProc, SetLastError = true)]
    private static extern int proc_pidinfo(int pid, int flavor, ulong argument, out BsdInfo buffer, int bufferSize);
    [DllImport(LibProc)]
    private static extern int proc_signal_with_audittoken(in MacAuditToken token, int signal);
    [DllImport(LibSystem)]
    private static extern int task_name_for_pid(uint task, int pid, out uint name);
    [DllImport(LibSystem)]
    private static extern int task_info(uint task, int flavor, out MacAuditToken token, ref uint count);
    [DllImport(LibSystem)]
    private static extern int mach_port_deallocate(uint task, uint name);
    [DllImport(LibSystem, SetLastError = true)]
    private static extern IntPtr realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr resolved);
    [DllImport(LibSystem)]
    private static extern void free(IntPtr pointer);
}
