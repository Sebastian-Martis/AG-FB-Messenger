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

        var updateItem = new MenuItem { Header = "Sprawdź aktualizacje" };
        updateItem.Click += (s, e) => 
        {
            _mainWindow.CheckForUpdates(false);
        };
        menu.Items.Add(updateItem);

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

    public void ShowBalloonTip(string title, string message, int unreadCount = 1)
    {
        _trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
        
        // Flash icon with badge showing unread count
        FlashIcon(unreadCount);
    }

    private System.Windows.Threading.DispatcherTimer? _flashTimer;
    private bool _showingBadge = false;
    private System.Drawing.Icon? _originalIcon;
    private System.Drawing.Icon? _badgeIcon;
    private int _currentUnreadCount = 0;

    public void FlashIcon(int unreadCount)
    {
        _currentUnreadCount = unreadCount;
        
        // Generate badge icon with number
        _badgeIcon = CreateBadgeIcon(unreadCount);
        
        if (_flashTimer != null)
        {
            // Already flashing, just update the badge
            return;
        }

        _originalIcon = _trayIcon.Icon; // Store original
        _flashTimer = new System.Windows.Threading.DispatcherTimer();
        _flashTimer.Interval = TimeSpan.FromMilliseconds(600);
        _flashTimer.Tick += (s, e) =>
        {
            if (_showingBadge)
            {
                _trayIcon.Icon = _originalIcon;
            }
            else
            {
                _trayIcon.Icon = _badgeIcon ?? _originalIcon;
            }
            _showingBadge = !_showingBadge;
        };
        _flashTimer.Start();
        
        // Show badge immediately
        _trayIcon.Icon = _badgeIcon ?? _originalIcon;
        _showingBadge = true;
    }

    public void UpdateBadge(int count)
    {
        if (count == 0)
        {
            StopFlashing();
            return;
        }

        _badgeIcon = CreateBadgeIcon(count);
        
        // If flashing, just update the icon resource so next tick picks it up
        // If not flashing, set it immediately (static badge)
        if (_flashTimer == null)
        {
             _trayIcon.Icon = _badgeIcon;
        }
    }

    private System.Drawing.Icon CreateBadgeIcon(int count)
    {
        // Create a 16x16 bitmap with the count number
        int size = 16;
        using var bitmap = new System.Drawing.Bitmap(size, size);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        
        // Background - red circle
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);
        
        using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 53, 69)); // Bootstrap danger red
        graphics.FillEllipse(bgBrush, 0, 0, size - 1, size - 1);
        
        // Text - white number
        string text = count > 9 ? "9+" : count.ToString();
        using var font = new System.Drawing.Font("Segoe UI", count > 9 ? 6f : 8f, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        
        var textSize = graphics.MeasureString(text, font);
        float x = (size - textSize.Width) / 2;
        float y = (size - textSize.Height) / 2;
        graphics.DrawString(text, font, textBrush, x, y);
        
        // Convert to icon
        IntPtr hIcon = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    public void StopFlashing()
    {
        if (_flashTimer != null)
        {
            _flashTimer.Stop();
            _flashTimer = null;
            if (_originalIcon != null) _trayIcon.Icon = _originalIcon;
            _showingBadge = false;
            _badgeIcon = null;
            _currentUnreadCount = 0;
        }
    }

    public void Dispose()
    {
        StopFlashing();
        _trayIcon.Dispose();
    }
}
