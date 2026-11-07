using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mue.Wpf;

namespace Mue.Sample.Wpf;

/// <summary>
/// Appli d'exemple « Notebook » : une version 1.3.0 simulée par <see cref="FakeUpdateBackend"/>, sans réseau.
/// <c>--capture &lt;dossier&gt;</c> écrit des captures neutres des fenêtres de Mue (clair et sombre, en et fr) puis quitte ;
/// le dossier conseillé est <c>artifacts/wpf</c>, ignoré par git.
/// </summary>
public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        UpdateTexts.AppName = "Notebook";
        var capture = e.Args.Length == 2 && e.Args[0] == "--capture" ? e.Args[1] : null;
        if (capture != null) { await CaptureAllAsync(capture); Shutdown(); return; }

        MueStyles.AddMissingDefaults(this);
        var service = DemoService();
        _ = service.CheckAsync();
        var main = new MainWindow(service, new DemoHost());
        main.Closed += (_, _) => { service.Dispose(); Shutdown(); };
        main.Show();
    }

    public App() => Startup += OnStartup;

    internal static UpdateService DemoService() => new(new FakeUpdateBackend
    {
        CurrentVersion = "1.2.0",
        Release = new("1.3.0", DemoNotes, new object(), 12_400_000),
        ShutdownBehavior = UpdateShutdownBehavior.AskToQuit,
    });

    private const string DemoNotes = "## 1.3.0 — 2026-11-04\n### What's new\n- Notes can be pinned to the top of the list.\n- Faster search across all notebooks.\n";

    private async Task CaptureAllAsync(string folder)
    {
        foreach (var theme in new[] { MueTheme.Light, MueTheme.Dark })
            foreach (var language in new[] { "en", "fr" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                MueStyles.AddMissingDefaults(this, theme);
                var target = Path.Combine(folder, $"{theme.ToString().ToLowerInvariant()}-{language}");
                Directory.CreateDirectory(target);
                await CaptureStatesAsync(target);
            }
    }

    private static async Task CaptureStatesAsync(string folder)
    {
        var host = new DemoHost();
        using var available = DemoService();
        await available.CheckAsync();
        Capture(new UpdatesDialog(available, host), Path.Combine(folder, "updates-available.png"));

        using var ready = DemoService();
        await ready.CheckAsync();
        await ready.DownloadAsync();
        Capture(new UpdatesDialog(ready, host), Path.Combine(folder, "updates-ready.png"));

        Capture(new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory("demo"), new(DemoHost.Blockers), host,
            UpdateShutdownBehavior.AskToQuit), Path.Combine(folder, "blockers.png"));
        Capture(new UpdateBlockersDialog(UpdateBlockerQuery.ForDirectory("demo"), new([]), host,
            UpdateShutdownBehavior.Kill, summary: "2 profiles will be migrated to the new format."), Path.Combine(folder, "confirmation.png"));
        Capture(new MainWindow(ready, host), Path.Combine(folder, "badge.png"));
    }

    /// <summary>Capture de la zone cliente, hors barre de titre, à 96 ppp.</summary>
    private static void Capture(Window window, string file)
    {
        window.ShowActivated = false;
        window.Show();
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        // Le fond appartient à la fenêtre, pas à son contenu : il est peint d'abord.
        var size = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, size);
            drawing.DrawRectangle(new VisualBrush(content), null, size);
        }
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(file)) encoder.Save(stream);
        window.Close();
    }
}
