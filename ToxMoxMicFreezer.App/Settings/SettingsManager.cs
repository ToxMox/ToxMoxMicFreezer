// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ToxMoxMicFreezer.App.Services;
using ToxMoxMicFreezer.App.UserInterface;
using LogLevel = ToxMoxMicFreezer.App.Services.LogLevel;

namespace ToxMoxMicFreezer.App.Settings
{
    /// <summary>
    /// Manages application settings using dependency injection
    /// Delegates to specialized services for specific functionality
    /// </summary>
    public class SettingsManager
    {
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _loggingService;
        
        // Legacy constants for backward compatibility
        private const string RegKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string RegValueName = "SelectedDevices";
        private const string FrozenVolumesKeyPath = @"SOFTWARE\ToxMoxMicFreezer\FrozenVolumes";
        private const string AppName = "ToxMoxMicFreezer";

        public SettingsManager(ISettingsService settingsService, ILoggingService loggingService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Loads debug mode setting from registry
        /// </summary>
        public void LoadDebugMode()
        {
            _settingsService.LoadDebugMode();
        }
        
        /// <summary>
        /// Saves debug mode setting to registry
        /// </summary>
        public void SaveDebugMode(LogLevel logLevel)
        {
            _settingsService.SaveDebugMode(logLevel);
        }
        
        /// <summary>
        /// Loads log panel height setting from registry
        /// </summary>
        public void LoadLogPanelHeight()
        {
            _settingsService.LoadLogPanelHeight();
        }
        
        /// <summary>
        /// Saves log panel height setting to registry
        /// </summary>
        public void SaveLogPanelHeight(double height)
        {
            _settingsService.SaveLogPanelHeight(height);
        }
        
        /// <summary>
        /// Loads device selection and frozen volume settings from registry
        /// </summary>
        public void LoadSelection()
        {
            _settingsService.LoadSelection();
        }

        /// <summary>
        /// Saves device selection and frozen volume settings to registry
        /// </summary>
        public void SaveSelection()
        {
            _settingsService.SaveSelection();
        }

        /// <summary>
        /// Initializes all UI toggle controls with their saved settings
        /// </summary>
        public void InitializeAllToggles()
        {
            _settingsService.InitializeAllToggles();
        }


        /// <summary>
        /// Handles startup toggle changes
        /// </summary>
        public void SetStartupEnabled(bool enabled)
        {
            _settingsService.SetStartupEnabled(enabled);
        }

        /// <summary>
        /// Handles start minimized toggle changes
        /// </summary>
        public void SetStartMinimizedEnabled(bool enabled)
        {
            _settingsService.SetStartMinimizedEnabled(enabled);
        }

        /// <summary>
        /// Saves window position to registry
        /// </summary>
        public void SaveWindowPosition(double left, double top, double width, double height, WindowState windowState)
        {
            _settingsService.SaveWindowPosition(left, top, width, height, windowState);
        }

        /// <summary>
        /// Loads window position from registry
        /// </summary>
        public (double left, double top, double width, double height, WindowState state) LoadWindowPosition()
        {
            return _settingsService.LoadWindowPosition();
        }

    }
}