using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mue.Wpf;

/// <summary>
/// Valeurs par défaut et thème des fenêtres de Mue. Tant que l'hôte n'appelle ni <see cref="AddMissingDefaults(Application)"/>
/// ni <see cref="SetTheme"/>, Mue ne touche ni aux ressources, ni à la barre de titre, ni aux événements système.
/// Une clé fournie par l'hôte n'est jamais remplacée : Mue ne change que les valeurs qu'il a lui-même ajoutées.
/// </summary>
public static class MueStyles
{
    private enum Palette { Light, Dark, HighContrast }

    // Valeurs de Mue, dans un dictionnaire fusionné en premier : toute ressource de l'hôte, directe ou dans un dictionnaire
    // fusionné même chargé plus tard, l'emporte sur lui.
    private static readonly ResourceDictionary Fallback = new();
    private static Application? _app;
    private static MueTheme? _theme;
    private static bool _fillsMissingKeys;
    private static bool _followsSystem;

    /// <summary>Faux : l'hôte peint lui-même la barre de titre des fenêtres de Mue (voir <see cref="WindowOpened"/>).</summary>
    public static bool ManageTitleBar { get; set; } = true;

    /// <summary>Levé pour chaque fenêtre de Mue, dès que sa poignée existe et avant son affichage.</summary>
    public static event EventHandler<Window>? WindowOpened;

    // Coutures de test : appels DWM, réglages Windows.
    internal static Action<Window, bool> PaintTitleBar { get; set; } = PaintTitleBarWithDwm;
    internal static Func<bool> IsSystemDark { get; set; } = ReadAppsUseDarkTheme;
    internal static Func<bool> IsHighContrast { get; set; } = () => SystemParameters.HighContrast;
    internal static bool FollowsSystem => _followsSystem;
    internal static MueTheme? Theme => _theme;

    /// <summary>Ajoute les clés absentes, en suivant le thème clair ou sombre du système (comportement de la 0.4.0).</summary>
    public static void AddMissingDefaults(Application app) => AddMissingDefaults(app, MueTheme.System);

    /// <summary>Ajoute les clés absentes dans le thème choisi, et les tient à jour quand le thème change.</summary>
    public static void AddMissingDefaults(Application app, MueTheme theme)
    {
        _fillsMissingKeys = true;
        SetTheme(app, theme);
    }

    /// <summary>
    /// Choisit le thème des valeurs ajoutées par Mue et de la barre de titre de ses fenêtres. <c>null</c> rend la main à l'hôte :
    /// Mue retire les valeurs qu'il a ajoutées, se désabonne des réglages Windows et ne peint plus la barre de titre.
    /// </summary>
    public static void SetTheme(Application app, MueTheme? theme)
    {
        _app = app;
        _theme = theme;
        if (theme == null)
        {
            StopFollowingSystem();
            RemoveAddedValues(app);
            _fillsMissingKeys = false;
            return;
        }
        if (theme == MueTheme.System) StartFollowingSystem(); else StopFollowingSystem();
        Apply(app);
    }

    /// <summary>Appelé par chaque fenêtre de Mue à sa construction.</summary>
    internal static void Attach(Window window) => window.SourceInitialized += (_, _) =>
    {
        WindowOpened?.Invoke(null, window);
        PaintIfManaged(window, EffectivePalette());
    };

    private static void Apply(Application app)
    {
        var values = Defaults(EffectivePalette() ?? Palette.Light);
        foreach (var key in Mue.UpdateStyleKeys.Required)
        {
            if (Fallback.Contains(key)) Fallback[key] = values[key];
            else if (_fillsMissingKeys && app.TryFindResource(key) == null) Fallback.Add(key, values[key]);
        }
        if (Fallback.Count > 0 && app.Resources.MergedDictionaries.Contains(Fallback) == false)
            app.Resources.MergedDictionaries.Insert(0, Fallback);
        foreach (var window in app.Windows.OfType<Window>().Where(window => window is UpdatesDialog or UpdateBlockersDialog))
            PaintIfManaged(window, EffectivePalette());
    }

