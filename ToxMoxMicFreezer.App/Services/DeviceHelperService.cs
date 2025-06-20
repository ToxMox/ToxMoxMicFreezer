// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Runtime.InteropServices;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service providing device utility methods and helper functions
    /// Handles safe device access checking and obsolete volume methods
    /// Note: Volume calculations are handled by VolumeEnforcementService, device ID parsing by NativeDeviceAccessService
    /// </summary>
    public class DeviceHelperService : IDeviceHelperService
    {
        private readonly ILoggingService _loggingService;

        public DeviceHelperService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Safely checks if a device is accessible with COM exception handling
        /// </summary>
        /// <param name="device">The audio device to check</param>
        /// <returns>True if device is accessible and active, false otherwise</returns>
        public bool IsDeviceAccessible(NAudioAudioDevice device)
        {
            try
            {
                // Test basic access to device properties
                _ = device.FriendlyName;
                _ = device.State;
                
                // Only consider active audio devices
                return device.IsActive;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80070002))
            {
                // Device not found (ERROR_FILE_NOT_FOUND)
                return false;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80070005))
            {
                // Access denied (ERROR_ACCESS_DENIED)
                return false;
            }
            catch (COMException)
            {
                // Other COM exceptions - device likely inaccessible
                return false;
            }
            catch (Exception ex)
            {
                // Any other exception - treat as inaccessible
                _loggingService.Log($"Device accessibility check failed: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }

        /// <summary>
        /// Safely gets device volume with COM exception handling (marked obsolete)
        /// </summary>
        /// <param name="device">The audio device</param>
        /// <param name="volumeLevel">Output volume level</param>
        /// <returns>True if successful, false on error</returns>
        [System.Obsolete("Use native device methods instead")]
        public bool TryGetDeviceVolumeObsolete(NAudioAudioDevice device, out float volumeLevel)
        {
            volumeLevel = 0.0f;
            try
            {
                volumeLevel = device.GetVolumeLevel();
                return true;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80070002))
            {
                // Device not found - likely disconnected
                return false;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80070005))
            {
                // Access denied
                return false;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                // AUDCLNT_E_DEVICE_INVALIDATED - device removed
                return false;
            }
            catch (Exception ex)
            {
                // Other exceptions
                _loggingService.Log($"Error reading device volume: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }
    }
}