// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Event-driven audio metering service using WASAPI captures for real-time peak level monitoring
    /// Replaces timer-based polling with efficient audio buffer processing from DataAvailable events
    /// </summary>
    public class AudioMeteringService : IAudioMeteringService
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService? _loggingService;
        private readonly IAudioBufferProcessor _bufferProcessor;
        
        // Event-driven device captures - no more timers!
        private readonly ConcurrentDictionary<string, IDeviceAudioCapture> _deviceCaptures = new();
        
        private bool _enabled = false;
        private bool _disposed = false;
        private string _activeTab = "Recording";
        
        // Thread synchronization
        private readonly object _refreshLock = new object();
        private bool _refreshInProgress = false;
        private CancellationTokenSource? _tabSwitchCancellation = null;
        
        // Sequential tab switching
        private readonly SemaphoreSlim _tabSwitchSemaphore = new SemaphoreSlim(1, 1);
        private string? _pendingTab = null;
        private string? _currentSwitchingTab = null;
        private readonly object _pendingTabLock = new object();
        
        // PHASE 3 FIX: Enhanced monitoring and metrics
        private readonly System.Diagnostics.Stopwatch _performanceStopwatch = System.Diagnostics.Stopwatch.StartNew();
        private int _totalDeviceCaptures = 0;
        private int _totalCaptureErrors = 0;
        private int _totalRefreshOperations = 0;
        private long _totalMemoryUsageBytes = 0;
        private DateTime _lastMemoryCheck = DateTime.MinValue;
        private DateTime _lastPerformanceReport = DateTime.MinValue;
        private readonly object _metricsLock = new object();

        /// <summary>
        /// Event fired when peak levels change for any monitored device
        /// </summary>
        public event Action<string, float>? PeakLevelChanged;

        /// <summary>
        /// Event fired when stereo peak levels change for any monitored device
        /// </summary>
        public event Action<string, StereoPeakLevels>? StereoPeakLevelChanged;

        /// <summary>
        /// Event fired when actual channel count is detected for a device
        /// </summary>
        public event Action<string, int>? ChannelCountDetected;

        /// <summary>
        /// Simple device info tracking
        /// </summary>
        private class DeviceMeteringInfo
        {
            public bool IsRecordingDevice { get; set; } = false;
            public string DeviceName { get; set; } = string.Empty;
        }
        
        private readonly ConcurrentDictionary<string, DeviceMeteringInfo> _deviceInfo = new();

        /// <summary>
        /// Get the current number of actively monitored devices
        /// </summary>
        public int ActiveDeviceCount => _deviceCaptures.Count;

        /// <summary>
        /// Get whether metering is currently enabled
        /// </summary>
        public bool IsEnabled => _enabled;

        public AudioMeteringService(MainWindow mainWindow, ILoggingService? loggingService = null)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService;
            _bufferProcessor = new AudioBufferProcessor(loggingService);
        }

        /// <summary>
        /// Enable or disable audio metering globally
        /// </summary>
        public void EnableMetering(bool enabled)
        {
            if (_disposed) return;

            _enabled = enabled;

            if (enabled)
            {
                _loggingService?.Log($"[AUDIO_METERING] Enabling metering for {_activeTab} tab", LogLevel.Debug);
                StartEventDrivenMetering();
            }
            else
            {
                _loggingService?.Log("[AUDIO_METERING] Disabling all metering", LogLevel.Debug);
                // Stop metering first to prevent any new peak level events
                StopEventDrivenMetering();
                // Then reset all meter displays to zero and clear peak hold data
                ResetAllMeterDisplays();
            }

            _loggingService?.Log($"Event-driven audio metering {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Reset all meter displays to zero by firing PeakLevelChanged events with 0.0f peak levels
        /// This ensures meters visually reset to empty when metering is disabled
        /// Uses a two-step process to force peak hold reset: brief high peak then zero
        /// </summary>
        private void ResetAllMeterDisplays()
        {
            try
            {
                // Reset meters for Recording devices
                foreach (var device in _mainWindow.Devices)
                {
                    // Force peak hold reset with brief high peak, then immediate zero
                    PeakLevelChanged?.Invoke(device.Id, 1.0f);
                    PeakLevelChanged?.Invoke(device.Id, 0.0f);
                }

                // Reset meters for Playback devices
                foreach (var device in _mainWindow.PlaybackDevices)
                {
                    // Force peak hold reset with brief high peak, then immediate zero
                    PeakLevelChanged?.Invoke(device.Id, 1.0f);
                    PeakLevelChanged?.Invoke(device.Id, 0.0f);
                }

                _loggingService?.Log("Reset all volume meter displays and peak hold data to zero", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error resetting meter displays: {ex.Message}");
            }
        }

        /// <summary>
        /// Set which tab is currently active to optimize metering
        /// </summary>
        public void SetActiveTab(string tabName)
        {
            if (_disposed) return;

            _loggingService?.Log($"[AUDIO_METERING] SetActiveTab called for {tabName} (current: {_activeTab})", LogLevel.Debug);

            // Update pending tab to the most recent request
            lock (_pendingTabLock)
            {
                _pendingTab = tabName;
            }

            // Start the queue processor if not already running
            if (_enabled)
            {
                Task.Run(ProcessTabSwitchQueue);
            }
            else
            {
                // If metering is disabled, just update the active tab directly
                _activeTab = tabName;
                _loggingService?.Log($"Audio metering disabled - active tab set to: {tabName}");
            }
        }

        /// <summary>
        /// Process tab switch queue sequentially to prevent race conditions
        /// </summary>
        private async Task ProcessTabSwitchQueue()
        {
            var acquired = await _tabSwitchSemaphore.WaitAsync(0); // Try to acquire immediately
            if (!acquired)
            {
                _loggingService?.Log("[AUDIO_METERING] Tab switch already in progress, request queued", LogLevel.Debug);
                return; // Another switch is already in progress
            }

            try
            {
                while (true)
                {
                    string? targetTab = null;
                    
                    // Get the pending tab
                    lock (_pendingTabLock)
                    {
                        if (_pendingTab != null && _pendingTab != _activeTab)
                        {
                            targetTab = _pendingTab;
                            _currentSwitchingTab = targetTab;
                            _pendingTab = null; // Clear pending since we're processing it
                        }
                    }

                    if (targetTab == null)
                    {
                        // No pending tab or it's the same as current
                        _loggingService?.Log("[AUDIO_METERING] No pending tab switch needed", LogLevel.Debug);
                        break;
                    }

                    // Perform the actual tab switch
                    _loggingService?.Log($"[AUDIO_METERING] Starting sequential tab switch from {_activeTab} to {targetTab}", LogLevel.Debug);
                    var switchStartTime = DateTime.Now;

                    try
                    {
                        // Stop all current captures
                        StopAllDeviceCaptures();
                        
                        // Update the active tab
                        _activeTab = targetTab;
                        _loggingService?.Log($"Audio metering active tab set to: {_activeTab}");
                        
                        // Refresh captures for the new tab
                        await RefreshDeviceCaptures();
                        
                        var switchDuration = (DateTime.Now - switchStartTime).TotalMilliseconds;
                        _loggingService?.Log($"[AUDIO_METERING] Tab switch to {targetTab} completed in {switchDuration:F0}ms", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Error during tab switch to {targetTab}: {ex.Message}", LogLevel.Error);
                    }
                    finally
                    {
                        lock (_pendingTabLock)
                        {
                            _currentSwitchingTab = null;
                        }
                    }

                    // Check if there's another pending tab after we finished
                    // This handles rapid tab switching by processing only the final tab
                }
            }
            finally
            {
                _tabSwitchSemaphore.Release();
                _loggingService?.Log("[AUDIO_METERING] Tab switch queue processing completed", LogLevel.Debug);
            }
        }


        /// <summary>
        /// Start event-driven metering system
        /// </summary>
        private void StartEventDrivenMetering()
        {
            if (_deviceCaptures.Count > 0) return; // Already started

            Task.Run(RefreshDeviceCaptures);
            _loggingService?.Log("Event-driven audio metering system started");
        }

        /// <summary>
        /// Stop event-driven metering system
        /// </summary>
        private void StopEventDrivenMetering()
        {
            // Stop and dispose all device captures
            foreach (var capture in _deviceCaptures.Values)
            {
                try
                {
                    // Unsubscribe from events before disposal
                    capture.AudioLevelChanged -= OnAudioLevelChanged;
                    capture.StereoAudioLevelChanged -= OnStereoAudioLevelChanged;
                    capture.CaptureError -= OnCaptureError;
                    capture.ChannelCountDetected -= OnChannelCountDetected;
                    
                    capture.StopCapture();
                    capture.Dispose();
                }
                catch (Exception ex)
                {
                    _loggingService?.Log($"Error disposing device capture: {ex.Message}");
                }
            }
            
            _deviceCaptures.Clear();
            _deviceInfo.Clear();
            _loggingService?.Log("Event-driven audio metering system stopped");
        }

        /// <summary>
        /// Stop all device captures without clearing the metering system
        /// Used for clean tab switching
        /// </summary>
        private void StopAllDeviceCaptures()
        {
            if (_deviceCaptures.Count == 0) return; // Nothing to stop
            
            _loggingService?.Log($"[AUDIO_METERING] Stopping all {_deviceCaptures.Count} device captures for tab switch", LogLevel.Debug);
            
            // Use parallel processing for faster cleanup when many devices
            var captures = _deviceCaptures.ToList();
            if (captures.Count > 10)
            {
                // Parallel cleanup for many devices
                Parallel.ForEach(captures, kvp =>
                {
                    try
                    {
                        var capture = kvp.Value;
                        
                        // Unsubscribe from events before disposal
                        capture.AudioLevelChanged -= OnAudioLevelChanged;
                        capture.StereoAudioLevelChanged -= OnStereoAudioLevelChanged;
                        capture.CaptureError -= OnCaptureError;
                        capture.ChannelCountDetected -= OnChannelCountDetected;
                        
                        capture.StopCapture();
                        capture.Dispose();
                        _deviceCaptures.TryRemove(kvp.Key, out _);
                        _deviceInfo.TryRemove(kvp.Key, out _);
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.Log($"Error stopping capture for device {kvp.Key}: {ex.Message}", LogLevel.Debug);
                    }
                });
            }
            else
            {
                // Sequential cleanup for few devices
                foreach (var kvp in captures)
                {
                    try
                    {
                        var capture = kvp.Value;
                        
                        // Unsubscribe from events before disposal
                        capture.AudioLevelChanged -= OnAudioLevelChanged;
                        capture.StereoAudioLevelChanged -= OnStereoAudioLevelChanged;
                        capture.CaptureError -= OnCaptureError;
                        capture.ChannelCountDetected -= OnChannelCountDetected;
                        
                        capture.StopCapture();
                        capture.Dispose();
                        _deviceCaptures.TryRemove(kvp.Key, out _);
                        _deviceInfo.TryRemove(kvp.Key, out _);
                    }
                    catch (Exception ex)
                    {
                        _loggingService?.Log($"Error stopping capture for device {kvp.Key}: {ex.Message}", LogLevel.Debug);
                    }
                }
            }
            
            _loggingService?.Log("[AUDIO_METERING] All device captures stopped", LogLevel.Debug);
        }

        /// <summary>
        /// Refresh device captures based on active tab - replaces timer-based device list
        /// Public method to be called from device refresh events
        /// PHASE 3 FIX: Enhanced with performance monitoring and memory tracking
        /// </summary>
        public async Task RefreshDeviceCaptures()
        {
            if (_disposed || !_enabled) return;

            lock (_refreshLock)
            {
                if (_refreshInProgress) return;
                _refreshInProgress = true;
            }

            // 🔧 PHASE 3 FIX: Performance and memory tracking
            var refreshStartTime = DateTime.Now;
            lock (_metricsLock)
            {
                _totalRefreshOperations++;
            }

            try
            {
                // BULLETPROOF FIX: Always start fresh - stop all captures first
                _loggingService?.Log($"[AUDIO_METERING] Starting fresh refresh - stopping all existing captures", LogLevel.Debug);
                StopAllDeviceCaptures();
                
                // Get devices from the appropriate collection based on active tab
                IEnumerable<AudioDeviceViewModel> devices;
                if (_activeTab == "Recording")
                {
                    devices = _mainWindow.Devices;
                }
                else if (_activeTab == "Playback")
                {
                    devices = _mainWindow.PlaybackDevices;
                }
                else if (_activeTab == "Favorites")
                {
                    // BULLETPROOF FIX: Get favorites directly from source collections
                    // This ensures we get the actual favorite devices, not stale collections
                    var allDevices = _mainWindow.Devices.Concat(_mainWindow.PlaybackDevices);
                    devices = allDevices.Where(d => d.IsFavorite).ToList();
                    _loggingService?.Log($"[AUDIO_METERING] Favorites tab - found {devices.Count()} favorite devices from source collections", LogLevel.Debug);
                }
                else
                {
                    devices = _mainWindow.Devices; // Default to recording
                }
                
                _loggingService?.Log($"[AUDIO_METERING] RefreshDeviceCaptures starting - Active tab: {_activeTab}, Found {devices.Count()} devices to meter", LogLevel.Debug);
                
                // Since we're starting fresh, all devices are "new"
                var devicesToMeter = devices.ToList();
                
                _loggingService?.Log($"[AUDIO_METERING] Creating captures for {devicesToMeter.Count} devices", LogLevel.Debug);
                if (devicesToMeter.Count > 0)
                {
                    foreach (var device in devicesToMeter)
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Device to meter: {device.Name} (ID: {device.Id})", LogLevel.Debug);
                    }
                }
                
                // Create captures for all devices in the active tab
                await CreateCapturesForNewDevices(devicesToMeter);

                // 🔧 PHASE 3 FIX: Performance and memory reporting
                var refreshDuration = (DateTime.Now - refreshStartTime).TotalMilliseconds;
                lock (_metricsLock)
                {
                    _totalDeviceCaptures = _deviceCaptures.Count;
                }
                
                _loggingService?.Log($"Refreshed device captures: monitoring {_deviceCaptures.Count} devices in {_activeTab} tab (took {refreshDuration:F0}ms)");
                
                // 🔧 PHASE 3 FIX: Periodic performance reporting
                CheckAndReportPerformanceMetrics();
                
                // 🔧 PHASE 3 FIX: Memory usage monitoring
                CheckMemoryUsage();
            }
            catch (Exception ex)
            {
                lock (_metricsLock)
                {
                    _totalCaptureErrors++;
                }
                _loggingService?.Log($"Error refreshing device captures for metering: {ex.Message}", LogLevel.Error);
                _loggingService?.Log($"Stack trace: {ex.StackTrace}", LogLevel.Debug);
            }
            finally
            {
                lock (_refreshLock)
                {
                    _refreshInProgress = false;
                }
            }
        }

        /// <summary>
        /// Synchronous version of RefreshDeviceCaptures for event handlers
        /// Used by MainWindowOrchestratorService to refresh captures after device changes
        /// </summary>
        public void RefreshDeviceCapturesSync()
        {
            if (_disposed)
            {
                _loggingService?.Log("[AUDIO_METERING] RefreshDeviceCapturesSync called but service is disposed", LogLevel.Debug);
                return;
            }
            
            if (!_enabled)
            {
                _loggingService?.Log("[AUDIO_METERING] RefreshDeviceCapturesSync called but metering is disabled", LogLevel.Debug);
                return;
            }
            
            _loggingService?.Log($"[AUDIO_METERING] RefreshDeviceCapturesSync called - starting refresh for {_activeTab} tab", LogLevel.Debug);
            Task.Run(RefreshDeviceCaptures);
        }

        /// <summary>
        /// Create capture instances for new devices
        /// </summary>
        private async Task CreateCapturesForNewDevices(List<AudioDeviceViewModel> newDevices)
        {
            var enumerator = new MMDeviceEnumerator();
            
            // Debug logging to investigate Favorites tab processing
            var multiChannelCount = newDevices.Count(d => d.IsMultiChannel);
            _loggingService?.Log($"[AUDIO_METERING] CreateCapturesForNewDevices called - Tab: {_activeTab}, Total devices: {newDevices.Count}, Multi-channel: {multiChannelCount}", LogLevel.Debug);
            
            foreach (var device in newDevices)
            {
                try
                {
                    // Skip generic/problematic device names that are likely virtual or disconnected
                    if (IsGenericDeviceName(device.Name))
                    {
                        _loggingService?.Log($"Skipping generic device name for metering: {device.Name}", LogLevel.Debug);
                        continue;
                    }

                    // Skip multi-channel devices (more than 2 channels) - not supported for metering
                    if (device.IsMultiChannel)
                    {
                        _loggingService?.Log($"Skipping multi-channel device for metering: {device.Name} ({device.Channels} channels)", LogLevel.Debug);
                        continue;
                    }

                    // Create device info - use device's actual type, not active tab
                    // This is crucial for Favorites tab which contains both recording and playback devices
                    var deviceInfo = new DeviceMeteringInfo
                    {
                        IsRecordingDevice = device.DeviceType == AudioDeviceType.Recording,
                        DeviceName = device.Name
                    };
                    _deviceInfo[device.Id] = deviceInfo;
                    
                    _loggingService?.Log($"[AUDIO_METERING] Creating capture for {device.Name} - Type: {device.DeviceType}, IsRecording: {deviceInfo.IsRecordingDevice}", LogLevel.Debug);

                    // Find the MMDevice
                    MMDevice? mmDevice = await Task.Run(() => FindMMDevice(enumerator, device.Id, device.Name, deviceInfo.IsRecordingDevice));
                    
                    if (mmDevice != null)
                    {
                        // Create the appropriate capture
                        var capture = new DeviceAudioCapture(device.Id, device.Name, deviceInfo.IsRecordingDevice, _bufferProcessor, _loggingService);
                        
                        // Subscribe to events
                        capture.AudioLevelChanged += OnAudioLevelChanged;
                        capture.StereoAudioLevelChanged += OnStereoAudioLevelChanged;
                        capture.CaptureError += OnCaptureError;
                        capture.ChannelCountDetected += OnChannelCountDetected;
                        
                        // Initialize and start
                        if (capture.Initialize(mmDevice))
                        {
                            _deviceCaptures[device.Id] = capture;
                            
                            // Start capture immediately for visible devices
                            if (capture.StartCapture())
                            {
                                _loggingService?.Log($"Started event-driven capture for device: {device.Name}", LogLevel.Debug);
                            }
                        }
                        else
                        {
                            capture.Dispose();
                            _loggingService?.Log($"Failed to initialize capture for device: {device.Name}");
                        }
                    }
                    else
                    {
                        _loggingService?.Log($"Could not find MMDevice for: {device.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService?.Log($"Error creating capture for device {device.Name}: {ex.Message}");
                }
            }
            
            enumerator.Dispose();
        }

        /// <summary>
        /// Check if device name is a generic Windows placeholder that shouldn't be metered
        /// </summary>
        private bool IsGenericDeviceName(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return true;
            
            // Windows generic device names that are usually problematic for audio capture
            string[] genericNames = {
                "Microphone",
                "Speakers", 
                "Headphones",
                "Headset",
                "Line In",
                "Digital Audio",
                "Communications Headphones",
                "Communications Microphone"
            };
            
            // Exact match or starts with generic name
            return genericNames.Any(generic => 
                string.Equals(deviceName.Trim(), generic, StringComparison.OrdinalIgnoreCase) ||
                deviceName.StartsWith(generic + " ", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Find MMDevice by ID or name matching
        /// </summary>
        private MMDevice? FindMMDevice(MMDeviceEnumerator enumerator, string deviceId, string deviceName, bool isRecordingDevice)
        {
            try
            {
                var dataFlow = isRecordingDevice ? DataFlow.Capture : DataFlow.Render;
                var deviceCollection = enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);

                _loggingService?.Log($"[AUDIO_METERING] Searching for device '{deviceName}' (ID: {deviceId}) in {dataFlow} devices", LogLevel.Debug);

                // First try exact ID match
                foreach (var device in deviceCollection)
                {
                    if (device.ID == deviceId)
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Found device by exact ID match: {device.FriendlyName}", LogLevel.Debug);
                        return device;
                    }
                }

                // Log available devices for debugging
                _loggingService?.Log($"[AUDIO_METERING] No exact ID match. Available {dataFlow} devices:", LogLevel.Debug);
                foreach (var device in deviceCollection)
                {
                    _loggingService?.Log($"[AUDIO_METERING]   - {device.FriendlyName} (ID: {device.ID})", LogLevel.Debug);
                }

                // Fallback to name matching - try multiple strategies
                // First try exact name match
                foreach (var device in deviceCollection)
                {
                    if (string.Equals(device.FriendlyName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Found device by exact name match: {device.FriendlyName}", LogLevel.Debug);
                        return device;
                    }
                }
                
                // Try containing the full device name
                foreach (var device in deviceCollection)
                {
                    if (device.FriendlyName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0 || 
                        deviceName.IndexOf(device.FriendlyName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Found device by full name contains: {device.FriendlyName}", LogLevel.Debug);
                        return device;
                    }
                }
                
                // Try partial name matching (first word)
                var deviceFirstWord = deviceName.Split(' ')[0];
                foreach (var device in deviceCollection)
                {
                    var mmDeviceFirstWord = device.FriendlyName.Split(' ')[0];
                    if (device.FriendlyName.IndexOf(deviceFirstWord, StringComparison.OrdinalIgnoreCase) >= 0 || 
                        deviceName.IndexOf(mmDeviceFirstWord, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _loggingService?.Log($"[AUDIO_METERING] Found device by partial name match: {device.FriendlyName}", LogLevel.Debug);
                        return device;
                    }
                }

                _loggingService?.Log($"[AUDIO_METERING] Could not find device '{deviceName}' in {dataFlow} devices", LogLevel.Warning);
                return null;
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error finding MMDevice for {deviceName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handle audio level changes from device captures - event-driven core!
        /// </summary>
        private void OnAudioLevelChanged(string deviceId, float peakLevel, float rmsLevel)
        {
            if (_disposed) return; // Add disposal guard
            
            try
            {
                // Fire the event for UI updates - event-driven updates!
                PeakLevelChanged?.Invoke(deviceId, peakLevel);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error handling audio level change for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handle stereo audio level changes from device captures
        /// </summary>
        private void OnStereoAudioLevelChanged(string deviceId, StereoPeakLevels stereoPeakLevels)
        {
            if (_disposed) return;
            
            try
            {
                // Fire the stereo event for UI updates
                StereoPeakLevelChanged?.Invoke(deviceId, stereoPeakLevels);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error handling stereo audio level change for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handle capture errors
        /// </summary>
        private void OnCaptureError(string deviceId, Exception exception)
        {
            if (_disposed) return; // Add disposal guard
            
            // Filter out common expected errors to reduce log spam
            var errorMessage = exception.Message;
            bool shouldLog = true;
            bool shouldRestart = false;
            
            if (errorMessage.Contains("Previous recording still in progress"))
            {
                shouldLog = false; // Don't log these - they're expected during restart attempts
            }
            else if (errorMessage.Contains("0x8889000A") || errorMessage.Contains("AUDCLNT_E_DEVICE_IN_USE"))
            {
                // Device is in use by another application - log once
                if (_deviceInfo.TryGetValue(deviceId, out var info))
                {
                    _loggingService?.Log($"Audio metering unavailable for {info.DeviceName}: device in use by another application", LogLevel.Debug);
                    shouldLog = false; // Already logged
                }
            }
            else
            {
                // Other errors - log and consider restart
                shouldRestart = true;
                _loggingService?.Log($"Capture error for device {deviceId}: {errorMessage}");
            }
            
            if (shouldLog && !shouldRestart)
            {
                // Just log without restart for device-in-use errors
                return;
            }
            
            if (shouldRestart)
            {
                // Try to restart the capture after a brief delay for unexpected errors
                Task.Delay(3000).ContinueWith(_ => 
                {
                    if (_deviceCaptures.TryGetValue(deviceId, out var capture))
                    {
                        try
                        {
                            if (capture.IsCapturing)
                            {
                                capture.StopCapture();
                                Task.Delay(1000).Wait();
                            }
                            capture.StartCapture();
                        }
                        catch (Exception ex)
                        {
                            _loggingService?.Log($"Failed to restart capture for device {deviceId}: {ex.Message}", LogLevel.Debug);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Handle channel count detection from device captures
        /// </summary>
        private void OnChannelCountDetected(string deviceId, int channelCount)
        {
            if (_disposed) return;
            
            try
            {
                _loggingService?.Log($"[AUDIO_METERING] Actual channel count detected for device {deviceId}: {channelCount} channels", LogLevel.Debug);
                
                // Propagate the event to listeners (MainWindowOrchestratorService)
                ChannelCountDetected?.Invoke(deviceId, channelCount);
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error handling channel count detection for device {deviceId}: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// PHASE 3 FIX: Check and report performance metrics periodically
        /// </summary>
        private void CheckAndReportPerformanceMetrics()
        {
            try
            {
                var now = DateTime.Now;
                lock (_metricsLock)
                {
                    // Report metrics every 5 minutes
                    if ((now - _lastPerformanceReport).TotalMinutes >= 5.0)
                    {
                        var uptimeMinutes = _performanceStopwatch.Elapsed.TotalMinutes;
                        var errorRate = _totalRefreshOperations > 0 ? (_totalCaptureErrors * 100.0 / _totalRefreshOperations) : 0.0;
                        
                        _loggingService?.Log($"[METRICS] AudioMeteringService performance report:", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Uptime: {uptimeMinutes:F1} minutes", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Total refresh operations: {_totalRefreshOperations}", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Active device captures: {_totalDeviceCaptures}", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Total capture errors: {_totalCaptureErrors}", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Error rate: {errorRate:F2}%", LogLevel.Info);
                        _loggingService?.Log($"[METRICS] - Estimated memory usage: {_totalMemoryUsageBytes / 1024:F0} KB", LogLevel.Info);
                        
                        _lastPerformanceReport = now;
                        
                        // Alert on high error rates
                        if (errorRate > 10.0 && _totalRefreshOperations > 10)
                        {
                            _loggingService?.Log($"[ALERT] High error rate detected in AudioMeteringService: {errorRate:F1}%", LogLevel.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error in performance metrics reporting: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Monitor memory usage to detect potential leaks
        /// </summary>
        private void CheckMemoryUsage()
        {
            try
            {
                var now = DateTime.Now;
                
                // Check memory every 2 minutes
                if ((now - _lastMemoryCheck).TotalMinutes >= 2.0)
                {
                    lock (_metricsLock)
                    {
                        // Estimate memory usage from device captures
                        _totalMemoryUsageBytes = _deviceCaptures.Count * 150 * 1024; // Rough estimate: 150KB per capture
                        
                        // Get process memory for comparison
                        var process = System.Diagnostics.Process.GetCurrentProcess();
                        var processMemoryMB = process.WorkingSet64 / (1024 * 1024);
                        
                        _loggingService?.Log($"[MEMORY] AudioMeteringService estimated usage: {_totalMemoryUsageBytes / 1024:F0} KB, Process total: {processMemoryMB:F0} MB", LogLevel.Debug);
                        
                        // Alert on excessive memory usage (more than 10MB just for audio captures)
                        if (_totalMemoryUsageBytes > 10 * 1024 * 1024)
                        {
                            _loggingService?.Log($"[ALERT] High memory usage detected in AudioMeteringService: {_totalMemoryUsageBytes / (1024 * 1024):F1} MB", LogLevel.Warning);
                            _loggingService?.Log($"[ALERT] Active captures: {_deviceCaptures.Count}, consider investigating memory leaks", LogLevel.Warning);
                        }
                        
                        _lastMemoryCheck = now;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error in memory usage monitoring: {ex.Message}", LogLevel.Warning);
            }
        }
        
        /// <summary>
        /// PHASE 3 FIX: Enhanced disposal with metrics reporting
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Cancel any pending tab switch operations
                _tabSwitchCancellation?.Cancel();
                _tabSwitchCancellation?.Dispose();
                
                // Clean up tab switch semaphore
                _tabSwitchSemaphore?.Dispose();
                
                // Final performance report before disposal
                lock (_metricsLock)
                {
                    var uptimeMinutes = _performanceStopwatch.Elapsed.TotalMinutes;
                    var errorRate = _totalRefreshOperations > 0 ? (_totalCaptureErrors * 100.0 / _totalRefreshOperations) : 0.0;
                    
                    _loggingService?.Log($"[DISPOSE] AudioMeteringService final metrics:", LogLevel.Info);
                    _loggingService?.Log($"[DISPOSE] - Total uptime: {uptimeMinutes:F1} minutes", LogLevel.Info);
                    _loggingService?.Log($"[DISPOSE] - Total operations: {_totalRefreshOperations}", LogLevel.Info);
                    _loggingService?.Log($"[DISPOSE] - Total errors: {_totalCaptureErrors}", LogLevel.Info);
                    _loggingService?.Log($"[DISPOSE] - Final error rate: {errorRate:F2}%", LogLevel.Info);
                }
                
                StopEventDrivenMetering();
                _performanceStopwatch.Stop();
                
                _loggingService?.Log("AudioMeteringService disposed successfully");
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error disposing AudioMeteringService: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}