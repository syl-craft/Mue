using System.Net;
using System.Net.Http;
using System.Text.Json;
using Mue.Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Mue.Tests;

public sealed class VelopackSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mue-source-" + Guid.NewGuid());
    public VelopackSourceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task ANewerVersionOutsideLatestIsIgnored()
    {
        using var handler = new GithubHandler();
        using var http = new HttpClient(handler);
        var release = await Backend(http).CheckAsync(CancellationToken.None);
        Assert.Equal("1.0.1", release?.Version);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/releases/latest", handler.Requests[0]);
        Assert.DoesNotContain(handler.Requests, url => url.Contains("higher"));
    }

    [Theory]
    [InlineData("missing-channel")]
    [InlineData("missing-latest")]
    [InlineData("prerelease")]
    [InlineData("null")]
    public async Task AnUnusableLatestDoesNotFallBackToOtherReleases(string scenario)
    {
        using var handler = new GithubHandler { Scenario = scenario };
        using var http = new HttpClient(handler);
        Assert.Null(await Backend(http).CheckAsync(CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInterruptsTheHttpRequest(bool index)
    {
        using var handler = new GithubHandler { Hold = true, HoldIndex = index };
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var checking = Backend(http).CheckAsync(cancellation.Token);
        var requestToken = await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checking.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(requestToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StandardSourceAndCustomUserAgentRemainAvailable()
    {
        using var handler = new GithubHandler();
        using var http = new HttpClient(handler);
        var release = await Backend(http, latest: false).CheckAsync(CancellationToken.None);
        Assert.Equal("9.0.0", release?.Version);
        Assert.All(handler.UserAgents, value => Assert.Equal("Host/2.0", value));
    }

    private VelopackBackend Backend(HttpClient http, bool latest = true) => new(
        new("https://github.com/example/application") { LatestReleaseOnly = latest, UserAgent = "Host/2.0" }, http,
        new TestVelopackLocator("App", "1.0.0", Path.Combine(_root, "packages"),
            appDir: Path.Combine(_root, "current"), rootDir: _root, updateExe: Path.Combine(_root, "Update.exe"), channel: "win"));

    private sealed class GithubHandler : HttpMessageHandler
    {
        public string Scenario { get; init; } = "normal";
        public bool Hold { get; init; }
        public bool HoldIndex { get; init; }
        public List<string> Requests { get; } = [];
        public List<string> UserAgents { get; } = [];
        public TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var url = request.RequestUri?.AbsoluteUri ?? throw new InvalidOperationException();
            Requests.Add(url);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            var api = url.Contains("api.github.com");
            if (Hold && api != HoldIndex) { Started.TrySetResult(token); await Task.Delay(Timeout.Infinite, token); }
            if (api && Scenario == "missing-latest") return new(HttpStatusCode.NotFound);
            var latest = new GithubRelease
            {
                Name = "v1.0.1", PublishedAt = new DateTime(2026, 11, 1), Prerelease = Scenario == "prerelease",
                Assets = Scenario == "missing-channel" ? [] : [new() { Name = "releases.win.json", BrowserDownloadUrl = "https://example.invalid/latest.json" }],
            };
            var higher = new GithubRelease
            {
                Name = "v9.0.0", PublishedAt = new DateTime(2026, 10, 1),
                Assets = [new() { Name = "releases.win.json", BrowserDownloadUrl = "https://example.invalid/higher.json" }],
            };
            var content = api ? (Scenario == "null" ? "null" : url.EndsWith("/releases/latest")
                ? JsonSerializer.Serialize(latest) : JsonSerializer.Serialize(new[] { higher, latest }))
                : JsonSerializer.Serialize(new { Assets = new[] { new
                {
                    PackageId = "App", Version = url.Contains("higher") ? "9.0.0" : "1.0.1", Type = "Full",
                    FileName = "App-full.nupkg", SHA1 = new string('a', 40), SHA256 = new string('b', 64), Size = 100,
                } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(content) };
        }
    }
}
