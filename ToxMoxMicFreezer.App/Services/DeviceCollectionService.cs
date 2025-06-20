// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing audio device collections
    /// Handles ObservableCollection updates and device organization
    /// </summary>
    public class DeviceCollectionService : IDeviceCollectionService
    {
        private readonly ILoggingService _loggingService;

        public DeviceCollectionService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Updates the recording device collection with new devices
        /// </summary>
        public void UpdateRecordingDevices(
            List<AudioDeviceViewModel> deviceList,
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState)
        {
            // Clear existing recording devices
            ClearDeviceCollection(recordingDevices, deviceCache, nativeDeviceCache, true);

            // Add new recording devices
            int addedCount = 0;
            foreach (var device in deviceList)
            {
                // Skip devices with empty or invalid data
                if (string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.Name))
                    continue;

                // Create new view model for recording device
                var vm = new AudioDeviceViewModel
                {
                    Id = device.Id,
                    Name = device.Name,
                    Label = device.Label,
                    VolumeDb = device.VolumeDb,
                    MinVolumeDb = device.MinVolumeDb,
                    MaxVolumeDb = device.MaxVolumeDb,
                    DeviceType = device.DeviceType,
                    IsMuted = device.IsMuted,
                    Channels = device.Channels,
                    IsMultiChannel = device.IsMultiChannel // CRITICAL: Copy multi-channel flag for UI
                };
                
                // Suppress SelectionChanged events during device creation to prevent unwanted registry saves
                AudioDeviceViewModel.IsLoadingDevices = true;
                try
                {
                    vm.IsSelected = persistentSelectionState.Contains(device.Id);
                }
                finally
                {
                    AudioDeviceViewModel.IsLoadingDevices = false;
                }

                // Add to device cache and collection
                deviceCache.TryAdd(device.Id, vm);
                recordingDevices.Add(vm);
                addedCount++;
            }

        }

        /// <summary>
        /// Updates the playback device collection with new devices
        /// </summary>
        public void UpdatePlaybackDevices(
            List<AudioDeviceViewModel> deviceList,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache)
        {
            // Clear existing playback devices
            ClearDeviceCollection(playbackDevices, deviceCache, nativeDeviceCache, false);

            // Add new playback devices
            int addedCount = 0;
            foreach (var device in deviceList)
            {
                // Skip devices with empty or invalid data
                if (string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.Name))
                    continue;

                // Add directly to cache and collection (device already has correct data)
                deviceCache.TryAdd(device.Id, device);
                playbackDevices.Add(device);
                addedCount++;
            }

        }

        /// <summary>
        /// Removes stale devices from cache and collections
        /// </summary>
        public int RemoveStaleDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices)
        {
            var devicesToRemove = new List<string>();

            // Find devices in cache that are no longer active
            foreach (var cachedDevice in deviceCache.Values)
            {
                if (!currentActiveDevices.Contains(cachedDevice.Id))
                {
                    devicesToRemove.Add(cachedDevice.Id);
                }
            }

            // Remove each stale device
            foreach (var deviceId in devicesToRemove)
            {
                if (deviceCache.TryRemove(deviceId, out var device))
                {
                    bool recordingRemoved = recordingDevices.Remove(device);
                    bool playbackRemoved = playbackDevices.Remove(device);
                    if (recordingRemoved || playbackRemoved)
                    {
                        _loggingService.Log($"Removed device: {device.Name}", LogLevel.Info);
                    }
                }

                // Also clean up native device cache
                if (nativeDeviceCache.TryRemove(deviceId, out var nativeDevice))
                {
                    nativeDevice.Dispose();
                }
            }

            if (devicesToRemove.Count > 0)
            {
                _loggingService.Log($"Cleaned up {devicesToRemove.Count} stale devices", LogLevel.Info);
            }

            return devicesToRemove.Count;
        }

        /// <summary>
        /// Clears a device collection and performs cleanup
        /// </summary>
        private void ClearDeviceCollection(
            ObservableCollection<AudioDeviceViewModel> deviceCollection,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            bool isRecording)
        {
            string collectionType = isRecording ? "recording" : "playback";

            // Clear and dispose devices - but DON'T remove from native cache during clear
            // The native cache will be properly managed during the full refresh process
            foreach (var device in deviceCollection.ToList())
            {
                deviceCache.TryRemove(device.Id, out _);
                // NOTE: Don't remove from native cache here - it's shared between recording/playback
                // and will be properly cleaned up during the RemoveStaleDevices phase
            }
            deviceCollection.Clear();

        }
    }
}