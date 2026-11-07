namespace Mue.Avalonia;

/// <summary>Réglages facultatifs d'<see cref="UpdatesDialog"/>. Sans options, le parcours est celui de la 0.3.0.</summary>
public sealed record UpdatesDialogOptions
{
    /// <summary>
    /// Vrai : « Download » télécharge seulement et s'arrête sur « New version ready » ; « Restart and install » lance ensuite
    /// la préparation et l'installation. Faux (défaut) : un seul clic enchaîne téléchargement et installation.
    /// </summary>
    public bool SeparateInstallStep { get; init; }

    /// <summary>Affichage de la fenêtre des bloqueurs ; par défaut <c>ShowDialog</c> sur cette fenêtre. Sert surtout aux tests.</summary>
    public Func<UpdateBlockersDialog, Task<bool?>>? ShowBlockers { get; init; }

    /// <summary>Crédit « Updates by Mue » et lien vers le projet dans le pied de la fenêtre ; vrai par défaut.</summary>
    public bool ShowCredit { get; init; } = true;
}
