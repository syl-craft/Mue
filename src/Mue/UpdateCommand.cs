using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mue;

/// <summary>
/// Mise à jour en ligne de commande, pour un script ou un agent IA. L'appli la relaie depuis sa propre ligne de commande
/// (par exemple <c>MonAppli.exe update --json</c>) et retourne le code obtenu. Même déroulé que la fenêtre, sans fenêtre :
/// recherche, téléchargement, vérification de l'hôte, résumé, bloqueurs, contrôle final, une seule préparation, installation.
/// Le processus courant n'est jamais compté parmi les bloqueurs (<see cref="UpdateBlockerQuery.IncludeCurrentProcess"/> faux).
/// </summary>
public static class UpdateCommand
{
    public const int Done = 0;
    public const int Available = 10;
    public const int Blocked = 20;
    public const int ConsentRequired = 21;
    public const int Unavailable = 22;
    public const int Refused = 23;
    public const int Failed = 1;

    /// <summary>Propriétaire passé à <see cref="IUpdateDialogHost.CheckAsync"/> et <see cref="IUpdateDialogHost.PrepareAsync"/>.</summary>
    public static object Owner { get; } = new CommandOwner();

    /// <summary>
    /// Options : <c>--check</c> (recherche seulement), <c>--yes</c> (accepte le résumé annoncé par l'hôte),
    /// <c>--close-blockers</c> (ferme les programmes qui bloquent), <c>--restart</c> (relance l'appli après
    /// l'installation), <c>--json</c> (un seul objet JSON). Code 0 après installation : l'appli doit alors quitter,
    /// Velopack copie la nouvelle version après sa sortie.
    /// </summary>
    public static async Task<int> RunAsync(UpdateService service, IUpdateDialogHost host, IReadOnlyList<string> args,
        TextWriter output, CancellationToken token = default)
    {
        var options = CommandOptions.Parse(args);
        var report = new Report(service.CurrentVersion);
        var code = await UpdateAsync(service, host, options, report, token);
        Write(output, options.Json, report);
        return code;
    }

    private static async Task<int> UpdateAsync(UpdateService service, IUpdateDialogHost host, CommandOptions options,
        Report report, CancellationToken token)
    {
        if (service.State == UpdateState.Unavailable)
            return report.End(Unavailable, "unavailable", UpdateTexts.T("Update_Unavailable"));

        await service.CheckAsync();
        if (service.State == UpdateState.Failed) return report.End(Failed, "error", UpdateTexts.T("Update_Failed"));
        if (service.Release is not { } release) return report.End(Done, "up-to-date", UpdateTexts.T("Update_UpToDate"));
        report.Available = release.Version;
        if (options.CheckOnly) return report.End(Available, "available", UpdateTexts.F("Update_Available", release.Version));

        if (await service.DownloadAsync() == false) return report.End(Failed, "error", UpdateTexts.T("Update_Failed"));
        service.BeginPreparation();
        try
        {
            return await InstallAsync(service, host, options, report, token);
        }
        catch (Exception ex)
        {
            host.Log.Warn(ex, "Cannot apply update from the command line");
            return report.End(Failed, "error", ex.Message);
        }
        finally
        {
            // Sans effet une fois l'outil de mise à jour lancé : l'état est alors Installing.
            service.CancelPreparation();
        }
    }

    private static async Task<int> InstallAsync(UpdateService service, IUpdateDialogHost host, CommandOptions options,
        Report report, CancellationToken token)
    {
        var check = await host.CheckAsync(Owner, token);
        report.Summary = check.Summary;
        if (check.CanContinue == false) return report.End(Refused, "refused", UpdateTexts.T("Update_Failed"));
        if (check.Summary != null && options.Yes == false)
            return report.End(ConsentRequired, "consent-required", check.Summary);

        var query = service.BlockerQuery;
        var scan = host.Finder.Find(query, host.Classifier);
        if (scan.Blockers.Count > 0 && options.CloseBlockers)
        {
            // Fermeture douce de ce qui peut l'être ; un processus Unavailable n'est jamais fermé.
            foreach (var blocker in scan.Blockers.Where(blocker => blocker.Unavailable == false))
                await host.Finder.CloseAsync(blocker, force: false, token);
            scan = host.Finder.Find(query, host.Classifier);
        }
        if (scan.Blockers.Count > 0) return report.Blocked(scan.Blockers);

        var finalScan = host.Finder.Find(query, host.Classifier, enrich: false);
        if (finalScan.Blockers.Count > 0) return report.Blocked(finalScan.Blockers);

        if (await host.PrepareAsync(Owner, token) == false) return report.End(Refused, "refused", UpdateTexts.T("Update_Failed"));
        host.BeforeApply();
        try { service.Apply(silent: true, restart: options.Restart); }
        catch { host.ApplyFailed(); throw; }
        return report.End(Done, "updated", UpdateTexts.T("Update_Installing"));
    }

    private static void Write(TextWriter output, bool json, Report report)
    {
        if (json) { output.WriteLine(JsonSerializer.Serialize(report, JsonOptions)); return; }
        output.WriteLine(report.Message);
        if (report.Summary != null && report.Summary != report.Message) output.WriteLine(report.Summary);
        foreach (var blocker in report.Blockers) output.WriteLine($"  {blocker.Group} · {blocker.Name} · {blocker.Detail}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Accents lisibles : ce JSON est lu par un script ou un agent, jamais inséré dans du HTML.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record CommandOptions(bool CheckOnly, bool Yes, bool CloseBlockers, bool Restart, bool Json)
    {
        public static CommandOptions Parse(IReadOnlyList<string> args) => new(
            CheckOnly: args.Contains("--check"), Yes: args.Contains("--yes"), CloseBlockers: args.Contains("--close-blockers"),
            Restart: args.Contains("--restart"), Json: args.Contains("--json"));
    }

    /// <summary>Résultat écrit en JSON : clés anglaises fixes, documentées dans le README.</summary>
    private sealed class Report(string current)
    {
        public string Status { get; private set; } = "";
        public string Current { get; } = current;
        public string? Available { get; set; }
        public string? Summary { get; set; }
        public List<BlockerReport> Blockers { get; } = [];
        public string Message { get; private set; } = "";

        public int End(int code, string status, string message)
        {
            Status = status;
            Message = message;
            return code;
        }

        public int Blocked(IReadOnlyList<UpdateBlocker> blockers)
        {
            foreach (var group in UpdateBlockerGroups.Group(blockers))
                foreach (var blocker in group.Processes)
                    Blockers.Add(new(blocker.DisplayName, blocker.Pid, blocker.Detail, blocker.IsRelay, group.Name));
            return End(UpdateCommand.Blocked, "blocked", UpdateTexts.T("Update_BlockedTitle"));
        }
    }

    private sealed record BlockerReport(string Name, int Pid, string Detail, bool Relay, string Group);

    private sealed class CommandOwner;
}
