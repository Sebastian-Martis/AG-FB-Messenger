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
    private readonly UpdateService _updateService;
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
        _updateService = new UpdateService();

        // Restore window state
        _windowStateManager.RestoreWindowState(this);

        // Setup keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.R, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => Refresh()), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ToggleDevTools()), Key.F12, ModifierKeys.None));

        // Initialize WebView2
        InitializeWebViewAsync();

        // Check for updates checks
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
        // When user focuses the window, assume they are reading messages
        if (_internalMessageCounter > 0 || _realUnreadCount > 0)
        {
            _internalMessageCounter = 0;
            _realUnreadCount = 0; // Optimistic reset
            _trayIconService.StopFlashing();
        }
    }

    public void CheckForUpdates(bool silent = false)
    {
        // Fire and forget
        _ = _updateService.CheckForUpdatesAsync(silent);
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

            // Inject aggressive message detection script
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    let lastContent = '';
                    let msgCount = 0;
                    
                    // Audio Hook
                    const origAudioPlay = window.Audio.prototype.play;
                    window.Audio.prototype.play = function() {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'audio_notification' }));
                        return origAudioPlay.apply(this, arguments);
                    };

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
                    
                    // Polling
                    setInterval(function() {
                        const title = document.title || '';
                        const content = title.replace(/^\(\d+\)\s*/, '').trim();
                        
                        if (content && content !== lastContent && content !== 'Messenger') {
                            if (content.includes(':') || content.includes(' sent ') || content.includes(' wysłał')) {
                                lastContent = content;
                                // Audio handles count, this handles content
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
    private string _lastNotifiedSpecificContent = "";
    private DateTime _lastSpecificNotificationTime = DateTime.MinValue;
    private int _confirmedUnreadCount = 0;
    private int _realUnreadCount = 0; // True unread count from DOM monitoring
    private int _internalMessageCounter = 0; // Our own counter - increments on each new message
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;
    private string _lastKnownSender = ""; // Track sender for generic notifications
    private string _lastSeenSpecificContent = ""; // What user has "seen" when window is active
    private DateTime _windowActivatedTime = DateTime.MinValue;

    private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
    {
        var title = WebView.CoreWebView2.DocumentTitle;
        if (string.IsNullOrEmpty(title) || title == _lastTitle) return;
        _lastTitle = title;

        int? currentCount = GetUnreadCount(title);
        string currentContent = GetCleanContent(title);
        bool isSpecific = IsSpecific(currentContent);

        // DEBUG: Uncomment to trace title changes
        // System.Diagnostics.Debug.WriteLine($"[TITLE] '{title}' | Count={currentCount} | Specific={isSpecific} | Content='{currentContent}'");

        // Case A: Specific Content (e.g. "Bob: Hello", "Bob sent a photo")
        // This is the PRIMARY notification trigger - content-based, not count-based
        if (isSpecific)
        {
            // Extract sender name for later use
            var senderParts = currentContent.Split(new[] { ':' }, 2);
            if (senderParts.Length >= 1)
            {
                _lastKnownSender = senderParts[0].Trim();
            }

            // KEY FIX: Notify on ANY change in specific content, 
            // UNLESS it's the same content we already notified about OR user just saw it
            bool alreadyNotified = (currentContent == _lastNotifiedSpecificContent);
            bool userJustSawIt = IsActive && (currentContent == _lastSeenSpecificContent);
            
            // Also check if we recently activated window (give 1 second grace period)
            bool justActivated = IsActive && (DateTime.Now - _windowActivatedTime).TotalSeconds < 1;
            
            if (!alreadyNotified && !userJustSawIt && !justActivated)
            {
                _lastNotifiedSpecificContent = currentContent;
                _lastSpecificNotificationTime = DateTime.Now;
                
                // INCREMENT our internal counter - this is the real message count
                _internalMessageCounter++;
                
                TriggerSmartNotification(currentContent, true);
            }
            
            // Update what we're showing now
            if (IsActive)
            {
                _lastSeenSpecificContent = currentContent;
            }
            
            return;
        }

        // Case B: Generic Title with Count (e.g. "(2) Messenger", "(1) Alice")
        // Secondary trigger - only used when no specific content available
        if (currentCount.HasValue && currentCount.Value > 0)
        {
            if (currentCount.Value > _confirmedUnreadCount)
            {
                // New Message Detected via count increase
                
                // Cancel any pending "Read" debounce
                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                    _debounceTimer = null;
                }

                _confirmedUnreadCount = currentCount.Value;
                
                // Only show generic notification if we haven't shown a specific one recently
                if ((DateTime.Now - _lastSpecificNotificationTime).TotalSeconds > 2)
                {
                    TriggerSmartNotification(currentContent, false);
                }
            }
            else if (currentCount.Value < _confirmedUnreadCount)
            {
                // Potential Read (Count Decreased)
                // Start Debounce to verify
                if (_debounceTimer == null)
                {
                    _debounceTimer = new System.Windows.Threading.DispatcherTimer();
                    _debounceTimer.Interval = TimeSpan.FromSeconds(2);
                    _debounceTimer.Tick += (s, args) => 
                    {
                        var timer = _debounceTimer;
                        if (timer != null) 
                        {
                            timer.Stop(); 
                            _debounceTimer = null;
                        }

                        var freshTitle = WebView.CoreWebView2.DocumentTitle;
                        var freshCount = GetUnreadCount(freshTitle);

                        if (freshCount.HasValue && freshCount.Value < _confirmedUnreadCount)
                        {
                            _confirmedUnreadCount = freshCount.Value;
                            if (_confirmedUnreadCount == 0)
                            {
                                StopFlashingUI();
                                // Reset ALL tracking when all messages read
                                _lastNotifiedSpecificContent = "";
                                _lastSeenSpecificContent = "";
                                _internalMessageCounter = 0; // Reset our counter
                            }
                        }
                    };
                    _debounceTimer.Start();
                }
            }
        }
        else if (currentCount.HasValue && currentCount.Value == 0)
        {
            // Count is 0 - possibly read all messages
            if (_confirmedUnreadCount > 0)
            {
                // Start debounce for read confirmation
                if (_debounceTimer == null)
                {
                    _debounceTimer = new System.Windows.Threading.DispatcherTimer();
                    _debounceTimer.Interval = TimeSpan.FromSeconds(2);
                    _debounceTimer.Tick += (s, args) => 
                    {
                        var timer = _debounceTimer;
                        if (timer != null) 
                        {
                            timer.Stop(); 
                            _debounceTimer = null;
                        }

                        var freshTitle = WebView.CoreWebView2.DocumentTitle;
                        var freshCount = GetUnreadCount(freshTitle);

                        if (freshCount.HasValue && freshCount.Value == 0)
                        {
                            _confirmedUnreadCount = 0;
                            StopFlashingUI();
                            _lastNotifiedSpecificContent = "";
                            _lastSeenSpecificContent = "";
                            _internalMessageCounter = 0; // Reset our counter
                        }
                    };
                    _debounceTimer.Start();
                }
            }
        }
    }

    private string GetCleanContent(string title)
    {
        // Remove "(N) " prefix from string
        return System.Text.RegularExpressions.Regex.Replace(title, @"^\(\d+\)\s*", "").Trim();
    }

    private bool IsSpecific(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        // Must contain colon OR action verbs to be a "Sender Message"
        // Simple names like "Alice" are rejected (they are likely just the active chat).
        return content.Contains(":") 
            || content.Contains("wysłał") 
            || content.Contains("sent a") 
            || content.Contains("przesyła");
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
            // Try "Sender: Message"
            var parts = cleanContent.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                notifyTitle = parts[0].Trim();
                notifyBody = parts[1].Trim();
                _lastKnownSender = notifyTitle; // Remember sender
            }
            else
            {
                // Action verb format: "John sent a photo"
                // Use generic title, but specific body
                notifyTitle = "FB-Messenger";
                notifyBody = cleanContent;
            }
        }
        else
        {
            // Generic notification - use last known sender if available
            if (!string.IsNullOrEmpty(_lastKnownSender))
            {
                notifyTitle = _lastKnownSender;
                notifyBody = "Nowa wiadomość";
            }
        }

        Dispatcher.Invoke(() => 
        {
            _trayIconService.ShowBalloonTip(notifyTitle, notifyBody, _internalMessageCounter > 0 ? _internalMessageCounter : Math.Max(_realUnreadCount, _confirmedUnreadCount));
            
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
        // 1. Explicit count regex: "(N) ..." -> Return N
        var match = System.Text.RegularExpressions.Regex.Match(title, @"^\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
        {
            return count;
        }

        // 2. Specific Titles (Sender: Msg) -> Indeterminate (null)
        // We don't want to mess up the count based on these.
        if (IsSpecific(title)) return null;

        // 3. Simple Titles (Messenger, Alice) -> 0 Unread
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
            _trayIconService.ShowBalloonTip(cleanTitle, body, _internalMessageCounter > 0 ? _internalMessageCounter : Math.Max(_realUnreadCount, _confirmedUnreadCount));
            
            if (Visibility == Visibility.Hidden || Visibility == Visibility.Collapsed)
            {
                Show();
                WindowState = WindowState.Minimized;
            }
            
            FlashTaskbar();
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

            using var doc = System.Text.Json.JsonDocument.Parse(json);
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
                // Trusted "New Message" trigger based on sound
                // Using dispatcher to ensure thread safety for UI updates
                Dispatcher.Invoke(() =>
                {
                    _internalMessageCounter++;
                    _trayIconService.FlashIcon(_internalMessageCounter);
                    FlashTaskbar();
                    
                    // Show notification with last known sender or generic
                    // If we have recent specific content, us it; otherwise generic
                    string title = !string.IsNullOrEmpty(_lastKnownSender) ? _lastKnownSender : "FB-Messenger";
                    string body = !string.IsNullOrEmpty(_lastNotifiedSpecificContent) ? _lastNotifiedSpecificContent : "Nowa wiadomość";
                    
                    // Don't show balloon if we just showed one for this content (debounce handled in JS but good to double check)
                    // But for audio, we ALWAYS want to flash/count
                    _trayIconService.ShowBalloonTip(title, body, _internalMessageCounter);
                });
            }
            else if (messageType == "title_content_update")
            {
                // Just update the content text, don't increment counter (audio did that)
                // Unless audio failed (muted?), but we prioritize audio for counting
                var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "FB-Messenger";
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";
                
                _lastKnownSender = title;
                _lastNotifiedSpecificContent = body;
                
                // If we somehow missed the audio catch (e.g. muted), ensure we have at least 1 count
                if (_internalMessageCounter == 0) _internalMessageCounter = 1;
                
                Dispatcher.Invoke(() =>
                {
                    // Refresh the notification bubble with specific text
                    _trayIconService.ShowBalloonTip(title, body, _internalMessageCounter);
                });
            }
            else if (messageType == "all_read")
            {
                // All messages read - reset everything
                _internalMessageCounter = 0;
                _realUnreadCount = 0;
                _confirmedUnreadCount = 0;
                _lastNotifiedSpecificContent = "";
                _lastSeenSpecificContent = "";
                
                Dispatcher.Invoke(() => StopFlashingUI());
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
            int badgeCount = _internalMessageCounter > 0 ? _internalMessageCounter : Math.Max(_realUnreadCount, _confirmedUnreadCount);
            _trayIconService.ShowBalloonTip(title, body, badgeCount > 0 ? badgeCount : 1);
            
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

    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => CheckForUpdates(silent: false);

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
        
        // Track when window was activated to prevent notification spam on open
        _windowActivatedTime = DateTime.Now;
        _lastSeenSpecificContent = _lastNotifiedSpecificContent;
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
