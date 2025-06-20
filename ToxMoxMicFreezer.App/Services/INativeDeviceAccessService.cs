// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for providing access to native audio devices
    /// Implements caching to eliminate COM object creation overhead during volume bar dragging
    /// </summary>
    public interface INativeDeviceAccessService : IDisposable
    {
        /// <summary>
        /// Gets a native audio device by ID with caching support
        /// </summary>
        /// <param name="deviceId">Device ID to retrieve</param>
        /// <returns>NAudioAudioDevice instance or null if not found</returns>
        NAudioAudioDevice? GetDevice(string deviceId);

        /// <summary>
        /// Extracts the base device ID (without endpoint information)
        /// </summary>
        /// <param name="fullDeviceId">Full device ID with endpoint</param>
        /// <returns>Base device ID</returns>
        string GetBaseDeviceId(string fullDeviceId);

        /// <summary>
        /// Clears all cached devices - called during device hot-plug events
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Removes a specific device from the cache
        /// </summary>
        /// <param name="deviceId">Device ID to remove from cache</param>
        void RemoveFromCache(string deviceId);

        /// <summary>
        /// Gets the current cache count for diagnostic purposes
        /// </summary>
        /// <returns>Number of cached devices</returns>
        int GetCacheCount();
    }
}