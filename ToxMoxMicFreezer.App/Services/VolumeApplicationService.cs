// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Threading.Tasks;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for applying volume changes to audio devices
    /// Handles the interaction with the audio system to set device volumes
    /// </summary>
    public class VolumeApplicationService : IVolumeApplicationService
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;
        private readonly IVolumeCalculationService _volumeCalculationService;

        public VolumeApplicationService(
            MainWindow mainWindow,
            ILoggingService loggingService,
            IVolumeCalculationService volumeCalculationService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _volumeCalculationService = volumeCalculationService ?? throw new ArgumentNullException(nameof(volumeCalculationService));
        }

        /// <summary>
        /// Sets the volume for a specific device
        /// </summary>
        public bool SetDeviceVolume(string deviceId, float volumeDb)
        {
            return SetDeviceVolumeInternal(deviceId, volumeDb);
        }

        /// <summary>
        /// Sets the volume for a device with internal caching and optimization
        /// </summary>
        public bool SetDeviceVolumeInternal(string deviceId, float volumeDb, NAudioAudioDevice? cachedDevice = null)
        {
            try
            {
                // Clamp volume to valid range
                float clampedVolume = _volumeCalculationService.ClampVolume(volumeDb);

                // Use cached device if provided, otherwise get from device manager
                var nativeDevice = cachedDevice ?? _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null)
                {
                    _loggingService.Log($"Failed to get native device for volume change: {deviceId}", LogLevel.Warning);
                    return false;
                }

                // Set the volume using NAudio
                nativeDevice.SetVolumeLevel(clampedVolume);
                
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error setting device volume {deviceId}: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Applies a volume change and updates the UI immediately
        /// </summary>
        public bool ApplyVolumeChange(AudioDeviceViewModel device, float volumeDb)
        {
            try
            {
                // Clamp volume to valid range
                float clampedVolume = _volumeCalculationService.ClampVolume(volumeDb);

                // Update device model immediately for responsive UI
                device.VolumeDb = clampedVolume.ToString("F1");

                // Apply to actual audio device
                bool success = SetDeviceVolume(device.Id, clampedVolume);
                
                if (success)
                {
                }
                else
                {
                    _loggingService.Log($"Failed to apply volume change for {device.Name}", LogLevel.Warning);
                }

                return success;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error applying volume change for device {device.Id}: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Validates if a volume change is allowed for the device
        /// </summary>
        public bool CanChangeDeviceVolume(string deviceId)
        {
            try
            {
                // Check if device exists and is available
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null)
                {
                    return false;
                }

                // Additional validation can be added here (e.g., device state, permissions)
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error validating volume change permission for device {deviceId}: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }
    }
}