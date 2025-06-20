// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Settings
{
    /// <summary>
    /// Manages frozen volume persistence with enhanced error handling and immediate volume capture
    /// Based on OldReference working implementation patterns
    /// </summary>
    public class FrozenVolumeManager
    {
        private readonly MainWindow _mainWindow;
        private const string FrozenVolumesKeyPath = @"SOFTWARE\ToxMoxMicFreezer\FrozenVolumes";
        
        // Performance optimization: Constants for floating-point comparison
        private const float FROZEN_VOLUME_NOT_SET = 0.0f; // Default value indicating not set
        private const float FLOAT_TOLERANCE = 0.01f; // 0.01dB tolerance for floating-point comparisons
        
        public FrozenVolumeManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }
        
        /// <summary>
        /// Performance optimization: Safe floating-point comparison with tolerance
        /// </summary>
        private static bool IsFrozenVolumeSet(float frozenVolume)
        {
            // Use tolerance-based comparison instead of direct equality
            // This prevents precision issues and allows legitimate 0dB frozen volumes
            return Math.Abs(frozenVolume - FROZEN_VOLUME_NOT_SET) > FLOAT_TOLERANCE;
        }
        
        /// <summary>
        /// Load frozen volumes for all selected devices with enhanced error handling
        /// </summary>
        public void LoadFrozenVolumes()
        {
            try
            {
                using var frozenKey = Registry.CurrentUser.OpenSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    var selectedDevices = _mainWindow.Devices.Where(d => d.IsSelected);
                    foreach (var dev in selectedDevices)
                    {
                        LoadFrozenVolumeForDevice(frozenKey, dev);
                    }
                    
                    _mainWindow.AppendLog($"Loaded frozen volumes for {selectedDevices.Count()} selected devices");
                }
                else
                {
                    _mainWindow.AppendLog("No frozen volume registry key found - this is normal for first run");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error loading frozen volumes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save frozen volumes for all selected devices with enhanced error handling
        /// </summary>
        public void SaveFrozenVolumes(bool suppressLog = false)
        {
            try
            {
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    var selectedDevices = _mainWindow.Devices.Where(d => d.IsSelected);
                    foreach (var device in selectedDevices)
                    {
                        SaveFrozenVolumeForDevice(frozenKey, device, suppressLog);
                    }
                    
                    if (!suppressLog)
                    {
                        _mainWindow.AppendLog($"Saved frozen volumes for {selectedDevices.Count()} selected devices");
                    }
                }
                else
                {
                    _mainWindow.AppendLog("Error: Could not create frozen volumes registry key");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error saving frozen volumes: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously save frozen volumes for all selected devices
        /// </summary>
        public async Task SaveFrozenVolumesAsync(bool suppressLog = false)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                    if (frozenKey != null)
                    {
                        var selectedDevices = _mainWindow.Devices.Where(d => d.IsSelected).ToList();
                        foreach (var device in selectedDevices)
                        {
                            SaveFrozenVolumeForDevice(frozenKey, device, suppressLog);
                        }
                        
                        if (!suppressLog)
                        {
                            _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                                _mainWindow.AppendLog($"Saved frozen volumes for {selectedDevices.Count} selected devices"));
                        }
                    }
                    else
                    {
                        _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                            _mainWindow.AppendLog("Error: Could not create frozen volumes registry key"));
                    }
                });
            }
            catch (Exception ex)
            {
                _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                    _mainWindow.AppendLog($"Error saving frozen volumes asynchronously: {ex.Message}"));
            }
        }

        /// <summary>
        /// Asynchronously load frozen volumes for all selected devices
        /// </summary>
        public async Task LoadFrozenVolumesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    using var frozenKey = Registry.CurrentUser.OpenSubKey(FrozenVolumesKeyPath);
                    if (frozenKey != null)
                    {
                        var selectedDevices = _mainWindow.Devices.Where(d => d.IsSelected).ToList();
                        foreach (var dev in selectedDevices)
                        {
                            LoadFrozenVolumeForDevice(frozenKey, dev);
                        }
                        
                        _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                            _mainWindow.AppendLog($"Loaded frozen volumes for {selectedDevices.Count} selected devices"));
                    }
                    else
                    {
                        _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                            _mainWindow.AppendLog("No frozen volume registry key found - this is normal for first run"));
                    }
                });
            }
            catch (Exception ex)
            {
                _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                    _mainWindow.AppendLog($"Error loading frozen volumes asynchronously: {ex.Message}"));
            }
        }
        
        // Guard against infinite loops
        private readonly HashSet<string> _currentlyCapturing = new();
        
        /// <summary>
        /// Immediately capture current volume as frozen volume when device becomes selected
        /// Based on OldReference implementation
        /// </summary>
        public void CaptureCurrentVolumeAsFrozen(AudioDeviceViewModel device)
        {
            if (device == null || !device.IsSelected)
                return;
                
            // Performance optimization: Use safe floating-point comparison with tolerance
            // Only capture if device doesn't already have a frozen volume set (prevent overwriting existing settings)
            if (IsFrozenVolumeSet(device.FrozenVolumeDb))
            {
                _mainWindow.AppendLog($"Device {device.Name} already has frozen volume {device.FrozenVolumeDb:F1}dB - skipping capture");
                return;
            }
                
            // PREVENT INFINITE LOOP - check if we're already capturing this device
            if (_currentlyCapturing.Contains(device.Id))
            {
                _mainWindow.AppendLog($"Skipping duplicate capture for {device.Name} - already in progress");
                return;
            }
            
            _currentlyCapturing.Add(device.Id);
                
            try
            {
                // Get the actual current volume from the device
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(device.Id);
                if (nativeDevice != null)
                {
                    float currentVolume = nativeDevice.GetVolumeLevel();
                    device.FrozenVolumeDb = currentVolume;
                    
                    _mainWindow.AppendLog($"Captured current volume {currentVolume:F1}dB as frozen volume for {device.Name}");
                    
                    // Immediately save to registry
                    SaveSingleFrozenVolume(device);
                }
                else
                {
                    // Fallback: try to parse from display value
                    if (float.TryParse(device.VolumeDb, out float displayVolume))
                    {
                        device.FrozenVolumeDb = displayVolume;
                        _mainWindow.AppendLog($"Captured display volume {displayVolume:F1}dB as frozen volume for {device.Name} (device not cached)");
                        
                        // Immediately save to registry
                        SaveSingleFrozenVolume(device);
                    }
                    else
                    {
                        _mainWindow.AppendLog($"Warning: Could not capture current volume for {device.Name} - device not accessible");
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error capturing frozen volume for {device.Name}: {ex.Message}");
            }
            finally
            {
                // ALWAYS remove from capturing set to prevent permanent locks
                _currentlyCapturing.Remove(device.Id);
            }
        }
        
        /// <summary>
        /// Save a single device's frozen volume immediately to registry
        /// </summary>
        private void SaveSingleFrozenVolume(AudioDeviceViewModel device)
        {
            try
            {
                using var frozenKey = Registry.CurrentUser.CreateSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    SaveFrozenVolumeForDevice(frozenKey, device);
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error saving frozen volume for {device.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load frozen volume for a specific device with enhanced error handling
        /// </summary>
        private void LoadFrozenVolumeForDevice(RegistryKey frozenKey, AudioDeviceViewModel dev)
        {
            try
            {
                var frozenVolumeValue = frozenKey.GetValue(dev.Id) as string;
                if (!string.IsNullOrEmpty(frozenVolumeValue) && float.TryParse(frozenVolumeValue, out float frozenVolume))
                {
                    dev.FrozenVolumeDb = frozenVolume;
                    _mainWindow.AppendLog($"Restored frozen volume for {dev.Name}: {frozenVolume:F1}dB");
                }
                else
                {
                    _mainWindow.AppendLog($"No saved frozen volume found for {dev.Name} - will capture on first volume change");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error loading frozen volume for {dev.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save frozen volume for a specific device with enhanced error handling
        /// </summary>
        private void SaveFrozenVolumeForDevice(RegistryKey frozenKey, AudioDeviceViewModel device, bool suppressLog = false)
        {
            try
            {
                frozenKey.SetValue(device.Id, device.FrozenVolumeDb.ToString("F1"));
                if (!suppressLog)
                {
                    _mainWindow.AppendLog($"Saved frozen volume for {device.Name}: {device.FrozenVolumeDb:F1}dB");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error saving frozen volume for {device.Name}: {ex.Message}");
            }
        }
    }
}