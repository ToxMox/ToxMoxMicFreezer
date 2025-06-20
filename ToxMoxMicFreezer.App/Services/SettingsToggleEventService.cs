// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using ToxMoxMicFreezer.App.UserInterface;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for handling settings toggle events
    /// Coordinates with settings managers and logging service for configuration changes
    /// </summary>
    public class SettingsToggleEventService : ISettingsToggleEventService
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;
        private readonly TabManager _tabManager;
        private const string AppName = "ToxMox's Mic Freezer+";

        public SettingsToggleEventService(
            MainWindow mainWindow, 
            ILoggingService loggingService,
            TabManager tabManager)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _tabManager = tabManager ?? throw new ArgumentNullException(nameof(tabManager));
        }

        /// <summary>
        /// Handles startup toggle checked event
        /// </summary>
        public void OnStartupToggleChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainModule = Process.GetCurrentProcess().MainModule;
                if (mainModule?.FileName == null)
                {
                    _loggingService.Log("Error: Could not get executable path");
                    return;
                }
                string exePath = mainModule.FileName;
                
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key == null)
                {
                    _loggingService.Log("Error: Could not access registry key");
                    return;
                }

                key.SetValue(AppName, exePath);
                _loggingService.Log("Added to startup.");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Startup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles startup toggle unchecked event
        /// </summary>
        public void OnStartupToggleUnchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key == null)
                {
                    _loggingService.Log("Error: Could not access registry key");
                    return;
                }

                key.DeleteValue(AppName, false);
                _loggingService.Log("Removed from startup.");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Startup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles minimize to tray toggle checked event
        /// </summary>
        public void OnMinimizeToTrayToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveMinimizeToTraySetting(true);
            _loggingService.Log("Minimize to tray enabled.");
        }

        /// <summary>
        /// Handles minimize to tray toggle unchecked event
        /// </summary>
        public void OnMinimizeToTrayToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveMinimizeToTraySetting(false);
            _loggingService.Log("Minimize to tray disabled.");
        }

        /// <summary>
        /// Handles start minimized toggle checked event
        /// </summary>
        public void OnStartMinimizedToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveStartMinimizedSetting(true);
            _loggingService.Log("Start minimized to tray enabled.");
        }

        /// <summary>
        /// Handles start minimized toggle unchecked event
        /// </summary>
        public void OnStartMinimizedToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveStartMinimizedSetting(false);
            _loggingService.Log("Start minimized to tray disabled.");
        }

        /// <summary>
        /// Handles hide fixed volume devices toggle checked event
        /// </summary>
        public void OnHideFixedVolumeToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveHideFixedVolumeDevicesSetting(true);
            _loggingService.Log("Hide fixed volume devices enabled");
            
            // Always rebalance tab device grids
            _tabManager.RebalanceTabDeviceGrids();
        }

        /// <summary>
        /// Handles hide fixed volume devices toggle unchecked event
        /// </summary>
        public void OnHideFixedVolumeToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveHideFixedVolumeDevicesSetting(false);
            _loggingService.Log("Hide fixed volume devices disabled");
            
            // Always rebalance tab device grids
            _tabManager.RebalanceTabDeviceGrids();
        }

        /// <summary>
        /// Handles popup notifications toggle checked event
        /// </summary>
        public void OnPopupNotificationsToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SavePopupNotificationsSetting(true);
            // _mainWindow._notificationManager?.UnmutePopups(); // Disabled for clean slate
            _loggingService.Log("Popup notification setting saved");
            _mainWindow.UpdateNotificationMuteUI();
        }

        /// <summary>
        /// Handles popup notifications toggle unchecked event
        /// </summary>
        public void OnPopupNotificationsToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SavePopupNotificationsSetting(false);
            // _mainWindow._notificationManager?.MutePopupsIndefinitely(); // Disabled for clean slate
            _loggingService.Log("Popup notification setting saved");
            _mainWindow.UpdateNotificationMuteUI();
        }

        /// <summary>
        /// Handles debug logging toggle checked event
        /// </summary>
        public void OnDebugLoggingToggleChecked(object sender, RoutedEventArgs e)
        {
            _mainWindow._currentLogLevel = LogLevel.Debug;
            _loggingService.SetLogLevel(LogLevel.Debug);
            _mainWindow._settingsManager?.SaveDebugMode(LogLevel.Debug);
            _loggingService.Log("Debug logging enabled", LogLevel.Info);
        }

        /// <summary>
        /// Handles audio metering toggle checked event
        /// </summary>
        public void OnAudioMeteringToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveAudioMeteringEnabledSetting(true);
            _loggingService.Log("Audio metering enabled");
            
            // Enable metering in the orchestrator service  
            _mainWindow._mainWindowOrchestrator?.AudioMeteringService?.EnableMetering(true);
        }

        /// <summary>
        /// Handles audio metering toggle unchecked event
        /// </summary>
        public void OnAudioMeteringToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveAudioMeteringEnabledSetting(false);
            _loggingService.Log("Audio metering disabled");
            
            // Disable metering in the orchestrator service
            _mainWindow._mainWindowOrchestrator?.AudioMeteringService?.EnableMetering(false);
        }

        /// <summary>
        /// Handles debug logging toggle unchecked event
        /// </summary>
        public void OnDebugLoggingToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow._currentLogLevel = LogLevel.Info;
            _loggingService.SetLogLevel(LogLevel.Info);
            _mainWindow._settingsManager?.SaveDebugMode(LogLevel.Info);
            _loggingService.Log("Debug logging disabled", LogLevel.Info);
        }
    }
}