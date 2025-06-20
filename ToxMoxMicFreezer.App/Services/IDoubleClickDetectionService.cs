// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for detecting double-clicks and managing click intent detection
    /// Handles the timing and logic to distinguish between single clicks and double clicks
    /// </summary>
    public interface IDoubleClickDetectionService
    {
        /// <summary>
        /// Event fired when a single click intent is detected (after delay)
        /// </summary>
        event Action<System.Windows.Point, AudioDeviceViewModel>? SingleClickDetected;

        /// <summary>
        /// Event fired when a double click is detected immediately
        /// </summary>
        event Action<AudioDeviceViewModel>? DoubleClickDetected;

        /// <summary>
        /// Processes a mouse click and determines if it's a single or double click
        /// </summary>
        /// <param name="device">Device that was clicked</param>
        /// <param name="mousePosition">Position of the click</param>
        /// <returns>True if this was determined to be a double click</returns>
        bool ProcessClick(AudioDeviceViewModel device, System.Windows.Point mousePosition);

        /// <summary>
        /// Cancels any pending click intent detection
        /// </summary>
        void CancelPendingIntent();

        /// <summary>
        /// Gets whether click intent detection is currently active
        /// </summary>
        bool IsWaitingForIntent { get; }

        /// <summary>
        /// Gets the current intent detection delay in milliseconds
        /// </summary>
        int IntentDetectionDelayMs { get; }

        /// <summary>
        /// Disposes of the service and cleans up resources
        /// </summary>
        void Dispose();
    }
}