// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Services;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.VolumeMonitoring
{
    /// <summary>
    /// Manages volume change notifications using NAudio's built-in events
    /// Provides real-time volume change notifications and enforcement without polling
    /// </summary>
    public class VolumeChangeManager : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private readonly ILoggingService? _loggingService;
        private readonly Dictionary<string, NAudioAudioDevice> _registeredDevices = new();
        private readonly Dictionary<string, AudioEndpointVolumeNotificationDelegate> _volumeCallbacks = new(); // Store callbacks for proper disposal
        private readonly Dictionary<string, bool> _lastMuteStates = new(); // Track last known mute states
        private bool _disposed = false;
        
        // Device lookup cache for performance optimization
        private readonly Dictionary<string, AudioDeviceViewModel> _deviceLookupCache = new();
        
        // Volume notification control to prevent feedback loops during dragging
        private readonly HashSet<string> _pausedNotificationDevices = new();
        private readonly object _pausedDevicesLock = new();
        
        // Event coalescing to reduce dispatcher queue pressure
        private readonly Dictionary<string, DateTime> _lastVolumeEventTime = new();
        private readonly Dictionary<string, float> _lastVolumeValue = new();
        private const int VOLUME_EVENT_COALESCE_MS = 50; // Coalesce rapid events within 50ms
        
        // Volume enforcement throttling to prevent external change feedback loops
        private readonly Dictionary<string, DateTime> _lastVolumeEnforcement = new();
        private readonly Dictionary<string, float> _pendingEnforcementVolume = new();
        private readonly Dictionary<string, System.Timers.Timer> _enforcementTimers = new();
        private const int VOLUME_ENFORCEMENT_THROTTLE_MS = 16; // 60 FPS maximum for enforcement
        
        // UI update throttling to prevent external volume change dispatcher flooding
        private readonly Dictionary<string, DateTime> _lastUIUpdate = new();
        private readonly Dictionary<string, float> _pendingUIVolume = new();
        private readonly Dictionary<string, System.Timers.Timer> _uiUpdateTimers = new();
        private const int UI_UPDATE_THROTTLE_MS = 16; // 60 FPS maximum for UI updates
        
        // Event fired when any device volume is restored (for notification/logging only)
        public event Action<string, string, float>? VolumeRestored;
        
        public VolumeChangeManager(MainWindow mainWindow, ILoggingService? loggingService = null)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
            _loggingService = loggingService;
        }
        
        /// <summary>
        /// Pauses volume change notifications for a specific device to prevent feedback loops during dragging
        /// </summary>
        public void PauseVolumeNotifications(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            lock (_pausedDevicesLock)
            {
                _pausedNotificationDevices.Add(deviceId);
            }
        }
        
        /// <summary>
        /// Resumes volume change notifications for a specific device after dragging is complete
        /// </summary>
        public void ResumeVolumeNotifications(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;
            
            lock (_pausedDevicesLock)
            {
                _pausedNotificationDevices.Remove(deviceId);
            }
        }
        
        /// <summary>
        /// Checks if volume notifications are currently paused for a specific device
        /// </summary>
        private bool IsNotificationPaused(string deviceId)
        {
            lock (_pausedDevicesLock)
            {
                return _pausedNotificationDevices.Contains(deviceId);
            }
        }
        
        /// <summary>
        /// Determines if a volume event should be processed based on coalescing rules
        /// Helps reduce dispatcher queue pressure by filtering out rapid similar events
        /// </summary>
        private bool ShouldProcessVolumeEvent(string deviceId, float volumeDb)
        {
            var now = DateTime.Now;
            
            if (_lastVolumeEventTime.TryGetValue(deviceId, out var lastTime) &&
                _lastVolumeValue.TryGetValue(deviceId, out var lastVolume))
            {
                var timeSinceLastEvent = (now - lastTime).TotalMilliseconds;
                var volumeDifference = Math.Abs(volumeDb - lastVolume);
                
                // Skip if too recent and similar volume (coalesce rapid similar events)
                if (timeSinceLastEvent < VOLUME_EVENT_COALESCE_MS && volumeDifference < 0.5f)
                {
                    return false;
                }
            }
            
            // Update tracking data for next evaluation
            _lastVolumeEventTime[deviceId] = now;
            _lastVolumeValue[deviceId] = volumeDb;
            return true;
        }
        
        /// <summary>
        /// Schedules throttled UI update to prevent dispatcher queue flooding from external volume changes
        /// </summary>
        private void ScheduleThrottledUIUpdate(string deviceId, float volumeDb)
        {
            var now = DateTime.Now;
            _pendingUIVolume[deviceId] = volumeDb; // Always store latest value
            
            if (_lastUIUpdate.TryGetValue(deviceId, out var lastUpdate) &&
                (now - lastUpdate).TotalMilliseconds < UI_UPDATE_THROTTLE_MS)
            {
                // Schedule delayed execution to maintain throttling
                if (_uiUpdateTimers.TryGetValue(deviceId, out var existingTimer))
                {
                    existingTimer.Stop();
                    existingTimer.Dispose();
                }
                
                var timer = new System.Timers.Timer(UI_UPDATE_THROTTLE_MS) { AutoReset = false };
                timer.Elapsed += (s, e) => ExecuteThrottledUIUpdate(deviceId);
                _uiUpdateTimers[deviceId] = timer;
                timer.Start();
            }
            else
            {
                // Execute immediately if enough time has passed
                ExecuteThrottledUIUpdate(deviceId);
            }
        }
        
        /// <summary>
        /// Executes throttled UI update on dispatcher thread
        /// </summary>
        private void ExecuteThrottledUIUpdate(string deviceId)
        {
            if (!_pendingUIVolume.TryGetValue(deviceId, out var volumeDb))
                return;
                
            _lastUIUpdate[deviceId] = DateTime.Now;
            _pendingUIVolume.Remove(deviceId);
            
            // Clean up timer if it exists
            if (_uiUpdateTimers.TryGetValue(deviceId, out var timer))
            {
                timer.Dispose();
                _uiUpdateTimers.Remove(deviceId);
            }
            
            // Execute UI update on dispatcher thread
            _dispatcher.BeginInvoke(() => {
                ProcessVolumeChangeUI(deviceId, volumeDb);
            });
        }
        
        /// <summary>
        /// Processes volume change UI updates (extracted from original dispatcher lambda)
        /// </summary>
        private void ProcessVolumeChangeUI(string deviceId, float volumeDb)
        {
            try
            {
                // Use cached device lookup instead of expensive LINQ searches
                AudioDeviceViewModel? device = null;
                if (_deviceLookupCache.TryGetValue(deviceId, out device))
                {
                    // Fast path: device found in cache
                }
                else
                {
                    // Slow path: search collections and cache result for next time
                    device = _mainWindow.Devices.FirstOrDefault(d => d.Id == deviceId) ?? 
                            _mainWindow.PlaybackDevices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        _deviceLookupCache[deviceId] = device;
                    }
                }
                if (device == null) return;
                
                // Always update UI display
                device.VolumeDb = volumeDb.ToString("F1");
                
                // Simple volume enforcement for frozen devices only - NO HEAVY PROCESSING!
                if (device.IsSelected && !_mainWindow.PauseManager.IsPaused)
                {
                    float volumeDifference = Math.Abs(volumeDb - device.FrozenVolumeDb);
                    if (volumeDifference > 0.1f) // Only restore if significant difference
                    {
                        var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                        if (nativeDevice != null)
                        {
                            try
                            {
                                nativeDevice.SetVolumeLevel(device.FrozenVolumeDb);
                                device.VolumeDb = device.FrozenVolumeDb.ToString("F1");
                                
                                // Fire VolumeRestored event for logging and notifications
                                VolumeRestored?.Invoke(deviceId, device.Name, device.FrozenVolumeDb);
                            }
                            catch (Exception)
                            {
                                // Ignore restore errors - don't spam logs during volume changes
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error in throttled UI update for device {deviceId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Register volume change notifications for a device using NAudio's OnVolumeNotification event
        /// </summary>
        public bool RegisterDevice(NAudioAudioDevice device)
        {
            if (_disposed || device == null) return false;
            
            try
            {
                // Skip if already registered
                if (_registeredDevices.ContainsKey(device.Id))
                {
                    return true;
                }
                
                // Use NAudio's built-in OnVolumeNotification event
                if (device.AudioEndpointVolume != null)
                {
                    // Create and store callback delegate for proper disposal
                    AudioEndpointVolumeNotificationDelegate callback = (data) => OnDeviceVolumeChanged(device.Id, device.FriendlyName, data);
                    device.AudioEndpointVolume.OnVolumeNotification += callback;
                    
                    // Store references to prevent garbage collection and enable proper disposal
                    _registeredDevices[device.Id] = device;
                    _volumeCallbacks[device.Id] = callback;
                    
                    return true;
                }
                else
                {
                    _mainWindow.AppendLog($"No AudioEndpointVolume available for: {device.FriendlyName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Failed to register volume notifications for {device.FriendlyName}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Unregister volume change notifications for a device
        /// </summary>
        public void UnregisterDevice(string deviceId)
        {
            if (_disposed || string.IsNullOrEmpty(deviceId)) return;
            
            try
            {
                if (_registeredDevices.TryGetValue(deviceId, out var device))
                {
                    // CRITICAL: Properly unregister stored callback to prevent access violations
                    if (device.AudioEndpointVolume != null && _volumeCallbacks.TryGetValue(deviceId, out var callback))
                    {
                        device.AudioEndpointVolume.OnVolumeNotification -= callback;
                        _volumeCallbacks.Remove(deviceId);
                    }
                    
                    // Remove from tracking
                    _registeredDevices.Remove(deviceId);
                    
                    _mainWindow.AppendLog($"Unregistered volume notifications for: {device.FriendlyName}");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error unregistering volume notifications for {deviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up stale device registrations during re-enumeration
        /// Called when devices are re-enumerated to remove registrations for devices no longer present
        /// </summary>
        public void CleanupStaleRegistrations()
        {
            if (_disposed) return;
            
            try
            {
                var nativeDeviceCache = _mainWindow._deviceManager?.NativeDeviceCache;
                if (nativeDeviceCache == null) return;
                
                var activeDeviceIds = new HashSet<string>(nativeDeviceCache.Keys);
                var staleDeviceIds = new List<string>();
                
                // Find registered devices that are no longer in the active device cache
                foreach (var registeredDeviceId in _registeredDevices.Keys)
                {
                    if (!activeDeviceIds.Contains(registeredDeviceId))
                    {
                        staleDeviceIds.Add(registeredDeviceId);
                    }
                }
                
                // Remove stale registrations
                foreach (var staleDeviceId in staleDeviceIds)
                {
                    if (_registeredDevices.TryGetValue(staleDeviceId, out var staleDevice))
                    {
                        _registeredDevices.Remove(staleDeviceId);
                        _mainWindow.AppendLog($"Cleaned up stale volume registration: {staleDevice.FriendlyName} (ID: {staleDeviceId})", LogLevel.Debug);
                    }
                }
                
                if (staleDeviceIds.Count > 0)
                {
                    _mainWindow.AppendLog($"Cleaned up {staleDeviceIds.Count} stale device registrations", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error cleaning up stale device registrations: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Register volume notifications for all devices in the device manager cache
        /// </summary>
        public void RegisterAllDevices()
        {
            if (_disposed) return;
            
            try
            {
                
                int successCount = 0;
                int totalCount = 0;
                
                // Get all NAudioAudioDevice objects from native device cache
                var nativeDeviceCache = _mainWindow._deviceManager?.NativeDeviceCache;
                if (nativeDeviceCache != null && nativeDeviceCache.Count > 0)
                {
                    
                    foreach (var device in nativeDeviceCache.Values)
                    {
                        totalCount++;
                        if (RegisterDevice(device))
                        {
                            successCount++;
                        }
                    }
                }
                else
                {
                    _mainWindow.AppendLog("Native device cache is null or empty - cannot register volume notifications");
                }
                
                _mainWindow.AppendLog($"Volume notifications registered for {successCount}/{totalCount} devices");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error registering volume notifications for all devices: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a device is ready for volume notification registration with retry logic
        /// </summary>
        private async Task<bool> IsDeviceReadyForRegistration(NAudioAudioDevice device, int maxRetries = 10, int delayMs = 100)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Check if AudioEndpointVolume is accessible and functional
                    if (device.AudioEndpointVolume != null)
                    {
                        // Try to access volume level to verify COM object is ready
                        var _ = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                        return true; // Device is ready
                    }
                }
                catch (Exception)
                {
                    // Device not ready yet, continue retrying
                }
                
                if (attempt < maxRetries - 1) // Don't delay on last attempt
                {
                    await Task.Delay(delayMs);
                }
            }
            
            return false; // Device not ready after all retries
        }

        /// <summary>
        /// Register volume notifications for hot-plugged devices with device readiness checking
        /// </summary>
        public async Task RegisterHotPluggedDevicesAsync()
        {
            if (_disposed) return;
            
            try
            {
                _loggingService?.Log("Starting hot-plug device volume notification registration with readiness checking", LogLevel.Debug);
                
                await Task.Run(async () =>
                {
                    int successCount = 0;
                    int totalCount = 0;
                    int notReadyCount = 0;
                    
                    // Get all NAudioAudioDevice objects from native device cache
                    var nativeDeviceCache = _mainWindow._deviceManager?.NativeDeviceCache;
                    if (nativeDeviceCache != null && nativeDeviceCache.Count > 0)
                    {
                        foreach (var device in nativeDeviceCache.Values)
                        {
                            totalCount++;
                            
                            // Skip already registered devices
                            if (_registeredDevices.ContainsKey(device.Id))
                            {
                                successCount++; // Count as success since it's already working
                                continue;
                            }
                            
                            // Check if device is ready for registration
                            bool isReady = await IsDeviceReadyForRegistration(device);
                            if (!isReady)
                            {
                                notReadyCount++;
                                _ = _dispatcher.BeginInvoke(() =>
                                    _mainWindow.AppendLog($"Device not ready for volume registration: {device.FriendlyName}"));
                                continue;
                            }
                            
                            // Register the ready device
                            if (RegisterDevice(device))
                            {
                                successCount++;
                                _ = _dispatcher.BeginInvoke(() =>
                                    _mainWindow.AppendLog($"Successfully registered hot-plugged device: {device.FriendlyName}", LogLevel.Debug));
                            }
                        }
                    }
                    else
                    {
                        _ = _dispatcher.BeginInvoke(() =>
                            _mainWindow.AppendLog("Native device cache is null or empty - cannot register volume notifications"));
                        return;
                    }
                    
                    _ = _dispatcher.BeginInvoke(() =>
                    {
                        if (notReadyCount > 0)
                        {
                            _mainWindow.AppendLog($"Hot-plug registration: {successCount}/{totalCount} devices registered, {notReadyCount} devices not ready");
                        }
                        else
                        {
                            _mainWindow.AppendLog($"Hot-plug registration: {successCount}/{totalCount} devices registered successfully");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _ = _dispatcher.BeginInvoke(() =>
                    _mainWindow.AppendLog($"Error registering hot-plugged devices: {ex.Message}"));
            }
        }

        /// <summary>
        /// Asynchronously register volume notifications for all devices in the device manager cache
        /// PHASE 2 FIX: Wait for device loading to complete before registration to prevent race conditions
        /// </summary>
        public async Task RegisterAllDevicesAsync()
        {
            if (_disposed) return;
            
            try
            {
                // 🔧 PHASE 2 FIX: Wait for device loading to complete to prevent volume enforcement race conditions
                var maxWaitTime = TimeSpan.FromSeconds(5);
                var startTime = DateTime.Now;
                
                while (_mainWindow.DeviceStateManager?.CurrentState != ToxMoxMicFreezer.App.Services.DeviceLoadingState.Ready 
                       && DateTime.Now - startTime < maxWaitTime)
                {
                    await Task.Delay(100);
                }
                
                if (_mainWindow.DeviceStateManager?.CurrentState != ToxMoxMicFreezer.App.Services.DeviceLoadingState.Ready)
                {
                    _loggingService?.Log("Warning: Volume notifications registered before device loading completed", LogLevel.Warning);
                }
                else
                {
                    _loggingService?.Log("Device loading completed - proceeding with volume notification registration", LogLevel.Debug);
                }
                
                await Task.Run(() =>
                {
                    int successCount = 0;
                    int totalCount = 0;
                    
                    // Get all NAudioAudioDevice objects from native device cache
                    var nativeDeviceCache = _mainWindow._deviceManager?.NativeDeviceCache;
                    if (nativeDeviceCache != null && nativeDeviceCache.Count > 0)
                    {
                        foreach (var device in nativeDeviceCache.Values)
                        {
                            totalCount++;
                            if (RegisterDevice(device))
                            {
                                successCount++;
                            }
                        }
                    }
                    else
                    {
                        _ = _dispatcher.BeginInvoke(() =>
                            _mainWindow.AppendLog("Native device cache is null or empty - cannot register volume notifications"));
                        return;
                    }
                    
                    _ = _dispatcher.BeginInvoke(() =>
                        _mainWindow.AppendLog($"Volume notifications registered for {successCount}/{totalCount} devices"));
                });
            }
            catch (Exception ex)
            {
                _ = _dispatcher.BeginInvoke(() =>
                    _mainWindow.AppendLog($"Error registering volume notifications asynchronously: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Called when any registered device volume changes via NAudio's OnVolumeNotification
        /// Intent-based approach: All events are processed, no time-based ignoring
        /// </summary>
        private void OnDeviceVolumeChanged(string deviceId, string deviceName, AudioVolumeNotificationData data)
        {
            if (_disposed) return;
            
            
            try
            {
                // FIRST CHECK: Skip if notifications paused for this device (prevents feedback loop)
                if (IsNotificationPaused(deviceId)) return;
                
                // SIMPLIFIED: Minimal processing for maximum performance
                
                // Device-specific dB calculation - get actual volume level from device instead of assuming fixed range
                float volumeDb = 0.0f;
                bool isMuted = data.Muted;
                float volumeScalar = data.MasterVolume; // Keep for logging
                
                // Check for mute state changes and notify MuteService
                bool muteStateChanged = false;
                if (_lastMuteStates.TryGetValue(deviceId, out bool lastMuteState))
                {
                    muteStateChanged = lastMuteState != isMuted;
                }
                else
                {
                    // First time tracking this device - consider it a change if muted
                    muteStateChanged = isMuted;
                }
                
                // Update last known mute state
                _lastMuteStates[deviceId] = isMuted;
                
                var device = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (device != null)
                {
                    try
                    {
                        // Get the actual current volume level directly from device
                        volumeDb = device.GetVolumeLevel();
                    }
                    catch (Exception)
                    {
                        // Fallback: use scalar conversion with device-specific volume range
                        var (minDb, maxDb, _) = device.GetVolumeRange();
                        volumeDb = minDb + (volumeScalar * (maxDb - minDb));
                    }
                }
                else
                {
                    // Fallback: assume typical range only when device lookup fails
                    volumeDb = -96.0f + (volumeScalar * 96.0f);
                }
                
                // SECOND CHECK: Skip processing if user is actively editing this device (during dragging)
                bool isBeingEdited = _mainWindow._volumeBarHandler?.IsDeviceBeingEdited(deviceId) ?? false;
                if (isBeingEdited) return;
                
                // Handle mute state changes if detected (before other processing)
                if (muteStateChanged)
                {
                    _ = _dispatcher.BeginInvoke(() => {
                        _mainWindow.MuteService?.HandleExternalMuteChange(deviceId, isMuted);
                    });
                }
                
                // THIRD CHECK: Event coalescing - skip rapid similar events to reduce dispatcher pressure
                if (!ShouldProcessVolumeEvent(deviceId, volumeDb)) return;
                
                // THROTTLED UI UPDATE: Prevent dispatcher queue flooding from external volume changes
                ScheduleThrottledUIUpdate(deviceId, volumeDb);
            }
            catch (Exception ex)
            {
                _ = _dispatcher.BeginInvoke(() => 
                    _loggingService?.Log($"Error processing NAudio volume change for {deviceName}: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Handle external volume changes by restoring to frozen level if not paused
        /// </summary>
        private void HandleExternalVolumeChange(AudioDeviceViewModel device, float newVolumeDb, string deviceId)
        {
            if (device == null) return;
            
            try
            {
                // Skip COM calls entirely during active volume bar dragging
                bool isBeingEdited = _mainWindow._volumeBarHandler?.IsDeviceBeingEdited(deviceId) ?? false;
                if (isBeingEdited) 
                {
                    // Only update UI display - no COM calls during drag
                    device.VolumeDb = newVolumeDb.ToString("F1");
                    return;
                }
                
                // Check if monitoring is paused
                if (_mainWindow.PauseManager.IsPaused)
                {
                    // During pause, just update UI but don't restore volume
                    device.VolumeDb = newVolumeDb.ToString("F1");
                    return;
                }
                
                // Update UI display
                device.VolumeDb = newVolumeDb.ToString("F1");
                
                // Check if the new volume differs from the frozen volume
                float volumeDifference = Math.Abs(newVolumeDb - device.FrozenVolumeDb);
                if (volumeDifference > 0.1f) // Only restore if difference is significant (> 0.1dB)
                {
                    // Get the native device for volume setting
                    var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                    if (nativeDevice != null)
                    {
                        // Restore to frozen volume
                        nativeDevice.SetVolumeLevel(device.FrozenVolumeDb);
                        
                        // Update UI to show restored volume
                        device.VolumeDb = device.FrozenVolumeDb.ToString("F1");
                        
                        // Fire notification event for logging
                        VolumeRestored?.Invoke(deviceId, device.Name, device.FrozenVolumeDb);
                    }
                    else
                    {
                        _mainWindow.AppendLog($"Warning: Could not restore frozen device {device.Name} - native device not found");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error handling external volume change for {device.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get count of registered devices
        /// </summary>
        public int RegisteredDeviceCount => _registeredDevices.Count;
        
        
        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                _loggingService?.Log("Disposing NAudio volume change manager...");
                
                // Clean up UI update timers to prevent memory leaks
                foreach (var timer in _uiUpdateTimers.Values)
                {
                    timer?.Stop();
                    timer?.Dispose();
                }
                _uiUpdateTimers.Clear();
                
                // Clean up enforcement timers to prevent memory leaks
                foreach (var timer in _enforcementTimers.Values)
                {
                    timer?.Stop();
                    timer?.Dispose();
                }
                _enforcementTimers.Clear();
                
                // Unregister all devices
                var deviceIds = _registeredDevices.Keys.ToList();
                foreach (var deviceId in deviceIds)
                {
                    UnregisterDevice(deviceId);
                }
                
                _registeredDevices.Clear();
                _volumeCallbacks.Clear(); // Clear callback references
                
                _mainWindow.AppendLog("NAudio volume change manager disposed successfully");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error disposing NAudio volume change manager: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}