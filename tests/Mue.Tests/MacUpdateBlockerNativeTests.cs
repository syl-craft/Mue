using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mue.Tests;

[SupportedOSPlatform("macos15.0")]
[Trait("Category", "MacNative")]
[Trait("Category", "RealMachine")]
public class MacUpdateBlockerNativeTests
{
    [MacFact]
    public void BsdInfoMatchesCurrentProcessIdentity()
    {
        Assert.Equal(136, Marshal.SizeOf<MacProcessAccess.BsdInfo>());
        Assert.Equal(32, Marshal.SizeOf<MacAuditToken>());
        var access = new MacProcessAccess();
        using var current = Process.GetCurrentProcess();
        var identity = access.ReadIdentity(current.Id);
        Assert.Equal(MacProcessState.Readable, identity.State);
        Assert.Equal(current.Id, identity.Pid);
        var token = identity.Token ?? throw new InvalidOperationException("No audit token for the current process.");
        Assert.Equal((uint)current.Id, token.Pid);
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
        Assert.DoesNotContain(finder.Find(new([bundle.Path]), new ProgramClassifier()).Blockers, item => item.Pid == outside.Id);
        Assert.Equal(UpdateBlockerCloseResult.Closed, await finder.CloseAsync(blocker, force: false));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(128 + 15, process.ExitCode);
        Assert.False(outside.HasExited);
        Assert.Equal(UpdateBlockerCloseResult.Gone, await finder.CloseAsync(blocker, force: false));
    }

    [MacFact]
    public async Task ForceSendsSigkill()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        Assert.Equal(UpdateBlockerCloseResult.Closed, await finder.CloseAsync(blocker, force: true));
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
        Assert.Equal(UpdateBlockerCloseResult.IdentityChanged,
            await finder.CloseAsync(new() { Pid = blocker.Pid, Started = blocker.Started - 10 }, force: true));
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied,
            await finder.CloseAsync(new() { Pid = blocker.Pid, Started = 0 }, force: true));
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
        Assert.Equal(UpdateBlockerCloseResult.StillRunning, await finder.CloseAsync(blocker, force: false));
        Assert.False(process.HasExited);
        Assert.Equal(UpdateBlockerCloseResult.Closed, await finder.CloseAsync(blocker, force: true));
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

    [MacFact]
    public async Task SymbolicParentIsResolvedBeforeDotDotEvenWhenTheLexicalBundleExists()
    {
        using var bundle = new TestBundle(relativePath: "releases/Test.app", rootDirectory: "/tmp");
        var process = bundle.StartSleep();
        var target = System.IO.Path.Combine(bundle.Root, "releases", "v1");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(System.IO.Path.Combine(bundle.Root, "Test.app"));
        var alias = System.IO.Path.Combine(bundle.Root, "alias");
        Directory.CreateSymbolicLink(alias, target);
        var original = alias + "/../Test.app";
        Assert.Equal(MacProcessAccess.ResolvePath(bundle.Path), MacProcessAccess.ResolvePath(original));
        var blocker = await FindProcess(new MacUpdateBlockerFinder(), original, process);
        Assert.Equal(process.Id, blocker.Pid);
    }

    [MacFact]
    public async Task SoftCloseWaitsForAProcessThatFinishesAfterThreeSeconds()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartDelayedExit();
        Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        var finder = new MacUpdateBlockerFinder();
        var blocker = await FindProcess(finder, bundle.Path, process);
        var elapsed = Stopwatch.StartNew();
        Assert.Equal(UpdateBlockerCloseResult.Closed, await finder.CloseAsync(blocker, force: false));
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(2));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(0, process.ExitCode);
    }

    [MacFact]
    public async Task KernelRejectsAnObsoleteTokenBeforeDeliveringSigkill()
    {
        using var bundle = new TestBundle();
        var process = bundle.StartSleep();
        await FindProcess(new MacUpdateBlockerFinder(), bundle.Path, process);
        var access = new MacProcessAccess();
        var token = access.ReadIdentity(process.Id).Token
            ?? throw new InvalidOperationException("No audit token for the child process.");
        var obsolete = token with { Version = unchecked(token.Version + 1) };
        Assert.Equal(3, access.SendSignal(obsolete, 9)); // ESRCH : mauvaise génération, PID pourtant vivant.
        Assert.False(process.HasExited);
        Assert.Equal(0, access.SendSignal(token, 15));
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, access.SendSignal(token, 9));
    }

    private static async Task<UpdateBlocker> FindProcess(IUpdateBlockerFinder finder, string bundle, Process process)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.False(process.HasExited);
            var blocker = finder.Find(new([bundle]), new ProgramClassifier()).Blockers.Where(item => item.Pid == process.Id).SingleOrDefault();
            if (blocker != null) return blocker;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("The test process was not found in its temporary bundle.");
    }

    private sealed class TestBundle(string relativePath = "Essai é.app", string? rootDirectory = null) : IDisposable
    {
        private readonly List<Process> _processes = [];
        public string Root { get; } = System.IO.Path.Combine(rootDirectory ?? System.IO.Path.GetTempPath(), "Mue-Mac-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Root, relativePath);

        public Process StartSleep(bool outsideBundle = false) => Start("/bin/sleep", ["60"], outsideBundle);
        public Process StartIgnoringSigterm() => Start("/bin/sh", ["-c", "trap '' TERM; printf 'ready\\n'; while :; do :; done"]);
        public Process StartDelayedExit() => Start("/bin/sh", ["-c", "trap 'sleep 3; exit 0' TERM; printf 'ready\\n'; while :; do :; done"]);

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
        if (!OperatingSystem.IsMacOSVersionAtLeast(15)) Skip = "Requires macOS 15+ and libproc.";
    }
}
