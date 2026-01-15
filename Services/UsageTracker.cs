using System.IO;
using System.Text.Json;

namespace AGMessenger.Services;

/// <summary>
/// Usage Tracker - tracks application usage statistics
/// Equivalent to usage-tracker.js from Electron version
/// </summary>
public class UsageTracker
{
    private static UsageTracker? _instance;
    private static readonly object _lock = new();

    public static UsageTracker Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new UsageTracker();
                return _instance;
            }
        }
    }

    private readonly string _dataPath;
    private UsageData _data;
    private DateTime _sessionStart;

    public int LaunchCount => _data.LaunchCount;

    private UsageTracker()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AGMessenger");
        
        Directory.CreateDirectory(appDataPath);
        _dataPath = Path.Combine(appDataPath, "usage-stats.json");
        _sessionStart = DateTime.Now;
        _data = Load();
    }

    private UsageData Load()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                return JsonSerializer.Deserialize<UsageData>(json) ?? CreateDefaultData();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot load usage stats: {ex.Message}");
        }

        return CreateDefaultData();
    }

    private static UsageData CreateDefaultData()
    {
        return new UsageData
        {
            FirstLaunch = DateTime.Now,
            LaunchCount = 0,
            TotalSessionMinutes = 0,
            LastLaunch = null
        };
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot save usage stats: {ex.Message}");
        }
    }

    public void TrackLaunch()
    {
        _data.LaunchCount++;
        _data.LastLaunch = DateTime.Now;
        _sessionStart = DateTime.Now;
        Save();
    }

    public void TrackSessionEnd()
    {
        var sessionMinutes = (int)(DateTime.Now - _sessionStart).TotalMinutes;
        _data.TotalSessionMinutes += sessionMinutes;
        Save();
    }

    public string GetFormattedStats()
    {
        var hours = _data.TotalSessionMinutes / 60;
        var minutes = _data.TotalSessionMinutes % 60;
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        return $"""
            📊 Statystyki użycia FB-Messenger

            📅 Pierwsze uruchomienie: {_data.FirstLaunch:d}
            📅 Ostatnie uruchomienie: {_data.LastLaunch?.ToString("d") ?? "Brak"}

            🚀 Liczba uruchomień: {_data.LaunchCount}
            ⏱️ Łączny czas użycia: {hours}h {minutes}min

            📦 Wersja aplikacji: {version}
            """;
    }
}

public class UsageData
{
    public DateTime FirstLaunch { get; set; }
    public int LaunchCount { get; set; }
    public int TotalSessionMinutes { get; set; }
    public DateTime? LastLaunch { get; set; }
}
