using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Mue.Tests;

[SupportedOSPlatform("windows")]
public class ProcessInspectionTests
{
    [Theory]
    [InlineData(5, false)]
    [InlineData(87, false)]
    [InlineData(5, true)]
    [InlineData(87, true)]
    public void UnreadableProcessesAreKeptOnlyWhenRestartManagerReportedThem(int error, bool reported)
    {
        var blocker = reported ? new UpdateBlocker { Pid = 42, Name = "editor", Started = 100 } : null;
        var result = Inspect(new Inspection { Error = new Win32Exception(error) }, blocker);
        if (!reported) Assert.Null(result);
        else
        {
            Assert.NotNull(result);
            Assert.True(result.Unavailable);
            Assert.Equal("", result.File);
            Assert.True(new UpdateBlockerScan([result]).Incomplete);
            Assert.Equal("Update_ProcessUnavailable(42)", result.Detail);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreadableExitStatusUsesTheSameRule(bool reported)
    {
        var result = Inspect(new Inspection { Error = new Win32Exception(29), ExitError = true },
            reported ? new() { Pid = 42, Started = 100 } : null);
        Assert.Equal(reported, result?.Unavailable == true);
    }

    [Fact]
    public void UnexpectedInspectionErrorOfALivingProcessFailsTheScan()
    {
        Assert.Throws<InvalidOperationException>(() => Inspect(new Inspection { Error = new Win32Exception(29) }));
        Assert.Null(Inspect(new Inspection { Error = new Win32Exception(29), Exited = true }));
    }

    [Fact]
    public void QueryMatchesAllDirectoriesAndIndividualFiles()
    {
        var query = new UpdateBlockerQuery([@"C:\app\current", @"C:\other\helper.exe"]);
        Assert.True(query.Contains(@"C:\app\current\app.exe"));
        Assert.True(query.Contains(@"C:\other\helper.exe"));
        Assert.False(query.Contains(@"C:\app\current2\app.exe"));
        Assert.False(query.Contains(@"C:\other\another.exe"));
        var scan = WindowsUpdateBlockerFinder.InspectProcess(new Inspection { Image = @"C:\other\helper.exe" }, query, new ProgramClassifier(), null);
        Assert.NotNull(scan);
        Assert.False(scan.Unavailable);
        Assert.Equal(100, scan.Started);
    }

    [Fact]
    public async Task UnavailableBlockerCannotBeClosedEvenWithForce()
    {
        var blocker = new UpdateBlocker { Pid = -1, Unavailable = true };
        Assert.Equal(UpdateBlockerCloseResult.AccessDenied, await new WindowsUpdateBlockerFinder().CloseAsync(blocker, true));
    }

    [Fact]
    [Trait("Category", "RealMachine")]
    public async Task CompleteScanFindsAndClosesATemporaryExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "mue-process-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        Process? process = null;
        try
        {
            var executable = Path.Combine(root, "mue-test.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), executable);
            process = Process.Start(new ProcessStartInfo(executable, "/d /c pause")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true });
            Assert.NotNull(process);
            var finder = new WindowsUpdateBlockerFinder();
            var scan = finder.Find(UpdateBlockerQuery.ForDirectory(root), new ProgramClassifier(), enrich: false);
            var blocker = Assert.Single(scan.Blockers, b => b.Pid == process.Id);
            Assert.Equal(UpdateBlockerCloseResult.Closed, await finder.CloseAsync(blocker, force: true));
            Assert.Empty(finder.Find(UpdateBlockerQuery.ForDirectory(root), new ProgramClassifier(), enrich: false).Blockers);
        }
        finally
        {
            if (process != null) { if (!process.HasExited) process.Kill(); process.Dispose(); }
            Directory.Delete(root, true);
        }
    }

    private static UpdateBlocker? Inspect(Inspection process, UpdateBlocker? blocker = null) =>
        WindowsUpdateBlockerFinder.InspectProcess(process, UpdateBlockerQuery.ForDirectory(@"C:\app"), new ProgramClassifier(), blocker);

    private sealed class Inspection : WindowsUpdateBlockerFinder.IProcessInspection
    {
        public Exception? Error { get; init; }
        public bool ExitError { get; init; }
        public bool Exited { get; init; }
        public string Image { get; init; } = @"C:\app\editor.exe";
        public int Id => 42;
        public string Name => "editor";
        public long Started => 100;
        public string FileName => Error == null ? Image : throw Error;
        public bool HasExited => ExitError ? throw new Win32Exception(5) : Exited;
    }
}
