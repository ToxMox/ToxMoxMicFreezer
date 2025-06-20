// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing Windows startup registry entries
    /// Handles adding/removing application from Windows startup
    /// </summary>
    public class ApplicationStartupService : IApplicationStartupService
    {
        private readonly IRegistryService _registryService;
        private readonly ILoggingService _loggingService;
        
        private const string StartupKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string AppName = "ToxMoxMicFreezer";

        public ApplicationStartupService(IRegistryService registryService, ILoggingService loggingService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Checks if the application is currently in Windows startup
        /// </summary>
        public bool IsInStartup()
        {
            try
            {
                using var key = _registryService.OpenSubKey(StartupKeyPath);
                if (key != null)
                {
                    var value = key.GetValue(AppName);
                    return value != null;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error checking startup status: {ex.Message}", LogLevel.Warning);
            }

            return false;
        }

        /// <summary>
        /// Sets whether the application starts with Windows
        /// </summary>
        public bool SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = _registryService.OpenSubKeyWritable(StartupKeyPath);
                if (key != null)
                {
                    if (enabled)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            string command = IsStartMinimizedEnabled() 
                                ? $"\"{exePath}\" --minimized"
                                : $"\"{exePath}\"";
                            
                            key.SetValue(AppName, command);
                            _loggingService.Log("Added to Windows startup", LogLevel.Info);
                            return true;
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                        _loggingService.Log("Removed from Windows startup", LogLevel.Info);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error updating startup setting: {ex.Message}", LogLevel.Error);
            }

            return false;
        }

        /// <summary>
        /// Checks if the application is set to start minimized
        /// </summary>
        public bool IsStartMinimizedEnabled()
        {
            return _registryService.GetValue(AppKeyPath, "StartMinimizedToTray", false);
        }

        /// <summary>
        /// Sets whether the application starts minimized
        /// </summary>
        public bool SetStartMinimizedEnabled(bool enabled)
        {
            try
            {
                // Save the setting to our app registry key
                bool settingSaved = _registryService.SetValue(AppKeyPath, "StartMinimizedToTray", enabled ? 1 : 0, RegistryValueKind.DWord);
                
                // Update the startup command if the app is in startup
                if (IsInStartup())
                {
                    using var key = _registryService.OpenSubKeyWritable(StartupKeyPath);
                    if (key != null)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            string command = enabled 
                                ? $"\"{exePath}\" --minimized"
                                : $"\"{exePath}\"";
                            
                            key.SetValue(AppName, command);
                        }
                    }
                }
                
                _loggingService.Log($"Start minimized: {(enabled ? "Enabled" : "Disabled")}", LogLevel.Info);
                return settingSaved;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error updating start minimized setting: {ex.Message}", LogLevel.Error);
                return false;
            }
        }
    }
}