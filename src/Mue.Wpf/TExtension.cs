using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace Mue.Wpf;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = UpdateTextSource.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}

internal sealed class UpdateTextSource : INotifyPropertyChanged
{
    public static UpdateTextSource Instance { get; } = new();
    public string this[string key] => UpdateTexts.T(key);
    public event PropertyChangedEventHandler? PropertyChanged;

    private UpdateTextSource() => UpdateTexts.LanguageChanged += (_, _) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}
