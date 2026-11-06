using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Mue.Avalonia.Tests;

public sealed class StyleTests
{
    [AvaloniaFact]
    public void CompleteHostDictionaryIsUntouched()
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var original = app.Resources;
        var originalStyles = app.Styles.ToArray();
        app.Styles.Clear();
        try
        {
            app.Resources = new ResourceDictionary();
            foreach (var key in MueStyleKeys.Required) app.Resources.Add(key, new object());
            var expected = MueStyleKeys.Required.ToDictionary(key => key, key => app.Resources[key]);
            MueStyles.AddMissingDefaults(app);
            Assert.Empty(app.Styles);
            Assert.Empty(app.Resources.MergedDictionaries);
            Assert.Empty(app.Resources.ThemeDictionaries);
            Assert.Equal(expected.Count, app.Resources.Count);
            foreach (var pair in expected) Assert.Same(pair.Value, app.Resources[pair.Key]);
        }
        finally
        {
            app.Resources = original;
            app.Styles.Clear();
            app.Styles.AddRange(originalStyles);
        }
    }

    [AvaloniaFact]
    public void OnlyMissingKeysReceiveFallbackAndLaterHostDictionaryWins()
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var original = app.Resources;
        var originalStyles = app.Styles.ToArray();
        app.Styles.Clear();
        try
        {
            app.Resources = new ResourceDictionary();
            foreach (var key in MueStyleKeys.Required.Where(key => key != "Brush.Text")) app.Resources.Add(key, new object());
            MueStyles.AddMissingDefaults(app);
            Assert.Empty(app.Resources.MergedDictionaries);
            var fallback = Assert.IsType<ResourceDictionary>(Assert.IsType<Styles>(Assert.Single(app.Styles)).Resources);
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                var resources = Assert.IsType<ResourceDictionary>(fallback.ThemeDictionaries[variant]);
                Assert.Single(resources);
                Assert.True(resources.ContainsKey("Brush.Text"));
            }
            MueStyles.AddMissingDefaults(app);
            Assert.Single(app.Styles);
            var hostBrush = new SolidColorBrush(Colors.Purple);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { ["Brush.Text"] = hostBrush });
            Assert.True(app.TryFindResource("Brush.Text", ThemeVariant.Dark, out var result));
            Assert.Same(hostBrush, result);
        }
        finally
        {
            app.Resources = original;
            app.Styles.Clear();
            app.Styles.AddRange(originalStyles);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplicationStylesThemeWinsBeforeAndAfterFallback(bool addThemeAfterFallback)
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var original = app.Resources;
        var originalStyles = app.Styles.ToArray();
        var theme = new Styles();
        var lightBrush = new SolidColorBrush(Colors.Purple);
        var darkBrush = new SolidColorBrush(Colors.Orange);
        theme.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary { ["Brush.Text"] = lightBrush };
        theme.Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary { ["Brush.Text"] = darkBrush };
        try
        {
            app.Resources = new ResourceDictionary();
            app.Styles.Clear();
            if (!addThemeAfterFallback) app.Styles.Add(theme);
            MueStyles.AddMissingDefaults(app);
            if (addThemeAfterFallback) app.Styles.Add(theme);
            MueStyles.AddMissingDefaults(app);
            Assert.Equal(2, app.Styles.Count);
            Assert.Same(theme, app.Styles[1]);
            Assert.Empty(app.Resources.MergedDictionaries);
            Assert.True(app.TryFindResource("Brush.Text", ThemeVariant.Light, out var light));
            Assert.Same(lightBrush, light);
            Assert.True(app.TryFindResource("Brush.Text", ThemeVariant.Dark, out var dark));
            Assert.Same(darkBrush, dark);
        }
        finally
        {
            app.Resources = original;
            app.Styles.Clear();
            app.Styles.AddRange(originalStyles);
        }
    }

    [AvaloniaFact]
    public void OpenWindowAndBadgeFollowThemeChangesAndHostReplacements()
    {
        var app = Application.Current ?? throw new InvalidOperationException();
        var previousTheme = app.RequestedThemeVariant;
        var original = app.Resources;
        using var service = new UpdateService(Fixtures.Backend());
        var dialog = new UpdatesDialog(service, new TestHost());
        var badge = new UpdateBadge { VersionIndicator = "v1.0.0 → 1.1.0", HasRelease = true, Status = "Disponible" };
        var badgeWindow = new Window { Width = 200, Height = 60, Content = badge };
        try
        {
            app.Resources = new ResourceDictionary();
            MueStyles.AddMissingDefaults(app);
            app.RequestedThemeVariant = ThemeVariant.Light;
            dialog.Show(); badgeWindow.Show();
            var light = Assert.IsAssignableFrom<ISolidColorBrush>(dialog.Background).Color;
            var lightBadge = Assert.IsAssignableFrom<ISolidColorBrush>(badge.Foreground).Color;
            app.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.NotEqual(light, Assert.IsAssignableFrom<ISolidColorBrush>(dialog.Background).Color);
            Assert.NotEqual(lightBadge, Assert.IsAssignableFrom<ISolidColorBrush>(badge.Foreground).Color);
            var hostBrush = new SolidColorBrush(Colors.Purple);
            app.Resources["Brush.Surface"] = hostBrush;
            Assert.Same(hostBrush, dialog.Background);
            var theme = new ControlTheme(typeof(Button));
            app.Resources["SecondaryButton"] = theme;
            Assert.Same(theme, Fixtures.Control<Button>(dialog, "InstallButton").Theme);
            Assert.Empty(dialog.Resources);
            Assert.Empty(dialog.Resources.MergedDictionaries);
        }
        finally
        {
            dialog.Close(); badgeWindow.Close();
            app.Resources = original;
            app.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaFact]
    public void BadgeHasFullyClickableFortyPixelTargetAndTwentySixPixelPill()
    {
        var badge = new UpdateBadge { VersionIndicator = "v1.0.0", Status = "À jour" };
        var window = new Window { Width = 200, Height = 60, Content = badge };
        var clicks = 0;
        var command = new BadgeCommand();
        badge.Command = command;
        badge.CommandParameter = "updates";
        badge.Click += (_, _) => clicks++;
        window.Show();
        Assert.Equal(40, badge.Bounds.Height);
        var pill = badge.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "Badge").Single();
        Assert.Equal(26, pill.Bounds.Height);
        var top = badge.TranslatePoint(new Point(badge.Bounds.Width / 2, 1), window) ?? throw new InvalidOperationException();
        window.MouseDown(top, MouseButton.Left); window.MouseUp(top, MouseButton.Left);
        var bottom = badge.TranslatePoint(new Point(badge.Bounds.Width / 2, 39), window) ?? throw new InvalidOperationException();
        window.MouseDown(bottom, MouseButton.Left); window.MouseUp(bottom, MouseButton.Left);
        Assert.Equal(2, clicks);
        badge.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.Equal(3, clicks);
        Assert.Equal(3, command.Calls);
        Assert.Equal("updates", command.Parameter);
        Assert.Equal("À jour", ToolTip.GetTip(badge));
        window.Close();
    }

    private sealed class BadgeCommand : System.Windows.Input.ICommand
    {
        public int Calls { get; private set; }
        public object? Parameter { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Calls++; Parameter = parameter; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    [Fact]
    public void StyleContractMatchesWpfExactlyAndNoOtherDynamicKeyIsUsed()
    {
        var source = File.ReadAllText(Path.Combine(Fixtures.Root, "src/Mue.Wpf/MueStyleKeys.cs"));
        var keys = System.Text.RegularExpressions.Regex.Matches(source, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(keys, MueStyleKeys.Required);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Fixtures.Root, "src/Mue.Avalonia"), "*.axaml", SearchOption.AllDirectories))
        {
            var references = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"\{DynamicResource ([^}]+)\}");
            foreach (System.Text.RegularExpressions.Match reference in references) Assert.Contains(reference.Groups[1].Value, MueStyleKeys.Required);
        }
    }
}
