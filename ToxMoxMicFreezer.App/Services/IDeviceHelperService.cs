// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for device utility methods and helper functions
    /// Provides safe device access checking and obsolete volume methods
    /// Note: Volume calculations are handled by VolumeEnforcementService, device ID parsing by NativeDeviceAccessService
    /// </summary>
    public interface IDeviceHelperService
    {
        /// <summary>
        /// Safely checks if a device is accessible with COM exception handling
        /// </summary>
        /// <param name="device">The audio device to check</param>
        /// <returns>True if device is accessible and active, false otherwise</returns>
        bool IsDeviceAccessible(NAudioAudioDevice device);

        /// <summary>
        /// Safely gets device volume with COM exception handling (marked obsolete)
        /// </summary>
        /// <param name="device">The audio device</param>
        /// <param name="volumeLevel">Output volume level</param>
        /// <returns>True if successful, false on error</returns>
        [System.Obsolete("Use native device methods instead")]
        bool TryGetDeviceVolumeObsolete(NAudioAudioDevice device, out float volumeLevel);
    }
}