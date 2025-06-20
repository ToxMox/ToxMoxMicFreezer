// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for Windows registry operations
    /// Provides safe registry access with error handling
    /// </summary>
    public class RegistryService : IRegistryService
    {
        private readonly ILoggingService _loggingService;

        public RegistryService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Creates or opens a registry key under HKEY_CURRENT_USER
        /// </summary>
        public RegistryKey? CreateSubKey(string keyPath)
        {
            try
            {
                return Registry.CurrentUser.CreateSubKey(keyPath);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to create registry key '{keyPath}': {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        /// <summary>
        /// Opens a registry key under HKEY_CURRENT_USER for reading
        /// </summary>
        public RegistryKey? OpenSubKey(string keyPath)
        {
            try
            {
                return Registry.CurrentUser.OpenSubKey(keyPath);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to open registry key '{keyPath}': {ex.Message}", LogLevel.Debug);
                return null;
            }
        }

        /// <summary>
        /// Opens a registry key under HKEY_CURRENT_USER for writing
        /// </summary>
        public RegistryKey? OpenSubKeyWritable(string keyPath)
        {
            try
            {
                return Registry.CurrentUser.OpenSubKey(keyPath, true);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to open registry key '{keyPath}' for writing: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        /// <summary>
        /// Gets a value from registry with default fallback
        /// </summary>
        public T GetValue<T>(string keyPath, string valueName, T defaultValue)
        {
            try
            {
                using var key = OpenSubKey(keyPath);
                if (key != null)
                {
                    var value = key.GetValue(valueName);
                    if (value != null)
                    {
                        if (typeof(T) == typeof(string))
                            return (T)(object)value.ToString()!;
                        if (typeof(T) == typeof(int) && value is int intValue)
                            return (T)(object)intValue;
                        if (typeof(T) == typeof(bool) && value is int boolIntValue)
                            return (T)(object)(boolIntValue == 1);
                        if (typeof(T) == typeof(double))
                            return (T)(object)Convert.ToDouble(value);
                        if (typeof(T) == typeof(string[]) && value is string[] arrayValue)
                            return (T)(object)arrayValue;
                        
                        // Try direct cast as fallback
                        if (value is T directValue)
                            return directValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to get registry value '{valueName}' from '{keyPath}': {ex.Message}", LogLevel.Warning);
            }

            return defaultValue;
        }

        /// <summary>
        /// Sets a value in registry
        /// </summary>
        public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                using var key = CreateSubKey(keyPath);
                if (key != null)
                {
                    key.SetValue(valueName, value, valueKind);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to set registry value '{keyPath}\\{valueName}': {ex.Message}", LogLevel.Warning);
            }

            return false;
        }

        /// <summary>
        /// Deletes a value from registry
        /// </summary>
        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                using var key = OpenSubKeyWritable(keyPath);
                if (key != null)
                {
                    key.DeleteValue(valueName, false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to delete registry value '{valueName}' from '{keyPath}': {ex.Message}", LogLevel.Warning);
            }

            return false;
        }
        
        /// <summary>
        /// Gets the last selected tab from registry
        /// </summary>
        public int GetLastSelectedTab()
        {
            return GetValue(@"SOFTWARE\ToxMoxMicFreezer", "LastSelectedTab", 0);
        }
        
        /// <summary>
        /// Sets the last selected tab in registry
        /// </summary>
        public void SetLastSelectedTab(int tabIndex)
        {
            SetValue(@"SOFTWARE\ToxMoxMicFreezer", "LastSelectedTab", tabIndex, RegistryValueKind.DWord);
        }
    }
}