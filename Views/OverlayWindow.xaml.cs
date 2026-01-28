using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using EzanVakti.Models;
using EzanVakti.Services;

namespace EzanVakti.Views;

public partial class OverlayWindow : Window
{
    private System.Timers.Timer? _updateTimer;
    private NextPrayerInfo? _currentNextPrayer;
    private IntPtr _hwnd;

    #region Win32 API
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    #endregion

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
        Closing += OverlayWindow_Closing;
    }

    private async void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Embed into taskbar at far left
        EmbedInTaskbar();

        // Initial load
        await LoadPrayerTimesAsync();

        // Countdown timer
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        _updateTimer.Start();

        ConfigService.Instance.ConfigChanged += OnConfigChanged;
    }

    private void EmbedInTaskbar()
    {
        try
        {
            // Find taskbar
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) 
            {
                PositionAsOverlay();
                return;
            }

            // Get taskbar dimensions
            GetWindowRect(taskbarHandle, out RECT taskbarRect);
            int taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;

            // Set as child window
            int style = GetWindowLong(_hwnd, GWL_STYLE);
            SetWindowLong(_hwnd, GWL_STYLE, style | WS_CHILD);

            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Parent to taskbar
            SetParent(_hwnd, taskbarHandle);

            // Position at FAR LEFT (x = 0), vertically centered in taskbar
            Dispatcher.BeginInvoke(() =>
            {
                Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), DesiredSize));
                UpdateLayout();
                
                int x = 0; // Far left
                int height = (int)ActualHeight;
                if (height == 0) height = 40; // Fallback
                
                int y = (taskbarHeight - height) / 2;
                if (y < 0) y = 0;
                
                SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch
        {
            PositionAsOverlay();
        }
    }

    private void PositionAsOverlay()
    {
        Topmost = true;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var workAreaBottom = SystemParameters.WorkArea.Bottom;
        var taskbarHeight = screenHeight - workAreaBottom;
        Left = 0;
        Top = workAreaBottom + (taskbarHeight - ActualHeight) / 2;
    }

    private void OverlayWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        ConfigService.Instance.ConfigChanged -= OnConfigChanged;
    }

    private async void OnConfigChanged()
    {
        PrayerService.Instance.ClearCache();
        await LoadPrayerTimesAsync();
    }
    
    private async Task LoadPrayerTimesAsync()
    {
        try
        {
            _currentNextPrayer = await PrayerService.Instance.GetNextPrayerAsync();
            Dispatcher.Invoke(UpdateUI);
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                PrayerNameText.Text = "HATA";
                PrayerTimeText.Text = "--:--";
                var run = CountdownText.Inlines.FirstInline as Run;
                if (run != null) run.Text = "--:--";
            });
        }
    }

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_currentNextPrayer == null) return;

        var now = DateTime.Now;
        var remaining = _currentNextPrayer.PrayerTime - now;

        if (remaining <= TimeSpan.Zero)
        {
            _ = LoadPrayerTimesAsync();
        }
        else
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _currentNextPrayer.TimeRemaining = remaining;
                    var newText = FormatCountdown(remaining);
                    
                    var run = CountdownText.Inlines.FirstInline as Run;
                    if (run != null && run.Text != newText)
                    {
                        run.Text = newText;
                    }
                });
            }
            catch { /* Ignore if window is closing */ }
        }
    }

    private static string FormatCountdown(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void UpdateUI()
    {
        if (_currentNextPrayer == null) return;

        PrayerNameText.Text = _currentNextPrayer.PrayerName.ToUpperInvariant();
        PrayerTimeText.Text = _currentNextPrayer.PrayerTime.ToString("HH:mm");
        
        var run = CountdownText.Inlines.FirstInline as Run;
        if (run != null)
        {
            run.Text = FormatCountdown(_currentNextPrayer.TimeRemaining);
        }
    }
    
    public void RefreshDisplay()
    {
        _ = LoadPrayerTimesAsync();
    }
}
