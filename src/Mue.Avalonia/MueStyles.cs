using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Mue.Avalonia;

public static class MueStyles
{
    public static void AddMissingDefaults(Application app)
    {
        var defaults = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new Uri("avares://Mue.Avalonia/Themes/Defaults.axaml"));
        var fallback = new ResourceDictionary();
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var resources = new ResourceDictionary();
            foreach (var key in MueStyleKeys.Required)
            {
                if (app.TryFindResource(key, variant, out _)) continue;
                if (defaults.TryGetResource(key, variant, out var value)) resources.Add(key, value);
            }
            if (resources.Count > 0) fallback.ThemeDictionaries.Add(variant, resources);
        }
        // Le premier dictionnaire fusionné reste derrière les ressources de l'hôte, même ajoutées ensuite.
        if (fallback.ThemeDictionaries.Count > 0) app.Resources.MergedDictionaries.Insert(0, fallback);
    }
}
