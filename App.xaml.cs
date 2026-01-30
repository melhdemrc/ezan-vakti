using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EzanVakti.Models;
using EzanVakti.Services;
using EzanVakti.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace EzanVakti;

public partial class App : Application
{
    private const string MutexName = "Global\\EzanVakti_SingleInstance_Mutex";
    private static Mutex? _instanceMutex;
    
    private TaskbarIcon? _trayIcon;
    private OverlayWindow? _overlayWindow;
    private DispatcherTimer? _memoryTimer;

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Single-instance check - must be first
        _instanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        
        if (!isNewInstance)
        {
            // Another instance is already running
            MessageBox.Show(
                "Ezan Vakti uygulamasi zaten calisiyor.\n\nLutfen sistem tepsisindeki simgeyi kontrol edin.",
                "Uygulama Zaten Calisiyor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }
        
        // 1. Disable GPU usage completely
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        
        base.OnStartup(e);

        // 2. Setup aggressive memory trimming (every 30 seconds)
        _memoryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _memoryTimer.Tick += (s, args) => FlushMemory();
        _memoryTimer.Start();

        // Load config
        await ConfigService.Instance.LoadAsync();
        
        // Auto-detect location on first run
        if (ConfigService.Instance.IsNewConfig)
        {
            await UseWindowsLocation(true);
        }

        // Create overlay
        _overlayWindow = new OverlayWindow();
        _overlayWindow.Show();

        // System tray with full menu
        InitializeTrayIcon();
        
        // Initial flush
        FlushMemory();
    }

    private void FlushMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
            }
        }
        catch { }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TaskbarIcon();
        
        try
        {
            // Try to load custom icon
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
            
            if (System.IO.File.Exists(iconPath))
            {
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                // Try embedded resource
                var iconUri = new Uri("pack://application:,,,/Assets/icon.ico");
                var streamInfo = Application.GetResourceStream(iconUri);
                
                if (streamInfo != null)
                {
                    _trayIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                }
                else
                {
                    // Fallback to system icon
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to system icon on any error
            System.Diagnostics.Debug.WriteLine($"Icon error: {ex.Message}");
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        
        _trayIcon.ToolTipText = "Ezan Vakti - Namaz Vakitleri";

        // Handle right-click to show menu manually
        _trayIcon.TrayRightMouseUp += OnTrayRightMouseUp;
    }

    private void OnTrayRightMouseUp(object sender, RoutedEventArgs e)
    {
        // Create and show menu at cursor position
        var menu = BuildTrayMenu();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
        menu.IsOpen = true;
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        
        // Rebuild menu when it opens to reflect current state
        menu.Opened += (s, e) => RefreshMenuItems(menu);
        
        var config = ConfigService.Instance.Config;
        
        // Check if using GPS
        bool usingGpsLocation = config.Latitude.HasValue && 
                                 config.Longitude.HasValue && 
                                 config.LocationName == "Windows Konum";

        // Current location display
        var locationHeader = new MenuItem 
        { 
            Header = $"📍 Konum: {ConfigService.Instance.GetLocationDisplay()}",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        menu.Items.Add(locationHeader);
        menu.Items.Add(new Separator());

        // Windows Location option
        var gpsItem = new MenuItem 
        { 
            Header = "🌍 Windows Konumunu Kullan",
            IsCheckable = true,
            IsChecked = usingGpsLocation
        };
        gpsItem.Click += async (s, e) => 
        {
            await UseWindowsLocation(false);
        };
        menu.Items.Add(gpsItem);
        menu.Items.Add(new Separator());

        // Cities submenu
        var citiesMenu = new MenuItem { Header = "🏙️ Şehir Seç" };
        
        var groupedCities = TurkishCities.GetAllCities()
            .GroupBy(c => c.Name[0])
            .OrderBy(g => g.Key);

        foreach (var group in groupedCities)
        {
            var groupItem = new MenuItem { Header = $"── {group.Key} ──", IsEnabled = false };
            citiesMenu.Items.Add(groupItem);

            foreach (var city in group)
            {
                var cityItem = new MenuItem
                {
                    Header = city.Name,
                    IsCheckable = true,
                    IsChecked = !usingGpsLocation && city.Name == config.City,
                    Tag = city
                };
                cityItem.Click += async (s, e) => 
                {
                    await SelectCity(city);
                };
                citiesMenu.Items.Add(cityItem);
            }
        }
        
        menu.Items.Add(citiesMenu);
        menu.Items.Add(new Separator());
        
        // Panel Position submenu
        var positionMenu = new MenuItem { Header = "Panel Konumu" };
        var currentPosition = config.PanelPosition;
        
        var leftItem = new MenuItem 
        { 
            Header = "Sol",
            IsCheckable = true,
            IsChecked = currentPosition == PanelPosition.Left
        };
        leftItem.Click += async (s, e) => await SetPanelPosition(PanelPosition.Left);
        positionMenu.Items.Add(leftItem);
        
        var centerItem = new MenuItem 
        { 
            Header = "Orta",
            IsCheckable = true,
            IsChecked = currentPosition == PanelPosition.Center
        };
        centerItem.Click += async (s, e) => await SetPanelPosition(PanelPosition.Center);
        positionMenu.Items.Add(centerItem);
        
        var rightItem = new MenuItem 
        { 
            Header = "Sag",
            IsCheckable = true,
            IsChecked = currentPosition == PanelPosition.Right
        };
        rightItem.Click += async (s, e) => await SetPanelPosition(PanelPosition.Right);
        positionMenu.Items.Add(rightItem);
        
        menu.Items.Add(positionMenu);
        menu.Items.Add(new Separator());
        
        // Exit
        var exitItem = new MenuItem { Header = "Cikis" };
        exitItem.Click += (s, e) => ExitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void RefreshMenuItems(ContextMenu menu)
    {
        var config = ConfigService.Instance.Config;
        bool usingGpsLocation = config.Latitude.HasValue && 
                                 config.Longitude.HasValue && 
                                 config.LocationName == "Windows Konum";

        // Update location header (first item)
        if (menu.Items[0] is MenuItem locationHeader)
        {
            locationHeader.Header = $"📍 Konum: {ConfigService.Instance.GetLocationDisplay()}";
        }

        // Update GPS checkbox (third item, after separator)
        if (menu.Items[2] is MenuItem gpsItem)
        {
            gpsItem.IsChecked = usingGpsLocation;
        }

        // Update city checkmarks (in submenu at index 4)
        if (menu.Items[4] is MenuItem citiesMenu)
        {
            foreach (var item in citiesMenu.Items)
            {
                if (item is MenuItem cityItem && cityItem.Tag is CityInfo city)
                {
                    cityItem.IsChecked = !usingGpsLocation && city.Name == config.City;
                }
            }
        }
    }

    private async Task UseWindowsLocation(bool silent = false)
    {
        try
        {
            // Try Windows location first, then IP fallback
            var location = await LocationService.GetLocationAsync();
            
            if (location != null && location.Status == LocationStatus.Success)
            {
                var sourceName = location.Source == "IP" ? "IP Konum" : "Windows Konum";
                
                ConfigService.Instance.UpdateCoordinates(
                    location.Latitude, 
                    location.Longitude, 
                    sourceName);
                    
                await ConfigService.Instance.SaveAsync();
                
                if (!silent)
                {
                    var sourceInfo = location.Source == "IP" 
                        ? "(IP adresi uzerinden - yaklasik konum)" 
                        : location.DisplayWithAccuracy;
                        
                    MessageBox.Show(
                        $"Konum alindi:\n\n{location.DisplayName}\n{sourceInfo}\n\nNamaz vakitleri bu konuma gore hesaplanacak.", 
                        "Konum Basarili", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                
                // Force overlay refresh
                _overlayWindow?.RefreshDisplay();
            }
            else
            {
                // All location methods failed - prompt for manual city selection
                if (!silent)
                {
                    var statusMessage = location?.Status switch
                    {
                        LocationStatus.AccessDenied => "Konum erisimi reddedildi.",
                        LocationStatus.Timeout => "Konum istegi zaman asimina ugradi.",
                        _ => "Konum alinamadi."
                    };
                    
                    MessageBox.Show(
                        $"{statusMessage}\n\n" +
                        "Lutfen sistem tepsisindeki simgeye sag tiklayarak\n" +
                        "manuel olarak sehir secin.", 
                        "Konum Bulunamadi", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Location error: {ex.Message}");
            if (!silent)
            {
                MessageBox.Show(
                    "Konum alinamadi.\n\nLutfen manuel sehir secin.", 
                    "Hata", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }

    private async Task SelectCity(CityInfo city)
    {
        ConfigService.Instance.UpdateCity(city.Name);
        await ConfigService.Instance.SaveAsync();
        
        // Force overlay refresh
        _overlayWindow?.RefreshDisplay();
    }

    private async Task SetPanelPosition(PanelPosition position)
    {
        ConfigService.Instance.Config.PanelPosition = position;
        await ConfigService.Instance.SaveAsync();
    }

    private void ExitApp()
    {
        _trayIcon?.Dispose();
        _overlayWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _memoryTimer?.Stop();
        _trayIcon?.Dispose();
        
        // Release the single-instance mutex
        if (_instanceMutex != null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        
        base.OnExit(e);
    }
}
