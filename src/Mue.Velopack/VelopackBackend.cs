using System.Threading;
using System.Threading.Tasks;
using global::Velopack;
using global::Velopack.Sources;
using global::Velopack.Locators;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Mue.Velopack;

public sealed record VelopackBackendOptions(string GitHubUrl)
{
    public bool Prerelease { get; init; }
    public string[] RestartArgs { get; init; } = [];
    public string FallbackVersion { get; init; } = "";
    public UpdateShutdownBehavior ShutdownBehavior { get; init; } = OperatingSystem.IsMacOS()
        ? UpdateShutdownBehavior.AskToQuit : UpdateShutdownBehavior.Kill;
    public string UserAgent { get; init; } = "Mue";
    public bool LatestReleaseOnly { get; init; }
}

public sealed class VelopackBackend : IUpdateBackend
{
    private readonly UpdateManager _manager;
    private readonly VelopackBackendOptions _options;
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Func<CancellationToken, UpdateManager>? _createCheckManager;
    public VelopackBackend(VelopackBackendOptions options)
        : this(options, Http, null) { }
    public VelopackBackend(VelopackBackendOptions options, HttpClient http, IVelopackLocator? locator)
    {
        _options = options;
        _createCheckManager = token => new UpdateManager(CreateSource(options, new UpdateDownloader(http, token, options.UserAgent)), locator: locator);
        _manager = _createCheckManager(CancellationToken.None);
    }
    public VelopackBackend(UpdateManager manager, VelopackBackendOptions options)
    {
        _manager = manager;
        _options = options;
    }
    public UpdateShutdownBehavior ShutdownBehavior => _options.ShutdownBehavior;
    public bool IsInstalled => _manager.IsInstalled;
    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? _options.FallbackVersion;
    public string ContentDirectory => AppContext.BaseDirectory;
    public async Task<UpdateRelease?> CheckAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var manager = _createCheckManager?.Invoke(token) ?? _manager;
        var result = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return result is null ? null : new(result.TargetFullRelease.Version.ToString(),
            result.TargetFullRelease.NotesMarkdown ?? "", result, result.TargetFullRelease.Size);
    }
    public Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token) =>
        _manager.DownloadUpdatesAsync((UpdateInfo)release.Package, progress, token);
    public void Apply(UpdateRelease release) => _manager.WaitExitThenApplyUpdates(
        ((UpdateInfo)release.Package).TargetFullRelease, silent: false, restart: true,
        restartArgs: _options.RestartArgs);

    private static GithubSource CreateSource(VelopackBackendOptions options, IFileDownloader downloader) => options.LatestReleaseOnly
        ? new LatestReleaseSource(options.GitHubUrl, downloader)
        : new GithubSource(options.GitHubUrl, null, options.Prerelease, downloader);

    private sealed class LatestReleaseSource(string url, IFileDownloader downloader) : GithubSource(url, null, false, downloader)
    {
        protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
        {
            var url = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases/latest");
            try
            {
                var json = await Downloader.DownloadString(url.AbsoluteUri, GetRequestHeaders("application/vnd.github+json"));
                var release = JsonSerializer.Deserialize<GithubRelease>(json);
                return release == null || release.Prerelease ? [] : [release];
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return []; }
        }
    }

    private sealed class UpdateDownloader(HttpClient http, CancellationToken checkingToken, string userAgent) : IFileDownloader
    {
        public async Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30) =>
            Encoding.UTF8.GetString(await DownloadBytes(url, headers, timeout));

        public async Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(checkingToken);
            cancellation.CancelAfter(TimeSpan.FromMinutes(timeout));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(userAgent);
            if (headers != null)
                foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellation.Token).ConfigureAwait(false);
        }

        public Task DownloadFile(string url, string targetFile, Action<int>? progress = null,
            IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
        {
            var requestHeaders = headers == null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers);
            requestHeaders["User-Agent"] = userAgent;
            return new HttpClientFileDownloader().DownloadFile(url, targetFile, progress ?? (_ => { }), requestHeaders, timeout, cancelToken);
        }
    }
}

