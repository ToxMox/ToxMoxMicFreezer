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
    public interface IRegistryService
    {
        /// <summary>
        /// Creates or opens a registry key under HKEY_CURRENT_USER
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <returns>Registry key or null if failed</returns>
        RegistryKey? CreateSubKey(string keyPath);

        /// <summary>
        /// Opens a registry key under HKEY_CURRENT_USER for reading
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <returns>Registry key or null if not found</returns>
        RegistryKey? OpenSubKey(string keyPath);

        /// <summary>
        /// Opens a registry key under HKEY_CURRENT_USER for writing
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <returns>Registry key or null if failed</returns>
        RegistryKey? OpenSubKeyWritable(string keyPath);

        /// <summary>
        /// Gets a value from registry with default fallback
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <param name="valueName">Value name</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Registry value or default</returns>
        T GetValue<T>(string keyPath, string valueName, T defaultValue);

        /// <summary>
        /// Sets a value in registry
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <param name="valueName">Value name</param>
        /// <param name="value">Value to set</param>
        /// <param name="valueKind">Registry value type</param>
        /// <returns>True if successful</returns>
        bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind);

        /// <summary>
        /// Deletes a value from registry
        /// </summary>
        /// <param name="keyPath">Registry key path</param>
        /// <param name="valueName">Value name</param>
        /// <returns>True if successful</returns>
        bool DeleteValue(string keyPath, string valueName);
        
        /// <summary>
        /// Gets the last selected tab from registry
        /// </summary>
        /// <returns>Tab index (0=Recording, 1=Playback, 2=Favorites)</returns>
        int GetLastSelectedTab();
        
        /// <summary>
        /// Sets the last selected tab in registry
        /// </summary>
        /// <param name="tabIndex">Tab index (0=Recording, 1=Playback, 2=Favorites)</param>
        void SetLastSelectedTab(int tabIndex);
    }
}