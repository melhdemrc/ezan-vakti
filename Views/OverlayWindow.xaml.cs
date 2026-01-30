using System;

using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;


using System.Windows.Interop;
using EzanVakti.Models;
using EzanVakti.Services;

namespace EzanVakti.Views;

public partial class OverlayWindow : Window
{
    private System.Timers.Timer? _updateTimer;
    private NextPrayerInfo? _currentNextPrayer;
    private IntPtr _hwnd;
    
    // Prevent infinite loop when loading fails
    private bool _isLoading = false;
    private int _retryCount = 0;
    private DateTime _lastLoadAttempt = DateTime.MinValue;
    private const int MaxRetries = 3;
    private const int RetryBackoffSeconds = 60; // Wait 60s after max retries
    
    // Timers for animation handling and robustness
    private System.Timers.Timer? _stabilizationTimer;
    private const int StabilizationInterval = 500; // Check every 500ms after an event
    private int _stabilizationChecks = 0;

    // Optimization: Track last position to avoid redundant updates
    private int _lastX = -1;
    private int _lastY = -1;
    private int _lastWidth = -1;
    private int _lastHeight = -1;
    
    // Debounce mechanism to prevent flickering
    private DateTime _lastEmbedCall = DateTime.MinValue;
    private const int DebounceMs = 50; // Minimum ms between embed calls

    #region Win32 API
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

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

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetParent(IntPtr hWnd);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterShellHookWindow(IntPtr hWnd);

