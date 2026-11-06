using System.Globalization;
using System.Runtime.CompilerServices;

namespace Mue.Tests;

internal sealed class TestUpdateTexts : IUpdateTexts
{
    public bool TryGet(string key, out string text) { text = T(key); return true; }
    public string T(string key) => key;
    public string F(string key, params object?[] args) =>
        key + "(" + string.Join(", ", args.Select(value => Convert.ToString(value, Formatting))) + ")";
    public CultureInfo Formatting => CultureInfo.InvariantCulture;
    public event EventHandler? LanguageChanged { add { } remove { } }

    [ModuleInitializer]
    internal static void Initialize() => UpdateTexts.Configure(new TestUpdateTexts());
}
