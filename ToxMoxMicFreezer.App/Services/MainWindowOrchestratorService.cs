// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using ToxMoxMicFreezer.App.DeviceManagement;
using ToxMoxMicFreezer.App.Models;
using ToxMoxMicFreezer.App.Settings;
using ToxMoxMicFreezer.App.UserInterface;
using ToxMoxMicFreezer.App.VolumeMonitoring;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for orchestrating MainWindow initialization and service coordination
    /// Manages complex initialization sequences and dependency injection setup
    /// Follows the dependency injection pattern to reduce MainWindow complexity
    /// </summary>
    public class MainWindowOrchestratorService : IMainWindowOrchestratorService
    {
        private readonly MainWindow _mainWindow;
        private bool _isInitialized = false;

        // Core services
        private ILoggingService? _loggingService;
        private IRegistryService? _registryService;
        private IDeviceStateManager? _deviceStateManager;
        private IVolumeEnforcementService? _volumeEnforcementService;
        private IDeviceHelperService? _deviceHelperService;
        private ISettingsService? _settingsService;
        private IUIEventRouter? _uiEventRouter;
        private IFavoritesService? _favoritesService;
        private IFavoritesDragDropService? _favoritesDragDropService;

        // Managers
        private DeviceManager? _deviceManager;
        private IDisposable? _deviceNotificationHandler; // Can be DeviceNotificationHandler or SimpleDeviceNotificationHandler
        private VolumeChangeManager? _volumeChangeManager;
        private IAudioMeteringService? _audioMeteringService;
        private SettingsManager? _settingsManager;
        private FrozenVolumeManager? _frozenVolumeManager;
        private WindowManager? _windowManager;
        private VolumeBarInteractionHandler? _volumeBarHandler;
        private SettingsToggleManager? _settingsToggleManager;
        private EventHandlerCoordinator? _eventHandlerCoordinator;
        private TabManager? _tabManager;
        private DebouncedRegistryManager? _debouncedRegistryManager;
        
        // Audio meter UI update throttling
        private readonly Dictionary<string, float> _pendingPeakLevels = new Dictionary<string, float>();
        private readonly Dictionary<string, StereoPeakLevels> _pendingStereoPeakLevels = new Dictionary<string, StereoPeakLevels>();
        private readonly object _peakLevelLock = new object();
        private DispatcherTimer? _meterUpdateTimer;
        private const int METER_UPDATE_INTERVAL_MS = 33; // ~30 FPS updates regardless of monitor refresh rate

        // Property accessors
        public ILoggingService LoggingService => _loggingService ?? throw new InvalidOperationException("LoggingService not initialized");
        public IRegistryService RegistryService => _registryService ?? throw new InvalidOperationException("RegistryService not initialized");
        public IDeviceStateManager DeviceStateManager => _deviceStateManager ?? throw new InvalidOperationException("DeviceStateManager not initialized");
        public IVolumeEnforcementService VolumeEnforcementService => _volumeEnforcementService ?? throw new InvalidOperationException("VolumeEnforcementService not initialized");
        public IDeviceHelperService DeviceHelperService => _deviceHelperService ?? throw new InvalidOperationException("DeviceHelperService not initialized");
        public IUIEventRouter UIEventRouter => _uiEventRouter ?? throw new InvalidOperationException("UIEventRouter not initialized");
        public ISettingsService SettingsService => _settingsService ?? throw new InvalidOperationException("SettingsService not initialized");
        public IFavoritesService FavoritesService => _favoritesService ?? throw new InvalidOperationException("FavoritesService not initialized");
        public IFavoritesDragDropService FavoritesDragDropService => _favoritesDragDropService ?? throw new InvalidOperationException("FavoritesDragDropService not initialized");

        // Manager accessors for compatibility
        public UserInterface.TabManager? TabManager => _tabManager;
        public UserInterface.WindowManager? WindowManager => _windowManager;
        public Settings.SettingsManager? SettingsManager => _settingsManager;
        public Settings.FrozenVolumeManager? FrozenVolumeManager => _frozenVolumeManager;
        public VolumeMonitoring.VolumeChangeManager? VolumeChangeManager => _volumeChangeManager;
        public IAudioMeteringService? AudioMeteringService => _audioMeteringService;
        public Settings.DebouncedRegistryManager? DebouncedRegistryManager => _debouncedRegistryManager;

        public MainWindowOrchestratorService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Initializes all core services with dependency injection
        /// </summary>
        public void InitializeCoreServices()
        {
            if (_isInitialized)
                return;

            // Initialize logging service first
            _loggingService = new LoggingService();
            _loggingService.Initialize(_mainWindow.LogPanel.LogTextBoxControl, _mainWindow.LogPanel.LogScrollViewerControl, _mainWindow.Dispatcher);
            _loggingService.SetLogLevel(_mainWindow._currentLogLevel);

            // Initialize device state manager
            _deviceStateManager = new DeviceStateManager(_mainWindow, _loggingService);

            // Initialize volume enforcement service
            _volumeEnforcementService = new VolumeEnforcementService(_mainWindow, _loggingService);

            // Initialize device helper service
            _deviceHelperService = new DeviceHelperService(_loggingService);

            // Initialize SettingsService with all its dependencies
            _registryService = new RegistryService(_loggingService);
            var applicationStartupService = new ApplicationStartupService(_registryService, _loggingService);
            var windowPositionService = new WindowPositionService(_mainWindow, _registryService, _loggingService);
            var settingsInitializationService = new SettingsInitializationService(_mainWindow, applicationStartupService, windowPositionService, _registryService, _loggingService);
            var deviceSelectionPersistenceService = new DeviceSelectionPersistenceService(_registryService, _loggingService);

            _settingsService = new SettingsService(
                _mainWindow,
                applicationStartupService,
                windowPositionService,
                settingsInitializationService,
                deviceSelectionPersistenceService,
                _loggingService);
            
            // Initialize favorites service
            _favoritesService = new FavoritesService(_registryService, _loggingService);
            
            // Initialize favorites drag & drop service
            _favoritesDragDropService = new FavoritesDragDropService(_loggingService, _favoritesService);

            _loggingService.Log("Core services initialized successfully");
        }

        /// <summary>
        /// Initializes device management components
        /// </summary>
        public void InitializeDeviceManagement()
        {
            if (_loggingService == null)
                throw new InvalidOperationException("Core services must be initialized first");

            // Initialize DeviceManager with service dependencies
            var deviceEnumerationService = new DeviceEnumerationService(_loggingService);
            var deviceCollectionService = new DeviceCollectionService(_loggingService);
            var nativeDeviceAccessService = new NativeDeviceAccessService(_loggingService);
            var deviceManagerService = new DeviceManagerService(
                deviceEnumerationService, deviceCollectionService, nativeDeviceAccessService, _loggingService);

            _deviceManager = new DeviceManager(
                _mainWindow,
                _mainWindow.Devices,
                _mainWindow.PlaybackDevices,
                _mainWindow.RecordingDevicesLeft,
                _mainWindow.RecordingDevicesRight,
                _mainWindow.PlaybackDevicesLeft,
                _mainWindow.PlaybackDevicesRight,
                deviceManagerService);

            // Store reference in MainWindow for compatibility
            _mainWindow._deviceManager = _deviceManager;

            // Initialize NUCLEAR device notification handler for process restart approach
            var nuclearHandler = new DeviceManagement.NuclearDeviceNotificationHandler(_mainWindow);
            if (nuclearHandler.Initialize())
            {
                _deviceNotificationHandler = nuclearHandler;
                _loggingService?.Log("NUCLEAR device notification handler initialized successfully");
            }
            else
            {
                _loggingService?.Log("Warning: NUCLEAR device change notifications may not work properly");
            }

        }

        /// <summary>
        /// Initializes volume monitoring components
        /// </summary>
        public void InitializeVolumeMonitoring()
        {
            if (_loggingService == null)
                throw new InvalidOperationException("Core services must be initialized first");

            // Initialize volume change manager for event-driven volume enforcement
            _volumeChangeManager = new VolumeChangeManager(_mainWindow, _loggingService);
            
            // Assign to MainWindow for compatibility with existing code
            _mainWindow._volumeChangeManager = _volumeChangeManager;

            // Initialize audio metering service for real-time peak level monitoring
            _audioMeteringService = new AudioMeteringService(_mainWindow, _loggingService);
            
            // Enable metering if setting is enabled
            bool meteringEnabled = _settingsService?.GetAudioMeteringEnabled() ?? true;
            _audioMeteringService.EnableMetering(meteringEnabled);
            
            // Connect to peak level changes
            _audioMeteringService.PeakLevelChanged += OnPeakLevelChanged;
            _audioMeteringService.StereoPeakLevelChanged += OnStereoPeakLevelChanged;
            _audioMeteringService.ChannelCountDetected += OnChannelCountDetected;
            
            // Initialize meter update timer for throttled UI updates
            InitializeMeterUpdateTimer();

        }

        /// <summary>
        /// Initializes settings management components
        /// </summary>
        public void InitializeSettingsManagement()
        {
            if (_loggingService == null || _settingsService == null)
                throw new InvalidOperationException("Core services must be initialized first");

            // Initialize settings manager
            _settingsManager = new SettingsManager(_settingsService, _loggingService);
            _frozenVolumeManager = new FrozenVolumeManager(_mainWindow);

            // Initialize registry optimization manager
            _debouncedRegistryManager = new DebouncedRegistryManager(_mainWindow);
            
            // Assign to MainWindow for compatibility with existing code
            _mainWindow._settingsManager = _settingsManager;
            _mainWindow._frozenVolumeManager = _frozenVolumeManager;
            _mainWindow._debouncedRegistryManager = _debouncedRegistryManager;

        }

        /// <summary>
        /// Initializes UI management components
        /// </summary>
        public void InitializeUIManagement()
        {
            if (_loggingService == null)
                throw new InvalidOperationException("Core services must be initialized first");

            // Initialize UI managers
            _windowManager = new WindowManager(_mainWindow);
            _volumeBarHandler = new VolumeBarInteractionHandler(_mainWindow);
            _settingsToggleManager = new SettingsToggleManager(_mainWindow);
            _eventHandlerCoordinator = new EventHandlerCoordinator(_mainWindow);
            _tabManager = new TabManager(_mainWindow);
            
            // Assign to MainWindow for compatibility with existing code
            _mainWindow._windowManager = _windowManager;
            _mainWindow._volumeBarHandler = _volumeBarHandler;
            _mainWindow._settingsToggleManager = _settingsToggleManager;
            _mainWindow._eventHandlerCoordinator = _eventHandlerCoordinator;
            _mainWindow._tabManager = _tabManager;

        }

        /// <summary>
        /// Initializes event routing and coordination
        /// </summary>
        public void InitializeEventCoordination()
        {
            if (_loggingService == null || _windowManager == null || _eventHandlerCoordinator == null || 
                _tabManager == null || _volumeBarHandler == null)
                throw new InvalidOperationException("UI management must be initialized first");

            // Initialize WindowEventService after UI managers are created
            var windowEventService = new WindowEventService(_mainWindow, _windowManager, _eventHandlerCoordinator, _loggingService);

            // Initialize SettingsToggleEventService
            var settingsToggleEventService = new SettingsToggleEventService(_mainWindow, _loggingService, _tabManager);

            // Initialize VolumeBarEventService
            var volumeBarEventService = new VolumeBarEventService(_volumeBarHandler, _loggingService);

            // Initialize UI event router with services
            _uiEventRouter = new UIEventRouter(_mainWindow, _loggingService, windowEventService, settingsToggleEventService, volumeBarEventService);

            _loggingService.Log("Event coordination initialized successfully");
        }

        /// <summary>
        /// Sets up window icons and UI elements
        /// </summary>
        public void SetupWindowIcons()
        {
            if (_windowManager == null)
                throw new InvalidOperationException("UI management must be initialized first");

            // Set up window icons after UI managers are created
            _windowManager.SetupTitleBarIcon();
            _windowManager.SetupWindowIcon();

        }

        /// <summary>
        /// Sets up device selection change handling
        /// </summary>
        public void SetupDeviceSelectionHandling()
        {
            if (_deviceStateManager == null)
                throw new InvalidOperationException("Device state manager must be initialized first");

            // Hook into device selection changes with immediate processing
            AudioDeviceViewModel.SelectionChanged = (device, isSelected) =>
            {
                _deviceStateManager.ProcessDeviceSelectionChange(device, isSelected);
            };
            
            // Hook into favorites changes
            AudioDeviceViewModel.FavoritesChanged = (device, isFavorite) =>
            {
                _loggingService?.Log($"Device '{device.Name}' favorite status changed to: {isFavorite}");
                
                // If becoming a favorite, assign column and position
                if (isFavorite && _favoritesDragDropService != null)
                {
                    var targetColumn = _favoritesDragDropService.GetLeastPopulatedColumn(
                        _mainWindow.FavoriteDevicesLeft, 
                        _mainWindow.FavoriteDevicesRight);
                    
                    device.FavoriteColumn = targetColumn;
                    
                    // Assign position at the end of the chosen column
                    if (targetColumn == FavoriteColumnType.Left)
                    {
                        device.FavoritePosition = _mainWindow.FavoriteDevicesLeft.Count;
                    }
                    else
                    {
                        device.FavoritePosition = _mainWindow.FavoriteDevicesRight.Count;
                    }
                    
                    _loggingService?.Log($"Assigned device '{device.Name}' to {targetColumn} column at position {device.FavoritePosition}");
                }
                
                // Save favorites
                var allDevices = _mainWindow.Devices.Concat(_mainWindow.PlaybackDevices);
                _favoritesService?.SaveFavorites(allDevices);
                
                // Update UI
                _tabManager?.RebalanceTabDeviceGrids();
                
                // Refresh audio metering if we're on the Favorites tab
                if (_audioMeteringService != null && 
                    _audioMeteringService.IsEnabled && 
                    _mainWindow.DeviceTabView?.SelectedIndex == 2) // Favorites tab is index 2
                {
                    _loggingService?.Log("[AUDIO_METERING] Refreshing captures due to favorites change", LogLevel.Debug);
                    _audioMeteringService.RefreshDeviceCapturesSync();
                }
            };

        }

        /// <summary>
        /// Performs final initialization steps after window is loaded
        /// </summary>
        public void PerformFinalInitialization()
        {
            if (_deviceManager == null)
                throw new InvalidOperationException("Device management must be initialized first");

            // Load audio devices without blocking UI
            Task.Run(() => _deviceManager.BasicLoadAudioDevices());

            // Ensure log starts scrolled to bottom and in live mode
            _mainWindow.Dispatcher.BeginInvoke(() => {
                try
                {
                    // Ensure auto-scroll is enabled and UI reflects it
                    _loggingService?.SetAutoScroll(true);
                    _mainWindow.LogPanel.LogTextBoxControl.ScrollToEnd();
                    _mainWindow.LogPanel.LogScrollViewerControl.ScrollToBottom();
                }
                catch { /* Ignore scroll errors during initialization */ }
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            _isInitialized = true;
            
            _loggingService?.Log("Final initialization completed successfully");
        }


        /// <summary>
        /// Initialize the meter update timer for throttled UI updates
        /// This prevents UI thread saturation on high refresh rate monitors
        /// </summary>
        private void InitializeMeterUpdateTimer()
        {
            _meterUpdateTimer = new DispatcherTimer(DispatcherPriority.Render);
            _meterUpdateTimer.Interval = TimeSpan.FromMilliseconds(METER_UPDATE_INTERVAL_MS);
            _meterUpdateTimer.Tick += OnMeterUpdateTimerTick;
            _meterUpdateTimer.Start();
            
            _loggingService?.Log($"Audio meter UI update throttling enabled at {1000 / METER_UPDATE_INTERVAL_MS} FPS", LogLevel.Debug);
        }
        
        /// <summary>
        /// Timer tick handler that applies batched peak level updates to the UI
        /// </summary>
        private void OnMeterUpdateTimerTick(object? sender, EventArgs e)
        {
            lock (_peakLevelLock)
            {
                // Apply all pending peak level updates
                foreach (var kvp in _pendingPeakLevels)
                {
                    UpdateDevicePeakLevel(kvp.Key, kvp.Value);
                }
                _pendingPeakLevels.Clear();
                
                // Apply all pending stereo peak level updates
                foreach (var kvp in _pendingStereoPeakLevels)
                {
                    UpdateDeviceStereoPeakLevels(kvp.Key, kvp.Value);
                }
                _pendingStereoPeakLevels.Clear();
            }
        }

        /// <summary>
        /// Handles peak level changes from the audio metering service
        /// Stores updates for batched application to prevent UI thread saturation
        /// </summary>
        private void OnPeakLevelChanged(string deviceId, float peakLevel)
        {
            lock (_peakLevelLock)
            {
                // Store the latest peak level for this device
                _pendingPeakLevels[deviceId] = peakLevel;
            }
        }
        
        /// <summary>
        /// Actually updates the device's peak level in the UI
        /// Called from the timer to batch updates
        /// </summary>
        private void UpdateDevicePeakLevel(string deviceId, float peakLevel)
        {
            try
            {
                // Find the device in either collection
                AudioDeviceViewModel? device = null;
                
                foreach (var d in _mainWindow.Devices)
                {
                    if (d.Id == deviceId)
                    {
                        device = d;
                        break;
                    }
                }
                
                if (device == null)
                {
                    foreach (var d in _mainWindow.PlaybackDevices)
                    {
                        if (d.Id == deviceId)
                        {
                            device = d;
                            break;
                        }
                    }
                }
                
                if (device != null)
                {
                    // Update peak level
                    device.PeakLevel = peakLevel;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error updating peak level for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handles stereo peak level changes from the audio metering service
        /// Stores updates for batched application to prevent UI thread saturation
        /// </summary>
        private void OnStereoPeakLevelChanged(string deviceId, StereoPeakLevels stereoPeakLevels)
        {
            lock (_peakLevelLock)
            {
                // Store the latest stereo peak levels for this device
                _pendingStereoPeakLevels[deviceId] = stereoPeakLevels;
            }
        }
        
        /// <summary>
        /// Actually updates the device's stereo peak levels in the UI
        /// Called from the timer to batch updates
        /// </summary>
        private void UpdateDeviceStereoPeakLevels(string deviceId, StereoPeakLevels stereoPeakLevels)
        {
            try
            {
                // Find the device in either collection
                AudioDeviceViewModel? device = null;
                
                foreach (var d in _mainWindow.Devices)
                {
                    if (d.Id == deviceId)
                    {
                        device = d;
                        break;
                    }
                }
                
                if (device == null)
                {
                    foreach (var d in _mainWindow.PlaybackDevices)
                    {
                        if (d.Id == deviceId)
                        {
                            device = d;
                            break;
                        }
                    }
                }
                
                if (device != null)
                {
                    // Update stereo peak levels
                    device.LeftPeakLevel = stereoPeakLevels.Left;
                    device.RightPeakLevel = stereoPeakLevels.Right;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error updating stereo peak level for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handle channel count detection from audio capture
        /// Updates the device's Channels property with the actual detected channel count
        /// </summary>
        private void OnChannelCountDetected(string deviceId, int channelCount)
        {
            try
            {
                // Find the device in either collection
                AudioDeviceViewModel? device = null;
                
                foreach (var d in _mainWindow.Devices)
                {
                    if (d.Id == deviceId)
                    {
                        device = d;
                        break;
                    }
                }
                
                if (device == null)
                {
                    foreach (var d in _mainWindow.PlaybackDevices)
                    {
                        if (d.Id == deviceId)
                        {
                            device = d;
                            break;
                        }
                    }
                }
                
                if (device != null)
                {
                    // Don't update channel count if device was already detected as multi-channel
                    if (device.IsMultiChannel)
                    {
                        _loggingService?.Log($"[ORCHESTRATOR] Ignoring channel count update for multi-channel device {device.Name} - keeping {device.Channels} channels", LogLevel.Debug);
                        return;
                    }
                    
                    if (device.Channels != channelCount)
                    {
                        _loggingService?.Log($"[ORCHESTRATOR] Updating channel count for device {device.Name} from {device.Channels} to {channelCount} channels", LogLevel.Debug);
                        device.Channels = channelCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error updating channel count for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Sets the active tab for audio metering optimization
        /// </summary>
        public void SetActiveTab(string tabName)
        {
            _audioMeteringService?.SetActiveTab(tabName);
        }
        
        /// <summary>
        /// Cleans up resources used by the orchestrator service
        /// Should be called when the application is closing
        /// </summary>
        public void Cleanup()
        {
            // Stop and dispose the meter update timer
            if (_meterUpdateTimer != null)
            {
                _meterUpdateTimer.Stop();
                _meterUpdateTimer.Tick -= OnMeterUpdateTimerTick;
                _meterUpdateTimer = null;
                
                _loggingService?.Log("Audio meter update timer stopped and cleaned up", LogLevel.Debug);
            }
            
            // Clear any pending updates
            lock (_peakLevelLock)
            {
                _pendingPeakLevels.Clear();
                _pendingStereoPeakLevels.Clear();
            }
        }
    }
}