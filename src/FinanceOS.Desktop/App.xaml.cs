using System.Windows;
using FinanceOS.Core;
using FinanceOS.Infrastructure;

namespace FinanceOS.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var path = AppDataPaths.DatabasePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        MainWindow = new MainWindow(new SqliteAppStore(path));
        MainWindow.Show();
    }
}
