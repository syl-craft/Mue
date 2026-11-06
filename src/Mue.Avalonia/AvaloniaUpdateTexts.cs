using System.Globalization;

namespace Mue.Avalonia;

internal static class AvaloniaUpdateTexts
{
    internal static string T(string key)
    {
        var text = UpdateTexts.T(key);
        if (text != $"[{key}]") return text;

        // Replis provisoires à remonter dans les ressources du cœur (docs/t2-avalonia.md).
        (string French, string English) fallback = key switch
        {
            "Common_Close" => ("Fermer", "Close"),
            "Update_DetectionIncomplete" => ("Détection incomplète : certains programmes n’ont pas pu être identifiés.", "Detection incomplete: some programs could not be identified."),
            "Update_CloseResultClosed" => ("Programme fermé", "Program closed"),
            "Update_CloseResultStillRunning" => ("Programme toujours ouvert", "Program still running"),
            "Update_CloseResultAccessDenied" => ("Fermeture refusée : accès non autorisé", "Cannot close: access denied"),
            "Update_CloseResultGone" => ("Programme déjà terminé", "Program already exited"),
            "Update_CloseResultIdentityChanged" => ("Processus remplacé : aucune fermeture effectuée", "Process replaced: no closure attempted"),
            _ => (text, text),
        };
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? fallback.French : fallback.English;
    }
}
