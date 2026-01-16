using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Hardcodet.Wpf.TaskbarNotification;
using AGMessenger.Services; 
using System.Text.Json; 

namespace AGMessenger;

public partial class MainWindow : Window
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private readonly string[] AllowedDomains = { "facebook.com", "messenger.com", "fb.com" };
    
    // Services
    private WindowStateManager _windowStateManager;
    private TrayIconService _trayIconService;
    private UpdateService _updateService;
    
    // State
    private bool _isQuitting = false;
    private bool _wasMinimizedNotificationShown = false;
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;
    
    // Notification Logic
    private int _confirmedUnreadCount = 0; 
    private DateTime _lastSpecificNotificationTime = DateTime.MinValue;
    private string _lastTitle = "";
    
    // New Smart Notification Logic
    private int _internalMessageCounter = 0; 
    private int _realUnreadCount = 0; 
    private string _lastKnownSender = ""; 
    private string _lastNotifiedSpecificContent = ""; 
    private string _lastSeenSpecificContent = ""; 
    private DateTime _windowActivatedTime = DateTime.MinValue;
    private DateTime _lastAudioNotification = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize services
        _windowStateManager = new WindowStateManager();
        _trayIconService = new TrayIconService(this);
        _updateService = new UpdateService();

        // Restore window state
        _windowStateManager.RestoreWindowState(this);

        // Setup keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.R, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleDevTools()), Key.F12, ModifierKeys.None));

        // Initialize WebView2
        InitializeWebViewAsync();

        // Check for updates
        CheckForUpdates(silent: true);
        
        // Start periodic update checker (every 1 hour)
        var updateTimer = new System.Windows.Threading.DispatcherTimer();
        updateTimer.Interval = TimeSpan.FromHours(1);
        updateTimer.Tick += (s, e) => CheckForUpdates(silent: true);
        updateTimer.Start();

        // Allow resetting count when window is focused
        this.Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _internalMessageCounter = 0;
        _realUnreadCount = 0;
        _confirmedUnreadCount = 0;
        _lastNotifiedSpecificContent = "";
        _lastSeenSpecificContent = "";
        
        // Explicitly clear badge to zero
        if (_trayIconService != null) _trayIconService.UpdateBadge(0);
        StopTaskbarFlash();
    }

    public async void CheckForUpdates(bool silent = false)
    {
        await _updateService.CheckForUpdatesAsync(silent);
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            // Create environment with persistent user data
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

            // Handle web messages
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Inject scripts
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    let lastContent = '';
                    let msgCount = 0;
                    
                    // Audio Hook (HTML5)
                    const origAudioPlay = window.Audio.prototype.play;
                    window.Audio.prototype.play = function() {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'audio_notification', source: 'html5' }));
                        return origAudioPlay.apply(this, arguments);
                    };

                    // Web Audio API Hook
                    if (window.AudioBufferSourceNode) {
                        const origStart = window.AudioBufferSourceNode.prototype.start;
                        window.AudioBufferSourceNode.prototype.start = function() {
                            window.chrome.webview.postMessage(JSON.stringify({ 
                                type: 'audio_notification', 
                                source: 'web_audio' 
                            }));
                            return origStart.apply(this, arguments);
                        };
                    }

                    // Notification Shim
                    const origNotif = window.Notification;
                    window.Notification = function(title, options) {
                        msgCount++;
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'notification',
                            title: title,
                            body: options ? options.body : '',
                            count: msgCount
                        }));
                        if (origNotif) try { return new origNotif(title, options); } catch(e) {}
                    };
                    window.Notification.permission = 'granted';
                    window.Notification.requestPermission = async () => 'granted';
                    if (origNotif) Object.assign(window.Notification, origNotif);
                    
                    // ARIA/Global Crawler (Max-Count Strategy)
                    setInterval(function() {
                        try {
                            // 1. Global Badge (Red Pill on Chats icon)
                            let badgeCount = 0;
                            // Search for specific navigation buttons for reliability
                            const navButtons = document.querySelectorAll('[role=""navigation""] [role=""button""]');
                            navButtons.forEach(btn => {
                                const label = btn.getAttribute('aria-label') || '';
                                if (label === 'Chats' || label === 'Czaty' || label.includes('Messenger')) {
                                    const badge = btn.querySelector('span span span');
                                    if (badge) {
                                        const val = parseInt(badge.innerText);
                                        if (!isNaN(val)) badgeCount = val;
                                    }
                                }
                            });
                             // Fallback: try raw selector if labeled lookup failed
                            if (badgeCount === 0) {
                                const rawBadge = document.querySelector('[role=""navigation""] [role=""button""] span span span');
                                if (rawBadge) {
                                     const val = parseInt(rawBadge.innerText);
                                     if (!isNaN(val)) badgeCount = val;
                                }
                            }

                            // 2. Title Count '(N) Messenger'
                            let titleCount = 0;
                            const titleMatch = document.title.match(/^\((\d+)\)/);
                            if (titleMatch) {
                                titleCount = parseInt(titleMatch[1]);
                            }

                            // 3. Row Summation
                            let rowSum = 0;
                            const elements = document.querySelectorAll('[aria-label]');
                            const regex = /(\d+)\s+(?:unread|nieprzeczytan|nowych|new)/i;
                            elements.forEach(el => {
                                const label = el.getAttribute('aria-label');
                                if (!label) return;
                                const match = label.match(regex);
                                if (match) {
                                    const count = parseInt(match[1]);
                                    if (count > 0 && count < 100) {
                                        rowSum += count;
                                    }
                                }
                            });

                            // DECISION: Take the MAXIMUM of identified counts
                            const finalCount = Math.max(badgeCount, titleCount, rowSum);
                            
                            let strategy = 'max_strategy';
                             if (finalCount === badgeCount && badgeCount > 0) strategy = 'badge';
                             else if (finalCount === titleCount && titleCount > 0) strategy = 'title';
                             else if (finalCount === rowSum && rowSum > 0) strategy = 'row_sum';

                             window.chrome.webview.postMessage(JSON.stringify({ 
                                type: 'aria_count_update', 
                                count: finalCount,
                                strategy: strategy,
                                debug: { badge: badgeCount, title: titleCount, rows: rowSum }
                            }));
                        } catch(e) {}
                    }, 2000);

                    // Polling
                    setInterval(function() {
                        const title = document.title || '';
                        const content = title.replace(/^\(\d+\)\s*/, '').trim();
                        
                        if (content && content !== lastContent && content !== 'Messenger') {
                            if (content.includes(':') || content.includes(' sent ') || content.includes(' wysłał')) {
                                lastContent = content;
                                let sender = 'FB-Messenger', msg = content;
                                const idx = content.indexOf(':');
                                if (idx > 0) {
                                    sender = content.substring(0, idx).trim();
                                    msg = content.substring(idx + 1).trim();
                                }
                                window.chrome.webview.postMessage(JSON.stringify({
                                    type: 'title_content_update',
                                    title: sender,
                                    body: msg
                                }));
                            }
                        }
                        
                        if (!document.title.match(/^\(\d+\)/) && msgCount > 0) {
                            msgCount = 0;
                            lastContent = '';
                            window.chrome.webview.postMessage(JSON.stringify({ type: 'all_read' }));
                        }
                    }, 500);
                })();
            ");

            // Handle permission requests
            WebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;

            // Handle title changes
            WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;

            // Handle web notifications
            WebView.CoreWebView2.NotificationReceived += CoreWebView2_NotificationReceived;
            
            // New Window
            WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            
            // Navigation
            WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // Load Messenger
            WebView.Source = new Uri("https://www.messenger.com/");
        }
        catch (Exception ex)
        {
            ShowError($"Błąd inicjalizacji WebView2: {ex.Message}");
        }
    }

    private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
    {
        string title = WebView.CoreWebView2.DocumentTitle;
        if (string.IsNullOrEmpty(title) || title == _lastTitle) return;
        _lastTitle = title;

        int? currentCount = GetUnreadCount(title);
        string currentContent = GetCleanContent(title);
        bool isSpecific = IsSpecific(currentContent);

        if (isSpecific)
        {
            var senderParts = currentContent.Split(new[] { ':' }, 2);
            if (senderParts.Length >= 1) _lastKnownSender = senderParts[0].Trim();

            bool alreadyNotified = (currentContent == _lastNotifiedSpecificContent);
            bool userJustSawIt = IsActive && (currentContent == _lastSeenSpecificContent);
            bool justActivated = IsActive && (DateTime.Now - _windowActivatedTime).TotalSeconds < 1;
            
            if (!alreadyNotified && !userJustSawIt && !justActivated)
            {
                _lastNotifiedSpecificContent = currentContent;
                _lastSpecificNotificationTime = DateTime.Now;
                _internalMessageCounter++;
                TriggerSmartNotification(currentContent, true);
            }
            if (IsActive) _lastSeenSpecificContent = currentContent;
            return;
        }

        if (currentCount.HasValue && currentCount.Value > 0)
        {
            if (currentCount.Value > _confirmedUnreadCount)
            {
                if (_debounceTimer != null) { _debounceTimer.Stop(); _debounceTimer = null; }
                _confirmedUnreadCount = currentCount.Value;
                if ((DateTime.Now - _lastSpecificNotificationTime).TotalSeconds > 2)
                    TriggerSmartNotification(currentContent, false);
            }
            else if (currentCount.Value < _confirmedUnreadCount)
            {
                if (_debounceTimer == null)
                {
                    _debounceTimer = new System.Windows.Threading.DispatcherTimer();
                    _debounceTimer.Interval = TimeSpan.FromSeconds(2);
                    _debounceTimer.Tick += (s, args) => 
                    {
                        var timer = _debounceTimer;
                        if (timer != null) { timer.Stop(); _debounceTimer = null; }
                        var freshTitle = WebView.CoreWebView2.DocumentTitle;
                        var freshCount = GetUnreadCount(freshTitle);
                        if (freshCount.HasValue && freshCount.Value < _confirmedUnreadCount)
                        {
                            _confirmedUnreadCount = freshCount.Value;
                            if (_confirmedUnreadCount == 0)
                            {
                                StopFlashingUI();
                                _lastNotifiedSpecificContent = "";
                                _lastSeenSpecificContent = "";
                                _internalMessageCounter = 0;
                            }
                        }
                    };
                    _debounceTimer.Start();
                }
            }
        }
        else if (currentCount.HasValue && currentCount.Value == 0)
        {
            if (_confirmedUnreadCount > 0)
            {
                if (_debounceTimer == null)
                {
                    _debounceTimer = new System.Windows.Threading.DispatcherTimer();
                    _debounceTimer.Interval = TimeSpan.FromSeconds(2);
                    _debounceTimer.Tick += (s, args) => 
                    {
                        var timer = _debounceTimer;
                        if (timer != null) { timer.Stop(); _debounceTimer = null; }
                        var freshTitle = WebView.CoreWebView2.DocumentTitle;
                        var freshCount = GetUnreadCount(freshTitle);
                        if (freshCount.HasValue && freshCount.Value == 0)
                        {
                            _confirmedUnreadCount = 0;
                            StopFlashingUI();
                            _lastNotifiedSpecificContent = "";
                            _lastSeenSpecificContent = "";
                            _internalMessageCounter = 0;
                        }
                    };
                    _debounceTimer.Start();
                }
            }
        }
    }

    private string GetCleanContent(string title)
    {
        return System.Text.RegularExpressions.Regex.Replace(title, @"^\(\d+\)\s*", "").Trim();
    }

    private bool IsSpecific(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        return content.Contains(":") || content.Contains("wysłał") || content.Contains("sent a") || content.Contains("przesyła");
    }

    private void StopFlashingUI()
    {
        Dispatcher.Invoke(() => 
        {
            _trayIconService.StopFlashing();
            StopTaskbarFlash();
        });
    }

    private void TriggerSmartNotification(string cleanContent, bool isSpecific)
    {
        string notifyTitle = "Nowa wiadomość";
        string notifyBody = "Masz nieprzeczytane wiadomości.";

        if (isSpecific)
        {
            var parts = cleanContent.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                notifyTitle = parts[0].Trim();
                notifyBody = parts[1].Trim();
                _lastKnownSender = notifyTitle;
            }
            else
            {
                notifyTitle = "FB-Messenger";
                notifyBody = cleanContent;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(_lastKnownSender))
            {
                notifyTitle = _lastKnownSender;
                notifyBody = "Nowa wiadomość";
            }
        }

        Dispatcher.Invoke(() => 
        {
            int count = _internalMessageCounter > 0 ? _internalMessageCounter : Math.Max(_realUnreadCount, _confirmedUnreadCount);
            _trayIconService.ShowBalloonTip(notifyTitle, notifyBody, count > 0 ? count : 1);
            
            if (Visibility == Visibility.Hidden || Visibility == Visibility.Collapsed)
            {
                Show();
                WindowState = WindowState.Minimized;
            }
            FlashTaskbar();
        });
    }

    private int? GetUnreadCount(string title)
    {
        var match = System.Text.RegularExpressions.Regex.Match(title, @"^\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int count)) return count;
        if (IsSpecific(title)) return null;
        return 0;
    }

    private void TriggerNotificationFromTitle(string title) { }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var url = e.Uri;
        bool isAllowed = AllowedDomains.Any(domain => url.Contains(domain));
        if (!isAllowed)
        {
            e.Handled = true;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else
        {
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
        if (!e.IsSuccess) ErrorOverlay.Visibility = Visibility.Visible;
        else ErrorOverlay.Visibility = Visibility.Collapsed;
    }

    private void CoreWebView2_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
        {
            var uri = e.Uri;
            if (AllowedDomains.Any(domain => uri.Contains(domain))) e.State = CoreWebView2PermissionState.Allow;
        }
        else if (e.PermissionKind == CoreWebView2PermissionKind.Microphone || e.PermissionKind == CoreWebView2PermissionKind.Camera)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }

    private void CoreWebView2_NotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        e.Handled = true;
        ShowNotification(e.Notification.Title, e.Notification.Body);
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var messageType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            if (messageType == "notification")
            {
                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "FB-Messenger";
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";
                ShowNotification(title ?? "FB-Messenger", body ?? "");
            }
            else if (messageType == "audio_notification")
            {
                if ((DateTime.Now - _lastAudioNotification).TotalMilliseconds < 200) return;
                _lastAudioNotification = DateTime.Now;
                Dispatcher.Invoke(() =>
                {
                    _internalMessageCounter++;
                    _trayIconService.FlashIcon(_internalMessageCounter);
                    FlashTaskbar();
                    string title = !string.IsNullOrEmpty(_lastKnownSender) ? _lastKnownSender : "FB-Messenger";
                    string body = !string.IsNullOrEmpty(_lastNotifiedSpecificContent) ? _lastNotifiedSpecificContent : "Nowa wiadomość";
                    _trayIconService.ShowBalloonTip(title, body, _internalMessageCounter);
                });
            }
            else if (messageType == "aria_count_update")
            {
                int ariaCount = root.TryGetProperty("count", out var countProp) ? countProp.GetInt32() : 0;
                string? strategy = root.TryGetProperty("strategy", out var straProp) ? straProp.GetString() : "unknown";

                // If user is actively reading (focused), suppress counts
                if (IsActive && WindowState != WindowState.Minimized)
                {
                   _internalMessageCounter = 0;
                   return;
                }

                if (ariaCount != _internalMessageCounter)
                {
                    bool increased = ariaCount > _internalMessageCounter;
                    _internalMessageCounter = ariaCount;
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (increased)
                        {
                            _trayIconService.FlashIcon(_internalMessageCounter);
                            FlashTaskbar();
                            
                            string title = !string.IsNullOrEmpty(_lastKnownSender) ? _lastKnownSender : "FB-Messenger";
                            string body = $"{_internalMessageCounter} nieprzeczytanych wiadomości";
                            
                            _trayIconService.ShowBalloonTip(title, body, _internalMessageCounter);
                        }
                        else if (ariaCount == 0)
                        {
                            _trayIconService.UpdateBadge(0);
                        }
                        else
                        {
                            _trayIconService.UpdateBadge(_internalMessageCounter);
                        }
                    });
                }
            }
            else if (messageType == "title_content_update")
            {
                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "FB-Messenger";
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";
                _lastKnownSender = title;
                _lastNotifiedSpecificContent = body;
                if (_internalMessageCounter == 0) _internalMessageCounter = 1;
                Dispatcher.Invoke(() => { _trayIconService.ShowBalloonTip(title, body, _internalMessageCounter); });
            }
            else if (messageType == "all_read")
            {
                if (_internalMessageCounter != 0)
                {
                    _internalMessageCounter = 0;
                    _realUnreadCount = 0;
                    _confirmedUnreadCount = 0;
                    _lastNotifiedSpecificContent = "";
                    _lastSeenSpecificContent = "";
                    Dispatcher.Invoke(() => StopFlashingUI());
                }
            }
        }
        catch { }
    }

    private void ShowNotification(string title, string body)
    {
        if (string.IsNullOrEmpty(title)) title = "FB-Messenger";
        Dispatcher.Invoke(() => 
        {
            int count = _internalMessageCounter > 0 ? _internalMessageCounter : Math.Max(_realUnreadCount, _confirmedUnreadCount);
            _trayIconService.ShowBalloonTip(title, body, count > 0 ? count : 1);
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
    
    private void FlashTaskbar()
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        FlashWindow.Flash(helper.Handle);
    }

    private void StopTaskbarFlash()
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        FlashWindow.StopFlash(helper.Handle);
    }

    // Menu Handlers
    private void Exit_Click(object sender, RoutedEventArgs e) { QuitApplication(); }
    private void RetryConnection_Click(object sender, RoutedEventArgs e) { ErrorOverlay.Visibility = Visibility.Collapsed; Refresh(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) { Refresh(); }
    private void DevTools_Click(object sender, RoutedEventArgs e) { ToggleDevTools(); }
    private void About_Click(object sender, RoutedEventArgs e) 
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        MessageBox.Show($"FB-Messenger\n\nWersja: {version}\nCreated by JaRoD-CENTER", "O programie", MessageBoxButton.OK, MessageBoxImage.Information); 
    }
    private void Stats_Click(object sender, RoutedEventArgs e)
    {
        var stats = "Statystyki niedostępne"; 
        MessageBox.Show(stats, "Statystyki", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void CheckUpdates_Click(object sender, RoutedEventArgs e) { CheckForUpdates(false); }
    private void Feedback_Click(object sender, RoutedEventArgs e) 
    { 
        Process.Start(new ProcessStartInfo("https://github.com/Sebastian-Martis/AG-FB-Messenger/issues") { UseShellExecute = true }); 
    }

    private void Refresh() { if (WebView.CoreWebView2 != null) WebView.CoreWebView2.Reload(); }
    private void ToggleDevTools() { if (WebView.CoreWebView2 != null) WebView.CoreWebView2.OpenDevToolsWindow(); }
    
    public void ShowWindow()
    {
        _trayIconService.StopFlashing();
        StopTaskbarFlash();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Focus();
        _windowActivatedTime = DateTime.Now;
        _lastSeenSpecificContent = _lastNotifiedSpecificContent;
    }

    public void HideWindow() { Hide(); }
    public void QuitApplication() 
    { 
        _isQuitting = true; 
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

    private void Window_Loaded(object sender, RoutedEventArgs e) { }
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isQuitting)
        {
            e.Cancel = true;
            Hide();
            if (!_wasMinimizedNotificationShown)
            {
                _trayIconService.ShowBalloonTip("FB-Messenger", "Aplikacja działa w tle. Kliknij ikonę w zasobniku aby otworzyć.");
                _wasMinimizedNotificationShown = true;
            }
        }
        else
        {
            _windowStateManager.SaveWindowState(this);
            _trayIconService.Dispose();
        }
    }
    private void Window_StateChanged(object sender, EventArgs e) { _windowStateManager.SaveWindowState(this); }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) { _windowStateManager.SaveWindowStateDebounced(this); }
    private void Window_LocationChanged(object sender, EventArgs e) { _windowStateManager.SaveWindowStateDebounced(this); }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
}

public static class FlashWindow
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
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

    private const uint FLASHW_STOP = 0;
    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    public static void Flash(IntPtr hwnd)
    {
        FLASHWINFO fInfo = new FLASHWINFO();
        fInfo.cbSize = Convert.ToUInt32(System.Runtime.InteropServices.Marshal.SizeOf(fInfo));
        fInfo.hwnd = hwnd;
        fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
        fInfo.uCount = uint.MaxValue;
        fInfo.dwTimeout = 0;
        FlashWindowEx(ref fInfo);
    }

    public static void StopFlash(IntPtr hwnd)
    {
        FLASHWINFO fInfo = new FLASHWINFO();
        fInfo.cbSize = Convert.ToUInt32(System.Runtime.InteropServices.Marshal.SizeOf(fInfo));
        fInfo.hwnd = hwnd;
        fInfo.dwFlags = FLASHW_STOP;
        fInfo.uCount = 0;
        fInfo.dwTimeout = 0;
        FlashWindowEx(ref fInfo);
    }
}
