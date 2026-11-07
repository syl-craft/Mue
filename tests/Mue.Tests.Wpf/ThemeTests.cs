using System.Windows;
using System.Windows.Media;
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
            Assert.Equal(Mue.MueStyleKeys.Required.Order(), Keys(MueStyles.Load(file)).Concat(styles).Order());
        var legacy = MueStyles.Load("Defaults.xaml");
        Assert.All(Mue.MueStyleKeys.Required, key => Assert.NotNull(legacy[key]));
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

    [Fact]
    public Task Without_a_host_call_Mue_never_touches_the_title_bar_or_system_events() => WithTheme(app =>
    {
        // Hôte qui fournit toute sa palette, comme DockPad, et n'appelle jamais AddMissingDefaults ni SetTheme.
        MueStyles.SetTheme(app, null);
        AddHostPalette(app);
        var (paints, opened) = OpenBlockersDialog();

        Assert.Empty(paints);
        Assert.Equal(1, opened);
        Assert.False(MueStyles.FollowsSystem);
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
        foreach (var key in Mue.MueStyleKeys.Required) app.Resources.Remove(key);
    }

    private static IEnumerable<string> Keys(ResourceDictionary dictionary) => dictionary.Keys.Cast<string>();
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
