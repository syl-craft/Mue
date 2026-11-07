namespace Mue;

/// <summary>
/// Ressources que les fenêtres de Mue lisent chez l'hôte, communes à Mue.Wpf et Mue.Avalonia.
/// </summary>
public static class UpdateStyleKeys
{
    public static IReadOnlyList<string> Brushes { get; } = Array.AsReadOnly(new[]
    {
        "Brush.Accent", "Brush.AccentBorderSoft", "Brush.AccentSelected", "Brush.AccentText", "Brush.AccentTint",
        "Brush.AccentTintStrong", "Brush.Border", "Brush.Danger", "Brush.DangerBorder", "Brush.DangerSurface",
        "Brush.Separator", "Brush.Surface", "Brush.SurfaceCard", "Brush.SurfaceMuted", "Brush.Text", "Brush.TextDetail",
        "Brush.TextDisabled", "Brush.TextLabel", "Brush.TextSmall", "Brush.TextSubtle", "Brush.WarnTextAlt",
    });

    public static IReadOnlyList<string> Styles { get; } = Array.AsReadOnly(new[]
    {
        "PrimaryButton", "SecondaryButton", "DangerButton", "DialogFooter",
    });

    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(Brushes.Concat(Styles).ToArray());

    public static IReadOnlyList<string> Missing(Func<object, object?> find) => Required.Where(key => find(key) == null).ToArray();
}

/// <summary>Thème des valeurs par défaut de Mue. <see cref="System"/> suit le réglage clair ou sombre du système.</summary>
public enum MueTheme { System, Light, Dark }
