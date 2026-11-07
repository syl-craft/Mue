using System.Windows;
using Mue.Wpf;

namespace Mue.Sample.Wpf;

public partial class MainWindow : Window
{
    private readonly UpdateService _updates;
    private readonly IUpdateDialogHost _host;

    public MainWindow(UpdateService updates, IUpdateDialogHost host)
    {
        InitializeComponent();
        _updates = updates;
        _host = host;
        DataContext = updates;
    }

    private void Badge_Click(object sender, RoutedEventArgs e) =>
        new UpdatesDialog(_updates, _host) { Owner = this }.ShowDialog();
}
