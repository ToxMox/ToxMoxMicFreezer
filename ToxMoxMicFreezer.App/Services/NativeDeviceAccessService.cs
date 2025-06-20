// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for providing access to native audio devices
    /// Implements true caching to eliminate COM object creation overhead during volume bar dragging
    /// </summary>
    public class NativeDeviceAccessService : INativeDeviceAccessService, IDisposable
    {
        private readonly ILoggingService _loggingService;
        private readonly Dictionary<string, NAudioAudioDevice> _nativeDeviceCache = new();
        private readonly object _cacheLock = new();
        private bool _disposed = false;

        public NativeDeviceAccessService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Gets a native audio device by ID with proper caching to eliminate COM overhead
        /// </summary>
        public NAudioAudioDevice? GetDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _loggingService.Log("GetDevice called with null or empty deviceId", LogLevel.Warning);
                return null;
            }

            if (_disposed)
            {
                _loggingService.Log("GetDevice called on disposed service", LogLevel.Warning);
                return null;
            }

            lock (_cacheLock)
            {
                // Check if cached
                if (_nativeDeviceCache.TryGetValue(deviceId, out var cachedDevice))
                {
                    return cachedDevice;
                }

                // Create new device and cache it
                try
                {
                    using var enumerator = new NAudioDeviceEnumerator();
                    var device = enumerator.GetDevice(deviceId);
                    if (device != null)
                    {
                        _nativeDeviceCache[deviceId] = device;
                        _loggingService.Log($"Cached native device: {deviceId}", LogLevel.Debug);
                    }
                    else
                    {
                        _loggingService.Log($"Device not found: {deviceId}", LogLevel.Debug);
                    }
                    
                    return device;
                }
                catch (Exception ex)
                {
                    _loggingService.Log($"Error retrieving device {deviceId}: {ex.Message}", LogLevel.Warning);
                    return null;
                }
            }
        }

        /// <summary>
        /// Extracts the base device ID (without endpoint information)
        /// </summary>
        public string GetBaseDeviceId(string fullDeviceId)
        {
            if (string.IsNullOrEmpty(fullDeviceId))
                return string.Empty;

            int lastSeparator = fullDeviceId.LastIndexOf('#');
            if (lastSeparator > 0)
            {
                return fullDeviceId.Substring(0, lastSeparator);
            }

            return fullDeviceId;
        }

        /// <summary>
        /// Clears all cached devices
        /// Called during device hot-plug events to ensure cache consistency
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                // Dispose cached devices
                foreach (var device in _nativeDeviceCache.Values)
                {
                    device?.Dispose();
                }
                _nativeDeviceCache.Clear();
                
                _loggingService.Log("Native device cache cleared", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Removes a specific device from the cache
        /// Called when a device is no longer available
        /// </summary>
        public void RemoveFromCache(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

            lock (_cacheLock)
            {
                if (_nativeDeviceCache.TryGetValue(deviceId, out var device))
                {
                    device?.Dispose();
                    _nativeDeviceCache.Remove(deviceId);
                    
                    _loggingService.Log($"Removed device from cache: {deviceId}", LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Gets the current cache statistics for diagnostic purposes
        /// </summary>
        public int GetCacheCount()
        {
            lock (_cacheLock)
            {
                return _nativeDeviceCache.Count;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ClearCache();
            _disposed = true;
            _loggingService.Log("NativeDeviceAccessService disposed", LogLevel.Debug);
        }
    }
}