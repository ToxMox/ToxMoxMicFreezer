// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for persisting device selections and frozen volume states
    /// Handles loading and saving device preferences to registry
    /// </summary>
    public class DeviceSelectionPersistenceService : IDeviceSelectionPersistenceService
    {
        private readonly IRegistryService _registryService;
        private readonly ILoggingService _loggingService;
        
        private const string AppKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string FrozenVolumesKeyPath = @"SOFTWARE\ToxMoxMicFreezer\FrozenVolumes";
        private const string DeviceFingerprintsKeyPath = @"SOFTWARE\ToxMoxMicFreezer\DeviceFingerprints";
        private const string DeviceMigrationKeyPath = @"SOFTWARE\ToxMoxMicFreezer\DeviceMigration";

        public DeviceSelectionPersistenceService(IRegistryService registryService, ILoggingService loggingService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// PHASE 3 FIX: Enhanced device selection loading with fingerprint-based device matching
        /// Loads device selection and frozen volume settings from registry with migration support
        /// </summary>
        public void LoadSelection(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            HashSet<string> persistentSelectionState)
        {
            try
            {
                // Load device selections
                var selectedIds = _registryService.GetValue<string[]>(AppKeyPath, "SelectedDevices", new string[0]);
                
                // 🔧 PHASE 3 FIX: Build device fingerprint mapping for migration
                var allDevices = recordingDevices.Concat(playbackDevices).ToList();
                var deviceFingerprintMap = BuildDeviceFingerprintMap(allDevices);
                
                // 🔧 PHASE 3 FIX: Attempt to migrate orphaned device IDs using fingerprints
                var migratedIds = AttemptDeviceIdMigration(selectedIds, deviceFingerprintMap);
                
                // Store selected device IDs in persistent set (including migrated ones)
                persistentSelectionState.Clear();
                foreach (var id in migratedIds)
                {
                    persistentSelectionState.Add(id);
                }
                
                // Suppress SelectionChanged events during restoration to prevent unwanted registry saves
                AudioDeviceViewModel.IsLoadingDevices = true;
                try
                {
                    // Apply selection states to recording devices
                    foreach (var dev in recordingDevices)
                    {
                        dev.IsSelected = persistentSelectionState.Contains(dev.Id);
                    }
                    
                    // Apply selection states to playback devices
                    foreach (var dev in playbackDevices)
                    {
                        dev.IsSelected = persistentSelectionState.Contains(dev.Id);
                    }
                    
                    // Load favorites while IsLoadingDevices is true
                    LoadFavoritesForDevices(recordingDevices, playbackDevices);
                }
                finally
                {
                    // Always restore event firing, even if exception occurs
                    AudioDeviceViewModel.IsLoadingDevices = false;
                }
                
                // Load frozen volume levels for selected devices
                LoadFrozenVolumesForDevices(recordingDevices.Where(d => d.IsSelected));
                LoadFrozenVolumesForDevices(playbackDevices.Where(d => d.IsSelected));
                
                // 🔧 PHASE 3 FIX: Update device fingerprints after successful load
                UpdateDeviceFingerprints(allDevices);
                
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to load device selection: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Saves device selection and frozen volume settings to registry
        /// </summary>
        public void SaveSelection(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            HashSet<string> persistentSelectionState)
        {
            try
            {
                // Performance optimization: Single-pass iteration over both collections
                var selectedDeviceIds = new List<string>();
                var deselectedDeviceIds = new List<string>();
                
                // Process recording devices in single pass
                foreach (var device in recordingDevices)
                {
                    if (device.IsSelected)
                        selectedDeviceIds.Add(device.Id);
                    else
                        deselectedDeviceIds.Add(device.Id);
                }
                
                // Process playback devices in single pass  
                foreach (var device in playbackDevices)
                {
                    if (device.IsSelected)
                        selectedDeviceIds.Add(device.Id);
                    else
                        deselectedDeviceIds.Add(device.Id);
                }
                
                // Update persistent set
                foreach (var id in selectedDeviceIds)
                {
                    persistentSelectionState.Add(id);
                }
                
                foreach (var id in deselectedDeviceIds)
                {
                    persistentSelectionState.Remove(id);
                }
                
                // Save device selections to registry
                bool success = _registryService.SetValue(AppKeyPath, "SelectedDevices", persistentSelectionState.ToArray(), RegistryValueKind.MultiString);
                
                if (success)
                {
                    // Save frozen volumes for selected devices
                    SaveFrozenVolumesForSelectedDevices(selectedDeviceIds, recordingDevices, playbackDevices);
                    
                    // Clean up frozen volume entries for deselected devices
                    CleanupFrozenVolumesForDeselectedDevices(deselectedDeviceIds);
                    
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to save device selection: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads frozen volume for a specific device
        /// </summary>
        public bool LoadFrozenVolumeForDevice(AudioDeviceViewModel device)
        {
            try
            {
                using var key = _registryService.OpenSubKey(FrozenVolumesKeyPath);
                if (key != null)
                {
                    var savedVolumeStr = key.GetValue(device.Id) as string;
                    if (!string.IsNullOrEmpty(savedVolumeStr) && float.TryParse(savedVolumeStr, out float savedVolume))
                    {
                        device.FrozenVolumeDb = savedVolume;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading frozen volume for device {device.Id}: {ex.Message}", LogLevel.Warning);
            }

            return false;
        }

        /// <summary>
        /// Saves frozen volume for a specific device
        /// </summary>
        public bool SaveFrozenVolumeForDevice(AudioDeviceViewModel device)
        {
            try
            {
                bool success = _registryService.SetValue(FrozenVolumesKeyPath, device.Id, device.FrozenVolumeDb.ToString("F1"), RegistryValueKind.String);
                return success;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error saving frozen volume for device {device.Id}: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }

        /// <summary>
        /// Removes frozen volume entry for a device
        /// </summary>
        public bool RemoveFrozenVolumeForDevice(string deviceId)
        {
            try
            {
                bool success = _registryService.DeleteValue(FrozenVolumesKeyPath, deviceId);
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void LoadFrozenVolumesForDevices(IEnumerable<AudioDeviceViewModel> devices)
        {
            using var frozenKey = _registryService.OpenSubKey(FrozenVolumesKeyPath);
            if (frozenKey != null)
            {
                foreach (var device in devices)
                {
                    LoadFrozenVolumeForDevice(device);
                }
            }
        }

        private void SaveFrozenVolumesForSelectedDevices(
            List<string> selectedDeviceIds,
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices)
        {
            foreach (var deviceId in selectedDeviceIds)
            {
                // Find the device in either collection and save its frozen volume
                var device = recordingDevices.FirstOrDefault(d => d.Id == deviceId) ?? 
                            playbackDevices.FirstOrDefault(d => d.Id == deviceId);
                
                if (device != null)
                {
                    SaveFrozenVolumeForDevice(device);
                }
            }
        }

        private void CleanupFrozenVolumesForDeselectedDevices(List<string> deselectedDeviceIds)
        {
            using var frozenKey = _registryService.OpenSubKey(FrozenVolumesKeyPath);
            if (frozenKey != null)
            {
                foreach (var deviceId in deselectedDeviceIds)
                {
                    // Only try to delete if the value actually exists
                    if (frozenKey.GetValue(deviceId) != null)
                    {
                        RemoveFrozenVolumeForDevice(deviceId);
                    }
                }
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Generate device fingerprint based on stable characteristics
        /// Creates a stable identifier based on device name, label, and audio capabilities
        /// </summary>
        private string GenerateDeviceFingerprint(AudioDeviceViewModel device)
        {
            // Return cached fingerprint if available
            if (!string.IsNullOrEmpty(device.CachedFingerprint))
            {
                return device.CachedFingerprint;
            }
            
            try
            {
                // Create stable identifier based on device characteristics that don't change with USB ports
                var fingerprintData = $"{device.Name?.Trim() ?? "Unknown"}|{device.Label?.Trim() ?? "Unknown"}|{device.MinVolumeDb:F1}|{device.MaxVolumeDb:F1}";
                
                // Use simple hash instead of expensive Base64 encoding for better performance
                var fingerprint = $"DEV_{Math.Abs(fingerprintData.GetHashCode()):X8}";
                
                // Cache the result for future calls
                device.CachedFingerprint = fingerprint;
                
                return fingerprint;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error generating fingerprint for device {device.Name}: {ex.Message}", LogLevel.Warning);
                return $"fallback_{device.Name?.Replace(" ", "_") ?? "unknown"}"; // Fallback fingerprint
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Build mapping of device fingerprints to current device IDs
        /// </summary>
        private Dictionary<string, string> BuildDeviceFingerprintMap(List<AudioDeviceViewModel> allDevices)
        {
            var fingerprintMap = new Dictionary<string, string>();
            
            foreach (var device in allDevices)
            {
                try
                {
                    var fingerprint = GenerateDeviceFingerprint(device);
                    if (!fingerprintMap.ContainsKey(fingerprint))
                    {
                        fingerprintMap[fingerprint] = device.Id;
                    }
                    else
                    {
                        _loggingService.Log($"Duplicate device fingerprint detected: {device.Name} and {fingerprintMap[fingerprint]}", LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.Log($"Error building fingerprint map for device {device.Name}: {ex.Message}", LogLevel.Warning);
                }
            }
            
            return fingerprintMap;
        }
        
        /// <summary>
        /// PHASE 3 FIX: Attempt to migrate orphaned device IDs using device fingerprints
        /// </summary>
        private List<string> AttemptDeviceIdMigration(string[] selectedIds, Dictionary<string, string> currentFingerprintMap)
        {
            var migratedIds = new List<string>();
            var migrationCount = 0;
            
            try
            {
                // Load saved device fingerprints from registry
                var savedFingerprints = LoadSavedDeviceFingerprints();
                
                foreach (var selectedId in selectedIds)
                {
                    // Check if the device ID still exists in current devices
                    bool deviceStillExists = currentFingerprintMap.ContainsValue(selectedId);
                    
                    if (deviceStillExists)
                    {
                        // Device ID hasn't changed, keep it
                        migratedIds.Add(selectedId);
                    }
                    else
                    {
                        // Device ID has changed, try to find it by fingerprint
                        if (savedFingerprints.TryGetValue(selectedId, out var savedFingerprint))
                        {
                            if (currentFingerprintMap.TryGetValue(savedFingerprint, out var newDeviceId))
                            {
                                // Found device with matching fingerprint but different ID
                                migratedIds.Add(newDeviceId);
                                migrationCount++;
                                
                                _loggingService.Log($"Device ID migration: {selectedId} → {newDeviceId} (fingerprint match)", LogLevel.Info);
                                
                                // Migrate frozen volume setting
                                MigrateFrozenVolumeForDevice(selectedId, newDeviceId);
                                
                                // Record the migration for future reference
                                RecordDeviceMigration(selectedId, newDeviceId, savedFingerprint);
                            }
                            else
                            {
                                _loggingService.Log($"Device {selectedId} not found - no matching fingerprint in current devices", LogLevel.Warning);
                            }
                        }
                        else
                        {
                            _loggingService.Log($"Device {selectedId} not found - no saved fingerprint available for migration", LogLevel.Warning);
                        }
                    }
                }
                
                if (migrationCount > 0)
                {
                    _loggingService.Log($"Device migration completed: {migrationCount} devices migrated to new IDs", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error during device ID migration: {ex.Message}", LogLevel.Error);
                // Return original IDs on error
                return selectedIds.ToList();
            }
            
            return migratedIds;
        }
        
        /// <summary>
        /// PHASE 3 FIX: Update device fingerprints in registry for future migration
        /// </summary>
        private void UpdateDeviceFingerprints(List<AudioDeviceViewModel> allDevices)
        {
            try
            {
                // Clear existing fingerprints by removing all values
                using (var fingerprintKey = _registryService.OpenSubKey(DeviceFingerprintsKeyPath))
                {
                    if (fingerprintKey != null)
                    {
                        var existingValues = fingerprintKey.GetValueNames();
                        foreach (var valueName in existingValues)
                        {
                            _registryService.DeleteValue(DeviceFingerprintsKeyPath, valueName);
                        }
                    }
                }
                
                // Save new fingerprints
                foreach (var device in allDevices)
                {
                    var fingerprint = GenerateDeviceFingerprint(device);
                    _registryService.SetValue(DeviceFingerprintsKeyPath, device.Id, fingerprint, Microsoft.Win32.RegistryValueKind.String);
                }
                
                _loggingService.Log($"Updated device fingerprints for {allDevices.Count} devices", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error updating device fingerprints: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Load saved device fingerprints from registry
        /// </summary>
        private Dictionary<string, string> LoadSavedDeviceFingerprints()
        {
            var fingerprints = new Dictionary<string, string>();
            
            try
            {
                using var key = _registryService.OpenSubKey(DeviceFingerprintsKeyPath);
                if (key != null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        var fingerprint = key.GetValue(valueName) as string;
                        if (!string.IsNullOrEmpty(fingerprint))
                        {
                            fingerprints[valueName] = fingerprint;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading saved device fingerprints: {ex.Message}", LogLevel.Warning);
            }
            
            return fingerprints;
        }
        
        /// <summary>
        /// PHASE 3 FIX: Migrate frozen volume from old device ID to new device ID
        /// </summary>
        private void MigrateFrozenVolumeForDevice(string oldDeviceId, string newDeviceId)
        {
            try
            {
                using var frozenKey = _registryService.OpenSubKey(FrozenVolumesKeyPath);
                if (frozenKey != null)
                {
                    var frozenVolume = frozenKey.GetValue(oldDeviceId) as string;
                    if (!string.IsNullOrEmpty(frozenVolume))
                    {
                        // Copy frozen volume to new device ID
                        _registryService.SetValue(FrozenVolumesKeyPath, newDeviceId, frozenVolume, Microsoft.Win32.RegistryValueKind.String);
                        
                        // Remove old frozen volume entry
                        _registryService.DeleteValue(FrozenVolumesKeyPath, oldDeviceId);
                        
                        _loggingService.Log($"Migrated frozen volume {frozenVolume}dB: {oldDeviceId} → {newDeviceId}", LogLevel.Debug);
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error migrating frozen volume for device {oldDeviceId}: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Record device migration for audit trail
        /// </summary>
        private void RecordDeviceMigration(string oldDeviceId, string newDeviceId, string fingerprint)
        {
            try
            {
                var migrationRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{oldDeviceId}|{newDeviceId}|{fingerprint}";
                _registryService.SetValue(DeviceMigrationKeyPath, Guid.NewGuid().ToString(), migrationRecord, Microsoft.Win32.RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error recording device migration: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Clean up orphaned registry entries for devices that no longer exist
        /// </summary>
        public void CleanupOrphanedRegistryEntries(List<AudioDeviceViewModel> currentDevices)
        {
            try
            {
                var currentDeviceIds = new HashSet<string>(currentDevices.Select(d => d.Id));
                var cleanedFrozenCount = 0;
                var cleanedFingerprintCount = 0;
                
                // Clean up orphaned frozen volume entries
                using (var frozenKey = _registryService.OpenSubKey(FrozenVolumesKeyPath))
                {
                    if (frozenKey != null)
                    {
                        var orphanedFrozenIds = frozenKey.GetValueNames()
                            .Where(deviceId => !currentDeviceIds.Contains(deviceId))
                            .ToList();
                        
                        foreach (var orphanedId in orphanedFrozenIds)
                        {
                            _registryService.DeleteValue(FrozenVolumesKeyPath, orphanedId);
                            cleanedFrozenCount++;
                        }
                    }
                }
                
                // Clean up orphaned fingerprint entries
                using (var fingerprintKey = _registryService.OpenSubKey(DeviceFingerprintsKeyPath))
                {
                    if (fingerprintKey != null)
                    {
                        var orphanedFingerprintIds = fingerprintKey.GetValueNames()
                            .Where(deviceId => !currentDeviceIds.Contains(deviceId))
                            .ToList();
                        
                        foreach (var orphanedId in orphanedFingerprintIds)
                        {
                            _registryService.DeleteValue(DeviceFingerprintsKeyPath, orphanedId);
                            cleanedFingerprintCount++;
                        }
                    }
                }
                
                if (cleanedFrozenCount > 0 || cleanedFingerprintCount > 0)
                {
                    _loggingService.Log($"Registry cleanup: removed {cleanedFrozenCount} orphaned frozen volumes, {cleanedFingerprintCount} orphaned fingerprints", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error during registry cleanup: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// Loads favorites for devices from registry
        /// </summary>
        private void LoadFavoritesForDevices(ObservableCollection<AudioDeviceViewModel> recordingDevices, ObservableCollection<AudioDeviceViewModel> playbackDevices)
        {
            try
            {
                // Load favorite IDs from registry
                var favoritesKeyPath = AppKeyPath + @"\Favorites";
                var idsJson = _registryService.GetValue(favoritesKeyPath, "FavoriteDeviceIds", string.Empty);
                if (string.IsNullOrEmpty(idsJson))
                {
                    _loggingService.Log("No favorites found in registry");
                    return;
                }
                
                var favoriteIds = JsonSerializer.Deserialize<string[]>(idsJson) ?? Array.Empty<string>();
                var favoriteIdSet = new HashSet<string>(favoriteIds);
                
                // Load sort order
                Dictionary<string, int>? orderDict = null;
                var orderJson = _registryService.GetValue(favoritesKeyPath, "FavoriteOrder", string.Empty);
                if (!string.IsNullOrEmpty(orderJson))
                {
                    orderDict = JsonSerializer.Deserialize<Dictionary<string, int>>(orderJson);
                }
                
                // Load new column-based positions
                Dictionary<string, FavoritePositionData>? positionsDict = null;
                var positionsJson = _registryService.GetValue(favoritesKeyPath, "FavoritePositions", string.Empty);
                if (!string.IsNullOrEmpty(positionsJson))
                {
                    positionsDict = JsonSerializer.Deserialize<Dictionary<string, FavoritePositionData>>(positionsJson);
                }
                
                // Apply favorites to all devices (while IsLoadingDevices is still true)
                int appliedCount = 0;
                bool needsMigration = positionsDict == null && orderDict != null;
                var allDevices = recordingDevices.Concat(playbackDevices);
                
                foreach (var device in allDevices)
                {
                    if (favoriteIdSet.Contains(device.Id))
                    {
                        device.IsFavorite = true;
                        
                        // Apply new column-based positions if available
                        if (positionsDict?.ContainsKey(device.Id) == true)
                        {
                            var posData = positionsDict[device.Id];
                            device.FavoriteColumn = Enum.Parse<Models.FavoriteColumnType>(posData.Column);
                            device.FavoritePosition = posData.Position;
                        }
                        else if (orderDict?.ContainsKey(device.Id) == true)
                        {
                            // Migration: Convert old order to column/position
                            device.FavoriteOrder = orderDict[device.Id];
                            // Distribute alternately between columns based on order
                            device.FavoriteColumn = device.FavoriteOrder % 2 == 0 ? Models.FavoriteColumnType.Left : Models.FavoriteColumnType.Right;
                            device.FavoritePosition = device.FavoriteOrder / 2;
                        }
                        else
                        {
                            // Auto-assign order and column/position if not found
                            device.FavoriteOrder = appliedCount;
                            device.FavoriteColumn = appliedCount % 2 == 0 ? Models.FavoriteColumnType.Left : Models.FavoriteColumnType.Right;
                            device.FavoritePosition = appliedCount / 2;
                        }
                        
                        appliedCount++;
                    }
                }
                
                _loggingService.Log($"Loaded {appliedCount} favorites from registry during device selection load");
                if (needsMigration && appliedCount > 0)
                {
                    _loggingService.Log("Migrated favorites from old order format to new column/position format");
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to load favorites during device selection: {ex.Message}", LogLevel.Error);
            }
        }
    }
}