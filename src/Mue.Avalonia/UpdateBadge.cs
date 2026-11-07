using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Mue.Avalonia;

public sealed class UpdateBadge : Button
{
    public static readonly StyledProperty<string> VersionIndicatorProperty =
        AvaloniaProperty.Register<UpdateBadge, string>(nameof(VersionIndicator), "");
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<UpdateBadge, string>(nameof(Status), "");
    public static readonly StyledProperty<bool> HasReleaseProperty =
        AvaloniaProperty.Register<UpdateBadge, bool>(nameof(HasRelease));

    public string VersionIndicator { get => GetValue(VersionIndicatorProperty); set => SetValue(VersionIndicatorProperty, value); }
    public string Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool HasRelease { get => GetValue(HasReleaseProperty); set => SetValue(HasReleaseProperty, value); }

    private Border? _badge;

    public UpdateBadge()
    {
        Height = 40;
        MinWidth = 40;
        // Police de la fenêtre en 12 px, contour fin au repos : une pastille lisible même sans nouvelle version.
        FontSize = 12;
        Cursor = new Cursor(StandardCursorType.Hand);
        Bind(ToolTip.TipProperty, new Binding(nameof(Status)) { Source = this });
        Bind(AutomationProperties.NameProperty, new Binding(nameof(VersionIndicator)) { Source = this });
        Bind(AutomationProperties.HelpTextProperty, new Binding(nameof(Status)) { Source = this });
        Template = new FuncControlTemplate<UpdateBadge>((control, _) =>
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            text.Bind(TextBlock.TextProperty, new Binding(nameof(VersionIndicator)) { Source = control });
            _badge = new Border
            {
                Name = "Badge", Height = 26, CornerRadius = new CornerRadius(13),
                VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0), Child = text,
                BorderThickness = new Thickness(1),
            };
            RefreshBrushes();
            return new Border { Background = Brushes.Transparent, Child = _badge };
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HasReleaseProperty || change.Property == IsPointerOverProperty
            || change.Property == IsPressedProperty || change.Property == IsFocusedProperty) RefreshBrushes();
    }

    private void RefreshBrushes()
    {
        if (_badge == null) return;
        var background = !HasRelease ? "Brush.SurfaceCard" : IsPressed ? "Brush.AccentSelected"
            : IsPointerOver ? "Brush.AccentTintStrong" : "Brush.AccentTint";
        _badge.Bind(Border.BackgroundProperty, new DynamicResourceExtension(background));
        _badge.Bind(Border.BorderBrushProperty, new DynamicResourceExtension(IsFocused ? "Brush.Accent"
            : HasRelease ? "Brush.AccentBorderSoft" : "Brush.Border"));
        Bind(ForegroundProperty, new DynamicResourceExtension(HasRelease ? "Brush.AccentText" : "Brush.TextLabel"));
    }
}