    private int _shellMsg;
    private const int HSHELL_WINDOWCREATED = 1;
    private const int HSHELL_WINDOWDESTROYED = 2;
    private const int HSHELL_TASKMAN = 6;
    private const int HSHELL_REDRAW = 9;
    private const int HSHELL_WINDOWACTIVATED = 4;
    private const int HSHELL_RUDEAPPACTIVATED = 32772;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    private void MinimizeMemory()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }
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

        // Register for shell messages to detect taskbar changes
        _shellMsg = RegisterWindowMessage("SHELLHOOK");
        RegisterShellHookWindow(_hwnd);
        HwndSource.FromHwnd(_hwnd).AddHook(HwndHook);

        // Also listen for general system changes (like resolution or taskbar settings)
        HwndSource.FromHwnd(_hwnd).AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
            if (msg == 0x001A || msg == 0x007E) // WM_SETTINGCHANGE or WM_DISPLAYCHANGE
            {
                EmbedInTaskbar();
            }
            return IntPtr.Zero;
        });

        // Embed into taskbar
        EmbedInTaskbar();

        // Initial load
        await LoadPrayerTimesAsync();

        // Countdown timer
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        _updateTimer.Start();

        // Monitor system events for faster response
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, args) => EmbedInTaskbar();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, args) => EmbedInTaskbar();

        // Stabilization timer for animations
        _stabilizationTimer = new System.Timers.Timer(StabilizationInterval);
        _stabilizationTimer.Elapsed += StabilizationTimer_Elapsed;
        _stabilizationTimer.AutoReset = true;

        _lastLocationKey = GetCurrentLocationKey();
        ConfigService.Instance.ConfigChanged += OnConfigChanged;
    }

    private void StabilizationTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Stop checking after a few attempts if stable
        _stabilizationChecks++;
        Dispatcher.BeginInvoke(() => EmbedInTaskbar());
        
        if (_stabilizationChecks >= 3) // Stop after 1.5 seconds
        {
            _stabilizationTimer?.Stop();
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _shellMsg || msg == 0x0047) // SHELLHOOK or WM_WINDOWPOSCHANGED
        {
            // Trigger immediate update
            EmbedInTaskbar();
            
            // Start stabilization sequence to catch layout changes during animations
            _stabilizationChecks = 0;
            _stabilizationTimer?.Start();
        }
        return IntPtr.Zero;
    }

    private void EmbedInTaskbar()
    {
        // Debounce: skip if called too recently
        var now = DateTime.Now;
        if ((now - _lastEmbedCall).TotalMilliseconds < DebounceMs)
            return;
        _lastEmbedCall = now;
        
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
            int taskbarWidth = taskbarRect.Right - taskbarRect.Left;

            // SMART EMBEDDING: Check if already parented correctly
            // This prevents the flicker caused by repeatedly calling SetParent/SetWindowLong
            // which was causing the "millisecond refresh" effect the user reported.
            IntPtr currentParent = GetParent(_hwnd);
            if (currentParent != taskbarHandle)
            {
                // Set as child window
                int style = GetWindowLong(_hwnd, GWL_STYLE);
                SetWindowLong(_hwnd, GWL_STYLE, style | WS_CHILD);

                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

                // Parent to taskbar
                SetParent(_hwnd, taskbarHandle);
            }

            // Position based on current taskbar state
            // IMPORTANT: Use synchronous Invoke with Render priority for immediate feedback
            Dispatcher.Invoke(() =>
            {
                // Force layout update to get accurate dimensions
                if (!IsLoaded) return;
                
                int panelWidth = (int)ActualWidth;
                if (panelWidth <= 0) panelWidth = 160;
                
                int height = (int)ActualHeight;
                if (height <= 0) height = 40;
                
                int x = CalculateXPosition(taskbarWidth, panelWidth, out bool shouldHide);
                
                if (shouldHide)
                {
                    if (this.Visibility != Visibility.Collapsed)
                        this.Visibility = Visibility.Collapsed;
                    return;
                }
                
                if (this.Visibility != Visibility.Visible)
                    this.Visibility = Visibility.Visible;
                    
                int y = (taskbarHeight - height) / 2;
                if (y < 0) y = 0;
                
                // Force update if needed
                if (x != _lastX || y != _lastY || panelWidth != _lastWidth || height != _lastHeight)
                {
                    SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
                    _lastX = x;
                    _lastY = y;
                    _lastWidth = panelWidth;
                    _lastHeight = height;
                }
            }, System.Windows.Threading.DispatcherPriority.Render); 
        }
        catch
        {
            PositionAsOverlay();
        }
    }

    private int CalculateXPosition(int taskbarWidth, int panelWidth, out bool shouldHide)
    {
        shouldHide = false;
        var preferredPosition = ConfigService.Instance.Config.PanelPosition;
        
        var (systemTrayLeft, _) = GetSystemTrayBounds();
        var (taskListLeft, taskListRight) = GetTaskListRange();
        
        // Define occupied zones
        int trayZoneStart = systemTrayLeft;
        int iconZoneStart = taskListLeft;
        int iconZoneEnd = taskListRight;

        // No Padding as requested - snap to edges of safe zones
        // User update: "en soldan hafif bi çok az padding olsun"
        // User update 2: "sağa yapışıkken de aynı şekilde hafif padding ver"
        int padding = 8; // Increased padding (8px) for better visibility
        
        int x = 0;
        bool found = false;

        // Define Segments (Safe Zones)
        int seg1Start = padding;
        int seg1End = iconZoneStart - padding;
        int seg1Width = seg1End - seg1Start;

        int seg2Start = iconZoneEnd + padding;
        int seg2End = trayZoneStart - padding;
        int seg2Width = seg2End - seg2Start;

        // Check Taskbar Alignment
        // 0 = Left, 1 = Center (Default in Win11)
        bool isLeftAligned = GetTaskbarAlignment() == 0;

        if (isLeftAligned)
        {
             // Rule: If Left Aligned -> Right Side Placement
             if (seg2Width >= panelWidth) 
             { 
                 x = seg2End - panelWidth; // Snap to Right Edge of Safe Zone 2
                 found = true; 
             }
        }
        else // Center Aligned
        {
             // Rule: If Center Aligned -> Left Side Placement
             if (seg1Width >= panelWidth)
             {
                 x = seg1Start; // Snap to Left Edge of Screen/SafeZone 1
                 found = true;
             }
        }

        // Buffer check (Don't overlap Start/Icons/Tray even if "found")
        if (!found || x < 0 || (x + panelWidth) > taskbarWidth)
        {
            shouldHide = true;
        }
        else
        {
            // Verify no overlap with icon zone (Strict check)
            if (Overlaps(x, panelWidth, iconZoneStart, iconZoneEnd))
                shouldHide = true;
            
            // Verify no overlap with tray zone (Strict check)
            if (Overlaps(x, panelWidth, trayZoneStart, taskbarWidth))
                shouldHide = true;
        }

        return x;
    }

    private int GetTaskbarAlignment()
    {
        try
        {
            // 0 = Left, 1 = Center
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            if (key != null)
            {
                var val = key.GetValue("TaskbarAl");
                if (val is int i) return i;
            }
        }
        catch { }
        return 1; // Default to Center if unknown (Win11 standard)
    }

    private bool Overlaps(int x1, int w1, int x2, int x2_end)
    {
        int end1 = x1 + w1;
        return x1 < x2_end && end1 > x2;
    }

    private (int left, int right) GetTaskListRange()
    {
        try
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return (0, 400);
            
            GetWindowRect(taskbar, out RECT tbRect);
            IntPtr taskList = FindTaskListWindow(taskbar);
            
            if (taskList != IntPtr.Zero && GetWindowRect(taskList, out RECT rect))
            {
                int left = rect.Left - tbRect.Left;
                int right = rect.Right - tbRect.Left;
                
                // If the task list seems too small (Windows 11 hidden icons bug), 
                // scan children for the actual rightmost boundary
                int maxRight = right;
                EnumChildWindows(taskList, (childHwnd, l) => {
                    if (GetWindowRect(childHwnd, out RECT cRect) && cRect.Right > 0)
                    {
                        int cRight = cRect.Right - tbRect.Left;
                        if (cRight > maxRight) maxRight = cRight;
                    }
                    return true;
                }, IntPtr.Zero);
                
                return (left, maxRight);
            }
        }
        catch { }
        return (0, 400); // More generous fallback
    }

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    private IntPtr FindTaskListWindow(IntPtr taskbar)
    {
        // Win10 chain
        IntPtr rebar = FindWindowEx(taskbar, IntPtr.Zero, "ReBarWindow32", null);
        if (rebar != IntPtr.Zero)
        {
            IntPtr taskSw = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
            if (taskSw != IntPtr.Zero)
            {
                IntPtr list = FindWindowEx(taskSw, IntPtr.Zero, "MSTaskListWClass", null);
                if (list != IntPtr.Zero) return list;
                return taskSw;
            }
        }
        
        // Win11 or direct
        IntPtr taskList = FindWindowEx(taskbar, IntPtr.Zero, "MSTaskListWClass", null);
        if (taskList != IntPtr.Zero) return taskList;
        
        return FindWindowEx(taskbar, IntPtr.Zero, "MSTaskSwWClass", null);
    }

    private (int left, int width) GetSystemTrayBounds()
    {
        try
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return (0, 250);
            
            IntPtr trayNotify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (trayNotify != IntPtr.Zero && GetWindowRect(trayNotify, out RECT trayRect))
            {
                GetWindowRect(taskbar, out RECT taskbarRect);
                return (trayRect.Left - taskbarRect.Left, trayRect.Right - trayRect.Left);
            }
        }
        catch { }
        return (0, 250);
    }

    private void PositionAsOverlay()
    {
        Topmost = true;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var workAreaBottom = SystemParameters.WorkArea.Bottom;
        var taskbarHeight = screenHeight - workAreaBottom;
        
        double panelWidth = ActualWidth > 0 ? ActualWidth : 150;
        int x = CalculateXPosition((int)screenWidth, (int)panelWidth, out bool shouldHide);
        
        if (shouldHide)
        {
            this.Visibility = Visibility.Collapsed;
        }
        else
        {
            this.Visibility = Visibility.Visible;
            Left = x;
            Top = workAreaBottom + (taskbarHeight - ActualHeight) / 2;
        }
    }

    private void OverlayWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _stabilizationTimer?.Stop();
        _stabilizationTimer?.Dispose();
        ConfigService.Instance.ConfigChanged -= OnConfigChanged;
    }

    private string _lastLocationKey = string.Empty;

    private string GetCurrentLocationKey()
    {
        var config = ConfigService.Instance.Config;
        if (config.Latitude.HasValue && config.Longitude.HasValue && config.LocationName == "Windows Konum")
            return $"gps_{config.Latitude:F4}_{config.Longitude:F4}";
        return config.City ?? string.Empty;
    }

    private async void OnConfigChanged()
    {
        // Only reload prayer times if location actually changed
        var currentKey = GetCurrentLocationKey();
        if (currentKey != _lastLocationKey)
        {
            _lastLocationKey = currentKey;
            PrayerService.Instance.ClearCache();
            await LoadPrayerTimesAsync();
        }
        
        // Reposition the panel when config changes (e.g. alignment change)
        Dispatcher.Invoke(() => EmbedInTaskbar());
        
        // Optimize memory after config reload
        MinimizeMemory();
    }
    
    private async Task LoadPrayerTimesAsync()
    {
        // Prevent re-entry
        if (_isLoading) return;
        _isLoading = true;
        _lastLoadAttempt = DateTime.Now;
        
        try
        {
            _currentNextPrayer = await PrayerService.Instance.GetNextPrayerAsync();
            
            if (_currentNextPrayer != null)
            {
                // Success - reset retry counter
                _retryCount = 0;
                Dispatcher.Invoke(UpdateUI);
            }
            else
            {
                // Failed to get prayer times
                _retryCount++;
                Dispatcher.Invoke(ShowErrorState);
            }
        }
        catch
        {
            _retryCount++;
            Dispatcher.Invoke(ShowErrorState);
        }
        finally
        {
            _isLoading = false;
            
            // Aggressive memory cleanup after large operations (XML parsing etc)
            // to keep RAM usage under 5MB as requested
            MinimizeMemory();
        }
    }
    
    private void ShowErrorState()
    {
        PrayerNameText.Text = "HATA";
        PrayerTimeText.Text = "--:--";
        var run = CountdownText.Inlines.FirstInline as Run;
        if (run != null) run.Text = "--:--";
    }

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Skip if already loading to prevent re-entry
        if (_isLoading) return;
        
        var now = DateTime.Now;
        
        // Handle case when prayer times failed to load
        if (_currentNextPrayer == null)
        {
            // Implement backoff to prevent infinite loop
            var secondsSinceLastAttempt = (now - _lastLoadAttempt).TotalSeconds;
            
            if (_retryCount >= MaxRetries)
            {
                // After max retries, wait longer before trying again
                if (secondsSinceLastAttempt < RetryBackoffSeconds)
                    return;
                // Reset retry count to try again
                _retryCount = 0;
            }
            
            // Exponential backoff: wait 2^retryCount seconds between attempts
            var backoffSeconds = Math.Pow(2, _retryCount);
            if (secondsSinceLastAttempt >= backoffSeconds)
            {
                _ = LoadPrayerTimesAsync();
            }
            return;
        }

        var remaining = _currentNextPrayer.PrayerTime - now;

        if (remaining <= TimeSpan.Zero)
        {
            // Prayer time passed, load next prayer
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
        
        // Recalculate position after content changes to avoid gap
        UpdateLayout();
        EmbedInTaskbar();
    }
    
    public void RefreshDisplay()
    {
        _ = LoadPrayerTimesAsync();
    }
}
