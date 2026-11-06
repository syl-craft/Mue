using System.Threading;
using System.Threading.Tasks;

namespace Mue;

public sealed class FakeUpdateBackend : IUpdateBackend
{
    public bool IsInstalled { get; set; } = true;
    public string CurrentVersion { get; set; } = "1.0.0";
    public string ContentDirectory { get; set; } = "";
    public UpdateShutdownBehavior ShutdownBehavior { get; set; } = UpdateShutdownBehavior.Kill;
    public UpdateRelease? Release { get; set; }
    public Exception? CheckFailure { get; set; }
    public Exception? DownloadFailure { get; set; }
    public Exception? ApplyFailure { get; set; }
    public IReadOnlyList<int> Progress { get; set; } = [100];
    public Func<CancellationToken, Task>? WaitForDownload { get; set; }
    public Func<CancellationToken, Task>? WaitForCheck { get; set; }
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public int Applies { get; private set; }

    public async Task<UpdateRelease?> CheckAsync(CancellationToken token)
    {
        Checks++;
        if (WaitForCheck != null) await WaitForCheck(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (CheckFailure is { } error) throw error;
        return Release;
    }

    public async Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token)
    {
        Downloads++;
        if (WaitForDownload != null) await WaitForDownload(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (DownloadFailure is { } error) throw error;
        foreach (var value in Progress)
        {
            token.ThrowIfCancellationRequested();
            progress(value);
        }
    }

    public void Apply(UpdateRelease release)
    {
        Applies++;
        if (ApplyFailure is { } error) throw error;
    }
}