    private static void RemoveAddedValues(Application app)
    {
        app.Resources.MergedDictionaries.Remove(Fallback);
        Fallback.Clear();
    }

    /// <summary>Palette du thème choisi ; <c>null</c> si l'hôte n'a rien choisi.</summary>
    private static Palette? EffectivePalette() => _theme switch
    {
        null => null,
        MueTheme.Light => Palette.Light,
        MueTheme.Dark => Palette.Dark,
        _ => IsHighContrast() ? Palette.HighContrast : IsSystemDark() ? Palette.Dark : Palette.Light,
    };

    private static void PaintIfManaged(Window window, Palette? palette)
    {
        // En contraste élevé, Windows peint lui-même la barre de titre aux couleurs système.
        if (palette == null || palette == Palette.HighContrast || ManageTitleBar == false) return;
        PaintTitleBar(window, palette == Palette.Dark);
    }

    private static Dictionary<string, object> Defaults(Palette palette)
    {
        var values = new Dictionary<string, object>();
        foreach (var key in Mue.UpdateStyleKeys.Styles) values[key] = SharedStyles.Value[key];
        if (palette == Palette.HighContrast)
            foreach (var (key, brush) in HighContrastBrushes()) values[key] = brush;
        else
        {
            var brushes = palette == Palette.Dark ? DarkBrushes.Value : LightBrushes.Value;
            foreach (var key in Mue.UpdateStyleKeys.Brushes) values[key] = brushes[key];
        }
        return values;
    }

    private static readonly Lazy<ResourceDictionary> LightBrushes = new(() => Load("Defaults.Light.xaml"));
    private static readonly Lazy<ResourceDictionary> DarkBrushes = new(() => Load("Defaults.Dark.xaml"));
    private static readonly Lazy<ResourceDictionary> SharedStyles = new(() => Load("Defaults.Styles.xaml"));

    internal static ResourceDictionary Load(string file) =>
        new() { Source = new Uri($"/Mue.Wpf;component/Themes/{file}", UriKind.Relative) };

    /// <summary>
    /// Couleurs système du contraste élevé, relues à chaque changement de réglage Windows. Le compteur des groupes
    /// (texte <c>TextSmall</c> sur fond <c>Border</c>) est inversé : fond couleur du texte, chiffre couleur de la fenêtre.
    /// </summary>
    internal static IEnumerable<(string Key, Brush Brush)> HighContrastBrushes()
    {
        var text = SystemColors.WindowTextBrush;
        var window = SystemColors.WindowBrush;
        var highlight = SystemColors.HighlightBrush;
        return new (string, Brush)[]
        {
            ("Brush.Accent", highlight), ("Brush.AccentBorderSoft", highlight), ("Brush.AccentSelected", window),
            ("Brush.AccentText", text), ("Brush.AccentTint", window), ("Brush.AccentTintStrong", window),
            ("Brush.Border", text), ("Brush.Danger", text), ("Brush.DangerBorder", text), ("Brush.DangerSurface", window),
            ("Brush.Separator", text), ("Brush.Surface", window), ("Brush.SurfaceCard", window), ("Brush.SurfaceMuted", window),
            ("Brush.Text", text), ("Brush.TextDetail", text), ("Brush.TextDisabled", SystemColors.GrayTextBrush),
            ("Brush.TextLabel", text), ("Brush.TextSmall", window), ("Brush.TextSubtle", text), ("Brush.WarnTextAlt", text),
        };
    }

    private static void StartFollowingSystem()
    {
        if (_followsSystem) return;
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        _followsSystem = true;
    }

    private static void StopFollowingSystem()
    {
        if (_followsSystem == false) return;
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        _followsSystem = false;
    }

    // Le mode clair ou sombre et le contraste élevé arrivent par cet événement, sur un thread système.
    internal static void SystemPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        var app = _app;
        app?.Dispatcher.BeginInvoke(() => { if (_theme == MueTheme.System) Apply(app); });
    }

    private static bool ReadAppsUseDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }

    private static void PaintTitleBarWithDwm(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        // 20 depuis Windows 10 20H1 ; 19 sur les versions antérieures.
        if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
