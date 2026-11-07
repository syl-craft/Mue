using System.Windows;

namespace Mue.Wpf;

public static class MueStyles
{
    public static void AddMissingDefaults(Application app)
    {
        var defaults = new ResourceDictionary { Source = new Uri("/Mue.Wpf;component/Themes/Defaults.xaml", UriKind.Relative) };
        foreach (var key in MueStyleKeys.Missing(app.TryFindResource)) app.Resources.Add(key, defaults[key]);
    }
}
