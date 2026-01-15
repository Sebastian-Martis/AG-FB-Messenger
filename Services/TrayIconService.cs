using System.Windows;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;

namespace AGMessenger.Services;

/// <summary>
/// System tray icon service - equivalent to Electron Tray
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly MainWindow _mainWindow;

    public TrayIconService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        // Create tray icon
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "FB-Messenger",
            ContextMenu = CreateContextMenu()
        };

        // Try to load custom icon
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                // Use default embedded icon
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        // Handle left-click on tray icon
        _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
        _trayIcon.TrayLeftMouseUp += TrayIcon_TrayLeftMouseUp;
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "Pokaż Messenger" };
        showItem.Click += (s, e) => _mainWindow.ShowWindow();
        menu.Items.Add(showItem);

        var refreshItem = new MenuItem { Header = "Odśwież" };
        refreshItem.Click += (s, e) =>
        {
            _mainWindow.ShowWindow();
            // MainWindow will handle refresh via menu
        };
        menu.Items.Add(refreshItem);

        menu.Items.Add(new Separator());

        var clearDataItem = new MenuItem { Header = "Wyczyść dane i zaloguj ponownie" };
        clearDataItem.Click += (s, e) =>
        {
            _mainWindow.ClearDataAndReload();
            _mainWindow.ShowWindow();
        };
        menu.Items.Add(clearDataItem);

        menu.Items.Add(new Separator());

        var testNotifyItem = new MenuItem { Header = "Test powiadomienia" };
        testNotifyItem.Click += (s, e) => ShowBalloonTip("Test", "To jest powiadomienie testowe!");
        menu.Items.Add(testNotifyItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Zamknij" };
        exitItem.Click += (s, e) => _mainWindow.QuitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TrayIcon_TrayLeftMouseUp(object? sender, RoutedEventArgs e)
    {
        _mainWindow.ShowWindow();
    }

    private void TrayIcon_TrayMouseDoubleClick(object? sender, RoutedEventArgs e)
    {
        _mainWindow.ShowWindow();
    }

    public void ShowBalloonTip(string title, string message)
    {
        _trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
        
        // Flash icon to indicate unread message
        FlashIcon();
    }

    private System.Windows.Threading.DispatcherTimer? _flashTimer;
    private bool _isIconTransparent = false;
    private System.Drawing.Icon? _originalIcon;

    private void FlashIcon()
    {
        if (_flashTimer != null) return; // Already flashing

        _originalIcon = _trayIcon.Icon; // Store original
        _flashTimer = new System.Windows.Threading.DispatcherTimer();
        _flashTimer.Interval = TimeSpan.FromMilliseconds(500);
        _flashTimer.Tick += (s, e) =>
        {
            if (_isIconTransparent)
            {
                _trayIcon.Icon = _originalIcon;
            }
            else
            {
                // Set to generic application icon or transparent if possible, 
                // but System.Drawing.Icon doesn't support full transparency easily without empty ico.
                // Switching to a standard "Warning" icon as "flash" state
                _trayIcon.Icon = System.Drawing.SystemIcons.Warning;
            }
            _isIconTransparent = !_isIconTransparent;
        };
        _flashTimer.Start();
    }

    public void StopFlashing()
    {
        if (_flashTimer != null)
        {
            _flashTimer.Stop();
            _flashTimer = null;
            if (_originalIcon != null) _trayIcon.Icon = _originalIcon;
            _isIconTransparent = false;
        }
    }

    public void Dispose()
    {
        StopFlashing();
        _trayIcon.Dispose();
    }
}
