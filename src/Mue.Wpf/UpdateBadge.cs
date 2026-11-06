using System.Windows;
using System.Windows.Controls;

namespace Mue.Wpf;

public sealed class UpdateBadge : Button
{
    static UpdateBadge() => DefaultStyleKeyProperty.OverrideMetadata(typeof(UpdateBadge), new FrameworkPropertyMetadata(typeof(UpdateBadge)));

    public static readonly DependencyProperty VersionIndicatorProperty = DependencyProperty.Register(
        nameof(VersionIndicator), typeof(string), typeof(UpdateBadge), new PropertyMetadata(""));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(string), typeof(UpdateBadge), new PropertyMetadata(""));
    public static readonly DependencyProperty HasReleaseProperty = DependencyProperty.Register(
        nameof(HasRelease), typeof(bool), typeof(UpdateBadge), new PropertyMetadata(false));

    public string VersionIndicator { get => (string)GetValue(VersionIndicatorProperty); set => SetValue(VersionIndicatorProperty, value); }
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool HasRelease { get => (bool)GetValue(HasReleaseProperty); set => SetValue(HasReleaseProperty, value); }
}
