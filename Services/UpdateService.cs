using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace AGMessenger.Services;

public class UpdateService
{
    // Updated to .pl to avoid SSL certificate issues on .center
    private const string UpdateInfoUrl = "https://jarod.pl/apps/messenger/version.json";
    
    public class UpdateInfo
    {
        public string version { get; set; } = "";
        public string url { get; set; } = "";
        public bool mandatory { get; set; }
        public string changelog { get; set; } = "";
    }

    public async Task CheckForUpdatesAsync(bool silent = false)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            
            // 1. Fetch version info
            var json = await client.GetStringAsync(UpdateInfoUrl);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json);

            if (updateInfo == null) return;

            // 2. Compare versions
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var remoteVersion = Version.Parse(updateInfo.version);

            if (remoteVersion > currentVersion)
            {
                // New version available
                Application.Current.Dispatcher.Invoke(() => ShowUpdateDialog(updateInfo, currentVersion!, remoteVersion));
            }
            else if (!silent)
            {
                Application.Current.Dispatcher.Invoke(() => 
                    MessageBox.Show("Twoja wersja jest aktualna.", "Brak aktualizacji", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                Application.Current.Dispatcher.Invoke(() => 
                    MessageBox.Show($"Błąd podczas sprawdzania aktualizacji:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
    }

    private void ShowUpdateDialog(UpdateInfo info, Version current, Version remote)
    {
        // Don't require owner - MessageBox works fine without it and avoids null errors
        var result = MessageBox.Show(
            $"Dostępna jest nowa wersja: {remote} (Obecna: {current})\n\n" +
            $"Zmiany:\n{info.changelog}\n\n" +
            "Czy chcesz pobrać i zainstalować aktualizację teraz?",
            "Aktualizacja dostępna",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _ = DownloadAndInstallAsync(info.url);
        }
    }

    private async Task DownloadAndInstallAsync(string downloadUrl)
    {
        try
        {
            // Create temp file path
            var tempPath = Path.Combine(Path.GetTempPath(), "FB-Messenger-Update.exe");

            // Delete if exists
            if (File.Exists(tempPath)) File.Delete(tempPath);

            using (var client = new HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempPath, bytes);
            }

            // Execute Installer
            Process.Start(new ProcessStartInfo(tempPath)
            {
                UseShellExecute = true,
                Arguments = "/SILENT" 
            });

            // Close current app
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd pobierania aktualizacji:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
