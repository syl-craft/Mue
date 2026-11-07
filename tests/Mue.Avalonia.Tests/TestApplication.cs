using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Mue.Avalonia.Tests.TestApplication))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mue.Avalonia.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        Styles.Add(new FluentTheme());
        MueStyles.AddMissingDefaults(this);
    }
}
