// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service for real-time audio metering using event-driven capture
    /// Provides peak level monitoring for audio devices
    /// </summary>
    public interface IAudioMeteringService : IDisposable
    {
        /// <summary>
        /// Enable or disable audio metering globally
        /// </summary>
        /// <param name="enabled">True to enable metering, false to disable</param>
        void EnableMetering(bool enabled);

        /// <summary>
        /// Set which tab is currently active to optimize metering
        /// Only devices in the active tab will be metered
        /// </summary>
        /// <param name="tabName">Name of active tab ("Recording" or "Playback")</param>
        void SetActiveTab(string tabName);
        
        /// <summary>
        /// Refresh device captures synchronously after device changes
        /// Used by device event handlers to ensure immediate metering for hot-plugged devices
        /// </summary>
        void RefreshDeviceCapturesSync();

        /// <summary>
        /// Event fired when peak levels change for any monitored device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="peakLevel">Peak level (0.0-1.0)</param>
        event Action<string, float> PeakLevelChanged;

        /// <summary>
        /// Event fired when stereo peak levels change for any monitored device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="stereoPeakLevels">Stereo peak levels (left and right)</param>
        event Action<string, StereoPeakLevels> StereoPeakLevelChanged;

        /// <summary>
        /// Event fired when actual channel count is detected for a device
        /// </summary>
        /// <param name="deviceId">Device ID</param>
        /// <param name="channelCount">Actual number of channels detected</param>
        event Action<string, int>? ChannelCountDetected;

        /// <summary>
        /// Get the current number of actively monitored devices
        /// </summary>
        int ActiveDeviceCount { get; }

        /// <summary>
        /// Get whether metering is currently enabled
        /// </summary>
        bool IsEnabled { get; }
    }
}