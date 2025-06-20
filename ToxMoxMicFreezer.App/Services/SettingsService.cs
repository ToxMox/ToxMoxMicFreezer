// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Coordinating service that manages all settings-related operations
    /// Orchestrates registry, startup, and device selection services
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly MainWindow _mainWindow;
        private readonly IApplicationStartupService _startupService;
        private readonly IWindowPositionService _windowPositionService;
        private readonly ISettingsInitializationService _initializationService;
        private readonly IDeviceSelectionPersistenceService _deviceSelectionService;
        private readonly ILoggingService _loggingService;

        public SettingsService(
            MainWindow mainWindow,
            IApplicationStartupService startupService,
            IWindowPositionService windowPositionService,
            ISettingsInitializationService initializationService,
            IDeviceSelectionPersistenceService deviceSelectionService,
            ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _windowPositionService = windowPositionService ?? throw new ArgumentNullException(nameof(windowPositionService));
            _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
            _deviceSelectionService = deviceSelectionService ?? throw new ArgumentNullException(nameof(deviceSelectionService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Initializes all UI toggle controls with their saved settings
        /// </summary>
        public void InitializeAllToggles()
        {
            _initializationService.InitializeAllToggles();
        }

        /// <summary>
        /// Loads and sets debug mode from registry
        /// </summary>
        public void LoadDebugMode()
        {
            _initializationService.LoadDebugMode();
        }

        /// <summary>
        /// Saves debug mode setting to registry
        /// </summary>
        public void SaveDebugMode(LogLevel logLevel)
        {
            _initializationService.SaveDebugMode(logLevel);
        }

        /// <summary>
        /// Loads log panel height and applies it to the UI
        /// </summary>
        public void LoadLogPanelHeight()
        {
            _initializationService.LoadLogPanelHeight();
        }

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        public void SaveLogPanelHeight(double height)
        {
            _initializationService.SaveLogPanelHeight(height);
        }

        /// <summary>
        /// Loads device selection and frozen volume settings from registry
        /// </summary>
        public void LoadSelection()
        {
            _deviceSelectionService.LoadSelection(
                _mainWindow.Devices,
                _mainWindow.PlaybackDevices,
                _mainWindow._persistentSelectionState);
        }

        /// <summary>
        /// Saves device selection and frozen volume settings to registry
        /// </summary>
        public void SaveSelection()
        {
            _deviceSelectionService.SaveSelection(
                _mainWindow.Devices,
                _mainWindow.PlaybackDevices,
                _mainWindow._persistentSelectionState);
        }

        /// <summary>
        /// Sets whether the application starts with Windows
        /// </summary>
        public void SetStartupEnabled(bool enabled)
        {
            _startupService.SetStartupEnabled(enabled);
        }

        /// <summary>
        /// Sets whether the application starts minimized
        /// </summary>
        public void SetStartMinimizedEnabled(bool enabled)
        {
            _startupService.SetStartMinimizedEnabled(enabled);
        }

        /// <summary>
        /// Saves window position to registry
        /// </summary>
        public void SaveWindowPosition(double left, double top, double width, double height, WindowState windowState)
        {
            _windowPositionService.SaveWindowPosition(left, top, width, height, windowState);
        }

        /// <summary>
        /// Loads window position from registry
        /// </summary>
        public (double left, double top, double width, double height, WindowState state) LoadWindowPosition()
        {
            return _windowPositionService.LoadWindowPosition();
        }

        /// <summary>
        /// Gets whether audio metering is enabled
        /// </summary>
        public bool GetAudioMeteringEnabled()
        {
            return App.GetAudioMeteringEnabledSetting();
        }

        /// <summary>
        /// Sets whether audio metering is enabled
        /// </summary>
        public void SetAudioMeteringEnabled(bool enabled)
        {
            App.SaveAudioMeteringEnabledSetting(enabled);
        }

    }
}