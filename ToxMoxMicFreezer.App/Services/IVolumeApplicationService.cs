// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for applying volume changes to audio devices
    /// Handles the interaction with the audio system to set device volumes
    /// </summary>
    public interface IVolumeApplicationService
    {
        /// <summary>
        /// Sets the volume for a specific device
        /// </summary>
        /// <param name="deviceId">ID of the device to set volume for</param>
        /// <param name="volumeDb">Volume in dB to set</param>
        /// <returns>True if volume was set successfully</returns>
        bool SetDeviceVolume(string deviceId, float volumeDb);

        /// <summary>
        /// Sets the volume for a device with internal caching and optimization
        /// </summary>
        /// <param name="deviceId">ID of the device to set volume for</param>
        /// <param name="volumeDb">Volume in dB to set</param>
        /// <param name="cachedDevice">Cached native device for performance</param>
        /// <returns>True if volume was set successfully</returns>
        bool SetDeviceVolumeInternal(string deviceId, float volumeDb, NAudioAudioDevice? cachedDevice = null);

        /// <summary>
        /// Applies a volume change and updates the UI immediately
        /// </summary>
        /// <param name="device">Device to update</param>
        /// <param name="volumeDb">Volume in dB to set</param>
        /// <returns>True if volume was applied successfully</returns>
        bool ApplyVolumeChange(AudioDeviceViewModel device, float volumeDb);

        /// <summary>
        /// Validates if a volume change is allowed for the device
        /// </summary>
        /// <param name="deviceId">Device ID to check</param>
        /// <returns>True if volume change is allowed</returns>
        bool CanChangeDeviceVolume(string deviceId);
    }
}