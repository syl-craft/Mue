namespace Mue.Wpf;

public static class MueStyleKeys
{
    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(new[]
    {
        "Brush.Accent", "Brush.AccentBorderSoft", "Brush.AccentSelected", "Brush.AccentText", "Brush.AccentTint",
        "Brush.AccentTintStrong", "Brush.Border", "Brush.Danger", "Brush.DangerBorder", "Brush.DangerSurface",
        "Brush.Separator", "Brush.Surface", "Brush.SurfaceCard", "Brush.SurfaceMuted", "Brush.Text", "Brush.TextDetail",
        "Brush.TextDisabled", "Brush.TextLabel", "Brush.TextSmall", "Brush.TextSubtle", "Brush.WarnTextAlt",
        "PrimaryButton", "SecondaryButton", "DangerButton", "DialogFooter",
    });

    public static IReadOnlyList<string> Missing(Func<object, object?> find) => Required.Where(key => find(key) == null).ToArray();
}
