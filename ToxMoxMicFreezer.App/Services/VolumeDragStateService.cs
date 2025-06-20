// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing volume drag state and device editing tracking
    /// Handles the state management during volume bar drag operations
    /// </summary>
    public class VolumeDragStateService : IVolumeDragStateService
    {
        private readonly ILoggingService _loggingService;
        
        // Drag state
        private bool _isDraggingVolumeBar = false;
        private AudioDeviceViewModel? _draggingDevice = null;
        private NAudioAudioDevice? _cachedDragDevice = null;
        private float _lastDisplayedVolume = float.MinValue;
        
        // Device editing tracking
        private readonly HashSet<string> _devicesBeingEdited = new();

        public VolumeDragStateService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Gets whether a volume bar is currently being dragged
        /// </summary>
        public bool IsDraggingVolumeBar => _isDraggingVolumeBar;

        /// <summary>
        /// Gets the device currently being dragged, if any
        /// </summary>
        public AudioDeviceViewModel? DraggingDevice => _draggingDevice;

        /// <summary>
        /// Gets the cached native device for performance during drag
        /// </summary>
        public NAudioAudioDevice? CachedDragDevice => _cachedDragDevice;

        /// <summary>
        /// Gets the last displayed volume to avoid redundant updates
        /// </summary>
        public float LastDisplayedVolume => _lastDisplayedVolume;

        /// <summary>
        /// Starts a drag operation for the specified device
        /// </summary>
        public void StartDrag(AudioDeviceViewModel device, NAudioAudioDevice? cachedDevice = null)
        {
            try
            {
                _isDraggingVolumeBar = true;
                _draggingDevice = device;
                _cachedDragDevice = cachedDevice;
                _lastDisplayedVolume = float.MinValue; // Reset to force update on first drag
                
                // Mark device as being edited
                StartEditingDevice(device.Id);
                
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error starting drag operation: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Ends the current drag operation
        /// </summary>
        public void EndDrag()
        {
            try
            {
                if (_draggingDevice != null)
                {
                    
                    // Stop editing the device
                    StopEditingDevice(_draggingDevice.Id);
                }

                _isDraggingVolumeBar = false;
                _draggingDevice = null;
                _cachedDragDevice = null;
                _lastDisplayedVolume = float.MinValue;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error ending drag operation: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Updates the last displayed volume value
        /// </summary>
        public void UpdateLastDisplayedVolume(float volume)
        {
            _lastDisplayedVolume = volume;
        }

        /// <summary>
        /// Checks if a device is currently being edited
        /// </summary>
        public bool IsDeviceBeingEdited(string deviceId)
        {
            return _devicesBeingEdited.Contains(deviceId);
        }

        /// <summary>
        /// Marks a device as being edited
        /// </summary>
        public void StartEditingDevice(string deviceId)
        {
            try
            {
                _devicesBeingEdited.Add(deviceId);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error starting device edit: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Removes the editing flag for a device
        /// </summary>
        public void StopEditingDevice(string deviceId)
        {
            try
            {
                if (_devicesBeingEdited.Remove(deviceId))
                {
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error stopping device edit: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Gets all devices currently being edited
        /// </summary>
        public IReadOnlyCollection<string> GetDevicesBeingEdited()
        {
            return new ReadOnlyCollection<string>(_devicesBeingEdited.ToArray());
        }

        /// <summary>
        /// Clears all editing states
        /// </summary>
        public void ClearAllEditingStates()
        {
            try
            {
                var count = _devicesBeingEdited.Count;
                _devicesBeingEdited.Clear();
                
                if (count > 0)
                {
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error clearing editing states: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}