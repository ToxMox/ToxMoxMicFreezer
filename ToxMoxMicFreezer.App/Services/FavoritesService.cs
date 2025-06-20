// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Helper class for storing favorite position data
    /// </summary>
    public class FavoritePositionData
    {
        public string Column { get; set; } = "Left";
        public int Position { get; set; } = 0;
    }
    /// <summary>
    /// Service for managing device favorites persistence in registry
    /// </summary>
    public class FavoritesService : IFavoritesService
    {
        private readonly IRegistryService _registryService;
        private readonly ILoggingService _loggingService;
        
        private const string AppKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string FavoritesKeyPath = AppKeyPath + @"\Favorites";
        private const string FavoriteDevicesValueName = "FavoriteDeviceIds";
        private const string FavoriteOrderValueName = "FavoriteOrder";
        private const string FavoritePositionsValueName = "FavoritePositions";

        public FavoritesService(IRegistryService registryService, ILoggingService loggingService)
        {
            _registryService = registryService;
            _loggingService = loggingService;
        }

        /// <summary>
        /// Saves the current favorites and their sort order to registry
        /// </summary>
        public void SaveFavorites(IEnumerable<AudioDeviceViewModel> allDevices)
        {
            try
            {
                var favoriteDevices = allDevices.Where(d => d.IsFavorite).ToList();
                
                if (!favoriteDevices.Any())
                {
                    // Clear favorites if none exist
                    _registryService.DeleteValue(FavoritesKeyPath, FavoriteDevicesValueName);
                    _registryService.DeleteValue(FavoritesKeyPath, FavoriteOrderValueName);
                    _loggingService.Log("Cleared all favorites from registry");
                    return;
                }
                
                // Save favorite device IDs as JSON array
                var favoriteIds = favoriteDevices.Select(d => d.Id).ToArray();
                var idsJson = JsonSerializer.Serialize(favoriteIds);
                _registryService.SetValue(FavoritesKeyPath, FavoriteDevicesValueName, idsJson, RegistryValueKind.String);
                
                // Save sort order as JSON dictionary (keep for backward compatibility)
                var orderDict = favoriteDevices.ToDictionary(d => d.Id, d => d.FavoriteOrder);
                var orderJson = JsonSerializer.Serialize(orderDict);
                _registryService.SetValue(FavoritesKeyPath, FavoriteOrderValueName, orderJson, RegistryValueKind.String);
                
                // Save new column-based positions as JSON dictionary
                var positionsDict = favoriteDevices.ToDictionary(
                    d => d.Id,
                    d => new FavoritePositionData 
                    { 
                        Column = d.FavoriteColumn.ToString(),
                        Position = d.FavoritePosition 
                    }
                );
                var positionsJson = JsonSerializer.Serialize(positionsDict);
                _registryService.SetValue(FavoritesKeyPath, FavoritePositionsValueName, positionsJson, RegistryValueKind.String);
                
                _loggingService.Log($"Saved {favoriteDevices.Count} favorite devices with positions to registry");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to save favorites: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads favorites from registry and applies to devices
        /// </summary>
        public void LoadFavorites(IEnumerable<AudioDeviceViewModel> allDevices)
        {
            try
            {
                // Load favorite IDs
                var idsJson = _registryService.GetValue(FavoritesKeyPath, FavoriteDevicesValueName, string.Empty);
                if (string.IsNullOrEmpty(idsJson))
                {
                    _loggingService.Log("No favorites found in registry");
                    return;
                }
                
                var favoriteIds = JsonSerializer.Deserialize<string[]>(idsJson) ?? Array.Empty<string>();
                var favoriteIdSet = new HashSet<string>(favoriteIds);
                
                // Load sort order
                Dictionary<string, int>? orderDict = null;
                var orderJson = _registryService.GetValue(FavoritesKeyPath, FavoriteOrderValueName, string.Empty);
                if (!string.IsNullOrEmpty(orderJson))
                {
                    orderDict = JsonSerializer.Deserialize<Dictionary<string, int>>(orderJson);
                }
                
                // Load new column-based positions
                Dictionary<string, FavoritePositionData>? positionsDict = null;
                var positionsJson = _registryService.GetValue(FavoritesKeyPath, FavoritePositionsValueName, string.Empty);
                if (!string.IsNullOrEmpty(positionsJson))
                {
                    positionsDict = JsonSerializer.Deserialize<Dictionary<string, FavoritePositionData>>(positionsJson);
                }
                
                // Apply favorites to devices
                int appliedCount = 0;
                bool needsMigration = positionsDict == null && orderDict != null;
                
                // Suppress change events during loading
                AudioDeviceViewModel.IsLoadingDevices = true;
                try
                {
                    foreach (var device in allDevices)
                    {
                        if (favoriteIdSet.Contains(device.Id))
                        {
                            device.IsFavorite = true;
                            
                            // Apply new column-based positions if available
                            if (positionsDict?.ContainsKey(device.Id) == true)
                            {
                                var posData = positionsDict[device.Id];
                                device.FavoriteColumn = Enum.Parse<FavoriteColumnType>(posData.Column);
                                device.FavoritePosition = posData.Position;
                            }
                            else if (orderDict?.ContainsKey(device.Id) == true)
                            {
                                // Migration: Convert old order to column/position
                                device.FavoriteOrder = orderDict[device.Id];
                                // Distribute alternately between columns based on order
                                device.FavoriteColumn = device.FavoriteOrder % 2 == 0 ? FavoriteColumnType.Left : FavoriteColumnType.Right;
                                device.FavoritePosition = device.FavoriteOrder / 2;
                            }
                            else
                            {
                                // Auto-assign if not found
                                device.FavoriteOrder = appliedCount;
                                device.FavoriteColumn = appliedCount % 2 == 0 ? FavoriteColumnType.Left : FavoriteColumnType.Right;
                                device.FavoritePosition = appliedCount / 2;
                            }
                            
                            appliedCount++;
                        }
                    }
                }
                finally
                {
                    AudioDeviceViewModel.IsLoadingDevices = false;
                }
                
                _loggingService.Log($"Loaded {appliedCount} favorites from registry");
                
                // Save migrated positions if needed
                if (needsMigration && appliedCount > 0)
                {
                    _loggingService.Log("Migrating favorites to new column-based format");
                    SaveFavorites(allDevices);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to load favorites: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Updates the sort order for a specific favorite device
        /// </summary>
        public void UpdateFavoriteOrder(string deviceId, int newOrder)
        {
            try
            {
                // Load existing order
                var orderJson = _registryService.GetValue(FavoritesKeyPath, FavoriteOrderValueName, string.Empty);
                Dictionary<string, int> orderDict;
                
                if (!string.IsNullOrEmpty(orderJson))
                {
                    orderDict = JsonSerializer.Deserialize<Dictionary<string, int>>(orderJson) ?? new Dictionary<string, int>();
                }
                else
                {
                    orderDict = new Dictionary<string, int>();
                }
                
                // Update order
                orderDict[deviceId] = newOrder;
                
                // Save back to registry
                var updatedJson = JsonSerializer.Serialize(orderDict);
                _registryService.SetValue(FavoritesKeyPath, FavoriteOrderValueName, updatedJson, RegistryValueKind.String);
                
                _loggingService.Log($"Updated favorite order for device {deviceId} to position {newOrder}");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to update favorite order: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Clears all favorites data from registry
        /// </summary>
        public void ClearAllFavorites()
        {
            try
            {
                _registryService.DeleteValue(FavoritesKeyPath, FavoriteDevicesValueName);
                _registryService.DeleteValue(FavoritesKeyPath, FavoriteOrderValueName);
                _loggingService.Log("Cleared all favorites from registry");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to clear favorites: {ex.Message}", LogLevel.Error);
            }
        }
    }
}