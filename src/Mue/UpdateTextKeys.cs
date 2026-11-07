using System.Collections;
using System.Globalization;
using System.Resources;

namespace Mue;

/// <summary>
/// Clés de texte de Mue. Une appli qui fournit tous ses textes vérifie dans ses tests que <see cref="Missing"/> est vide :
/// une clé ajoutée par une nouvelle version de Mue y apparaît au lieu d'afficher un texte mélangé.
/// </summary>
public static class UpdateTextKeys
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(ReadKeys(CultureInfo.InvariantCulture));

    /// <summary>Clés que <paramref name="texts"/> ne fournit pas. Informatif pour une appli qui garde le repli de Mue.</summary>
    public static IReadOnlyList<string> Missing(IUpdateTexts texts) =>
        All.Where(key => texts.TryGet(key, out _) == false).ToArray();

    /// <summary>Clés d'une langue intégrée, sans repli vers la ressource neutre.</summary>
    internal static string[] ReadKeys(CultureInfo culture)
    {
        var resources = new ResourceManager("Mue.Resources.Strings", typeof(UpdateTextKeys).Assembly);
        var set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No Mue texts for culture '{culture.Name}'");
        return set.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order(StringComparer.Ordinal).ToArray();
    }
}
