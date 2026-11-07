using System.IO;

namespace Mue.Tests;

public class UpdateServiceTests
{
    private sealed class Backend : IUpdateBackend
    {
        public bool IsInstalled { get; init; } = true;
        public string CurrentVersion => "1.0.0";
        public string ContentDirectory => Path.GetTempPath();
        public int Checks, Downloads, Applies;
        public bool FailCheck, FailDownload, FailApply;
        public UpdateRelease? Release = new("2.0.0", "Release notes", new object());
        public TaskCompletionSource? DownloadStarted, FinishDownload;
        public Task<UpdateRelease?> CheckAsync(CancellationToken token)
        {
            Checks++;
            return FailCheck ? Task.FromException<UpdateRelease?>(new IOException("offline")) : Task.FromResult(Release);
        }
        public async Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token)
        {
            Downloads++; DownloadStarted?.TrySetResult();
            if (FinishDownload is not null) await FinishDownload.Task.WaitAsync(token);
            if (FailDownload) throw new IOException("network interrupted");
            progress(100);
        }
        public void Apply(UpdateRelease release) { Applies++; if (FailApply) throw new IOException("updater unavailable"); }
    }

    [Fact]
    public async Task LegacyInstallationDoesNotCheckOrApply()
    {
        var backend = new Backend { IsInstalled = false };
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        Assert.False(await service.DownloadAsync());
        Assert.Equal(UpdateState.Unavailable, service.State);
        Assert.Equal(0, backend.Checks);
        Assert.Throws<InvalidOperationException>(service.Apply);
    }
    [Fact]
    public async Task SuccessfulUpdateRequiresDownloadBeforeApply()
    {
        var backend = new Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        Assert.Equal(UpdateState.Available, service.State);
        Assert.Equal("Release notes", service.Notes);
        Assert.Throws<InvalidOperationException>(service.Apply);
        Assert.True(await service.DownloadAsync());
        await service.CheckAsync(); // Keep a downloaded release instead of replacing it beneath Apply.
        Assert.Equal(1, backend.Checks);
        service.Apply();
        Assert.Equal(UpdateState.Installing, service.State);
        Assert.False(service.CanInstall);
        Assert.Equal(1, backend.Applies);
    }
    [Fact]
    public async Task InterruptedDownloadCanBeRetried()
    {
        var backend = new Backend { FailDownload = true };
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        Assert.False(await service.DownloadAsync());
        Assert.Equal(UpdateState.Failed, service.State);
        Assert.True(service.CanInstall);
        backend.FailDownload = false;
        Assert.True(await service.DownloadAsync());
        Assert.Equal(2, backend.Downloads);
    }
    [Fact]
    public async Task CancellationAndConcurrentClicksDoNotLaunchUpdater()
    {
        var backend = new Backend { DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously), FinishDownload = new() };
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var download = service.DownloadAsync();
        await backend.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await service.DownloadAsync());
        await service.CheckAsync();
        service.CancelDownload();
        Assert.False(await download.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(UpdateState.Available, service.State);
        Assert.Equal(1, backend.Downloads);
        Assert.Equal(1, backend.Checks);
        Assert.Equal(0, backend.Applies);
    }
    [Fact]
    public async Task FailedCheckAndApplyKeepRetryAvailable()
    {
        var backend = new Backend { FailCheck = true };
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        Assert.Equal(UpdateState.Failed, service.State);
        backend.FailCheck = false;
        await service.CheckAsync();
        Assert.True(await service.DownloadAsync());
        backend.FailApply = true;
        Assert.Throws<IOException>(service.Apply);
        Assert.Equal(UpdateState.Ready, service.State);
        backend.FailApply = false;
        service.Apply();
        Assert.Equal(1, backend.Downloads);
    }
    [Fact]
    public async Task NoUpdateReturnsToIdle()
    {
        using var service = new UpdateService(new Backend { Release = null });
        await service.CheckAsync();
        Assert.Equal(UpdateState.Idle, service.State);
        Assert.False(service.HasRelease);
        Assert.False(service.CanInstall);
    }

    [Fact]
    public async Task PendingWritesWaitsForAnAlreadyClosedDialog()
    {
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingWrites.Track(writing.Task);
        var drain = PendingWrites.CompleteAsync();
        Assert.False(drain.IsCompleted);
        writing.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AbandonedPreparationReturnsToReady()
    {
        using var service = new UpdateService(new Backend());
        await service.CheckAsync();
        await service.DownloadAsync();
        service.BeginPreparation();

        service.CancelPreparation();

        Assert.Equal(UpdateState.Ready, service.State);
        Assert.True(service.CanInstall);
        Assert.False(service.ShowsProgress);
    }

    [Fact]
    public async Task ApplyFromPreparationKeepsTheIndicatorWhileClosing()
    {
        var backend = new Backend();
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        await service.DownloadAsync();
        service.BeginPreparation();

        service.Apply();

        Assert.Equal(UpdateState.Installing, service.State);
        Assert.True(service.IsIndeterminate);
        Assert.Equal(1, backend.Applies);
    }

    [Fact]
    public async Task FailedApplyFromPreparationReturnsToReady()
    {
        using var service = new UpdateService(new Backend { FailApply = true });
        await service.CheckAsync();
        await service.DownloadAsync();
        service.BeginPreparation();

        Assert.Throws<IOException>(service.Apply);

        Assert.Equal(UpdateState.Ready, service.State);
    }

    [Fact]
    public async Task PreparationRequiresADownloadedRelease()
    {
        using var service = new UpdateService(new Backend());
        await service.CheckAsync();

        Assert.Throws<InvalidOperationException>(service.BeginPreparation);
        Assert.Equal(UpdateState.Available, service.State);
    }

    [Fact]
    public async Task DownloadKeepsADeterminateProgressBar()
    {
        var backend = new Backend { DownloadStarted = new(), FinishDownload = new() };
        using var service = new UpdateService(backend);
        await service.CheckAsync();
        var download = service.DownloadAsync();
        await backend.DownloadStarted.Task;

        Assert.True(service.ShowsProgress);
        Assert.False(service.IsIndeterminate);

        backend.FinishDownload.SetResult();
        await download;
    }

    /// <summary>
    /// Case « Tout sélectionner » : aucun, une partie, tout ; un clic coche tout sauf si tout
    /// l'était déjà ; une ligne cochée à la main est notifiée pour que la case suive.
    /// </summary>
    [Fact]
    public void SelectionStateCoversNoneSomeAndAll()
    {
        var a = new UpdateBlocker { Pid = 1 };
        var b = new UpdateBlocker { Pid = 2 };
        UpdateBlocker[] all = [a, b];

        Assert.False(UpdateBlockers.SelectionState(all));
        a.Selected = true;
        Assert.Null(UpdateBlockers.SelectionState(all));
        b.Selected = true;
        Assert.True(UpdateBlockers.SelectionState(all));
        Assert.False(UpdateBlockers.SelectionState([]));
    }

    [Fact]
    public void ToggleAllSelectsEverythingUnlessAllWereSelected()
    {
        var a = new UpdateBlocker { Pid = 1, Selected = true };
        var b = new UpdateBlocker { Pid = 2 };
        UpdateBlocker[] all = [a, b];

        UpdateBlockers.ToggleAll(all);
        Assert.True(a.Selected && b.Selected);

        UpdateBlockers.ToggleAll(all);
        Assert.False(a.Selected || b.Selected);
    }

    [Fact]
    public void SelectingABlockerNotifiesTheList()
    {
        var blocker = new UpdateBlocker { Pid = 1 };
        var changed = new List<string?>();
        blocker.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        blocker.Selected = true;
        blocker.Selected = true;

        Assert.Equal([nameof(UpdateBlocker.Selected)], changed);
    }
}
