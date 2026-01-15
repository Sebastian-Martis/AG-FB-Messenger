using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using AGMessenger.Services;

namespace AGMessenger;

public partial class MainWindow : Window
{
    private readonly WindowStateManager _windowStateManager;
    private readonly TrayIconService _trayIconService;
    private bool _isQuitting = false;
    private bool _wasMinimizedNotificationShown = false;

    // Configuration
    private const string MessengerUrl = "https://www.messenger.com/";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private static readonly string[] AllowedDomains = { "messenger.com", "facebook.com", "fbcdn.net", "google.com", "facebook.net" };

    public MainWindow()
    {
        InitializeComponent();

        // Initialize services
        _windowStateManager = new WindowStateManager();
        _trayIconService = new TrayIconService(this);

        // Restore window state
        _windowStateManager.RestoreWindowState(this);

        // Setup keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.R, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleDevTools()), Key.F12, ModifierKeys.None));

        // Initialize WebView2
        InitializeWebViewAsync();
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            // Create environment with persistent user data (cookies, sessions)
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AGMessenger", "WebView2Data");
            
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await WebView.EnsureCoreWebView2Async(env);

            // Configure WebView2
            var settings = WebView.CoreWebView2.Settings;
            settings.UserAgent = UserAgent;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsWebMessageEnabled = true;

            // Handle web messages (shim for notifications)
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Inject Notification API Shim
            // This ensures we catch notifications even if the native WebView2 event fails
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                function shimNotification() {
                    if (!window.Notification) {
                        window.Notification = function(title, options) {
                            this.title = title;
                            this.body = options ? options.body : '';
                            window.chrome.webview.postMessage(JSON.stringify({
                                type: 'notification',
                                title: this.title,
                                body: this.body
                            }));
                        };
                        window.Notification.permission = 'granted';
                        window.Notification.requestPermission = async function() { return 'granted'; };
                    } else {
                        const originalNotification = window.Notification;
                        window.Notification = function(title, options) {
                            window.chrome.webview.postMessage(JSON.stringify({
                                type: 'notification',
                                title: title,
                                body: options ? options.body : ''
                            }));
                            return new originalNotification(title, options);
                        };
                        Object.assign(window.Notification, originalNotification);
                        window.Notification.permission = 'granted';
                    }
                }
                shimNotification();
            ");

            // Handle permission requests (notifications, etc.)
            WebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;

            // Handle title changes (fallback for notifications)
            WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;

            // Handle web notifications - show as Windows toast
            WebView.CoreWebView2.NotificationReceived += CoreWebView2_NotificationReceived;

            // Handle new window requests (external links)
            WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            
            // Handle navigation (block leaving Messenger)
            WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            
            // Handle navigation failures
            WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // Load Messenger
            WebView.CoreWebView2.Navigate(MessengerUrl);
        }
        catch (Exception ex)
        {
            ShowError($"Nie można zainicjalizować WebView2: {ex.Message}");
        }
    }

    private string _lastTitle = "";
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private int _confirmedUnreadCount = 0;
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;

    private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
    {
        var title = WebView.CoreWebView2.DocumentTitle;
        if (string.IsNullOrEmpty(title) || title == _lastTitle) return;

        _lastTitle = title;
        int currentCount = GetUnreadCount(title);

        // Logic:
        // 1. If count INCREASES above confirmed -> Immediate Notification (New Message)
        // 2. If count DECREASES (to 0 or lower) -> Debounce (Wait to see if it's just a title toggle)
        
        if (currentCount > _confirmedUnreadCount)
        {
            // Cancel any pending 'read' confirmation
            if (_debounceTimer != null) 
            {
                _debounceTimer.Stop();
                _debounceTimer = null;
            }

            _confirmedUnreadCount = currentCount;
            TriggerNotificationFromTitle(title);
        }
        else if (currentCount < _confirmedUnreadCount)
        {
            // Potentially read, or just a title glitch (1 -> 0 -> 1)
            // Start debounce timer if not running
            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Windows.Threading.DispatcherTimer();
                _debounceTimer.Interval = TimeSpan.FromSeconds(2); // 2 second grace period
                _debounceTimer.Tick += (s, args) => 
                {
                    _debounceTimer.Stop();
                    _debounceTimer = null;
                    
                    // Verify correct state after delay
                    var freshTitle = WebView.CoreWebView2.DocumentTitle;
                    int freshCount = GetUnreadCount(freshTitle);
                    
                    if (freshCount < _confirmedUnreadCount)
                    {
                        // Confirmed: User actually read the messages
                        _confirmedUnreadCount = freshCount;
                        
                        // Stop flashing as we are now "Read"
                        if (_confirmedUnreadCount == 0)
                        {
                            StopFlashingUI();
                        }
                    }
                };
                _debounceTimer.Start();
            }
        }
    }

    private void StopFlashingUI()
    {
        Dispatcher.Invoke(() => 
        {
            _trayIconService.StopFlashing();
            StopTaskbarFlash();
        });
    }

    private int GetUnreadCount(string title)
    {
        // Simple and strict: If it has (N), it's N. Else it's 0.
        var match = System.Text.RegularExpressions.Regex.Match(title, @"^\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
        {
            return count;
        }
        return 0;
    }

    private void TriggerNotificationFromTitle(string title)
    {
        // Clean title: remove "(N) " prefix
        string cleanTitle = System.Text.RegularExpressions.Regex.Replace(title, @"^\(\d+\)\s*", "");
        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle == "Messenger" || cleanTitle == "Facebook") 
        {
            cleanTitle = "Nowa wiadomość";
        }
        
        string body = "Masz nieprzeczytane wiadomości na Messengerze.";
        
        // Use clean title as context if it's specific
        if (cleanTitle != "Nowa wiadomość")
        {
             body = cleanTitle;
             cleanTitle = "FB-Messenger";
        }

        Dispatcher.Invoke(() => 
        {
            _trayIconService.ShowBalloonTip(cleanTitle, body);
            
            if (Visibility == Visibility.Hidden || Visibility == Visibility.Collapsed)
            {
                Show();
                WindowState = WindowState.Minimized;
            }
            
            FlashTaskbar();
            _lastNotificationTime = DateTime.Now;
        });
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var url = e.Uri;
        bool isAllowed = AllowedDomains.Any(domain => url.Contains(domain));

        if (!isAllowed)
        {
            // Open in default browser
            e.Handled = true;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else
        {
            // Allow opening in app (new window)
            e.Handled = false;
        }
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var url = e.Uri;
        bool isAllowed = AllowedDomains.Any(domain => url.Contains(domain));

        if (!isAllowed)
        {
            e.Cancel = true;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ErrorOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void CoreWebView2_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        // Auto-allow notifications from Messenger/Facebook
        if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
        {
            var uri = e.Uri;
            bool isAllowed = AllowedDomains.Any(domain => uri.Contains(domain));
            
            if (isAllowed)
            {
                e.State = CoreWebView2PermissionState.Allow;
            }
        }
        // Auto-allow microphone and camera for calls
        else if (e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                 e.PermissionKind == CoreWebView2PermissionKind.Camera)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }

    private void CoreWebView2_NotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        // Mark as handled so we can show our own custom notification
        e.Handled = true;

        // Get notification content
        var title = e.Notification.Title;
        var body = e.Notification.Body;
        ShowNotification(title, body);
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            // Simple JSON parsing (avoiding dependency on Newtonsoft.Json if possible, assuming simple structure)
            // But we should use System.Text.Json
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "notification")
            {
                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "FB-Messenger";
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";
                
                ShowNotification(title ?? "FB-Messenger", body ?? "");
            }
        }
        catch 
        { 
            // Ignore parsing errors 
        }
    }

    private void ShowNotification(string title, string body)
    {
        if (string.IsNullOrEmpty(title)) title = "FB-Messenger";

        Dispatcher.Invoke(() => 
        {
            _trayIconService.ShowBalloonTip(title, body);
            
            // If window is hidden (tray only), show it minimized so taskbar flash is visible
            if (Visibility == Visibility.Hidden || Visibility == Visibility.Collapsed)
            {
                Show();
                WindowState = WindowState.Minimized;
            }
            
            FlashTaskbar();
        });
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #region Menu Handlers

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        MessageBox.Show($"FB-Messenger\n\nWersja: {version}\nCreated by JaRoD-CENTER", 
            "O programie", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Stats_Click(object sender, RoutedEventArgs e)
    {
        var stats = UsageTracker.Instance.GetFormattedStats();
        MessageBox.Show(stats, "Statystyki użycia", MessageBoxButton.OK, MessageBoxImage.Information);
    }



    #region Win32 API - Flash Window

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    private void FlashTaskbar()
    {
        var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
        var info = new FLASHWINFO
        {
            hwnd = interopHelper.Handle,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = uint.MaxValue,
            dwTimeout = 0
        };
        info.cbSize = Convert.ToUInt32(System.Runtime.InteropServices.Marshal.SizeOf(info));
        FlashWindowEx(ref info);
    }

    private void StopTaskbarFlash()
    {
        var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
        var info = new FLASHWINFO
        {
            hwnd = interopHelper.Handle,
            dwFlags = 0, // Stop flashing
            uCount = 0,
            dwTimeout = 0
        };
        info.cbSize = Convert.ToUInt32(System.Runtime.InteropServices.Marshal.SizeOf(info));
        FlashWindowEx(ref info);
    }

    #endregion

    private void Feedback_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("mailto:sebastian@jarod.center?subject=AG%20Messenger%20Feedback") 
            { UseShellExecute = true });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    
    private void DevTools_Click(object sender, RoutedEventArgs e) => ToggleDevTools();

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _isQuitting = true;
        UsageTracker.Instance.TrackSessionEnd();
        Application.Current.Shutdown();
    }

    private void RetryConnection_Click(object sender, RoutedEventArgs e)
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        Refresh();
    }

    #endregion

    #region Window Methods

    private void Refresh()
    {
        if (WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.Reload();
        }
    }

    private void ToggleDevTools()
    {
        if (WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.OpenDevToolsWindow();
        }
    }

    public void ShowWindow()
    {
        _trayIconService.StopFlashing();
        StopTaskbarFlash();
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    public void HideWindow()
    {
        Hide();
    }

    public void QuitApplication()
    {
        _isQuitting = true;
        UsageTracker.Instance.TrackSessionEnd();
        Application.Current.Shutdown();
    }

    public async void ClearDataAndReload()
    {
        if (WebView.CoreWebView2 != null)
        {
            await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync();
            WebView.CoreWebView2.Reload();
        }
    }

    #endregion

    #region Window Events

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Tray icon is already created in constructor
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isQuitting)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            Hide();

            // Show notification (only once)
            if (!_wasMinimizedNotificationShown)
            {
                _trayIconService.ShowBalloonTip("FB-Messenger", 
                    "Aplikacja działa w tle. Kliknij ikonę w zasobniku aby otworzyć.");
                _wasMinimizedNotificationShown = true;
            }
        }
        else
        {
            // Actually closing - save state and cleanup
            _windowStateManager.SaveWindowState(this);
            _trayIconService.Dispose();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        _windowStateManager.SaveWindowState(this);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _windowStateManager.SaveWindowStateDebounced(this);
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        _windowStateManager.SaveWindowStateDebounced(this);
    }

    #endregion
}

/// <summary>
/// Simple relay command for keyboard shortcuts
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
}
