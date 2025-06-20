// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Collections.Generic;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing volume drag state and device editing tracking
    /// Handles the state management during volume bar drag operations
    /// </summary>
    public interface IVolumeDragStateService
    {
        /// <summary>
        /// Gets whether a volume bar is currently being dragged
        /// </summary>
        bool IsDraggingVolumeBar { get; }

        /// <summary>
        /// Gets the device currently being dragged, if any
        /// </summary>
        AudioDeviceViewModel? DraggingDevice { get; }

        /// <summary>
        /// Gets the cached native device for performance during drag
        /// </summary>
        NAudioAudioDevice? CachedDragDevice { get; }

        /// <summary>
        /// Gets the last displayed volume to avoid redundant updates
        /// </summary>
        float LastDisplayedVolume { get; }

        /// <summary>
        /// Starts a drag operation for the specified device
        /// </summary>
        /// <param name="device">Device to start dragging</param>
        /// <param name="cachedDevice">Cached native device for performance</param>
        void StartDrag(AudioDeviceViewModel device, NAudioAudioDevice? cachedDevice = null);

        /// <summary>
        /// Ends the current drag operation
        /// </summary>
        void EndDrag();

        /// <summary>
        /// Updates the last displayed volume value
        /// </summary>
        /// <param name="volume">Volume value that was last displayed</param>
        void UpdateLastDisplayedVolume(float volume);

        /// <summary>
        /// Checks if a device is currently being edited
        /// </summary>
        /// <param name="deviceId">Device ID to check</param>
        /// <returns>True if device is being edited</returns>
        bool IsDeviceBeingEdited(string deviceId);

        /// <summary>
        /// Marks a device as being edited
        /// </summary>
        /// <param name="deviceId">Device ID to mark as being edited</param>
        void StartEditingDevice(string deviceId);

        /// <summary>
        /// Removes the editing flag for a device
        /// </summary>
        /// <param name="deviceId">Device ID to stop editing</param>
        void StopEditingDevice(string deviceId);

        /// <summary>
        /// Gets all devices currently being edited
        /// </summary>
        /// <returns>Collection of device IDs being edited</returns>
        IReadOnlyCollection<string> GetDevicesBeingEdited();

        /// <summary>
        /// Clears all editing states
        /// </summary>
        void ClearAllEditingStates();
    }
}