using System.Net;
using System.Net.Http;
using Mue.Velopack;
using Velopack.Locators;

namespace Mue.Tests;

/// <summary>
/// Appli lancée sans VelopackApp.Run et sans locator explicite, comme les tests qui démarrent l'appli complète :
/// le backend se dit non installé, et rien ne part vers GitHub.
/// </summary>
public sealed class VelopackWithoutLocatorTests
{
    private static readonly VelopackBackendOptions Options = new("https://github.com/demo/notebook") { FallbackVersion = "1.2.0" };

    [Fact]
    public async Task The_backend_is_built_without_error_and_reports_a_manual_installation()
    {
        Assert.False(VelopackLocator.IsCurrentSet);
        using var handler = new CountingHandler();
        using var http = new HttpClient(handler);

        var backend = new VelopackBackend(Options, http, locator: null);

        Assert.False(backend.IsInstalled);
        Assert.Equal("1.2.0", backend.CurrentVersion);
        Assert.Null(await backend.CheckAsync(CancellationToken.None));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public void The_default_constructor_no_longer_throws() =>
        Assert.False(new VelopackBackend(Options).IsInstalled);

    [Fact]
    public async Task Download_and_apply_refuse_explicitly()
    {
        var backend = new VelopackBackend(Options);
        var release = new UpdateRelease("1.3.0", "", new object());

        var download = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.DownloadAsync(release, _ => { }, CancellationToken.None));
        var apply = Assert.Throws<InvalidOperationException>(() => backend.Apply(release));

        Assert.Contains("VelopackApp.Run", download.Message);
        Assert.Contains("VelopackApp.Run", apply.Message);
    }

    [Fact]
    public async Task No_search_starts_even_with_automatic_updates_on()
    {
        using var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        var time = new ManualTimeProvider();
        var schedule = new MemoryUpdateSchedule { Automatic = true };
        var cycles = 0;
        using var service = new UpdateService(new VelopackBackend(Options, http, locator: null),
            new() { Time = time, Schedule = schedule, CanSearch = () => { cycles++; return true; } });

        Assert.Equal(UpdateState.Unavailable, service.State);
        service.StartPolling();
        // Le planificateur interroge CanSearch une fois par cycle : deux cycles prouvent que la boucle a tourné.
        time.Advance(TimeSpan.FromSeconds(20));
        await Until(() => cycles >= 1);
        time.Advance(TimeSpan.FromHours(7));
        await Until(() => cycles >= 2);
        await service.CheckAsync();

        Assert.Equal(UpdateState.Unavailable, service.State);
        Assert.Equal(default, schedule.LastCheck);
        Assert.Equal(0, handler.Requests);
    }

    private static async Task Until(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (condition() == false && DateTime.UtcNow < limit) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
