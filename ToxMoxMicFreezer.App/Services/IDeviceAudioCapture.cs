// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for managing audio capture from individual devices
    /// Handles WASAPI capture lifecycle and audio data processing
    /// </summary>
    public interface IDeviceAudioCapture : IDisposable
    {
        /// <summary>
        /// The device ID this capture is monitoring
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// The device name for logging and debugging
        /// </summary>
        string DeviceName { get; }

        /// <summary>
        /// Whether this capture is for a recording device (true) or playback device (false)
        /// </summary>
        bool IsRecordingDevice { get; }

        /// <summary>
        /// Whether the capture is currently active
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Current peak level (0.0 to 1.0)
        /// </summary>
        float CurrentPeakLevel { get; }

        /// <summary>
        /// Current RMS level (0.0 to 1.0)
        /// </summary>
        float CurrentRmsLevel { get; }

        /// <summary>
        /// Event fired when new peak level is calculated from audio data
        /// </summary>
        event Action<string, float, float>? AudioLevelChanged; // deviceId, peakLevel, rmsLevel

        /// <summary>
        /// Event fired when new stereo peak levels are calculated from audio data
        /// </summary>
        event Action<string, StereoPeakLevels>? StereoAudioLevelChanged; // deviceId, stereoPeakLevels

        /// <summary>
        /// Event fired when capture encounters an error
        /// </summary>
        event Action<string, Exception>? CaptureError; // deviceId, exception

        /// <summary>
        /// Event fired when actual channel count is detected during initialization
        /// </summary>
        event Action<string, int>? ChannelCountDetected; // deviceId, channelCount

        /// <summary>
        /// Initialize the capture with the specified MMDevice
        /// </summary>
        /// <param name="device">The MMDevice to capture from</param>
        /// <returns>True if initialization succeeded</returns>
        bool Initialize(MMDevice device);

        /// <summary>
        /// Start audio capture
        /// </summary>
        /// <returns>True if capture started successfully</returns>
        bool StartCapture();

        /// <summary>
        /// Stop audio capture
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Get the last calculated peak level without triggering new calculation
        /// </summary>
        /// <returns>Peak level (0.0 to 1.0)</returns>
        float GetLastPeakLevel();

        /// <summary>
        /// Get the last calculated RMS level without triggering new calculation
        /// </summary>
        /// <returns>RMS level (0.0 to 1.0)</returns>
        float GetLastRmsLevel();
    }
}