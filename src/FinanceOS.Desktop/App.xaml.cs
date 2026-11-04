using System.Windows;
using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FinanceOS.Desktop;

public partial class App : Application
{
    private FileStream? _instanceLock;
    protected override void OnExit(ExitEventArgs e)
    {
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var path = AppDataPaths.DatabasePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _instanceLock = new FileStream(path + ".instance-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { MessageBox.Show("FINANCE OS is already running, or its workspace cannot be locked. Close other instances and check workspace access."); Shutdown(2); return; }
        catch (UnauthorizedAccessException) { MessageBox.Show("Workspace access was denied. Check your Windows profile permissions."); Shutdown(2); return; }
        if (e.Args.Length > 0 && e.Args[0] == "--restore")
        {
            if (e.Args.Length != 2) { MessageBox.Show("Provide one backup file after --restore."); Shutdown(2); return; }
            if (MessageBox.Show("Replace this workspace with the selected backup? The current database will be retained as a safety copy.", "Restore FINANCE OS", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Shutdown(); return; }
            try { DatabaseRecovery.RestoreOfflineAsync(e.Args[1], path).GetAwaiter().GetResult(); }
            catch (Exception) { MessageBox.Show("Restore failed. The original workspace was preserved. Check backup compatibility and close any database tools."); Shutdown(2); return; }
        }
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
