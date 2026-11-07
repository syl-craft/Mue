namespace Mue.Wpf;

/// <summary>Réglages facultatifs d'<see cref="UpdatesDialog"/>. Sans options, le parcours est celui de la 0.3.0.</summary>
public sealed record UpdatesDialogOptions
{
    /// <summary>
    /// Vrai : « Download » télécharge seulement et s'arrête sur « New version ready » ; « Restart and install » lance ensuite
    /// la préparation et l'installation. Faux (défaut) : un seul clic enchaîne téléchargement et installation.
    /// </summary>
    public bool SeparateInstallStep { get; init; }

    /// <summary>Affichage de la fenêtre des bloqueurs ; par défaut <c>ShowDialog()</c>. Sert surtout aux tests.</summary>
    public Func<UpdateBlockersDialog, bool?>? ShowBlockers { get; init; }
}
