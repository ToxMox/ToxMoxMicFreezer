// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Event-driven audio capture for individual devices using WASAPI
    /// Replaces timer-based polling with efficient audio buffer processing
    /// </summary>
    public class DeviceAudioCapture : IDeviceAudioCapture
    {
        private readonly IAudioBufferProcessor _bufferProcessor;
        private readonly ILoggingService? _loggingService;
        private readonly object _captureLock = new object();

        // Device information
        public string DeviceId { get; private set; } = string.Empty;
        public string DeviceName { get; private set; } = string.Empty;
        public bool IsRecordingDevice { get; private set; }

        // Capture instances (only one will be used based on device type)
        private WasapiCapture? _recordingCapture;
        private WasapiLoopbackCapture? _playbackCapture;
        private bool _disposed = false;

        // Audio level tracking
        private volatile float _currentPeakLevel = 0.0f;
        private volatile float _currentRmsLevel = 0.0f;
        private AudioSampleFormat _sampleFormat = AudioSampleFormat.Float32;
        private int _channels = 1; // Number of audio channels

        // State tracking
        public bool IsCapturing { get; private set; } = false;
        public float CurrentPeakLevel => _currentPeakLevel;
        public float CurrentRmsLevel => _currentRmsLevel;

        // Events
        public event Action<string, float, float>? AudioLevelChanged;
        public event Action<string, StereoPeakLevels>? StereoAudioLevelChanged;
        public event Action<string, Exception>? CaptureError;
        public event Action<string, int>? ChannelCountDetected;

        public DeviceAudioCapture(string deviceId, string deviceName, bool isRecordingDevice, 
            IAudioBufferProcessor bufferProcessor, ILoggingService? loggingService = null)
        {
            DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            DeviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
            IsRecordingDevice = isRecordingDevice;
            _bufferProcessor = bufferProcessor ?? throw new ArgumentNullException(nameof(bufferProcessor));
            _loggingService = loggingService;
        }

        /// <summary>
        /// Initialize capture with the specified MMDevice
        /// </summary>
        public bool Initialize(MMDevice device)
        {
            if (_disposed) return false;

            try
            {
                lock (_captureLock)
                {
                    // Clean up any existing capture
                    DisposeCapture();

                    if (IsRecordingDevice)
                    {
                        // Use WasapiCapture for recording devices (microphones)
                        _recordingCapture = new WasapiCapture(device);
                        _recordingCapture.DataAvailable += OnDataAvailable;
                        _recordingCapture.RecordingStopped += OnRecordingStopped;
                        
                        // Determine sample format and channels from device format
                        _sampleFormat = GetSampleFormatFromWaveFormat(_recordingCapture.WaveFormat);
                        _channels = _recordingCapture.WaveFormat.Channels;
                    }
                    else
                    {
                        // Use WasapiLoopbackCapture for playback devices (speakers)
                        _playbackCapture = new WasapiLoopbackCapture(device);
                        _playbackCapture.DataAvailable += OnDataAvailable;
                        _playbackCapture.RecordingStopped += OnRecordingStopped;
                        
                        // Determine sample format and channels from device format
                        _sampleFormat = GetSampleFormatFromWaveFormat(_playbackCapture.WaveFormat);
                        _channels = _playbackCapture.WaveFormat.Channels;
                    }

                    _loggingService?.Log($"Initialized {(IsRecordingDevice ? "recording" : "playback")} capture for device: {DeviceName} - Channels: {_channels}, Format: {_sampleFormat}", LogLevel.Debug);
                    
                    // Check if device has more than 2 channels - not supported for metering
                    if (_channels > 2)
                    {
                        _loggingService?.Log($"Device {DeviceName} has {_channels} channels - not supported for audio metering", LogLevel.Info);
                        DisposeCapture();
                        return false;
                    }
                    
                    // Report the actual channel count detected from the capture device
                    ChannelCountDetected?.Invoke(DeviceId, _channels);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error initializing capture for device {DeviceName}: {ex.Message}");
                CaptureError?.Invoke(DeviceId, ex);
                return false;
            }
        }

        /// <summary>
        /// Start audio capture
        /// </summary>
        public bool StartCapture()
        {
            if (_disposed || IsCapturing) return false;

            try
            {
                lock (_captureLock)
                {
                    if (IsRecordingDevice && _recordingCapture != null)
                    {
                        _recordingCapture.StartRecording();
                    }
                    else if (!IsRecordingDevice && _playbackCapture != null)
                    {
                        _playbackCapture.StartRecording();
                    }
                    else
                    {
                        return false;
                    }

                    IsCapturing = true;
                    _loggingService?.Log($"Started capture for device: {DeviceName}", LogLevel.Debug);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error starting capture for device {DeviceName}: {ex.Message}");
                CaptureError?.Invoke(DeviceId, ex);
                return false;
            }
        }

        /// <summary>
        /// Stop audio capture
        /// </summary>
        public void StopCapture()
        {
            if (_disposed || !IsCapturing) return;

            try
            {
                lock (_captureLock)
                {
                    _recordingCapture?.StopRecording();
                    _playbackCapture?.StopRecording();
                    
                    IsCapturing = false;
                    _loggingService?.Log($"Stopped capture for device: {DeviceName}", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error stopping capture for device {DeviceName}: {ex.Message}");
                CaptureError?.Invoke(DeviceId, ex);
            }
        }

        /// <summary>
        /// Handle incoming audio data - this is where the magic happens!
        /// </summary>
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_disposed || e.BytesRecorded == 0) return;

            try
            {
                // Calculate stereo peak levels from raw audio buffer
                var stereoPeakLevels = _bufferProcessor.CalculateStereoPeakLevel(e.Buffer, e.BytesRecorded, _sampleFormat, _channels);
                
                // Calculate overall peak for backward compatibility (max of left/right)
                float peakLevel = Math.Max(stereoPeakLevels.Left, stereoPeakLevels.Right);
                float rmsLevel = _bufferProcessor.CalculateRmsLevel(e.Buffer, e.BytesRecorded, _sampleFormat);

                // Update current levels (thread-safe with volatile)
                _currentPeakLevel = peakLevel;
                _currentRmsLevel = rmsLevel;

                // Fire both events - old one for backward compatibility, new one for stereo
                AudioLevelChanged?.Invoke(DeviceId, peakLevel, rmsLevel);
                StereoAudioLevelChanged?.Invoke(DeviceId, stereoPeakLevels);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error processing audio data for device {DeviceName}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handle recording stopped event
        /// </summary>
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            IsCapturing = false;
            
            if (e.Exception != null)
            {
                _loggingService?.Log($"Recording stopped with error for device {DeviceName}: {e.Exception.Message}");
                CaptureError?.Invoke(DeviceId, e.Exception);
            }
            else
            {
                _loggingService?.Log($"Recording stopped normally for device: {DeviceName}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Determine audio sample format from NAudio WaveFormat
        /// </summary>
        private AudioSampleFormat GetSampleFormatFromWaveFormat(WaveFormat waveFormat)
        {
            // WASAPI typically uses 32-bit float, but check to be sure
            if (waveFormat.BitsPerSample == 32)
            {
                // Check if it's extensible format with specific subtype
                if (waveFormat is WaveFormatExtensible ext)
                {
                    // IEEE float subtype GUID: {00000003-0000-0010-8000-00aa00389b71}
                    var ieeeFloatGuid = new Guid("00000003-0000-0010-8000-00aa00389b71");
                    if (ext.SubFormat.Equals(ieeeFloatGuid))
                    {
                        return AudioSampleFormat.Float32;
                    }
                    else
                    {
                        return AudioSampleFormat.Int32;
                    }
                }
                else
                {
                    // Default to Float32 for WASAPI 32-bit
                    return AudioSampleFormat.Float32;
                }
            }
            else if (waveFormat.BitsPerSample == 16)
            {
                return AudioSampleFormat.Int16;
            }
            else
            {
                // Default to Float32 for WASAPI
                return AudioSampleFormat.Float32;
            }
        }

        /// <summary>
        /// Get the last calculated peak level
        /// </summary>
        public float GetLastPeakLevel() => _currentPeakLevel;

        /// <summary>
        /// Get the last calculated RMS level
        /// </summary>
        public float GetLastRmsLevel() => _currentRmsLevel;

        /// <summary>
        /// Dispose capture instances
        /// </summary>
        private void DisposeCapture()
        {
            try
            {
                if (_recordingCapture != null)
                {
                    _recordingCapture.DataAvailable -= OnDataAvailable;
                    _recordingCapture.RecordingStopped -= OnRecordingStopped;
                    _recordingCapture.Dispose();
                    _recordingCapture = null;
                }

                if (_playbackCapture != null)
                {
                    _playbackCapture.DataAvailable -= OnDataAvailable;
                    _playbackCapture.RecordingStopped -= OnRecordingStopped;
                    _playbackCapture.Dispose();
                    _playbackCapture = null;
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error disposing capture for device {DeviceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                StopCapture();
                
                lock (_captureLock)
                {
                    DisposeCapture();
                }
                
                _loggingService?.Log($"Disposed audio capture for device: {DeviceName}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error disposing DeviceAudioCapture for {DeviceName}: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}