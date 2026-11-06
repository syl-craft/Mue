using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mue.Tests;

[SupportedOSPlatform("macos")]
[Trait("Category", "MacNative")]
public class MacUpdateBlockerNativeTests
{
    [MacFact]
    public void BsdInfoMatchesCurrentProcessIdentity()
    {
        Assert.Equal(136, Marshal.SizeOf<MacProcessAccess.BsdInfo>());
        var access = new MacProcessAccess();
        using var current = Process.GetCurrentProcess();
        var identity = access.ReadIdentity(current.Id);
        Assert.Equal(MacProcessState.Readable, identity.State);
        Assert.Equal(current.Id, identity.Pid);
        Assert.InRange(Math.Abs(identity.Started - current.StartTime.ToFileTimeUtc()), 0, TimeSpan.TicksPerSecond);
        Assert.Equal(identity, access.ReadIdentity(current.Id));
        Assert.Contains(current.Id, access.ListPids());
    }

    [MacFact]
    public async Task BundleDetectionAndSoftCloseUseTheNativeIdentity()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        var outside = bundle.StartSleep(outsideBundle: true);
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        Assert.Equal(process.Id, blocker.Pid);
        Assert.True(blocker.Started > 0);
        Assert.EndsWith("/Contents/MacOS/Probe", blocker.File, StringComparison.Ordinal);
        Assert.False(blocker.Selected);
        Assert.DoesNotContain(finder.Find(bundle.Path, new ProgramClassifier()), item => item.Pid == outside.Id);
        Assert.True(await finder.CloseAsync(blocker, force: false));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(128 + 15, process.ExitCode);
        Assert.False(outside.HasExited);
        Assert.True(await finder.CloseAsync(blocker, force: false));
    }

    [MacFact]
    public async Task ForceSendsSigkill()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        Assert.True(await finder.CloseAsync(blocker, force: true));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(128 + 9, process.ExitCode);
    }

    [MacFact]
    public async Task StaleAndUnknownIdentitiesLeaveTheProcessAlive()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        Assert.True(await finder.CloseAsync(new() { Pid = blocker.Pid, Started = blocker.Started - 10 }, force: true));
        Assert.False(await finder.CloseAsync(new() { Pid = blocker.Pid, Started = 0 }, force: true));
        Assert.False(process.HasExited);
    }

    [MacFact]
    public async Task IgnoredSigtermDoesNotEscalateToSigkill()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartIgnoringSigterm();
        Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        Assert.False(await finder.CloseAsync(blocker, force: false));
        Assert.False(process.HasExited);
        Assert.True(await finder.CloseAsync(blocker, force: true));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(128 + 9, process.ExitCode);
    }

    [MacFact]
    public async Task SymbolicBundlePathFindsTheSameProcess()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        var alias = System.IO.Path.Combine(bundle.Root, "Alias.app");
        Directory.CreateSymbolicLink(alias, bundle.Path);
        var blocker = await FindProcess(new MacUpdateBlockerFinder(), alias, process);
        Assert.Equal(process.Id, blocker.Pid);
    }

    private static async Task<UpdateBlocker> FindProcess(MacUpdateBlockerFinder finder, string bundle, Process process)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.False(process.HasExited);
            var blocker = finder.Find(bundle, new ProgramClassifier()).Where(item => item.Pid == process.Id).SingleOrDefault();
            if (blocker != null) return blocker;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("The test process was not found in its temporary bundle.");
    }

    private sealed class TestBundle : IDisposable
    {
        private readonly List<Process> _processes = [];
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Mue-Mac-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Root, "Essai é.app");

        public Process StartSleep(bool outsideBundle = false) => Start("/bin/sleep", ["60"], outsideBundle);
        public Process StartIgnoringSigterm() => Start("/bin/sh", ["-c", "trap '' TERM; printf 'ready\\n'; while :; do :; done"]);

        private Process Start(string source, string[] arguments, bool outsideBundle = false)
        {
            var directory = System.IO.Path.Combine(outsideBundle ? Path + ".other" : Path, "Contents", "MacOS");
            Directory.CreateDirectory(directory);
            var executable = System.IO.Path.Combine(directory, "Probe");
            File.Copy(source, executable);
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the test process.");
            _processes.Add(process);
            return process;
        }

        public void Dispose()
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                    process.WaitForExit(5000);
                }
                finally { process.Dispose(); }
            }
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}

public sealed class MacFactAttribute : FactAttribute
{
    public MacFactAttribute()
    {
        if (!OperatingSystem.IsMacOS()) Skip = "Requires macOS and libproc.";
    }
}
