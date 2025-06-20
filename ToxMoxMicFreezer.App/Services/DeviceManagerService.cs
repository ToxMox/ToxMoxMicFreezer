// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Coordinating service that manages all device-related operations
    /// Orchestrates device enumeration, collection management, and event handling
    /// </summary>
    public class DeviceManagerService : IDeviceManagerService
    {
        private readonly IDeviceEnumerationService _enumerationService;
        private readonly IDeviceCollectionService _collectionService;
        private readonly INativeDeviceAccessService _nativeDeviceService;
        private readonly ILoggingService _loggingService;
        private bool _disposed = false;

        public DeviceManagerService(
            IDeviceEnumerationService enumerationService,
            IDeviceCollectionService collectionService,
            INativeDeviceAccessService nativeDeviceService,
            ILoggingService loggingService)
        {
            _enumerationService = enumerationService ?? throw new ArgumentNullException(nameof(enumerationService));
            _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
            _nativeDeviceService = nativeDeviceService ?? throw new ArgumentNullException(nameof(nativeDeviceService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Performs complete device enumeration and collection updates
        /// </summary>
        public (int recordingCount, int playbackCount, int removedCount) EnumerateAndUpdateDevices(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState)
        {
            if (_disposed) return (0, 0, 0);

            var startTime = DateTime.Now;
            var currentActiveDevices = new HashSet<string>();

            try
            {

                // Enumerate recording devices
                var recordingDeviceList = _enumerationService.EnumerateRecordingDevices(
                    currentActiveDevices, nativeDeviceCache, persistentSelectionState);

                // Enumerate playback devices
                var playbackDeviceList = _enumerationService.EnumeratePlaybackDevices(
                    currentActiveDevices, nativeDeviceCache, persistentSelectionState);

                // Sort the devices (matching original sorting behavior)
                recordingDeviceList = recordingDeviceList.OrderBy(d => d.Label.ToLowerInvariant())
                                                       .ThenBy(d => d.Name.ToLowerInvariant())
                                                       .ToList();
                
                playbackDeviceList = playbackDeviceList.OrderBy(d => d.Label.ToLowerInvariant())
                                                     .ThenBy(d => d.Name.ToLowerInvariant())
                                                     .ToList();

                // Remove stale devices
                int removedCount = _collectionService.RemoveStaleDevices(
                    currentActiveDevices, deviceCache, nativeDeviceCache, recordingDevices, playbackDevices);

                // Update device collections
                _collectionService.UpdateRecordingDevices(
                    recordingDeviceList, recordingDevices, deviceCache, nativeDeviceCache, persistentSelectionState);

                _collectionService.UpdatePlaybackDevices(
                    playbackDeviceList, playbackDevices, deviceCache, nativeDeviceCache);

                var elapsed = DateTime.Now - startTime;
                _loggingService.Log($"[DEVICE_MANAGER] Enumeration complete - {recordingDeviceList.Count} recording, {playbackDeviceList.Count} playback, {removedCount} removed (elapsed: {elapsed.TotalMilliseconds:F0}ms)", LogLevel.Info);

                return (recordingDeviceList.Count, playbackDeviceList.Count, removedCount);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[DEVICE_MANAGER] Error during device enumeration: {ex.Message}", LogLevel.Error);
                throw;
            }
        }


        /// <summary>
        /// Gets a native audio device by ID
        /// </summary>
        public NAudioAudioDevice? GetCachedNativeDevice(string deviceId)
        {
            return _nativeDeviceService.GetDevice(deviceId);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}