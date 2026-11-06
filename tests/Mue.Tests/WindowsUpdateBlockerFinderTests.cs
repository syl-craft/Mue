using System.Runtime.Versioning;

namespace Mue.Tests;

[SupportedOSPlatform("windows")]
public class WindowsUpdateBlockerFinderTests
{
    [Fact]
    public void WmiFailuresDiscardPartialMetadataInsteadOfBlockingInstallation()
    {
        Exception[] errors = [new System.Runtime.InteropServices.COMException("WMI unavailable"),
            new System.Management.ManagementException("WMI failed"), new UnauthorizedAccessException(), new InvalidOperationException()];
        foreach (var error in errors)
        {
            var metadata = new WindowsUpdateBlockerFinder().ReadProcessMetadata(partial =>
            {
                partial[1] = new(2, "DockPad.exe", null, "--mcp");
                throw error;
            });
            Assert.Empty(metadata);
        }
    }

    [Fact]
    public async Task ReusedPidIsNeverClosed()
    {
        // Even force=true must not touch the current test process for a stale PID identity.
        Assert.True(await new WindowsUpdateBlockerFinder().CloseAsync(new UpdateBlocker { Pid = Environment.ProcessId, Started = 0 }, force: true));
    }
}
