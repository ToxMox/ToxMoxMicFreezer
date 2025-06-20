// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Settings
{
    /// <summary>
    /// Provides efficient single-device registry operations to replace bulk processing
    /// </summary>
    public class SingleDeviceRegistryManager
    {
        // Registry paths - keep consistent with SettingsManager
        private const string RegKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string FrozenVolumesKeyPath = @"SOFTWARE\ToxMoxMicFreezer\FrozenVolumes";
        private const string SelectedDevicesValueName = "SelectedDevices";

        /// <summary>
        /// Update device selection for a single device without processing all devices
        /// </summary>
        public void UpdateDeviceSelection(string deviceId, bool isSelected)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                if (key != null)
                {
                    UpdateDeviceInMultiString(key, SelectedDevicesValueName, deviceId, isSelected);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating device selection for {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Update frozen volume for a single device
        /// </summary>
        public void UpdateDeviceFrozenVolume(string deviceId, float volume)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            try
            {
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    frozenKey.SetValue(deviceId, volume.ToString("F1"), RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating frozen volume for {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove device from both selection and frozen volume registry
        /// </summary>
        public void RemoveDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            try
            {
                // Remove from selected devices
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                if (key != null)
                {
                    UpdateDeviceInMultiString(key, SelectedDevicesValueName, deviceId, false);
                }

                // Remove from frozen volumes
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    try
                    {
                        frozenKey.DeleteValue(deviceId, false);
                    }
                    catch
                    {
                        // Silently ignore if value doesn't exist
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing device {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove frozen volume for a specific device from registry
        /// </summary>
        public void RemoveDeviceFrozenVolume(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            try
            {
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    try
                    {
                        frozenKey.DeleteValue(deviceId, false);
                    }
                    catch
                    {
                        // Silently ignore if value doesn't exist
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing frozen volume for {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Performance optimized: Efficiently modify multi-string registry values for a single device
        /// Uses HashSet for O(1) lookups and minimizes array/collection conversions
        /// </summary>
        private void UpdateDeviceInMultiString(RegistryKey registryKey, string valueName, string deviceId, bool add)
        {
            try
            {
                // Get current multi-string value
                var currentValues = registryKey.GetValue(valueName) as string[] ?? new string[0];
                
                // Performance optimization: Use HashSet for O(1) operations instead of List with O(n) Contains()
                var valuesSet = new HashSet<string>(currentValues);
                bool modified = false;
                
                if (add)
                {
                    // Add device if not already present (O(1) operation)
                    modified = valuesSet.Add(deviceId);
                }
                else
                {
                    // Remove device if present (O(1) operation)  
                    modified = valuesSet.Remove(deviceId);
                }
                
                // Only write to registry if actually modified
                if (modified)
                {
                    // Convert back to array for registry storage (single conversion)
                    var updatedArray = new string[valuesSet.Count];
                    valuesSet.CopyTo(updatedArray);
                    registryKey.SetValue(valueName, updatedArray, RegistryValueKind.MultiString);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating multi-string for {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get currently selected device IDs from registry (for verification/debugging)
        /// </summary>
        public string[] GetSelectedDeviceIds()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    return key.GetValue(SelectedDevicesValueName) as string[] ?? new string[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting selected device IDs: {ex.Message}");
            }
            
            return new string[0];
        }

        /// <summary>
        /// Get frozen volume for a specific device (for verification/debugging)
        /// </summary>
        public float? GetDeviceFrozenVolume(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;
            
            try
            {
                using var frozenKey = Registry.CurrentUser.OpenSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    var volumeStr = frozenKey.GetValue(deviceId) as string;
                    if (!string.IsNullOrEmpty(volumeStr) && float.TryParse(volumeStr, out float volume))
                    {
                        return volume;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting frozen volume for {deviceId}: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Batch update multiple devices efficiently (for app lifecycle operations)
        /// </summary>
        public void BatchUpdateDevices(Dictionary<string, (bool isSelected, float? frozenVolume)> deviceUpdates)
        {
            if (deviceUpdates == null || deviceUpdates.Count == 0) return;
            
            try
            {
                // Update selections in batch
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                if (key != null)
                {
                    var currentSelected = (key.GetValue(SelectedDevicesValueName) as string[] ?? new string[0]).ToList();
                    bool selectionChanged = false;
                    
                    foreach (var update in deviceUpdates)
                    {
                        if (update.Value.isSelected && !currentSelected.Contains(update.Key))
                        {
                            currentSelected.Add(update.Key);
                            selectionChanged = true;
                        }
                        else if (!update.Value.isSelected && currentSelected.Remove(update.Key))
                        {
                            selectionChanged = true;
                        }
                    }
                    
                    if (selectionChanged)
                    {
                        key.SetValue(SelectedDevicesValueName, currentSelected.ToArray(), RegistryValueKind.MultiString);
                    }
                }

                // Update frozen volumes in batch
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    foreach (var update in deviceUpdates)
                    {
                        if (update.Value.frozenVolume.HasValue)
                        {
                            frozenKey.SetValue(update.Key, update.Value.frozenVolume.Value.ToString("F1"), RegistryValueKind.String);
                        }
                        else if (!update.Value.isSelected)
                        {
                            // Remove frozen volume for deselected devices
                            try
                            {
                                frozenKey.DeleteValue(update.Key, false);
                            }
                            catch
                            {
                                // Silently ignore if value doesn't exist
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in batch update: {ex.Message}");
            }
        }
    }
}