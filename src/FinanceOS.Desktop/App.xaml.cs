using System.Windows;
using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FinanceOS.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var path = AppDataPaths.DatabasePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        MainWindow = new MainWindow(new SqliteAppStore(path), new SqliteTaskStore(path), new SqliteReminderStore(path), new SqliteWorkflowStore(path), new SqliteProductivityStore(path));
        try
        {
            ToastNotificationManagerCompat.OnActivated += _ => Dispatcher.Invoke(() =>
            {
                MainWindow.Show(); MainWindow.WindowState = WindowState.Normal; MainWindow.Activate();
                ((MainWindow)MainWindow).OpenReminderInbox();
            });
        }
        catch (Exception)
        {
            // OS integration can be denied by policy; the durable in-app inbox must still open.
        }
        MainWindow.Show();
        if (e.Args.Contains("--toast-smoke")) Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                new ToastContentBuilder().AddText("FINANCE OS verification").AddText("Synthetic Windows notification test.")
                    .Show(toast => { toast.Tag = "finance-smoke"; toast.Group = "verification"; });
                await Task.Delay(2000);
                var submitted = ToastNotificationManagerCompat.History.GetHistory().Any(x => x.Tag == "finance-smoke");
                ToastNotificationManagerCompat.History.Remove("finance-smoke", "verification");
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "finance-toast-smoke.txt"), submitted ? "PASS: Windows notification history contains submitted toast." : "FAIL: Toast was not found in Windows notification history.");
                Shutdown(submitted ? 0 : 2);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "finance-toast-smoke.txt"), "FAIL: Windows notification API: " + ex.GetType().Name);
                Shutdown(3);
            }
        });
    }
}
