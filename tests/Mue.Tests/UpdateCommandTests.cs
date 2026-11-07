using System.Globalization;
using System.Text.Json;
using Mue.Velopack;
using Velopack;
using Velopack.Locators;

namespace Mue.Tests;

public sealed class UpdateCommandTests
{
    [Fact]
    public async Task An_app_outside_Velopack_reports_unavailable()
    {
        var (code, json, _) = await Run(new FakeUpdateBackend { IsInstalled = false }, new CommandHost());

        Assert.Equal(UpdateCommand.Unavailable, code);
        Assert.Equal("unavailable", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task No_new_version_reports_up_to_date()
    {
        var (code, json, host) = await Run(new FakeUpdateBackend(), new CommandHost());

        Assert.Equal(UpdateCommand.Done, code);
        Assert.Equal("up-to-date", json.GetProperty("status").GetString());
        Assert.Empty(host.Calls);
    }

    [Fact]
    public async Task Check_only_reports_the_available_version_without_downloading()
    {
        var backend = Backend();
        var (code, json, host) = await Run(backend, new CommandHost(), "--check");

        Assert.Equal(UpdateCommand.Available, code);
        Assert.Equal("available", json.GetProperty("status").GetString());
        Assert.Equal("1.3.0", json.GetProperty("available").GetString());
        Assert.Equal(0, backend.Downloads);
        Assert.Empty(host.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_follows_the_window_order_and_installs_silently(bool restart)
    {
        var backend = Backend();
        var host = new CommandHost();
        var (code, json, _) = await Run(backend, host, restart ? ["--restart"] : []);

        Assert.Equal(UpdateCommand.Done, code);
        Assert.Equal("updated", json.GetProperty("status").GetString());
        Assert.Equal("1.2.0", json.GetProperty("current").GetString());
        Assert.Equal("1.3.0", json.GetProperty("available").GetString());
        Assert.Equal(new[] { "check", "find", "final", "prepare", "before" }, host.Calls);
        Assert.Equal((true, restart), backend.LastApplyMode);
        Assert.Same(UpdateCommand.Owner, host.Owners.Single());
    }

    [Fact]
    public async Task The_command_never_counts_itself_among_the_blockers()
    {
        var host = new CommandHost();
        await Run(Backend(), host);

        Assert.All(host.Queries, query => Assert.False(query.IncludeCurrentProcess));
    }

    [Fact]
    public async Task Blockers_stop_the_command_and_relays_are_listed()
    {
        var host = new CommandHost
        {
            Blockers = [new() { Pid = 7, Name = "node", ParentName = "Claude Code", IsRelay = true }, new() { Pid = 9, Name = "Preview" }],
        };
        var (code, json, _) = await Run(Backend(), host);

        Assert.Equal(UpdateCommand.Blocked, code);
        Assert.Equal("blocked", json.GetProperty("status").GetString());
        var blockers = json.GetProperty("blockers").EnumerateArray().ToArray();
        Assert.Equal(2, blockers.Length);
        Assert.True(blockers[0].GetProperty("relay").GetBoolean());
        Assert.Equal("Claude Code", blockers[0].GetProperty("name").GetString());
        Assert.Equal(UpdateTexts.T("Update_McpGroup"), blockers[0].GetProperty("group").GetString());
        Assert.False(blockers[1].GetProperty("relay").GetBoolean());
        Assert.Equal("Preview", blockers[1].GetProperty("group").GetString());
        Assert.Empty(host.CloseAttempts);
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(UpdateState.Ready, LastState);
    }

    [Fact]
    public async Task Close_blockers_closes_softly_and_installs()
    {
        var host = new CommandHost { Blockers = [new() { Pid = 9, Name = "Preview" }], ClosedOnRequest = true };
        var (code, _, _) = await Run(Backend(), host, "--close-blockers");

        Assert.Equal(UpdateCommand.Done, code);
        Assert.Equal(new[] { (9, false) }, host.CloseAttempts);
    }

    [Fact]
    public async Task Close_blockers_never_closes_an_unavailable_process_and_reports_what_stays_open()
    {
        var host = new CommandHost
        {
            Blockers = [new() { Pid = 9, Name = "Preview" }, new() { Pid = 11, Name = "Locked", Unavailable = true }],
            ClosedOnRequest = true,
        };
        var (code, json, _) = await Run(Backend(), host, "--close-blockers");

        Assert.Equal(UpdateCommand.Blocked, code);
        Assert.Equal(new[] { (9, false) }, host.CloseAttempts);
        Assert.Equal(11, json.GetProperty("blockers").EnumerateArray().Single().GetProperty("pid").GetInt32());
        Assert.DoesNotContain("prepare", host.Calls);
    }

    [Fact]
    public async Task A_summary_requires_yes()
    {
        var host = new CommandHost { Summary = "2 profiles will be migrated." };
        var (code, json, _) = await Run(Backend(), host);

        Assert.Equal(UpdateCommand.ConsentRequired, code);
        Assert.Equal("consent-required", json.GetProperty("status").GetString());
        Assert.Equal("2 profiles will be migrated.", json.GetProperty("summary").GetString());
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(UpdateState.Ready, LastState);

        var (accepted, _, _) = await Run(Backend(), new CommandHost { Summary = "2 profiles will be migrated." }, "--yes");
        Assert.Equal(UpdateCommand.Done, accepted);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_host_can_refuse(bool canContinue, bool canPrepare)
    {
        var backend = Backend();
        var (code, json, _) = await Run(backend, new CommandHost { CanContinue = canContinue, CanPrepare = canPrepare });

        Assert.Equal(UpdateCommand.Refused, code);
        Assert.Equal("refused", json.GetProperty("status").GetString());
        Assert.Equal(0, backend.Applies);
        Assert.Equal(UpdateState.Ready, LastState);
    }

    [Fact]
    public async Task The_current_process_is_excluded_even_if_the_backend_includes_it()
    {
        var host = new CommandHost();
        var backend = new IncludingBackend(Backend());
        await Run(backend, host, null, default);

        Assert.True(backend.BlockerQuery.IncludeCurrentProcess);
        Assert.NotEmpty(host.Queries);
        Assert.All(host.Queries, query => Assert.False(query.IncludeCurrentProcess));
        Assert.All(host.Queries, query => Assert.Equal(backend.BlockerQuery.Paths, query.Paths));
    }

    [Fact]
    public async Task A_search_refused_by_the_host_is_not_reported_as_up_to_date()
    {
        var (code, json, _) = await Run(Backend(), new CommandHost(), new UpdateServiceOptions { CanSearch = () => false }, default);

        Assert.Equal(UpdateCommand.Refused, code);
        Assert.Equal("refused", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_unexpected_exception_still_writes_one_json_object()
    {
        var (code, json, _) = await Run(Backend(), new CommandHost(),
            new UpdateServiceOptions { CanSearch = () => throw new InvalidOperationException("settings unreadable") }, default);

        Assert.Equal(UpdateCommand.Failed, code);
        Assert.Equal("error", json.GetProperty("status").GetString());
        Assert.Equal("settings unreadable", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Cancellation_stops_before_installing()
    {
        var backend = Backend();
        using var cancel = new CancellationTokenSource();
        var host = new CommandHost { OnCheck = cancel.Cancel };
        var (code, json, _) = await Run(backend, host, null, cancel.Token);

        Assert.Equal(UpdateCommand.Failed, code);
        Assert.Equal("error", json.GetProperty("status").GetString());
        Assert.Equal(0, backend.Applies);
        Assert.DoesNotContain("prepare", host.Calls);
        Assert.Equal(UpdateState.Ready, LastState);
    }

    [Fact]
    public void A_backend_without_silent_mode_installs_as_before()
    {
        var backend = new MinimalBackend();
        ((IUpdateBackend)backend).Apply(new UpdateRelease("1.3.0", "", new object()), silent: true, restart: false);

        Assert.Equal(1, backend.Applies);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("download")]
    [InlineData("apply")]
    public async Task Failures_give_code_1(string step)
    {
        var backend = Backend();
        var failure = new IOException("network down");
        if (step == "check") backend.CheckFailure = failure;
        if (step == "download") backend.DownloadFailure = failure;
        if (step == "apply") backend.ApplyFailure = failure;
        var host = new CommandHost();
        var (code, json, _) = await Run(backend, host);

        Assert.Equal(UpdateCommand.Failed, code);
        Assert.Equal("error", json.GetProperty("status").GetString());
        if (step == "apply") Assert.Contains("failed", host.Calls);
    }

    [Fact]
    public async Task Readable_output_is_written_without_json()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var service = new UpdateService(Backend());

        var code = await UpdateCommand.RunAsync(service, new CommandHost(), ["--check"], output);

        Assert.Equal(UpdateCommand.Available, code);
        Assert.Equal(UpdateTexts.F("Update_Available", "1.3.0"), output.ToString().Trim());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Velopack_passes_silent_and_restart_to_the_updater(bool silent, bool restart)
    {
        var root = Path.Combine(Path.GetTempPath(), "mue-command-" + Guid.NewGuid());
        var locator = new TestVelopackLocator("App", "1.2.0", Path.Combine(root, "packages"),
            appDir: Path.Combine(root, "current"), rootDir: root, updateExe: Path.Combine(root, "Update.exe"), channel: "win");
        var backend = new VelopackBackend(new("https://github.com/demo/notebook") { RestartArgs = ["--after-update"] },
            new HttpClient(), locator);
        var target = new VelopackAsset { FileName = "App-1.3.0-full.nupkg" };
        var received = default((VelopackAsset Asset, bool Silent, bool Restart, string[] Args)?);
        var original = VelopackBackend.ApplyUpdates;
        VelopackBackend.ApplyUpdates = (_, asset, s, r, args) => received = (asset, s, r, args);
        try
        {
            backend.Apply(new UpdateRelease("1.3.0", "", new UpdateInfo(target, false, null, [])), silent, restart);
        }
        finally { VelopackBackend.ApplyUpdates = original; }

        Assert.NotNull(received);
        Assert.Same(target, received.Value.Asset);
        Assert.Equal((silent, restart), (received.Value.Silent, received.Value.Restart));
        Assert.Equal(["--after-update"], received.Value.Args);
    }

    private static UpdateState LastState;

    private static FakeUpdateBackend Backend() => new()
    {
        CurrentVersion = "1.2.0",
        Release = new("1.3.0", "", new object()),
        ContentDirectory = Path.GetTempPath(),
    };

    private static Task<(int Code, JsonElement Json, CommandHost Host)> Run(FakeUpdateBackend backend, CommandHost host,
        params string[] args) => Run((IUpdateBackend)backend, host, null, default, args);

    private static async Task<(int Code, JsonElement Json, CommandHost Host)> Run(IUpdateBackend backend, CommandHost host,
        UpdateServiceOptions? options, CancellationToken token, params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var service = new UpdateService(backend, options);
        var code = await UpdateCommand.RunAsync(service, host, [.. args, "--json"], output, token);
        LastState = service.State;
        // Un seul objet JSON valide, quelle que soit l'issue.
        using var document = JsonDocument.Parse(output.ToString());
        return (code, document.RootElement.Clone(), host);
    }

    /// <summary>Backend dont la requête inclut le processus courant : la commande doit l'exclure malgré tout.</summary>
    private sealed class IncludingBackend(FakeUpdateBackend inner) : IUpdateBackend
    {
        public bool IsInstalled => inner.IsInstalled;
        public string CurrentVersion => inner.CurrentVersion;
        public string ContentDirectory => inner.ContentDirectory;
        public UpdateBlockerQuery BlockerQuery { get; } = new([inner.ContentDirectory], IncludeCurrentProcess: true);
        public Task<UpdateRelease?> CheckAsync(CancellationToken token) => inner.CheckAsync(token);
        public Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token) => inner.DownloadAsync(release, progress, token);
        public void Apply(UpdateRelease release) => inner.Apply(release);
    }

    /// <summary>Backend 0.4.1 : ne connaît que Apply(release).</summary>
    private sealed class MinimalBackend : IUpdateBackend
    {
        public int Applies { get; private set; }
        public bool IsInstalled => true;
        public string CurrentVersion => "1.2.0";
        public string ContentDirectory => Path.GetTempPath();
        public Task<UpdateRelease?> CheckAsync(CancellationToken token) => Task.FromResult<UpdateRelease?>(null);
        public Task DownloadAsync(UpdateRelease release, Action<int> progress, CancellationToken token) => Task.CompletedTask;
        public void Apply(UpdateRelease release) => Applies++;
    }

    private sealed class CommandHost : IUpdateDialogHost, IUpdateBlockerFinder
    {
        public IReadOnlyList<UpdateBlocker> Blockers { get; set; } = [];
        public string? Summary { get; init; }
        public bool CanContinue { get; init; } = true;
        public bool CanPrepare { get; init; } = true;
        public bool ClosedOnRequest { get; init; }
        public Action? OnCheck { get; init; }
        public List<string> Calls { get; } = [];
        public List<object> Owners { get; } = [];
        public List<UpdateBlockerQuery> Queries { get; } = [];
        public List<(int Pid, bool Force)> CloseAttempts { get; } = [];
        public IUpdateLog Log { get; } = new SilentUpdateLog();
        public IUpdateBlockerClassifier Classifier { get; } = new ProgramClassifier();
        public IUpdateBlockerFinder Finder => this;
        public bool IsExiting => false;
        public string LatestReleasePage => "unused";
        public string ReleasePage(string version) => "unused";

        public Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token)
        {
            Calls.Add("check"); Owners.Add(owner);
            OnCheck?.Invoke();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new UpdatePreparationCheck(CanContinue, Summary));
        }
        public Task<bool> PrepareAsync(object owner, CancellationToken token) { Calls.Add("prepare"); return Task.FromResult(CanPrepare); }
        public void BeforeApply() => Calls.Add("before");
        public void ApplyFailed() => Calls.Add("failed");
        public void ExitAfterApply() => Calls.Add("exit");
        public void ShowError(string message, object owner) => Calls.Add("error");

        public UpdateBlockerScan Find(UpdateBlockerQuery query, IUpdateBlockerClassifier classifier, bool enrich = true)
        {
            Queries.Add(query);
            Calls.Add(enrich ? "find" : "final");
            return new(Blockers);
        }
        public Task<UpdateBlockerCloseResult> CloseAsync(UpdateBlocker blocker, bool force, CancellationToken token = default)
        {
            CloseAttempts.Add((blocker.Pid, force));
            if (ClosedOnRequest) Blockers = Blockers.Where(b => b.Pid != blocker.Pid).ToArray();
            return Task.FromResult(ClosedOnRequest ? UpdateBlockerCloseResult.Closed : UpdateBlockerCloseResult.StillRunning);
        }
    }
}
