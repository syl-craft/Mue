namespace Mue.Wpf;

/// <summary>Conservé pour les hôtes 0.3.0 : renvoie à la liste unique de <see cref="Mue.MueStyleKeys"/>.</summary>
public static class MueStyleKeys
{
    public static IReadOnlyList<string> Required => Mue.MueStyleKeys.Required;

    public static IReadOnlyList<string> Missing(Func<object, object?> find) => Mue.MueStyleKeys.Missing(find);
}
