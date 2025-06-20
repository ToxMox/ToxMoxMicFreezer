// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Services;
using ToxMoxMicFreezer.App.Models;
using LogLevel = ToxMoxMicFreezer.App.Services.LogLevel;

namespace ToxMoxMicFreezer.App.DeviceManagement
{
    /// <summary>
    /// Manages audio device enumeration, caching, and collection updates
    /// </summary>
    public class DeviceManager : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private readonly object _deviceLock = new object();
        private readonly ConcurrentDictionary<string, AudioDeviceViewModel> _deviceCache = new();
        private readonly ConcurrentDictionary<string, NAudioAudioDevice> _nativeDeviceCache = new();
        private readonly IDeviceManagerService _deviceManagerService;
        
        // Device event management
        private bool _isEnumerating = false;
        
        // Device collections - referenced from MainWindow
        public ObservableCollection<AudioDeviceViewModel> RecordingDevices { get; private set; }
        public ObservableCollection<AudioDeviceViewModel> PlaybackDevices { get; private set; }
        public ObservableCollection<AudioDeviceViewModel> RecordingDevicesLeft { get; private set; }
        public ObservableCollection<AudioDeviceViewModel> RecordingDevicesRight { get; private set; }
        public ObservableCollection<AudioDeviceViewModel> PlaybackDevicesLeft { get; private set; }
        public ObservableCollection<AudioDeviceViewModel> PlaybackDevicesRight { get; private set; }
        
        private bool _disposed = false;
        
        public DeviceManager(MainWindow mainWindow, 
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            ObservableCollection<AudioDeviceViewModel> recordingDevicesLeft,
            ObservableCollection<AudioDeviceViewModel> recordingDevicesRight,
            ObservableCollection<AudioDeviceViewModel> playbackDevicesLeft,
            ObservableCollection<AudioDeviceViewModel> playbackDevicesRight,
            IDeviceManagerService deviceManagerService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
            _deviceManagerService = deviceManagerService ?? throw new ArgumentNullException(nameof(deviceManagerService));
            
            RecordingDevices = recordingDevices ?? throw new ArgumentNullException(nameof(recordingDevices));
            PlaybackDevices = playbackDevices ?? throw new ArgumentNullException(nameof(playbackDevices));
            RecordingDevicesLeft = recordingDevicesLeft ?? throw new ArgumentNullException(nameof(recordingDevicesLeft));
            RecordingDevicesRight = recordingDevicesRight ?? throw new ArgumentNullException(nameof(recordingDevicesRight));
            PlaybackDevicesLeft = playbackDevicesLeft ?? throw new ArgumentNullException(nameof(playbackDevicesLeft));
            PlaybackDevicesRight = playbackDevicesRight ?? throw new ArgumentNullException(nameof(playbackDevicesRight));
            
        }

