using System.Windows;

namespace Kerlo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Kerlo encountered an unexpected UI error.\n\n" + args.Exception.Message,
                "Kerlo", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
