using System.Windows;
using System.Windows.Threading;
using AGMessenger.Services;

namespace AGMessenger;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly int _splashDuration;

    public SplashWindow()
    {
        InitializeComponent();

        // Dynamic splash duration: returning users (>1 launch) = 2s, new = 6s
        var usageTracker = UsageTracker.Instance;
        _splashDuration = usageTracker.LaunchCount > 1 ? 2000 : 6000;

        // Track this launch
        usageTracker.TrackLaunch();

        // Setup timer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_splashDuration)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();
        OpenMainWindow();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }
}
