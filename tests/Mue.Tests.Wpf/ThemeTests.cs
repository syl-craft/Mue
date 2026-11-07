using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Mue.Wpf;

namespace Mue.Tests;

/// <summary>
/// Thème des fenêtres de Mue. Ces tests changent l'état global de <see cref="MueStyles"/> sur l'application de test,
/// et le rétablissent ensuite (AddMissingDefaults en System, comme WpfTestHost au démarrage).
/// </summary>
public sealed class ThemeTests
{
    [Fact]
    public Task Each_default_set_defines_exactly_the_shared_keys() => WpfTestHost.Run(() =>
    {
        var styles = Keys(MueStyles.Load("Defaults.Styles.xaml"));
        foreach (var file in new[] { "Defaults.Light.xaml", "Defaults.Dark.xaml" })
            Assert.Equal(Mue.UpdateStyleKeys.Required.Order(), Keys(MueStyles.Load(file)).Concat(styles).Order());
        var legacy = MueStyles.Load("Defaults.xaml");
        Assert.All(Mue.UpdateStyleKeys.Required, key => Assert.NotNull(legacy[key]));
    });

    [Theory]
    [InlineData("Defaults.Light.xaml")]
    [InlineData("Defaults.Dark.xaml")]
    public Task Default_colors_meet_WCAG_AA(string file) => WpfTestHost.Run(() =>
    {
        var brushes = MueStyles.Load(file);
        Assert.Empty(ContrastPairs.Failures(key =>
        {
            var color = ((SolidColorBrush)brushes[key]).Color;
            return (color.R, color.G, color.B);
        }));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Badge_keeps_its_inner_margin_and_outline_at_rest_and_with_a_release(bool hasRelease) => WpfTestHost.Run(() =>
    {
        // Avant 0.6.2 : sans marge au repos, la pastille était un cercle et « v1.2.0 » en débordait.
        var badge = new UpdateBadge { VersionIndicator = "v1.2.0", HasRelease = hasRelease };
        var window = new Window { Content = badge, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false };
        try
        {
            window.Show();
            Assert.Equal(new Thickness(10, 0, 10, 0), badge.Padding);
            Assert.Equal(new Thickness(1), badge.BorderThickness);
            Assert.Equal(12, badge.FontSize);
            var pill = (FrameworkElement)badge.Template.FindName("Badge", badge);
            var text = new FormattedText(badge.VersionIndicator, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(badge.FontFamily, badge.FontStyle, badge.FontWeight, badge.FontStretch), badge.FontSize, Brushes.Black, 1.0);
            Assert.True(pill.ActualWidth >= text.Width + 20, $"pastille {pill.ActualWidth:0} px pour un texte de {text.Width:0} px");
        }
        finally { window.Close(); }
    });

    public static TheoryData<string> BadgePalettes => new() { "HighContrast", "Defaults.Light.xaml", "Defaults.Dark.xaml" };

    [Theory]
    [MemberData(nameof(BadgePalettes))]
    public Task Every_badge_background_keeps_its_text_readable(string palette) => WpfTestHost.Run(() =>
    {
        // Régression 0.6.2 : à l'appui sans version, fond Brush.Border ; en contraste élevé, il vaut la couleur du texte (1:1).
        var brushes = palette == "HighContrast"
            ? MueStyles.HighContrastBrushes().ToDictionary(item => item.Key, item => ((SolidColorBrush)item.Brush).Color)
            : Mue.UpdateStyleKeys.Brushes.ToDictionary(key => key, key => ((SolidColorBrush)MueStyles.Load(palette)[key]).Color);
        // Le style par défaut (Generic.xaml) ne s'applique qu'une fois la pastille dans une fenêtre affichée.
        var badge = new UpdateBadge();
        var window = new Window { Content = badge, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false };
        window.Show();
        window.Close();
        var backgrounds = new List<(bool HasRelease, string Key)> { (false, "Brush.SurfaceCard"), (true, "Brush.AccentTint") };
        foreach (var trigger in badge.Template.Triggers.OfType<MultiTrigger>())
        {
            var hasRelease = trigger.Conditions.Where(condition => condition.Property == UpdateBadge.HasReleaseProperty)
                .Select(condition => (bool)condition.Value).Single();
            foreach (var setter in trigger.Setters.OfType<Setter>().Where(setter => setter.Property == Border.BackgroundProperty))
                backgrounds.Add((hasRelease, (string)((DynamicResourceExtension)setter.Value).ResourceKey));
        }
        Assert.Contains(backgrounds, item => item.HasRelease == false && item.Key != "Brush.SurfaceCard");
        foreach (var (hasRelease, key) in backgrounds)
        {
            var textColor = brushes[hasRelease ? "Brush.AccentText" : "Brush.TextLabel"];
            var background = brushes[key];
            var ratio = ContrastPairs.Ratio((textColor.R, textColor.G, textColor.B), (background.R, background.G, background.B));
            Assert.True(ratio >= ContrastPairs.AaNormalText, $"{palette} : texte sur {key} ({(hasRelease ? "avec" : "sans")} version) : {ratio:0.00}");
        }
    });

    [Fact]
    public Task Without_a_host_call_Mue_never_touches_the_title_bar_or_system_events() => WithTheme(app =>
    {
        // Hôte qui fournit toute sa palette, comme DockPad, et n'appelle jamais AddMissingDefaults ni SetTheme.
        // L'application de test a activé Mue au démarrage : SetTheme(null) le ramène à l'état « jamais appelé ».
        MueStyles.SetTheme(app, null);
        Assert.Null(app.TryFindResource("Brush.Surface"));
        AddHostPalette(app);
        var palette = Mue.UpdateStyleKeys.Required.ToDictionary(key => key, key => app.Resources[key]);
        var (paints, opened) = OpenBlockersDialog();
        MueStyles.SystemPreferenceChanged(null, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
        Flush();

        Assert.Empty(paints);
        Assert.Equal(1, opened);
        Assert.False(MueStyles.FollowsSystem);
        Assert.DoesNotContain(app.Resources.MergedDictionaries, dictionary => Mue.UpdateStyleKeys.Required.Any(dictionary.Contains));
        Assert.All(palette, pair => Assert.Same(pair.Value, app.Resources[pair.Key]));
        RemoveHostPalette(app);
    });

    [Fact]
    public Task Dark_theme_replaces_only_values_added_by_Mue_and_paints_the_title_bar() => WithTheme(app =>
    {
        var hostText = new SolidColorBrush(Colors.Orange);
        app.Resources["Brush.Text"] = hostText;

        MueStyles.SetTheme(app, MueTheme.Dark);
        var (paints, _) = OpenBlockersDialog();

        Assert.Equal(((SolidColorBrush)MueStyles.Load("Defaults.Dark.xaml")["Brush.Surface"]).Color, Brush(app, "Brush.Surface"));
        Assert.Same(hostText, app.Resources["Brush.Text"]);
        Assert.Equal(new[] { true }, paints);
        Assert.False(MueStyles.FollowsSystem);
        app.Resources.Remove("Brush.Text");
    });

    [Fact]
    public Task Host_dictionary_loaded_after_Mue_wins() => WithTheme(app =>
    {
        var hostText = new SolidColorBrush(Colors.Orange);
        var hostPalette = new ResourceDictionary { ["Brush.Text"] = hostText };
        app.Resources.MergedDictionaries.Add(hostPalette);

        Assert.Same(hostText, app.TryFindResource("Brush.Text"));
        MueStyles.SetTheme(app, MueTheme.Dark);
        Assert.Same(hostText, app.TryFindResource("Brush.Text"));
        app.Resources.MergedDictionaries.Remove(hostPalette);
    });

    [Fact]
    public Task Windows_change_is_applied_on_the_UI_thread() => WithTheme(app =>
    {
        MueStyles.IsSystemDark = () => false;
        MueStyles.SetTheme(app, MueTheme.System);
        Assert.Equal(LightSurface, Brush(app, "Brush.Surface"));

        MueStyles.IsSystemDark = () => true;
        // SystemEvents arrive sur un thread système : Mue doit repasser par le dispatcher de l'application.
        Task.Run(() => MueStyles.SystemPreferenceChanged(null, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General))).Wait();
        Flush();

        Assert.Equal(DarkSurface, Brush(app, "Brush.Surface"));
    });

    [Fact]
    public Task High_contrast_set_keeps_every_pair_readable() => WpfTestHost.Run(() =>
    {
        var brushes = MueStyles.HighContrastBrushes().ToDictionary(item => item.Key, item => (SolidColorBrush)item.Brush);
        Assert.Equal(Mue.UpdateStyleKeys.Brushes.Order(), brushes.Keys.Order());
        Assert.Empty(ContrastPairs.Failures(key => (brushes[key].Color.R, brushes[key].Color.G, brushes[key].Color.B)));
    });

    [Fact]
    public Task System_theme_follows_Windows_and_stops_when_the_host_takes_over() => WithTheme(app =>
    {
        MueStyles.IsSystemDark = () => true;
        MueStyles.SetTheme(app, MueTheme.System);
        Assert.True(MueStyles.FollowsSystem);
        Assert.Equal(((SolidColorBrush)MueStyles.Load("Defaults.Dark.xaml")["Brush.Surface"]).Color, Brush(app, "Brush.Surface"));

        MueStyles.SetTheme(app, null);
        Assert.False(MueStyles.FollowsSystem);
        Assert.Null(app.TryFindResource("Brush.Surface"));
    });

    [Fact]
    public Task High_contrast_uses_system_colors_and_leaves_the_title_bar_to_Windows() => WithTheme(app =>
    {
        MueStyles.IsHighContrast = () => true;
        MueStyles.SetTheme(app, MueTheme.System);
        var (paints, _) = OpenBlockersDialog();

        Assert.Equal(SystemColors.WindowBrush.Color, Brush(app, "Brush.Surface"));
        Assert.Equal(SystemColors.WindowTextBrush.Color, Brush(app, "Brush.Text"));
        Assert.Empty(paints);
    });

    [Fact]
    public Task Host_can_paint_the_title_bar_itself() => WithTheme(app =>
    {
        MueStyles.ManageTitleBar = false;
        MueStyles.SetTheme(app, MueTheme.Dark);
        var (paints, opened) = OpenBlockersDialog();

        Assert.Empty(paints);
        Assert.Equal(1, opened);
    });

    private static Task WithTheme(Action<Application> body) => WpfTestHost.Run(() =>
    {
        var app = Application.Current;
        var (paint, dark, contrast) = (MueStyles.PaintTitleBar, MueStyles.IsSystemDark, MueStyles.IsHighContrast);
        try { body(app); }
        finally
        {
            (MueStyles.PaintTitleBar, MueStyles.IsSystemDark, MueStyles.IsHighContrast) = (paint, dark, contrast);
            MueStyles.ManageTitleBar = true;
            MueStyles.SetTheme(app, null);
            MueStyles.AddMissingDefaults(app);
        }
    });

    /// <summary>Ouvre une fenêtre des bloqueurs et relève les peintures de barre de titre et les ouvertures signalées.</summary>
    private static (List<bool> Paints, int Opened) OpenBlockersDialog()
    {
        var paints = new List<bool>();
        var opened = 0;
        MueStyles.PaintTitleBar = (_, dark) => paints.Add(dark);
        EventHandler<Window> count = (_, _) => opened++;
        MueStyles.WindowOpened += count;
        try
        {
            var dialog = new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory("unused"), new([]), new NoHost(),
                UpdateShutdownBehavior.Kill, summary: "x");
            WpfTestHost.Show(dialog, _ => { });
        }
        finally { MueStyles.WindowOpened -= count; }
        return (paints, opened);
    }

    private static readonly string[] PaletteFiles = ["Defaults.Light.xaml", "Defaults.Styles.xaml"];
    private static void AddHostPalette(Application app)
    {
        foreach (var file in PaletteFiles)
        {
            var palette = MueStyles.Load(file);
            foreach (var key in Keys(palette)) app.Resources[key] = palette[key];
        }
    }
    private static void RemoveHostPalette(Application app)
    {
        foreach (var key in Mue.UpdateStyleKeys.Required) app.Resources.Remove(key);
    }

    private static IEnumerable<string> Keys(ResourceDictionary dictionary) => dictionary.Keys.Cast<string>();
    private static Color LightSurface => ((SolidColorBrush)MueStyles.Load("Defaults.Light.xaml")["Brush.Surface"]).Color;
    private static Color DarkSurface => ((SolidColorBrush)MueStyles.Load("Defaults.Dark.xaml")["Brush.Surface"]).Color;
    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    private static Color Brush(Application app, string key) => ((SolidColorBrush)app.Resources[key]).Color;

    private sealed class NoHost : IUpdateDialogHost
    {
        public IUpdateLog Log { get; } = new SilentUpdateLog();
        public IUpdateBlockerClassifier Classifier { get; } = new ProgramClassifier();
        public IUpdateBlockerFinder Finder => throw new NotSupportedException();
        public bool IsExiting => false;
        public string LatestReleasePage => "unused";
        public string ReleasePage(string version) => "unused";
        public Task<UpdatePreparationCheck> CheckAsync(object owner, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> PrepareAsync(object owner, CancellationToken token) => throw new NotSupportedException();
        public void BeforeApply() { }
        public void ApplyFailed() { }
        public void ExitAfterApply() { }
        public void ShowError(string message, object owner) { }
    }
}
