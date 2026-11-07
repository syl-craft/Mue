using System.Globalization;
using System.Resources;
using System.Reflection;
using SmartFormat;

namespace Mue;

public interface IUpdateTexts
{
    bool TryGet(string key, out string text);
    string T(string key);
    string F(string key, params object?[] args);
    CultureInfo Formatting { get; }
    event EventHandler? LanguageChanged;
}

public static class UpdateTexts
{
    private static IUpdateTexts? _texts;
    private static readonly ResourceManager Resources = new("Mue.Resources.Strings", typeof(UpdateTexts).Assembly);
    private static readonly SmartFormatter Formatter = Smart.CreateDefaultSmartFormat();
    public static string AppName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Mue";
    public static string T(string key) => _texts != null && _texts.TryGet(key, out _)
        ? _texts.T(key) : (Resources.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]").Replace("{AppName}", AppName);
    public static string F(string key, params object?[] args) => _texts != null && _texts.TryGet(key, out _)
        ? _texts.F(key, args) : Formatter.Format(Formatting, T(key), args);
    public static CultureInfo Formatting => _texts?.Formatting ?? CultureInfo.CurrentUICulture;
    public static event EventHandler? LanguageChanged;

    public static void Configure(IUpdateTexts? texts)
    {
        if (_texts != null) _texts.LanguageChanged -= OnLanguageChanged;
        _texts = texts;
        if (_texts != null) _texts.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged(null, EventArgs.Empty);
    }

    private static void OnLanguageChanged(object? sender, EventArgs e) => LanguageChanged?.Invoke(sender, e);
}
