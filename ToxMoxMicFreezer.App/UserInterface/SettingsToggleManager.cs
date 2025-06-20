// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Manages all settings toggle event handlers for application configuration
    /// </summary>
    public class SettingsToggleManager
    {
        private readonly MainWindow _mainWindow;
        private const string AppName = "ToxMox's Mic Freezer+";

        public SettingsToggleManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
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
                    _mainWindow.AppendLog("Error: Could not get executable path");
                    return;
                }
                string exePath = mainModule.FileName;
                
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key == null)
                {
                    _mainWindow.AppendLog("Error: Could not access registry key");
                    return;
                }

                key.SetValue(AppName, exePath);
                _mainWindow.AppendLog("Added to startup.");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Startup error: {ex.Message}");
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
                    _mainWindow.AppendLog("Error: Could not access registry key");
                    return;
                }

                key.DeleteValue(AppName, false);
                _mainWindow.AppendLog("Removed from startup.");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Startup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles minimize to tray toggle checked event
        /// </summary>
        public void OnMinimizeToTrayToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveMinimizeToTraySetting(true);
            _mainWindow.AppendLog("Minimize to tray enabled.");
        }

        /// <summary>
        /// Handles minimize to tray toggle unchecked event
        /// </summary>
        public void OnMinimizeToTrayToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveMinimizeToTraySetting(false);
            _mainWindow.AppendLog("Minimize to tray disabled.");
        }

        /// <summary>
        /// Handles start minimized toggle checked event
        /// </summary>
        public void OnStartMinimizedToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveStartMinimizedSetting(true);
            _mainWindow.AppendLog("Start minimized to tray enabled.");
        }

        /// <summary>
        /// Handles start minimized toggle unchecked event
        /// </summary>
        public void OnStartMinimizedToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveStartMinimizedSetting(false);
            _mainWindow.AppendLog("Start minimized to tray disabled.");
        }

        /// <summary>
        /// Handles hide fixed volume devices toggle checked event
        /// </summary>
        public void OnHideFixedVolumeToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SaveHideFixedVolumeDevicesSetting(true);
            _mainWindow.AppendLog("Hide fixed volume devices enabled");
            
            // Always rebalance tab device grids
            _mainWindow._tabManager?.RebalanceTabDeviceGrids();
        }

        /// <summary>
        /// Handles hide fixed volume devices toggle unchecked event
        /// </summary>
        public void OnHideFixedVolumeToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SaveHideFixedVolumeDevicesSetting(false);
            _mainWindow.AppendLog("Hide fixed volume devices disabled");
            
            // Always rebalance tab device grids
            _mainWindow._tabManager?.RebalanceTabDeviceGrids();
        }

        /// <summary>
        /// Handles popup notifications toggle checked event
        /// </summary>
        public void OnPopupNotificationsToggleChecked(object sender, RoutedEventArgs e)
        {
            App.SavePopupNotificationsSetting(true);
            // _mainWindow._notificationManager?.UnmutePopups(); // Disabled for clean slate
            _mainWindow.AppendLog("Popup notification setting saved");
        }

        /// <summary>
        /// Handles popup notifications toggle unchecked event
        /// </summary>
        public void OnPopupNotificationsToggleUnchecked(object sender, RoutedEventArgs e)
        {
            App.SavePopupNotificationsSetting(false);
            // _mainWindow._notificationManager?.MutePopupsIndefinitely(); // Disabled for clean slate
            _mainWindow.AppendLog("Popup notification setting saved");
        }
    }
}