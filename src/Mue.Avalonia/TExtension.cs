using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Mue.Avalonia;

public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding(nameof(UpdateTextSource.Text))
    {
        Source = UpdateTextSource.For(Key),
        Mode = BindingMode.OneWay,
    };
}

internal sealed class UpdateTextSource : INotifyPropertyChanged
{
    private static readonly Dictionary<string, UpdateTextSource> Sources = [];
    private readonly string _key;
    public string Text => UpdateTexts.T(_key);
    public event PropertyChangedEventHandler? PropertyChanged;

    public static UpdateTextSource For(string key)
    {
        if (!Sources.TryGetValue(key, out var source)) Sources[key] = source = new UpdateTextSource(key);
        return source;
    }

    private UpdateTextSource(string key)
    {
        _key = key;
        UpdateTexts.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
    }
}
