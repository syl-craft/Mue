using System.ComponentModel;
using System.Globalization;
using System.Resources;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mue.Tests;

public class UpdateContractTests
{
    [Theory]
    [InlineData(null, "1.0.0", UpdateLaunchResult.None)]
    [InlineData("v2.0.0", "2.0.0", UpdateLaunchResult.Succeeded)]
    [InlineData("2.0.0", "v2.0.0", UpdateLaunchResult.Succeeded)]
    [InlineData("2.0.0", "1.0.0", UpdateLaunchResult.RolledBack)]
    [InlineData("2.0.0", "3.0.0", UpdateLaunchResult.RolledBack)]
    public void LaunchVerificationConsumesTheExpectedVersion(string? expected, string current, UpdateLaunchResult result)
    {
        var store = new MemoryUpdatePendingStore { ExpectedVersion = expected };
        using var service = new UpdateService(new FakeUpdateBackend { CurrentVersion = current }, new() { PendingStore = store });
        Assert.Equal(new(result, expected, current), service.VerifyLaunch());
        Assert.Null(store.ExpectedVersion);
        Assert.Equal(UpdateLaunchResult.None, service.VerifyLaunch().Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyWritesBeforeTheBackendAndClearsOnFailure(bool fail)
    {
        var store = new MemoryUpdatePendingStore();
        var backend = new PendingBackend(store, fail);
        using var service = new UpdateService(backend, new() { PendingStore = store });
        await service.CheckAsync();
        await service.DownloadAsync();
        if (fail) Assert.Throws<IOException>(service.Apply);
        else service.Apply();
        Assert.Equal(fail ? null : "2.0.0", store.ExpectedVersion);
    }

    [Fact]
    public void FileStoreSurvivesRecreationAndRemovesItsMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), "mue-store-" + Guid.NewGuid());
        var path = Path.Combine(root, "pending");
        try
        {
            var store = new FileUpdatePendingStore(path);
            Assert.Null(store.ExpectedVersion);
            store.ExpectedVersion = "2.0.0";
            Assert.Equal("2.0.0", new FileUpdatePendingStore(path).ExpectedVersion);
            store.ExpectedVersion = "3.0.0";
            Assert.Equal("3.0.0", new FileUpdatePendingStore(path).ExpectedVersion);
            Assert.Single(Directory.GetFiles(root));
            store.ExpectedVersion = null;
            Assert.Null(new FileUpdatePendingStore(path).ExpectedVersion);
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("en", "information unavailable · PID 42")]
    [InlineData("fr", "informations indisponibles · PID 42")]
    public void PartialHostFallsBackToEmbeddedResources(string language, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            UpdateTexts.Configure(null);
            Assert.Equal(expected, UpdateTexts.F("Update_ProcessUnavailable", 42));
            UpdateTexts.Configure(new PartialTexts());
            Assert.Equal("host title", UpdateTexts.T("Update_Title"));
            Assert.Equal(expected, UpdateTexts.F("Update_ProcessUnavailable", 42));
            Assert.DoesNotContain("[Update_", UpdateTexts.T("Update_BlockedHelpAskToQuit"));
            var resources = new ResourceManager("Mue.Resources.Strings", typeof(UpdateService).Assembly);
            Assert.Equal(resources.GetString("Update_CloseCount", CultureInfo.CurrentUICulture), UpdateTexts.T("Update_CloseCount"));
            Assert.DoesNotContain("plural", UpdateTexts.F("Update_CloseCount", 2));
        }
        finally { CultureInfo.CurrentUICulture = previous; TestUpdateTexts.Initialize(); }
    }

    [Theory]
    [InlineData(UpdateShutdownBehavior.Refuse)]
    [InlineData(UpdateShutdownBehavior.AskToQuit)]
    [InlineData(UpdateShutdownBehavior.Kill)]
    public void ServicePreservesEachShutdownPolicy(UpdateShutdownBehavior behavior)
    {
        using var service = new UpdateService(new FakeUpdateBackend { ShutdownBehavior = behavior });
        Assert.Equal(behavior, service.ShutdownBehavior);
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("permission")]
    [InlineData("dispose")]
    public async Task AnAutomaticSearchIsCancelledWithoutAnError(string reason)
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowed = true;
        var time = new ManualTimeProvider();
        var backend = new FakeUpdateBackend { WaitForCheck = async token =>
        {
            started.TrySetResult(token);
            await Task.Delay(Timeout.Infinite, token);
        } };
        var log = new RecordingLog();
        using var service = new UpdateService(backend, new() { Time = time, CanSearch = () => allowed, Log = log });
        service.StartPolling();
        time.Advance(TimeSpan.FromSeconds(20));
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (reason == "automatic") service.Automatic = false;
        if (reason == "permission") { allowed = false; time.Advance(TimeSpan.FromMinutes(15)); }
        if (reason == "dispose") service.Dispose();
        await Until(() => service.State != UpdateState.Checking);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(UpdateState.Idle, service.State);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task DisablingAutomaticDoesNotCancelAManualSearch()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new UpdateService(new FakeUpdateBackend { WaitForCheck = async token =>
        { started.TrySetResult(token); await finish.Task.WaitAsync(token); } });
        var checking = service.CheckAsync();
        var token = await started.Task;
        service.Automatic = false;
        Assert.False(token.IsCancellationRequested);
        finish.SetResult();
        await checking;
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }

    private sealed class PartialTexts : IUpdateTexts
    {
        public bool TryGet(string key, out string text) { text = "host title"; return key == "Update_Title"; }
        public string T(string key) => "host title";
        public string F(string key, params object?[] args) => "host title";
        public CultureInfo Formatting => CultureInfo.CurrentUICulture;
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

    private sealed class RecordingLog : IUpdateLog
    {
        public List<Exception> Errors { get; } = [];
        public void Warn(Exception ex, string context) => Errors.Add(ex);
    }

    private sealed class PendingBackend(IUpdatePendingStore store, bool fail) : IUpdateBackend
    {
        public bool IsInstalled => true;
        public string CurrentVersion => "1.0.0";
        public string ContentDirectory => "unused";
        public Task<UpdateRelease?> CheckAsync(CancellationToken token) => Task.FromResult<UpdateRelease?>(new("2.0.0", "", new object()));
        public Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token) => Task.CompletedTask;
        public void Apply(UpdateRelease release)
        {
            Assert.Equal(release.Version, store.ExpectedVersion);
            if (fail) throw new IOException("apply failed");
        }
    }
}
