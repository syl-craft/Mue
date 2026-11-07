namespace Mue.Wpf;

/// <summary>Conservé pour les hôtes 0.3.0 : renvoie à la liste unique de <see cref="Mue.UpdateStyleKeys"/>.</summary>
public static class MueStyleKeys
{
    public static IReadOnlyList<string> Required => Mue.UpdateStyleKeys.Required;

    public static IReadOnlyList<string> Missing(Func<object, object?> find) => Mue.UpdateStyleKeys.Missing(find);
}
