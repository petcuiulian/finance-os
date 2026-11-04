using System.IO;
using System.Text.Json;
using System.Windows;
using FinanceOS.Core;
using FinanceOS.Infrastructure;

namespace FinanceOS.Desktop;

public partial class MainWindow
{
    private static string WorkspaceDatabase => AppDataPaths.DatabasePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    private async void OnBackupDatabase(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "SQLite workspace backup|*.db", FileName = "finance-os-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") };
        if (dialog.ShowDialog(this) != true) return;
        BackupDatabaseButton.IsEnabled = false;
        try { await DatabaseRecovery.BackupAsync(WorkspaceDatabase, dialog.FileName); RecoveryStatusText.Text = "Backup completed and integrity verified. Store it securely: it contains your workspace data."; }
        catch (Exception) { RecoveryStatusText.Text = "Backup failed. The workspace was preserved. Check the destination and available disk space."; }
        finally { BackupDatabaseButton.IsEnabled = true; }
    }
    private async void OnExportDiagnostics(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Diagnostics JSON|*.json", FileName = "finance-os-diagnostics" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var integrity = "ok";
            try { await Task.Run(() => DatabaseRecovery.CheckIntegrity(WorkspaceDatabase)); } catch (Exception) { integrity = "failed"; }
            var report = new { Application = "FINANCE OS", Version = typeof(App).Assembly.GetName().Version?.ToString(), Schema = _lastStatus?.SchemaVersion, Integrity = integrity, Framework = Environment.Version.ToString(), Windows = Environment.OSVersion.Version.ToString() };
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            RecoveryStatusText.Text = "Diagnostics exported without task contents, contact details or file paths.";
        }
        catch (Exception) { RecoveryStatusText.Text = "Diagnostics export failed. Check the destination."; }
    }
}
