// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;

namespace ToxMoxMicFreezer.App.Services
{
    public class MuteChangeEvent
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public bool IsMuted { get; set; }
        public bool IsExternalChange { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Service for managing device mute states using real OS-level mute
    /// </summary>
    public interface IMuteService
    {
        /// <summary>
        /// Toggles the mute state of a device and returns the new state
        /// </summary>
        bool ToggleMute(string deviceId);
        
        /// <summary>
        /// Sets the mute state of a device
        /// </summary>
        bool SetMuteState(string deviceId, bool muted);
        
        /// <summary>
        /// Gets the current mute state of a device
        /// </summary>
        bool GetMuteState(string deviceId);
        
        /// <summary>
        /// Refreshes mute states for all devices to sync with actual device states
        /// </summary>
        void RefreshMuteStates();
        
        /// <summary>
        /// Handles external mute change notification from volume monitoring
        /// </summary>
        void HandleExternalMuteChange(string deviceId, bool isMuted);
        
        /// <summary>
        /// Event raised when a device's mute state changes
        /// </summary>
        event EventHandler<MuteChangeEvent> MuteChanged;
    }
}