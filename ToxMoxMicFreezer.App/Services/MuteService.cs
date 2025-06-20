// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Linq;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service for managing device mute states using real OS-level mute via AudioEndpointVolume
    /// Follows Single Responsibility Principle - handles only mute operations
    /// </summary>
    public class MuteService : IMuteService
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;

        public event EventHandler<MuteChangeEvent>? MuteChanged;

        public MuteService(MainWindow mainWindow, ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public bool ToggleMute(string deviceId)
        {
            try
            {
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null)
                {
                    _loggingService.Log($"[MUTE_SERVICE] Device not found: {deviceId}");
                    return false;
                }

                var deviceViewModel = FindDeviceById(deviceId);
                if (deviceViewModel == null)
                {
                    _loggingService.Log($"[MUTE_SERVICE] Device view model not found: {deviceId}");
                    return false;
                }

                // Fixed volume devices can still be muted/unmuted

                // Use NAudio's real mute toggle
                bool newMuteState = nativeDevice.ToggleMute();
                
                // Update UI model
                UpdateDeviceMuteState(deviceId, newMuteState);
                
                // Log the change
                _loggingService.Log($"[MUTE_SERVICE] Toggled mute for '{deviceViewModel.Name}' - now {(newMuteState ? "MUTED" : "UNMUTED")}");
                
                // Raise event
                RaiseMuteChanged(deviceId, deviceViewModel.Name, newMuteState, false);
                
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[MUTE_SERVICE] Error toggling mute for device {deviceId}: {ex.Message}");
                return false;
            }
        }
        
        public bool SetMuteState(string deviceId, bool muted)
        {
            try
            {
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null) return false;

                var deviceViewModel = FindDeviceById(deviceId);
                if (deviceViewModel == null) return false;

                // Fixed volume devices can still be muted/unmuted

                // Only change if state is different
                if (nativeDevice.GetMuteState() == muted) return true;

                // Use NAudio's real mute set
                nativeDevice.SetMuteState(muted);
                
                // Update UI model
                UpdateDeviceMuteState(deviceId, muted);
                
                // Log the change
                _loggingService.Log($"[MUTE_SERVICE] Set mute for '{deviceViewModel.Name}' to {(muted ? "MUTED" : "UNMUTED")}");
                
                // Raise event
                RaiseMuteChanged(deviceId, deviceViewModel.Name, muted, false);
                
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[MUTE_SERVICE] Error setting mute state for device {deviceId}: {ex.Message}");
                return false;
            }
        }

        public bool GetMuteState(string deviceId)
        {
            try
            {
                var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (nativeDevice == null) return false;

                return nativeDevice.GetMuteState();
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[MUTE_SERVICE] Error getting mute state for device {deviceId}: {ex.Message}");
                return false;
            }
        }

        public void RefreshMuteStates()
        {
            try
            {
                var allDevices = GetAllDevices();
                
                foreach (var device in allDevices)
                {
                    var actualMuteState = GetMuteState(device.Id);
                    if (device.IsMuted != actualMuteState)
                    {
                        device.IsMuted = actualMuteState;
                        _loggingService.Log($"[MUTE_SERVICE] Synced mute state for '{device.Name}' - now {(actualMuteState ? "MUTED" : "UNMUTED")}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[MUTE_SERVICE] Error refreshing mute states: {ex.Message}");
            }
        }

        public void HandleExternalMuteChange(string deviceId, bool isMuted)
        {
            try
            {
                var deviceViewModel = FindDeviceById(deviceId);
                if (deviceViewModel == null) return;

                // Only process if state actually changed
                if (deviceViewModel.IsMuted == isMuted) return;

                // Update UI model
                deviceViewModel.IsMuted = isMuted;
                
                // Log external change
                _loggingService.Log($"[MUTE_SERVICE] External mute change detected for '{deviceViewModel.Name}' - now {(isMuted ? "MUTED" : "UNMUTED")}");
                
                // Raise event for external change
                RaiseMuteChanged(deviceId, deviceViewModel.Name, isMuted, true);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"[MUTE_SERVICE] Error handling external mute change for device {deviceId}: {ex.Message}");
            }
        }

        private void UpdateDeviceMuteState(string deviceId, bool isMuted)
        {
            var deviceViewModel = FindDeviceById(deviceId);
            if (deviceViewModel != null)
            {
                deviceViewModel.IsMuted = isMuted;
            }
        }

        private void RaiseMuteChanged(string deviceId, string deviceName, bool isMuted, bool isExternalChange)
        {
            MuteChanged?.Invoke(this, new MuteChangeEvent
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                IsMuted = isMuted,
                IsExternalChange = isExternalChange,
                Timestamp = DateTime.Now
            });
        }

        private AudioDeviceViewModel? FindDeviceById(string deviceId)
        {
            // Search in all device collections (same pattern as VolumeEnforcementService)
            var allDevices = GetAllDevices();
            return allDevices.FirstOrDefault(d => d.Id == deviceId);
        }

        private System.Collections.Generic.IEnumerable<AudioDeviceViewModel> GetAllDevices()
        {
            return _mainWindow.Devices
                .Concat(_mainWindow.PlaybackDevices)
                .Concat(_mainWindow.RecordingDevicesLeft)
                .Concat(_mainWindow.RecordingDevicesRight)
                .Concat(_mainWindow.PlaybackDevicesLeft)
                .Concat(_mainWindow.PlaybackDevicesRight);
        }
    }
}