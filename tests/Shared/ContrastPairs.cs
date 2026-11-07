namespace Mue.Tests;

/// <summary>
/// Couples texte / fond réellement dessinés par les fenêtres de Mue, et calcul du contraste WCAG 2.
/// Partagé par les tests WPF et Avalonia : les deux jeux par défaut doivent tenir le niveau AA (4,5:1).
/// </summary>
internal static class ContrastPairs
{
    public const double AaNormalText = 4.5;

    public static readonly (string Text, string Background)[] All =
    [
        ("Brush.Text", "Brush.Surface"), ("Brush.Text", "Brush.SurfaceCard"), ("Brush.Text", "Brush.AccentTint"),
        ("Brush.Text", "Brush.DangerSurface"),
        ("Brush.TextLabel", "Brush.Surface"), ("Brush.TextLabel", "Brush.SurfaceCard"), ("Brush.TextLabel", "Brush.SurfaceMuted"),
        ("Brush.TextDetail", "Brush.Surface"), ("Brush.TextDetail", "Brush.SurfaceCard"),
        ("Brush.TextSubtle", "Brush.Surface"), ("Brush.TextSubtle", "Brush.SurfaceCard"),
        ("Brush.TextSmall", "Brush.Border"), ("Brush.TextDisabled", "Brush.SurfaceCard"),
        ("Brush.AccentText", "Brush.Surface"), ("Brush.AccentText", "Brush.AccentTint"), ("Brush.AccentText", "Brush.AccentSelected"),
        ("Brush.Danger", "Brush.Surface"), ("Brush.Danger", "Brush.DangerSurface"),
        ("Brush.WarnTextAlt", "Brush.SurfaceCard"),
        // Boutons Avalonia survolés et enfoncés (Defaults.axaml) : le texte garde sa couleur, le fond change.
        ("Brush.Text", "Brush.AccentTintStrong"), ("Brush.Text", "Brush.AccentSelected"),
        ("Brush.AccentText", "Brush.AccentTintStrong"),
        ("Brush.Danger", "Brush.AccentTintStrong"), ("Brush.Danger", "Brush.AccentSelected"),
    ];

    /// <summary>Couples sous le niveau AA, avec leur ratio, pour un message d'échec lisible.</summary>
    public static string[] Failures(Func<string, (byte R, byte G, byte B)> color) => All
        .Select(pair => (pair, ratio: Ratio(color(pair.Text), color(pair.Background))))
        .Where(item => item.ratio < AaNormalText)
        .Select(item => $"{item.pair.Text} / {item.pair.Background} : {item.ratio:0.00}")
        .ToArray();

    public static double Ratio((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        var (light, dark) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance((byte R, byte G, byte B) c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte value)
    {
        var s = value / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
