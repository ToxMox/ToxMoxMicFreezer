// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for initializing UI settings controls
    /// Handles setting up all toggle controls with their saved states
    /// </summary>
    public class SettingsInitializationService : ISettingsInitializationService
    {
        private readonly MainWindow _mainWindow;
        private readonly IApplicationStartupService _startupService;
        private readonly IWindowPositionService _windowPositionService;
        private readonly IRegistryService _registryService;
        private readonly ILoggingService _loggingService;
        
        private const string AppKeyPath = @"SOFTWARE\ToxMoxMicFreezer";

        public SettingsInitializationService(
            MainWindow mainWindow,
            IApplicationStartupService startupService,
            IWindowPositionService windowPositionService,
            IRegistryService registryService,
            ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _windowPositionService = windowPositionService ?? throw new ArgumentNullException(nameof(windowPositionService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Initializes all UI toggle controls with their saved settings
        /// </summary>
        public void InitializeAllToggles()
        {
            InitializeStartupToggle();
            InitializeMinimizeToTrayToggle();
            InitializeStartMinimizedToggle();
            InitializeHideFixedVolumeToggle();
            InitializePopupNotificationsToggle();
            InitializeAudioMeteringToggle();
            InitializeDebugLoggingToggle();
        }

        /// <summary>
        /// Loads and sets debug mode from registry
        /// </summary>
        public void LoadDebugMode()
        {
            try
            {
                int debugMode = _registryService.GetValue(AppKeyPath, "DebugLogging", 0);
                _mainWindow._currentLogLevel = debugMode == 1 ? LogLevel.Debug : LogLevel.Info;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading debug mode setting: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Saves debug mode setting to registry
        /// </summary>
        public void SaveDebugMode(LogLevel logLevel)
        {
            try
            {
                bool success = _registryService.SetValue(AppKeyPath, "DebugLogging", logLevel == LogLevel.Debug ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error saving debug mode setting: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads log panel height and applies it to the UI
        /// </summary>
        public void LoadLogPanelHeight()
        {
            try
            {
                double height = _windowPositionService.LoadLogPanelHeight();
                _windowPositionService.ApplyLogPanelHeight(height);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading log panel height: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        public void SaveLogPanelHeight(double height)
        {
            _windowPositionService.SaveLogPanelHeight(height);
        }

        private void InitializeStartupToggle()
        {
            try
            {
                bool isInStartup = _startupService.IsInStartup();
                var startupToggle = _mainWindow.SettingsPanel?.StartupToggleControl;
                if (startupToggle != null)
                {
                    startupToggle.IsChecked = isInStartup;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing startup toggle: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InitializeMinimizeToTrayToggle()
        {
            try
            {
                bool minimizeToTray = ((App)System.Windows.Application.Current).GetMinimizeToTraySetting();
                var minimizeToggle = _mainWindow.SettingsPanel?.MinimizeToTrayToggleControl;
                if (minimizeToggle != null)
                {
                    minimizeToggle.IsChecked = minimizeToTray;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing minimize to tray toggle: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InitializeStartMinimizedToggle()
        {
            try
            {
                bool startMinimized = _startupService.IsStartMinimizedEnabled();
                var startMinimizedToggle = _mainWindow.SettingsPanel?.StartMinimizedToggleControl;
                if (startMinimizedToggle != null)
                {
                    startMinimizedToggle.IsChecked = startMinimized;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing start minimized toggle: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InitializeHideFixedVolumeToggle()
        {
            try
            {
                bool hideFixedVolume = App.GetHideFixedVolumeDevicesSetting();
                var hideFixedToggle = _mainWindow.SettingsPanel?.HideFixedVolumeToggleControl;
                if (hideFixedToggle != null)
                {
                    hideFixedToggle.IsChecked = hideFixedVolume;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing hide fixed volume toggle: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InitializePopupNotificationsToggle()
        {
            try
            {
                bool popupNotifications = ((App)System.Windows.Application.Current).GetNotificationsEnabledSetting();
                var popupNotificationsToggle = _mainWindow.SettingsPanel?.PopupNotificationsToggleControl;
                if (popupNotificationsToggle != null)
                {
                    popupNotificationsToggle.IsChecked = popupNotifications;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing popup notifications toggle: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InitializeAudioMeteringToggle()
        {
            try
            {
                bool audioMeteringEnabled = App.GetAudioMeteringEnabledSetting();
                var audioMeteringToggle = _mainWindow.SettingsPanel?.AudioMeteringToggleControl;
                if (audioMeteringToggle != null)
                {
                    audioMeteringToggle.IsChecked = audioMeteringEnabled;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing audio metering toggle: {ex.Message}", LogLevel.Warning);
            }
        }



        private void InitializeDebugLoggingToggle()
        {
            try
            {
                var debugLoggingToggle = _mainWindow.SettingsPanel?.DebugLoggingToggleControl;
                if (debugLoggingToggle != null)
                {
                    debugLoggingToggle.IsChecked = _mainWindow._currentLogLevel == LogLevel.Debug;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error initializing debug logging toggle: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}