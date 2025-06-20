// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
// Using optimized native Windows Core Audio APIs
using Wpf.Ui;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Text.Json;
using System.Text;
using System.Windows.Media.Animation;
using System.Timers;
using System.Collections.Concurrent;
using CommunityToolkit.WinUI.Notifications;
using ToxMoxMicFreezer.App.Services;
using ToxMoxMicFreezer.App.Models;
using ToxMoxMicFreezer.App.Converters;

namespace ToxMoxMicFreezer.App;

// Enumerations moved to Services namespace
using LogLevel = ToxMoxMicFreezer.App.Services.LogLevel;
using DeviceLoadingState = ToxMoxMicFreezer.App.Services.DeviceLoadingState;


/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IDisposable, INotifyPropertyChanged
{
    public ObservableCollection<AudioDeviceViewModel> Devices { get; set; } = new();
    
    // Separate collections for tabbed view
    public ObservableCollection<AudioDeviceViewModel> RecordingDevicesLeft { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> RecordingDevicesRight { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> PlaybackDevices { get; set; } = new(); // Main sorted collection for playback devices
    public ObservableCollection<AudioDeviceViewModel> PlaybackDevicesLeft { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> PlaybackDevicesRight { get; set; } = new();
    
    // Favorite devices collections
    public ObservableCollection<AudioDeviceViewModel> FavoriteDevicesLeft { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> FavoriteDevicesRight { get; set; } = new();
    internal CancellationTokenSource? _cts;
    internal bool _isExit;
    private const string AppName = "ToxMox's Mic Freezer+";
    internal bool _devicesLoaded = false; // Legacy field for compatibility
    internal bool _isLoadingDevices = false; // Legacy field for compatibility
    private readonly object _deviceLock = new object();
    private DateTime _lastDeviceRefresh = DateTime.MinValue;
    internal readonly HashSet<string> _persistentSelectionState = new HashSet<string>(); // Legacy field
    private readonly SemaphoreSlim _deviceUpdateSemaphore = new SemaphoreSlim(1, 1);
    private bool _disposed = false;
    public PauseManager PauseManager { get; } = new();
    private System.Timers.Timer? _pauseUIUpdateTimer;
    private System.Timers.Timer? _notificationMuteUIUpdateTimer;
    private bool _isInitialized = false;
    
    // Device management
    internal DeviceManagement.DeviceManager? _deviceManager;
    private IDisposable? _deviceNotificationHandler; // Can be DeviceNotificationHandler or SimpleDeviceNotificationHandler
    
    // Notification management
    internal Notifications.NotificationManager? _notificationManager;
    
    // Volume monitoring
    internal VolumeMonitoring.VolumeChangeManager? _volumeChangeManager;
    
    // Settings management
    internal Settings.SettingsManager? _settingsManager;
    internal Settings.FrozenVolumeManager? _frozenVolumeManager;
    
    // UI management
    internal UserInterface.WindowManager? _windowManager;
    internal UserInterface.VolumeBarInteractionHandler? _volumeBarHandler;
    internal UserInterface.SettingsToggleManager? _settingsToggleManager;
    internal UserInterface.EventHandlerCoordinator? _eventHandlerCoordinator;
    internal UserInterface.TabManager? _tabManager;
    
    // Orchestrator service for centralized initialization
    internal IMainWindowOrchestratorService? _mainWindowOrchestrator;
    
    // Service accessors for other classes (delegate to orchestrator)
    internal IDeviceStateManager? DeviceStateManager => _mainWindowOrchestrator?.DeviceStateManager;
    internal IVolumeEnforcementService? VolumeEnforcementService => _mainWindowOrchestrator?.VolumeEnforcementService;
    internal IDeviceHelperService? DeviceHelperService => _mainWindowOrchestrator?.DeviceHelperService;
    internal IUIEventRouter? _uiEventRouter => _mainWindowOrchestrator?.UIEventRouter;
    
    // Mute service for device mute/unmute operations
    internal IMuteService? MuteService { get; private set; }
    
    // Service accessor for drag & drop
    public T? GetService<T>() where T : class
    {
        if (typeof(T) == typeof(IFavoritesDragDropService))
            return _mainWindowOrchestrator?.FavoritesDragDropService as T;
        
        return null;
    }
    
    // Registry optimization
    internal Settings.DebouncedRegistryManager? _debouncedRegistryManager;
    
    // Log management moved to LoggingService
    

    public MainWindow()
    {
        // Set DataContext before loading UI
        DataContext = this;
        
        // Initialize the UI components from XAML
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing UI: {ex.Message}", "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        // Initialize orchestrator service for centralized initialization
        _mainWindowOrchestrator = new MainWindowOrchestratorService(this);
        
        // Initialize all components through the orchestrator
        _mainWindowOrchestrator.InitializeCoreServices();
        _mainWindowOrchestrator.InitializeDeviceManagement();
        _mainWindowOrchestrator.InitializeVolumeMonitoring();
        _mainWindowOrchestrator.InitializeSettingsManagement();
        _mainWindowOrchestrator.InitializeUIManagement();
        _mainWindowOrchestrator.InitializeEventCoordination();
        _mainWindowOrchestrator.SetupWindowIcons();
        _mainWindowOrchestrator.SetupDeviceSelectionHandling();
        
        // Restore the last selected tab immediately after initialization
        RestoreLastSelectedTab();
        
        // Initialize MuteService for device mute/unmute operations
        InitializeMuteService();
        
        // Show the window immediately before loading audio devices
        Loaded += (s, e) => InitializeAfterWindowLoaded();
        
        // Subscribe to SourceInitialized to hook into window messages for proper maximize behavior
        this.SourceInitialized += MainWindow_SourceInitialized;
        
        _mainWindowOrchestrator.LoggingService.Log("MainWindow initialized through orchestrator service.");
    }

    /// <summary>
    /// Restores the last selected tab from registry immediately after initialization
    /// </summary>
    private void RestoreLastSelectedTab()
    {
        try
        {
            int lastSelectedTab = _mainWindowOrchestrator?.RegistryService?.GetLastSelectedTab() ?? 0;
            
            if (lastSelectedTab >= 0 && lastSelectedTab < DeviceTabView.Items.Count)
            {
                DeviceTabView.SelectedIndex = lastSelectedTab;
                _mainWindowOrchestrator?.LoggingService?.Log($"Restored last selected tab: {lastSelectedTab switch { 0 => "Recording", 1 => "Playback", 2 => "Favorites", _ => "Unknown" }}");
            }
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error restoring last selected tab: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize the MuteService for device mute/unmute operations
    /// </summary>
    private void InitializeMuteService()
    {
        try
        {
            if (_mainWindowOrchestrator?.LoggingService != null)
            {
                MuteService = new MuteService(this, _mainWindowOrchestrator.LoggingService);
                _mainWindowOrchestrator.LoggingService.Log("MuteService initialized successfully.");
            }
            else
            {
                _mainWindowOrchestrator?.LoggingService?.Log("Failed to initialize MuteService - logging service not available.");
            }
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error initializing MuteService: {ex.Message}");
        }
    }

    /// <summary>
    /// Hook into the window's message loop to handle WM_GETMINMAXINFO for proper maximize behavior
    /// </summary>
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        if (source != null)
        {
            // Add the hook that will intercept Windows messages
            source.AddHook(HwndSourceHook);
            _mainWindowOrchestrator?.LoggingService?.Log("Window message hook installed for proper maximize behavior.");
        }
    }

    /// <summary>
    /// The message hook for the window. This is where we watch for messages.
    /// </summary>
    private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // The constant for the message we want to handle (sent when window size/position is changing)
        const int WM_GETMINMAXINFO = 0x0024;

        if (msg == WM_GETMINMAXINFO)
        {
            // We've received the message, so we handle it and stop others from handling it.
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// This method calculates and sets the proper maximum size and position for the window,
    /// ensuring it respects the taskbar's work area.
    /// </summary>
    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        // Marshal the lParam into the MINMAXINFO structure, which holds window dimension data
        var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

        // Get the handle to the monitor the window is currently on
        const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            // Get the monitor's information, which includes the work area
            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(monitor, ref monitorInfo);

            // Get the work area and monitor area rectangles
            var rcWorkArea = monitorInfo.rcWork;
            var rcMonitorArea = monitorInfo.rcMonitor;

            // Set the maximized position and size of the window to the monitor's work area
            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
        }

        // Marshal the modified structure back to the lParam pointer to apply the changes
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    // ProcessDeviceSelectionChange moved to DeviceStateManager

    // Legacy properties for compatibility - delegate to DeviceStateManager through orchestrator
    public DeviceLoadingState LoadingState => _mainWindowOrchestrator?.DeviceStateManager?.CurrentState ?? DeviceLoadingState.NotStarted;
    public bool IsOperationAllowed(DeviceLoadingState requiredState) => _mainWindowOrchestrator?.DeviceStateManager?.IsOperationAllowed(requiredState) ?? false;
    internal bool TrySetLoadingState(DeviceLoadingState newState) => _mainWindowOrchestrator?.DeviceStateManager?.TrySetLoadingState(newState) ?? false;

    // RestoreFrozenDevicesToSavedLevels moved to DeviceStateManager

    private void InitializeAfterWindowLoaded()
    {
        if (_isInitialized) return; // Prevent double initialization
        
        try
        {
            // Check if we're recovering from a process restart
            RestoreWindowStateFromRestart();
            
            // Add handlers for window size/position changes
            SizeChanged += Window_SizeChanged;
            LocationChanged += Window_LocationChanged;

            // Load saved window position and size
            _mainWindowOrchestrator?.WindowManager?.LoadWindowSettings();

            // Initialize pause manager
            PauseManager.LoadPauseState();
            PauseManager.PauseStateChanged += (isPaused) => {
                Dispatcher.BeginInvoke(() => {
                    UpdatePauseUI();
                    App.UpdateTrayIconForPauseState(isPaused);
                    
                    // When resuming from pause, immediately restore any devices that changed during pause
                    if (!isPaused)
                    {
                        _mainWindowOrchestrator?.DeviceStateManager?.RestoreFrozenDevicesToSavedLevels();
                    }
                });
            };
            UpdatePauseUI();
            UpdateNotificationMuteUI();
            App.UpdateTrayIconForPauseState(PauseManager.IsPaused);

            // Load saved theme early to prevent visual flash from default theme

            // Perform final initialization through orchestrator
            _mainWindowOrchestrator?.PerformFinalInitialization();
            
            // Initialize notification manager after orchestrator setup
            InitializeNotificationManager();
            
            _isInitialized = true;
            _mainWindowOrchestrator?.LoggingService?.Log("Application initialization completed.");
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error during initialization: {ex.Message}");
        }
    }

    public void ForceInitialization()
    {
        // This method is called when the app starts minimized to ensure volume monitoring works
        InitializeAfterWindowLoaded();
    }

    /// <summary>
    /// Initialize the notification manager and connect volume change event handlers
    /// </summary>
    private void InitializeNotificationManager()
    {
        try
        {
            // Create notification manager with logging service
            _notificationManager = new Notifications.NotificationManager(this, _mainWindowOrchestrator?.LoggingService);
            
            // Subscribe to mute state changes for UI updates
            _notificationManager.MuteStateChanged += (isMuted) => {
                Dispatcher.BeginInvoke(() => {
                    UpdateNotificationMuteUI();
                });
            };
            
            // Load saved mute state from registry
            _notificationManager.LoadMuteState();
            
            // Connect VolumeRestored event from VolumeChangeManager to logging and notifications
            var volumeChangeManager = _mainWindowOrchestrator?.VolumeChangeManager;
            if (volumeChangeManager != null)
            {
                volumeChangeManager.VolumeRestored += OnVolumeRestored;
                _mainWindowOrchestrator?.LoggingService?.Log("Connected VolumeRestored event to notification and logging system");
            }
            else
            {
                _mainWindowOrchestrator?.LoggingService?.Log("Warning: VolumeChangeManager not available for VolumeRestored event connection");
            }
            
            _mainWindowOrchestrator?.LoggingService?.Log("NotificationManager initialized successfully");
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error initializing NotificationManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles VolumeRestored event and triggers both logging and notifications
    /// </summary>
    private void OnVolumeRestored(string deviceId, string deviceName, float restoredVolume)
    {
        try
        {
            // Get the native device to determine previous volume for logging
            var nativeDevice = _deviceManager?.GetCachedNativeDevice(deviceId);
            float previousVolume = 0.0f;
            
            if (nativeDevice != null)
            {
                previousVolume = nativeDevice.GetVolumeLevel();
                
                // Use the sophisticated LogVolumeChange method with 2-second grouping
                if (nativeDevice.MMDevice != null)
                {
                    _mainWindowOrchestrator?.LoggingService?.LogVolumeChange(
                        nativeDevice.MMDevice, 
                        previousVolume, 
                        restoredVolume, 
                        "External");
                }
                else
                {
                    // Fallback to simple logging if MMDevice is not available
                    _mainWindowOrchestrator?.LoggingService?.Log($"{deviceName} changed externally (Set to {restoredVolume:F1}dB)");
                }
            }

            // Create VolumeChangeEvent for notification system
            var volumeEvent = new Services.VolumeChangeEvent
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                PreviousVolume = previousVolume,
                NewVolume = restoredVolume,
                IsDeviceFrozen = true, // Only frozen devices trigger this
                IsExternalChange = true, // This is an external change that was restored
                Timestamp = DateTime.Now
            };

            // Trigger notification system
            _notificationManager?.OnExternalVolumeChange(volumeEvent);
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error handling VolumeRestored event: {ex.Message}");
        }
    }
    
    




    private void RebalanceTabDeviceGrids()
    {
        _mainWindowOrchestrator?.TabManager?.RebalanceTabDeviceGrids();
    }

    private IEnumerable<AudioDeviceViewModel> GetFilteredDevices(IEnumerable<AudioDeviceViewModel> sourceDevices)
    {
        return _mainWindowOrchestrator?.TabManager?.GetFilteredDevices(sourceDevices) ?? Enumerable.Empty<AudioDeviceViewModel>();
    }

    private void DistributeDevices(IEnumerable<AudioDeviceViewModel> sourceDevices, 
                                 ObservableCollection<AudioDeviceViewModel> leftCollection, 
                                 ObservableCollection<AudioDeviceViewModel> rightCollection)
    {
        _mainWindowOrchestrator?.TabManager?.DistributeDevices(sourceDevices, leftCollection, rightCollection);
    }
    
    private void UpdateTabHeaders()
    {
        _mainWindowOrchestrator?.TabManager?.UpdateTabHeaders();
    }
    
    
    
    
    
    
    
    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Cancel volume monitoring
                _cts?.Cancel();
                
                _cts?.Dispose();
                _cts = null;
                
                
                // Stop and dispose pause UI update timer
                _pauseUIUpdateTimer?.Stop();
                _pauseUIUpdateTimer?.Dispose();
                _pauseUIUpdateTimer = null;
                
                // Stop and dispose notification mute UI update timer
                _notificationMuteUIUpdateTimer?.Stop();
                _notificationMuteUIUpdateTimer?.Dispose();
                _notificationMuteUIUpdateTimer = null;
                
                // Selection change timer removed - immediate processing used
                
                // ProcessVolumeDetector no longer used
                
                // Dispose notification manager
                _notificationManager?.Dispose();
                _notificationManager = null;
                
                // Dispose semaphore
                _deviceUpdateSemaphore?.Dispose();
                
                // Dispose device notification handler
                _deviceNotificationHandler?.Dispose();
                _deviceNotificationHandler = null;
                
                // Dispose volume change manager
                _mainWindowOrchestrator?.VolumeChangeManager?.Dispose();
                
                
                // Clean up device caches via DeviceManager
                _deviceManager?.Dispose();
                
                // Save any pending registry updates and dispose registry manager
                _mainWindowOrchestrator?.DebouncedRegistryManager?.SaveImmediately();
                _mainWindowOrchestrator?.DebouncedRegistryManager?.Dispose();
                
                // Log scroll state handled by LoggingService
                
                _mainWindowOrchestrator?.LoggingService?.Log("Resources disposed successfully");
                
                // Dispose logging service if it's disposable
                if (_mainWindowOrchestrator?.LoggingService is IDisposable loggingService)
                {
                    loggingService.Dispose();
                }
                _mainWindowOrchestrator = null;
            }
            catch (Exception ex)
            {
                // Log disposal errors but don't throw
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnTitleBarMouseLeftButtonDown(sender, e);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnMinimizeButtonClick(sender, e);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnMaximizeButtonClick(sender, e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnCloseButtonClick(sender, e);
    }

    private void DebugRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnDebugRefreshButtonClick(sender, e);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPauseButtonClick(sender, e);
    }

    private void Pause5Min_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPause5MinClick(sender, e);
    }

    private void Pause15Min_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPause15MinClick(sender, e);
    }

    private void Pause30Min_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPause30MinClick(sender, e);
    }

    private void Pause1Hour_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPause1HourClick(sender, e);
    }

    private void PauseIndefinite_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPauseIndefiniteClick(sender, e);
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnResumeButtonClick(sender, e);
    }

    private void ExtendPause_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnExtendPauseClick(sender, e);
    }

    // Smart Auto-Scroll Event Handlers

    /// <summary>
    /// Handles scroll position changes in the log viewer
    /// </summary>
    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnLogScrollViewerScrollChanged(sender, e);
    }

    /// <summary>
    /// Resume button click handler
    /// </summary>
    private void LogResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnLogResumeButtonClick(sender, e);
    }

    /// <summary>
    /// Clear button click handler
    /// </summary>
    private void LogClearButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnLogClearButtonClick(sender, e);
    }

    // Auto-scroll methods moved to LoggingService

    public void UpdatePauseUI()
    {
        if (PauseStatusBanner == null || PauseStatusText == null || PauseIcon == null)
            return;

        if (PauseManager.IsPaused)
        {
            PauseStatusBanner.Visibility = Visibility.Visible;
            PauseIcon.Text = "▶️";
            
            if (PauseManager.PauseEndTime.HasValue)
            {
                var remaining = PauseManager.TimeRemaining;
                if (remaining.HasValue && remaining.Value.TotalSeconds > 0)
                {
                    var minutes = (int)remaining.Value.TotalMinutes;
                    var seconds = remaining.Value.Seconds;
                    PauseStatusTextBlock.Text = $"Volume monitoring paused ({minutes} min {seconds} sec left)";
                }
                else
                {
                    PauseStatusTextBlock.Text = "Volume monitoring paused (expired)";
                    PauseManager.CheckAndHandleExpiration();
                    return;
                }
            }
            else
            {
                PauseStatusTextBlock.Text = "Volume monitoring paused (indefinitely)";
            }

            // Start timer if not already running
            if (_pauseUIUpdateTimer == null)
            {
                _pauseUIUpdateTimer = new System.Timers.Timer(1000); // Update every second
                _pauseUIUpdateTimer.Elapsed += (s, e) => {
                    Dispatcher.BeginInvoke(() => {
                        if (PauseManager.IsPaused)
                        {
                            UpdatePauseUI();
                        }
                    });
                };
                _pauseUIUpdateTimer.Start();
            }
        }
        else
        {
            PauseStatusBanner.Visibility = Visibility.Collapsed;
            PauseIcon.Text = "⏸️";
            
            // Stop timer when not paused
            _pauseUIUpdateTimer?.Stop();
            _pauseUIUpdateTimer?.Dispose();
            _pauseUIUpdateTimer = null;
        }
    }

    public void UpdateNotificationMuteUI()
    {
        if (NotificationMuteStatusBanner == null || NotificationMuteStatusText == null)
            return;

        // Only show banner for notification manager mutes (not settings disabling)
        // Check notification manager mute state
        if (_notificationManager?.IsMuted == true)
        {
            NotificationMuteStatusBanner.Visibility = Visibility.Visible;
            
            if (_notificationManager.MuteEndTime.HasValue)
            {
                var remaining = _notificationManager.TimeRemaining;
                if (remaining.HasValue && remaining.Value.TotalSeconds > 0)
                {
                    var minutes = (int)remaining.Value.TotalMinutes;
                    var seconds = remaining.Value.Seconds;
                    NotificationMuteStatusTextBlock.Text = $"Popup notifications muted ({minutes} min {seconds} sec left)";
                }
                else
                {
                    NotificationMuteStatusTextBlock.Text = "Popup notifications muted (expired)";
                    _notificationManager.CheckAndHandleExpiration();
                    return;
                }
            }
            else
            {
                NotificationMuteStatusTextBlock.Text = "Popup notifications muted (indefinitely)";
            }

            // Start timer if not already running
            if (_notificationMuteUIUpdateTimer == null)
            {
                _notificationMuteUIUpdateTimer = new System.Timers.Timer(1000); // Update every second
                _notificationMuteUIUpdateTimer.Elapsed += (s, e) => {
                    Dispatcher.BeginInvoke(() => {
                        if (_notificationManager?.IsMuted == true)
                        {
                            UpdateNotificationMuteUI();
                        }
                    });
                };
                _notificationMuteUIUpdateTimer.Start();
            }
        }
        else
        {
            NotificationMuteStatusBanner.Visibility = Visibility.Collapsed;
            
            // Stop timer when not muted
            _notificationMuteUIUpdateTimer?.Stop();
            _notificationMuteUIUpdateTimer?.Dispose();
            _notificationMuteUIUpdateTimer = null;
        }
    }

    private void EnableNotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // First, enable notifications in settings if they're disabled
            if (!((App)System.Windows.Application.Current).GetNotificationsEnabledSetting())
            {
                App.SaveNotificationsEnabledSetting(true);
                
                // Update the UI toggle if it exists
                var toggle = FindName("PopupNotificationsToggle") as System.Windows.Controls.CheckBox;
                if (toggle != null)
                {
                    toggle.IsChecked = true;
                }
            }

            // Also unmute temporary muting
            _notificationManager?.UnmutePopups();

            // Update the banner
            UpdateNotificationMuteUI();

            _mainWindowOrchestrator?.LoggingService?.Log("Popup notifications enabled via banner button");
        }
        catch (Exception ex)
        {
            _mainWindowOrchestrator?.LoggingService?.Log($"Error enabling notifications: {ex.Message}");
        }
    }

    internal void OnInternalVolumeChange(VolumeChangeEvent volumeEvent)
    {
        _mainWindowOrchestrator?.VolumeEnforcementService?.ProcessInternalVolumeChange(volumeEvent);
    }

    internal void OnExternalVolumeChange(VolumeChangeEvent volumeEvent)
    {
        _mainWindowOrchestrator?.VolumeEnforcementService?.ProcessExternalVolumeChange(volumeEvent);
    }

    /// <summary>
    /// Checks if popup notifications are muted specifically from tray actions (for system tray menu)
    /// </summary>
    public bool ArePopupsMutedFromTray()
    {
        return _notificationManager?.ArePopupsMutedFromTray() ?? false;
    }

    /// <summary>
    /// Mutes popup notifications for the specified duration (for system tray menu)
    /// </summary>
    public void MutePopupsFor(TimeSpan duration, string reason)
    {
        _notificationManager?.MutePopupsFor(duration, reason);
    }

    /// <summary>
    /// Mutes popup notifications indefinitely (for system tray menu)
    /// </summary>
    public void MutePopupsIndefinitely()
    {
        _notificationManager?.MutePopupsIndefinitely();
    }

    /// <summary>
    /// Unmutes popup notifications (for system tray menu)
    /// </summary>
    public void UnmutePopups()
    {
        _notificationManager?.UnmutePopups();
    }



    /// <summary>
    /// Loads device selection (for DeviceManager)
    /// </summary>
    internal void LoadSelection()
    {
        _mainWindowOrchestrator?.SettingsManager?.LoadDebugMode();
        _mainWindowOrchestrator?.SettingsManager?.LoadLogPanelHeight();
        _mainWindowOrchestrator?.SettingsManager?.LoadSelection();
        _mainWindowOrchestrator?.FrozenVolumeManager?.LoadFrozenVolumes();
        
        // Update UI to show loaded devices and favorites
        _tabManager?.RebalanceTabDeviceGrids();
    }

    // RestoreSelectionsFromPersistentState moved to DeviceStateManager

    /// <summary>
    /// Saves device selection (for SettingsManager)
    /// </summary>
    internal void SaveSelection(bool suppressLog = false)
    {
        _mainWindowOrchestrator?.SettingsManager?.SaveSelection();
        _mainWindowOrchestrator?.FrozenVolumeManager?.SaveFrozenVolumes(suppressLog);
    }


    /// <summary>
    /// Initializes all toggles (for DeviceManager)
    /// </summary>
    internal void InitializeAllToggles()
    {
        _mainWindowOrchestrator?.SettingsManager?.InitializeAllToggles();
    }









    
    
    
    

    // Current log level setting
    internal Services.LogLevel _currentLogLevel = Services.LogLevel.Info;
    
    // Temporary AppendLog delegation method - will be removed when all classes are refactored
    internal void AppendLog(string message, Services.LogLevel level = Services.LogLevel.Info)
    {
        _mainWindowOrchestrator?.LoggingService?.Log(message, level);
    }
    
    // Legacy overload for compatibility during refactoring
    internal void AppendLog(string message)
    {
        _mainWindowOrchestrator?.LoggingService?.Log(message, Services.LogLevel.Info);
    }


    private void StartupToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnStartupToggleChecked(sender, e);
    }

    private void StartupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnStartupToggleUnchecked(sender, e);
    }

    private void MinimizeToTrayToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnMinimizeToTrayToggleChecked(sender, e);
    }

    private void MinimizeToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnMinimizeToTrayToggleUnchecked(sender, e);
    }

    private void StartMinimizedToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnStartMinimizedToggleChecked(sender, e);
    }

    private void StartMinimizedToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnStartMinimizedToggleUnchecked(sender, e);
    }

    private void HideFixedVolumeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnHideFixedVolumeToggleChecked(sender, e);
    }

    private void HideFixedVolumeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnHideFixedVolumeToggleUnchecked(sender, e);
    }

    private void PopupNotificationsToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPopupNotificationsToggleChecked(sender, e);
    }

    private void PopupNotificationsToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnPopupNotificationsToggleUnchecked(sender, e);
    }

    private void DebugLoggingToggle_Checked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnDebugLoggingToggleChecked(sender, e);
    }

    private void DebugLoggingToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnDebugLoggingToggleUnchecked(sender, e);
    }

    private void LogPanelSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnLogPanelSplitterDragCompleted(sender, e);
    }



    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnExitButtonClick(sender, e);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnWindowClosing(sender, e);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnWindowStateChanged(sender, e);
    }



    // Volume bar interaction is now handled by VolumeBarInteractionHandler


    private void VolumeBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeBarMouseLeftButtonDown(sender, e);
    }

    private void VolumeBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeBarMouseMove(sender, e);
    }

    private void VolumeBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeBarMouseLeftButtonUp(sender, e);
    }

    private void VolumeBar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeBarMouseRightButtonDown(sender, e);
    }



    // Volume text editing event handlers
    private void VolumeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeTextMouseLeftButtonDown(sender, e);
    }

    private void VolumeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeTextBoxKeyDown(sender, e);
    }

    private void VolumeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeTextBoxLostFocus(sender, e);
    }

    private void VolumeTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnVolumeTextBoxLoaded(sender, e);
    }



    



    private void SaveWindowSettings()
    {
        _mainWindowOrchestrator?.WindowManager?.SaveWindowSettings();
    }

    private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnWindowSizeChanged(sender!, e);
    }

    // CalculateAndSetMinimumHeight moved to WindowEventService

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnWindowLocationChanged(sender!, e);
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnScrollViewerScrollChanged(sender, e);
    }

    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnMainScrollViewerPreviewMouseWheel(sender, e);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnSettingsButtonClick(sender, e);
    }

    // Resize grip handling - delegate to WindowManager
    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnResizeGripDragDelta(sender, e);
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle the event to prevent it from propagating, but don't do any resizing here
        // Resizing is handled exclusively by the edge elements
        e.Handled = true;
    }

    private void ResizeSide_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mainWindowOrchestrator?.UIEventRouter?.OnResizeSidePreviewMouseLeftButtonDown(sender, e);
    }



    // IsDeviceAccessible moved to DeviceHelperService
    
    // TryGetDeviceVolume_OBSOLETE moved to DeviceHelperService
    
    // Removed TrySetDeviceVolume - replaced with native device SetVolumeLevel method
    
    // CalculateTargetVolume moved to VolumeEnforcementService

    // GetBaseDeviceId moved to NativeDeviceAccessService
    
    /// <summary>
    /// Handle tab selection change to notify audio metering service
    /// </summary>
    private void DeviceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TabControl tabControl && tabControl.SelectedItem is TabItem selectedTab)
        {
            // Extract the tab name from the TabItem's position since Header is a complex TextBlock
            string tabName = tabControl.SelectedIndex switch
            {
                0 => "Recording",
                1 => "Playback",
                2 => "Favorites",
                _ => "Recording"
            };
            
            // Log tab switch for diagnostics
            _mainWindowOrchestrator?.LoggingService?.Log($"[UI] Tab selection changed to: {tabName} (index: {tabControl.SelectedIndex})");
            
            // Call SetActiveTab immediately for reliable metering activation
            // This ensures the AudioMeteringService properly refreshes for the Favorites tab
            _mainWindowOrchestrator?.SetActiveTab(tabName);
            
            // Save the selected tab to registry for persistence
            _mainWindowOrchestrator?.RegistryService?.SetLastSelectedTab(tabControl.SelectedIndex);
        }
    }
    
    
    // =========================================================================
    // NUCLEAR RESTART WINDOW STATE RESTORATION
    // =========================================================================
    
    /// <summary>
    /// Restore window state after process restart
    /// </summary>
    private void RestoreWindowStateFromRestart()
    {
        try
        {
            // Check if we're recovering from a restart using direct registry access
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ToxMoxMicFreezer\ProcessRestart");
                if (key == null) return;
                
                var restartInProgress = key.GetValue("RestartInProgress")?.ToString();
                if (restartInProgress != "true") return;
                
                AppendLog("Restoring window state after process restart...");
                
                // Restore window position and size
                var left = Convert.ToDouble(key.GetValue("LastWindowLeft", Left));
                var top = Convert.ToDouble(key.GetValue("LastWindowTop", Top));
                var width = Convert.ToDouble(key.GetValue("LastWindowWidth", Width));
                var height = Convert.ToDouble(key.GetValue("LastWindowHeight", Height));
                var state = Convert.ToInt32(key.GetValue("LastWindowState", (int)WindowState));
                
                Left = left;
                Top = top;
                Width = width;
                Height = height;
                WindowState = (WindowState)state;
                
                AppendLog($"Window state restored: {WindowState} at ({left}, {top}) size {width}x{height}");
                
                // Clear restart flag
                using var writeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ToxMoxMicFreezer\ProcessRestart", true);
                writeKey?.SetValue("RestartInProgress", "false");
            
            AppendLog("Process restart recovery completed successfully");
        }
        catch (Exception ex)
        {
            AppendLog($"Error restoring window state from restart: {ex.Message}");
        }
    }
    
    // =========================================================================
    // SIMPLIFIED DEVICE HOT-PLUG SYSTEM
    // =========================================================================
    
    /// <summary>
    /// Shows the device reset overlay with spinning icon and message
    /// </summary>
    public void ShowDeviceResetOverlay(string reason)
    {
        Dispatcher.BeginInvoke(() => {
            DeviceResetOverlay.SetMessage("Device changes detected", $"Resetting device system for stability... ({reason})");
            DeviceResetOverlay.Visibility = Visibility.Visible;
            
            // Disable all user controls during reset
            MainBorder.IsHitTestVisible = false;
        });
    }
    
    /// <summary>
    /// Hides the device reset overlay and re-enables user interaction
    /// </summary>
    public void HideDeviceResetOverlay()
    {
        Dispatcher.BeginInvoke(() => {
            DeviceResetOverlay.Visibility = Visibility.Collapsed;
            
            // Re-enable user controls
            MainBorder.IsHitTestVisible = true;
        });
    }

    #region P/Invoke Definitions for Win32 API - Window Maximize Fix

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}


public class WindowSettings
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public WindowState WindowState { get; set; }
}










// VolumeChangeBuffer moved to LoggingService

