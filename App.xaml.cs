using System.Windows;
using System.Threading;
using System.Runtime.InteropServices;

namespace AGMessenger;

/// <summary>
/// FB-Messenger - Desktop Messenger Client
/// Created by JaRoD-CENTER
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "AGMessenger_SingleInstance";
    private const string EventName = "AGMessenger_ShowWindow";
    private static EventWaitHandle? _showWindowEvent;
    private Thread? _listenerThread;

    // Win32 API for bringing window to front
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure single instance (like Electron requestSingleInstanceLock)
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running - signal it to show window
            try
            {
                var existingEvent = EventWaitHandle.OpenExisting(EventName);
                existingEvent.Set(); // Signal the first instance to show
                existingEvent.Dispose();
            }
            catch
            {
                // Fallback: just inform user
            }
            
            Shutdown();
            return;
        }

        // Create event for receiving "show window" signals from other instances
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        
        // Start listener thread
        _listenerThread = new Thread(ListenForShowSignal)
        {
            IsBackground = true,
            Name = "ShowWindowListener"
        };
        _listenerThread.Start();

        base.OnStartup(e);
    }

    private void ListenForShowSignal()
    {
        while (_showWindowEvent != null)
        {
            try
            {
                if (_showWindowEvent.WaitOne(500)) // Check every 500ms
                {
                    // Signal received - show main window on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is MainWindow mainWin)
                        {
                            mainWin.ShowWindow();
                        }
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                break; // Event was disposed, exit thread
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