        /// <summary>
        /// Complete device refresh using DeviceManagerService
        /// </summary>
        public void BasicLoadAudioDevices(string changeType = "Initial")
        {
            _mainWindow.AppendLog($"[DEBUG] BasicLoadAudioDevices called with changeType: {changeType}");
            
            if (_disposed) 
            {
                _mainWindow.AppendLog($"[DEBUG] Early return: DeviceManager is disposed");
                return;
            }
            
            _mainWindow.AppendLog($"[DEBUG] Current state: {_mainWindow.DeviceStateManager?.CurrentState}");
            _mainWindow.AppendLog($"[DEBUG] _isEnumerating: {_isEnumerating}");
            
            // Set enumeration state
            if (!_mainWindow.DeviceStateManager?.TrySetLoadingState(ToxMoxMicFreezer.App.Services.DeviceLoadingState.Enumerating) == true)
            {
                _mainWindow.AppendLog($"[STATE] Cannot start enumeration - invalid state transition (current: {_mainWindow.DeviceStateManager?.CurrentState})", LogLevel.Debug);
                return;
            }
            
            // Simple blocking: Prevent concurrent enumeration during device changes
            if (_isEnumerating)
            {
                _mainWindow.AppendLog($"[DEVICE_LOAD] Enumeration already in progress - skipping to prevent conflicts", LogLevel.Debug);
                return;
            }
            
            _isEnumerating = true;
            
            try
            {
                var startTime = DateTime.Now;
                
                // Use the device manager service for enumeration
                var (recordingCount, playbackCount, removedCount) = _deviceManagerService.EnumerateAndUpdateDevices(
                    RecordingDevices, PlaybackDevices, _deviceCache, _nativeDeviceCache, _mainWindow._persistentSelectionState);
                
                // Update UI on dispatcher thread
                _dispatcher.Invoke(() =>
                {
                    try
                    {
                        lock (_deviceLock)
                        {
                            // Always rebalance tab device grids
                            _mainWindow._tabManager?.RebalanceTabDeviceGrids();
                            
                            // === UNIFIED DEVICE PROCESSING ===
                            // Always do complete initialization (same as startup) regardless of initial load vs device refresh
                            _mainWindow.AppendLog($"[STATE] Processing complete device enumeration ({changeType})", LogLevel.Debug);
                            
                            // Restore selections from persistent state
                            _mainWindow.DeviceStateManager?.RestoreSelectionsFromPersistentState();
                            

                            
                            // Update UI state after selection restoration
                            _mainWindow._tabManager?.UpdateTabHeaders();
                            
                            // Initialize toggles (safe to call multiple times)
                            _mainWindow.InitializeAllToggles();
                            
                            // Register volume change notifications for all devices asynchronously
                            var registrationTask = _mainWindow._volumeChangeManager?.RegisterAllDevicesAsync();
                            if (registrationTask != null)
                            {
                                _ = registrationTask.ContinueWith(t =>
                                {
                                    // Transition to ready state after volume registration completes
                                    _mainWindow.Dispatcher.BeginInvoke(() =>
                                    {
                                        _mainWindow.DeviceStateManager?.TrySetLoadingState(ToxMoxMicFreezer.App.Services.DeviceLoadingState.Ready);
                                        _mainWindow._devicesLoaded = true;
                                        _mainWindow.DeviceStateManager?.SetDevicesLoaded(true);
                                        
                                        // Rebalance tab device grids after async volume registration
                                        _mainWindow._tabManager?.RebalanceTabDeviceGrids();
                                    });
                                });
                            }
                            else
                            {
                                // Fallback if no volume manager
                                _mainWindow.DeviceStateManager?.TrySetLoadingState(ToxMoxMicFreezer.App.Services.DeviceLoadingState.Ready);
                                _mainWindow._devicesLoaded = true;
                                _mainWindow.DeviceStateManager?.SetDevicesLoaded(true);
                                
                                // Rebalance tab device grids in fallback case
                                _mainWindow._tabManager?.RebalanceTabDeviceGrids();
                            }
                            
                            // CRITICAL: Restore frozen devices to their saved levels after enumeration completes
                            // This ensures device volume ranges are established before restoration
                            _mainWindow.DeviceStateManager?.RestoreFrozenDevicesToSavedLevels();
                            
                            // Rebalance tab device grids after all devices and favorites are loaded
                            _mainWindow._tabManager?.RebalanceTabDeviceGrids();
                        }
                        
                        // DO NOT auto-save selection during enumeration to preserve existing registry entries
                        // SaveSelection() will be called when user actually changes device selections
                        
                        var elapsed = DateTime.Now - startTime;
                        _mainWindow.AppendLog($"[DEVICE_LOAD] ===== ENUMERATION COMPLETE =====");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Final counts - Recording: {recordingCount}, Playback: {playbackCount}");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Removed: {removedCount} stale devices");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Device cache size: {_deviceCache.Count}");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Native device cache size: {_nativeDeviceCache.Count}");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Total elapsed time: {elapsed.TotalMilliseconds:F0}ms");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] ================================");
                        
                        // 🔧 TIMING FIX: Notify services AFTER UI collections are fully populated
                        // This ensures AudioMeteringService refresh happens when devices are available
                        // Use Background priority to ensure this runs after all normal UI updates complete
                        _mainWindow.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                _mainWindow.AppendLog($"[DEVICE_LOAD] Triggering post-enumeration service updates for hot-plugged devices", LogLevel.Debug);
                                
                                // 🔧 CRITICAL: Clean up stale volume registrations before registering new devices
                                // This prevents issues when devices are unplugged/replugged and get new device IDs
                                _mainWindow._volumeChangeManager?.CleanupStaleRegistrations();
                                
                                // 🔧 FIXED: Replace fixed delay with proper device readiness checking for hot-plugged devices
                                // Use async registration that checks device readiness with retry logic
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        if (_mainWindow._volumeChangeManager != null)
                                            await _mainWindow._volumeChangeManager.RegisterHotPluggedDevicesAsync();
                                    }
                                    catch (Exception ex)
                                    {
                                        _ = _mainWindow.Dispatcher.BeginInvoke(() =>
                                            _mainWindow.AppendLog($"Error registering volume change notifications for hot-plugged devices: {ex.Message}", LogLevel.Warning));
                                    }
                                });
                                
                                // Refresh audio metering service immediately (no delay needed for this)
                                _mainWindow._mainWindowOrchestrator?.AudioMeteringService?.RefreshDeviceCapturesSync();
                                
                                _mainWindow.AppendLog("Audio metering service refreshed with proper timing for hot-plugged devices", LogLevel.Debug);
                            }
                            catch (Exception ex)
                            {
                                _mainWindow.AppendLog($"Error during post-enumeration service updates: {ex.Message}", LogLevel.Warning);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        _mainWindow.AppendLog($"[DEVICE_LOAD] ERROR in UI update: {ex.Message}");
                        _mainWindow.AppendLog($"[DEVICE_LOAD] Stack trace: {ex.StackTrace}");
                    }
                    finally
                    {
                        _isEnumerating = false; // Always clear the flag
                    }
                });
            }
            catch (Exception ex)
            {
                _isEnumerating = false; // Clear flag on exception too
                _dispatcher.Invoke(() => {
                    _mainWindow.AppendLog($"[DEVICE_LOAD] CRITICAL ERROR in device enumeration: {ex.Message}");
                    _mainWindow.AppendLog($"[DEVICE_LOAD] Stack trace: {ex.StackTrace}");
                    _mainWindow.AppendLog($"[DEVICE_LOAD] Device counts after error - Recording: {RecordingDevices.Count}, Playback: {PlaybackDevices.Count}");
                });
            }
        }





        /// <summary>
        /// Get NAudio device for fast access - delegates to service
        /// </summary>
        public NAudioAudioDevice? GetCachedNativeDevice(string deviceId)
        {
            return _deviceManagerService.GetCachedNativeDevice(deviceId);
        }

        /// <summary>
        /// Access to device cache for external use
        /// </summary>
        public ConcurrentDictionary<string, AudioDeviceViewModel> DeviceCache => _deviceCache;

        /// <summary>
        /// Access to NAudio device cache for external use
        /// </summary>
        public ConcurrentDictionary<string, NAudioAudioDevice> NativeDeviceCache => _nativeDeviceCache;

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_deviceManagerService != null)
                {
                    _deviceManagerService.Dispose();
                }
                
                // Dispose native devices
                foreach (var nativeDevice in _nativeDeviceCache.Values)
                {
                    nativeDevice?.Dispose();
                }
                _nativeDeviceCache.Clear();
                
                _disposed = true;
            }
        }
    }
}