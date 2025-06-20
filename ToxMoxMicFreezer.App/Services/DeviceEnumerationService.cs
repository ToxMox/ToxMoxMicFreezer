// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for enumerating audio devices
    /// Handles device discovery and initial setup
    /// </summary>
    public class DeviceEnumerationService : IDeviceEnumerationService
    {
        private readonly ILoggingService _loggingService;

        public DeviceEnumerationService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Enumerates recording devices using NAudio
        /// </summary>
        public List<AudioDeviceViewModel> EnumerateRecordingDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState)
        {
            return EnumerateDevicesByType(true, currentActiveDevices, nativeDeviceCache, persistentSelectionState);
        }

        /// <summary>
        /// Enumerates playback devices using NAudio
        /// </summary>
        public List<AudioDeviceViewModel> EnumeratePlaybackDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState)
        {
            return EnumerateDevicesByType(false, currentActiveDevices, nativeDeviceCache, persistentSelectionState);
        }

        /// <summary>
        /// Core enumeration logic for both recording and playback devices
        /// </summary>
        private List<AudioDeviceViewModel> EnumerateDevicesByType(
            bool isRecording, 
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState)
        {
            string dataFlowName = isRecording ? "Capture" : "Render";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var deviceList = new List<AudioDeviceViewModel>();

            try
            {
                _loggingService.Log($"[DEVICE_ENUM] Starting NAudio {dataFlowName} device enumeration", LogLevel.Debug);

                // Use NAudio device enumerator for stable COM management
                using var enumerator = new NAudioDeviceEnumerator();
                uint dataFlow = isRecording ? (uint)NAudio.CoreAudioApi.DataFlow.Capture : (uint)NAudio.CoreAudioApi.DataFlow.Render;
                var devices = enumerator.EnumerateAudioDevices(dataFlow);

                stopwatch.Stop();
                stopwatch.Restart();

                // Pre-allocate for performance
                deviceList.Capacity = Math.Max(deviceList.Capacity, devices.Count);

                int deviceIndex = 0;
                foreach (var device in devices)
                {
                    try
                    {
                        deviceIndex++;

                        if (device == null)
                            continue;

                        string deviceId = device.Id;
                        string friendlyName = device.FriendlyName;

                        // Fast validation
                        if (string.IsNullOrWhiteSpace(friendlyName) || string.IsNullOrWhiteSpace(deviceId))
                            continue;

                        currentActiveDevices.Add(deviceId);

                        // Fast name parsing
                        string name = friendlyName;
                        string label = string.Empty;

                        int openParen = friendlyName.LastIndexOf('(');
                        int closeParen = friendlyName.LastIndexOf(')');

                        if (openParen > 0 && closeParen > openParen)
                        {
                            name = friendlyName.Substring(0, openParen).Trim();
                            label = friendlyName.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                        }

                        // Skip devices with empty names after parsing
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(label))
                            continue;

                        // Get volume info efficiently
                        var volumeRange = device.GetVolumeRange();
                        float volumeLevel = device.GetVolumeLevel();
                        string volumeDbStr = volumeLevel.ToString("F1");
                        
                        // Get mute state from the device
                        bool isMuted = device.GetMuteState();
                        
                        // Get channel count for stereo metering
                        int channels = device.GetChannelCount();

                        var vm = new AudioDeviceViewModel
                        {
                            Id = deviceId,
                            Name = name,
                            Label = label,
                            VolumeDb = volumeDbStr,
                            MinVolumeDb = volumeRange.min,
                            MaxVolumeDb = volumeRange.max,
                            DeviceType = isRecording ? AudioDeviceType.Recording : AudioDeviceType.Playback,
                            IsMuted = isMuted,
                            Channels = channels,
                            IsMultiChannel = channels > 2  // Set multi-channel flag during enumeration
                        };
                        
                        // Suppress SelectionChanged events during device enumeration to prevent unwanted registry saves
                        AudioDeviceViewModel.IsLoadingDevices = true;
                        try
                        {
                            vm.IsSelected = persistentSelectionState.Contains(deviceId);
                        }
                        finally
                        {
                            AudioDeviceViewModel.IsLoadingDevices = false;
                        }

                        deviceList.Add(vm);

                        // CRITICAL: Add device to native cache for volume change callbacks
                        nativeDeviceCache[deviceId] = device;

                        // Single clean log message per device
                        string channelInfo = channels > 2 ? $"{channels} channels (multi-channel, metering disabled)" : $"{channels} channel{(channels > 1 ? "s" : "")}";
                        _loggingService.Log($"Added device: {name} ({label}) - {channelInfo}", LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        _loggingService.Log($"Error processing device {deviceIndex}: {ex.Message}", LogLevel.Warning);
                    }
                }

                stopwatch.Stop();
                _loggingService.Log($"Found {deviceList.Count} {dataFlowName.ToLower()} devices", LogLevel.Info);
                return deviceList;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _loggingService.Log($"Critical error during {dataFlowName.ToLower()} device enumeration: {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}