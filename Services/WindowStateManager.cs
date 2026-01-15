using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace AGMessenger.Services;

/// <summary>
/// Manages window state persistence (position, size, maximized state)
/// Equivalent to electron-store functionality in Electron version
/// </summary>
public class WindowStateManager
{
    private readonly string _settingsPath;
    private WindowState _savedState;
    private DispatcherTimer? _saveDebounceTimer;

    public WindowStateManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AGMessenger");
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "window-state.json");
    }

    public void RestoreWindowState(Window window)
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var state = JsonSerializer.Deserialize<WindowStateData>(json);

                if (state != null)
                {
                    // Validate that the position is on a visible screen
                    if (IsPositionOnScreen(state.Left, state.Top, state.Width, state.Height))
                    {
                        window.Left = state.Left;
                        window.Top = state.Top;
                        window.Width = state.Width;
                        window.Height = state.Height;
                    }

                    if (state.IsMaximized)
                    {
                        window.WindowState = WindowState.Maximized;
                    }

                    _savedState = state.IsMaximized ? WindowState.Maximized : WindowState.Normal;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot restore window state: {ex.Message}");
        }
    }

    private static bool IsPositionOnScreen(double left, double top, double width, double height)
    {
        // Check if window position is visible on any screen
        var virtualScreenLeft = SystemParameters.VirtualScreenLeft;
        var virtualScreenTop = SystemParameters.VirtualScreenTop;
        var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
        var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

        return left >= virtualScreenLeft - width / 2 &&
               top >= virtualScreenTop - height / 2 &&
               left < virtualScreenLeft + virtualScreenWidth &&
               top < virtualScreenTop + virtualScreenHeight;
    }

    public void SaveWindowState(Window window)
    {
        try
        {
            var state = new WindowStateData
            {
                IsMaximized = window.WindowState == WindowState.Maximized
            };

            // Only save dimensions if not maximized
            if (!state.IsMaximized)
            {
                state.Left = window.Left;
                state.Top = window.Top;
                state.Width = window.Width;
                state.Height = window.Height;
            }
            else
            {
                // Keep previous dimensions for when window is restored
                try
                {
                    if (File.Exists(_settingsPath))
                    {
                        var json = File.ReadAllText(_settingsPath);
                        var prevState = JsonSerializer.Deserialize<WindowStateData>(json);
                        if (prevState != null)
                        {
                            state.Left = prevState.Left;
                            state.Top = prevState.Top;
                            state.Width = prevState.Width;
                            state.Height = prevState.Height;
                        }
                    }
                }
                catch { /* Ignore */ }
            }

            var output = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, output);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot save window state: {ex.Message}");
        }
    }

    /// <summary>
    /// Debounced save - waits 500ms before saving to avoid excessive disk writes
    /// </summary>
    public void SaveWindowStateDebounced(Window window)
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveDebounceTimer.Tick += (s, e) =>
        {
            _saveDebounceTimer?.Stop();
            SaveWindowState(window);
        };
        _saveDebounceTimer.Start();
    }
}

public class WindowStateData
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public bool IsMaximized { get; set; } = false;
}
