// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Linq;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    public class VolumeEnforcementService : IVolumeEnforcementService
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;

        public event EventHandler<VolumeChangeEvent>? VolumeEnforced;

        public VolumeEnforcementService(MainWindow mainWindow, ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void ProcessInternalVolumeChange(VolumeChangeEvent volumeEvent)
        {
            // Internal changes are user-initiated and don't need enforcement
        }

        public void ProcessExternalVolumeChange(VolumeChangeEvent volumeEvent)
        {
            // Only process frozen devices for external changes
            if (volumeEvent.IsDeviceFrozen) 
            {
                // Enforce the frozen volume if not paused
                if (!_mainWindow.PauseManager.IsPaused)
                {
                    EnforceVolumeForDevice(volumeEvent.DeviceId);
                }
            }
        }

        public float CalculateTargetVolume(NAudioAudioDevice device, AudioDeviceViewModel deviceViewModel)
        {
            try
            {
                var (minDb, maxDb, _) = device.GetVolumeRange();
                
                // Use the frozen volume instead of always targeting 0dB
                float targetDb = deviceViewModel.FrozenVolumeDb;
                
                // Clamp the target volume to the device's supported range
                targetDb = Math.Max(minDb, Math.Min(maxDb, targetDb));
                
                return targetDb;
            }
            catch
            {
                // Fallback to 0dB if we can't read volume range
                return 0.0f;
            }
        }

        public bool ShouldEnforceVolume(AudioDeviceViewModel device)
        {
            if (device == null) return false;
            
            // Only enforce volume for selected (frozen) devices
            if (!device.IsSelected) return false;
            
            // Don't enforce if application is paused
            if (_mainWindow.PauseManager.IsPaused) return false;
            
            // Don't enforce if device has no volume range (fixed volume)
            if (!device.HasVolumeRange) return false;
            
            return true;
        }

        public void EnforceVolumeForDevice(string deviceId)
        {
            try
            {
                // Find the device in collections
                var device = FindDeviceById(deviceId);
                if (device == null)
                {
                    return;
                }

                // Check if enforcement is needed
                if (!ShouldEnforceVolume(device))
                {
                    return;
                }

                // Get the native device for volume operations
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null)
                {
                    return;
                }

                // Calculate target volume
                float targetVolume = CalculateTargetVolume(nativeDevice, device);
                
                // Get current volume
                float currentVolume = nativeDevice.GetVolumeLevel();
                
                // Check if enforcement is needed (only if difference is significant)
                float volumeDifference = Math.Abs(currentVolume - targetVolume);
                if (volumeDifference <= 0.1f)
                {
                    return; // Volume is already close enough
                }

                // Enforce the target volume
                nativeDevice.SetVolumeLevel(targetVolume);
                
                // Update UI to reflect the enforced volume
                device.VolumeDb = targetVolume.ToString("F1");
                
                // Log the enforcement
                
                // Raise event
                VolumeEnforced?.Invoke(this, new VolumeChangeEvent
                {
                    DeviceId = deviceId,
                    DeviceName = device.Name,
                    PreviousVolume = currentVolume,
                    NewVolume = targetVolume,
                    IsDeviceFrozen = device.IsSelected,
                    IsExternalChange = false,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error enforcing volume for device {deviceId}: {ex.Message}");
            }
        }

        private AudioDeviceViewModel? FindDeviceById(string deviceId)
        {
            // Search in all device collections
            var allDevices = _mainWindow.Devices
                .Concat(_mainWindow.PlaybackDevices)
                .Concat(_mainWindow.RecordingDevicesLeft)
                .Concat(_mainWindow.RecordingDevicesRight)
                .Concat(_mainWindow.PlaybackDevicesLeft)
                .Concat(_mainWindow.PlaybackDevicesRight);

            return allDevices.FirstOrDefault(d => d.Id == deviceId);
        }
    }
}