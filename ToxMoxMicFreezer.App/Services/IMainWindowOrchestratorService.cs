// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for orchestrating MainWindow initialization and service coordination
    /// Handles complex initialization sequences and dependency injection setup
    /// </summary>
    public interface IMainWindowOrchestratorService
    {
        /// <summary>
        /// Initializes all core services with dependency injection
        /// </summary>
        void InitializeCoreServices();

        /// <summary>
        /// Initializes device management components
        /// </summary>
        void InitializeDeviceManagement();

        /// <summary>
        /// Initializes volume monitoring components
        /// </summary>
        void InitializeVolumeMonitoring();

        /// <summary>
        /// Initializes settings management components
        /// </summary>
        void InitializeSettingsManagement();

        /// <summary>
        /// Initializes UI management components
        /// </summary>
        void InitializeUIManagement();

        /// <summary>
        /// Initializes event routing and coordination
        /// </summary>
        void InitializeEventCoordination();

        /// <summary>
        /// Performs final initialization steps after window is loaded
        /// </summary>
        void PerformFinalInitialization();

        /// <summary>
        /// Sets up device selection change handling
        /// </summary>
        void SetupDeviceSelectionHandling();

        /// <summary>
        /// Sets up window icons and UI elements
        /// </summary>
        void SetupWindowIcons();

        /// <summary>
        /// Provides access to the logging service for other components
        /// </summary>
        ILoggingService LoggingService { get; }
        
        /// <summary>
        /// Provides access to the registry service
        /// </summary>
        IRegistryService RegistryService { get; }

        /// <summary>
        /// Provides access to the device state manager
        /// </summary>
        IDeviceStateManager DeviceStateManager { get; }

        /// <summary>
        /// Provides access to the volume enforcement service
        /// </summary>
        IVolumeEnforcementService VolumeEnforcementService { get; }

        /// <summary>
        /// Provides access to the device helper service
        /// </summary>
        IDeviceHelperService DeviceHelperService { get; }

        /// <summary>
        /// Provides access to the UI event router
        /// </summary>
        IUIEventRouter UIEventRouter { get; }

        /// <summary>
        /// Provides access to the settings service
        /// </summary>
        ISettingsService SettingsService { get; }
        
        /// <summary>
        /// Provides access to the favorites service
        /// </summary>
        IFavoritesService FavoritesService { get; }
        
        /// <summary>
        /// Provides access to the favorites drag & drop service
        /// </summary>
        IFavoritesDragDropService FavoritesDragDropService { get; }

        /// <summary>
        /// Provides access to the tab manager for device grid management
        /// </summary>
        UserInterface.TabManager? TabManager { get; }

        /// <summary>
        /// Provides access to the window manager for window operations
        /// </summary>
        UserInterface.WindowManager? WindowManager { get; }

        /// <summary>
        /// Provides access to the settings manager for settings persistence
        /// </summary>
        Settings.SettingsManager? SettingsManager { get; }

        /// <summary>
        /// Provides access to the frozen volume manager for volume state persistence
        /// </summary>
        Settings.FrozenVolumeManager? FrozenVolumeManager { get; }

        /// <summary>
        /// Provides access to the volume change manager for volume monitoring
        /// </summary>
        VolumeMonitoring.VolumeChangeManager? VolumeChangeManager { get; }

        /// <summary>
        /// Provides access to the debounced registry manager for registry optimization
        /// </summary>
        Settings.DebouncedRegistryManager? DebouncedRegistryManager { get; }

        /// <summary>
        /// Provides access to the audio metering service for real-time peak level monitoring
        /// </summary>
        IAudioMeteringService? AudioMeteringService { get; }

        /// <summary>
        /// Sets the active tab for audio metering optimization
        /// </summary>
        void SetActiveTab(string tabName);
        
        /// <summary>
        /// Cleans up resources used by the orchestrator service
        /// Should be called when the application is closing
        /// </summary>
        void Cleanup();
    }
}