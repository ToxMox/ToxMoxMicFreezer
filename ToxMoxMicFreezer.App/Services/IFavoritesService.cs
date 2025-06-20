// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Collections.Generic;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service for managing device favorites persistence
    /// </summary>
    public interface IFavoritesService
    {
        /// <summary>
        /// Saves the current favorites and their sort order to registry
        /// </summary>
        void SaveFavorites(IEnumerable<AudioDeviceViewModel> allDevices);
        
        /// <summary>
        /// Loads favorites from registry and applies to devices
        /// </summary>
        void LoadFavorites(IEnumerable<AudioDeviceViewModel> allDevices);
        
        /// <summary>
        /// Updates the sort order for a specific favorite device
        /// </summary>
        void UpdateFavoriteOrder(string deviceId, int newOrder);
        
        /// <summary>
        /// Clears all favorites data from registry
        /// </summary>
        void ClearAllFavorites();
    }
}