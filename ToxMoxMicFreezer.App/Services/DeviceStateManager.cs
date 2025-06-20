// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    public class DeviceStateManager : IDeviceStateManager
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;
        private DeviceLoadingState _loadingState = DeviceLoadingState.NotStarted;
        private readonly object _stateLock = new object();
        private bool _devicesLoaded = false;

        public DeviceLoadingState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _loadingState;
                }
            }
        }

        public bool IsLoadingDevices => 
            CurrentState != DeviceLoadingState.Ready && CurrentState != DeviceLoadingState.NotStarted;

        public event EventHandler<DeviceStateChangedEventArgs>? StateChanged;

        public DeviceStateManager(MainWindow mainWindow, ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public bool IsOperationAllowed(DeviceLoadingState requiredState)
        {
            lock (_stateLock)
            {
                return _loadingState == requiredState;
            }
        }

        public bool TrySetLoadingState(DeviceLoadingState newState)
        {
            lock (_stateLock)
            {
                // Define valid transitions
                var validTransitions = new Dictionary<DeviceLoadingState, DeviceLoadingState[]>
                {
                    [DeviceLoadingState.NotStarted] = new[] { DeviceLoadingState.Enumerating },
                    [DeviceLoadingState.Enumerating] = new[] { 
                        DeviceLoadingState.RestoringSavedState,  // Initial load path
                        DeviceLoadingState.Ready                 // Refresh completion path
                    },
                    [DeviceLoadingState.RestoringSavedState] = new[] { 
                        DeviceLoadingState.RegisteringVolumes,   // Normal progression
                        DeviceLoadingState.Enumerating           // Allow device refresh interrupt
                    },
                    [DeviceLoadingState.RegisteringVolumes] = new[] { 
                        DeviceLoadingState.Ready,                // Normal progression
                        DeviceLoadingState.Enumerating           // Allow device refresh interrupt
                    },
                    [DeviceLoadingState.Ready] = new[] { DeviceLoadingState.Enumerating } // Allow refresh
                };

                if (validTransitions.TryGetValue(_loadingState, out var allowed) && allowed.Contains(newState))
                {
                    var oldState = _loadingState;
                    _loadingState = newState;
                    
                    _loggingService.Log($"[STATE] Loading state changed: {oldState} → {newState}", LogLevel.Debug);
                    
                    // Update legacy flags for compatibility
                    AudioDeviceViewModel.IsLoadingDevices = IsLoadingDevices;
                    _devicesLoaded = newState == DeviceLoadingState.Ready;
                    
                    // Raise event
                    StateChanged?.Invoke(this, new DeviceStateChangedEventArgs 
                    { 
                        OldState = oldState, 
                        NewState = newState, 
                        Timestamp = DateTime.Now 
                    });
                    
                    return true;
                }
                
                _loggingService.Log($"[STATE] Invalid transition attempted: {_loadingState} → {newState}", LogLevel.Debug);
                return false;
            }
        }

        /// <summary>
        /// PHASE 2 FIX: Improved async device initialization with proper error recovery and timeout handling
        /// </summary>
        public async Task InitializeDevicesAsync()
        {
            if (!TrySetLoadingState(DeviceLoadingState.Enumerating))
            {
                _loggingService.Log($"Cannot start device initialization - current state: {CurrentState}", LogLevel.Warning);
                return;
            }

            var startTime = DateTime.Now;
            
            try
            {
                _loggingService.Log("[STATE] Starting device initialization sequence", LogLevel.Debug);
                
                // 🔧 PHASE 2 FIX: Use proper async/await with timeout for device enumeration
                var enumerationTask = Task.Run(() => _mainWindow._deviceManager?.BasicLoadAudioDevices());
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30 second timeout
                
                var completedTask = await Task.WhenAny(enumerationTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _loggingService.Log("Device enumeration timed out after 30 seconds", LogLevel.Error);
                    await HandleInitializationError("Enumeration timeout");
                    return;
                }
                
                await enumerationTask; // Ensure any exceptions are propagated
                _loggingService.Log($"[STATE] Device enumeration completed in {(DateTime.Now - startTime).TotalMilliseconds:F0}ms", LogLevel.Debug);
                
                // 🔧 PHASE 2 FIX: Atomic state transition to RestoringSavedState
                if (!TrySetLoadingState(DeviceLoadingState.RestoringSavedState))
                {
                    await HandleInitializationError("Failed to transition to RestoringSavedState");
                    return;
                }
                
                // Restore device states
                await Task.Run(() => RestoreSelectionsFromPersistentState());
                await Task.Run(() => RestoreFrozenDevicesToSavedLevels());
                
                // 🔧 PHASE 2 FIX: Atomic state transition to RegisteringVolumes
                if (!TrySetLoadingState(DeviceLoadingState.RegisteringVolumes))
                {
                    await HandleInitializationError("Failed to transition to RegisteringVolumes");
                    return;
                }
                
                // Register volume change callbacks with proper async handling
                if (_mainWindow._volumeChangeManager != null)
                {
                    var registrationTask = _mainWindow._volumeChangeManager.RegisterAllDevicesAsync();
                    var registrationTimeout = Task.Delay(TimeSpan.FromSeconds(10));
                    
                    var completedRegistration = await Task.WhenAny(registrationTask, registrationTimeout);
                    
                    if (completedRegistration == registrationTimeout)
                    {
                        _loggingService.Log("Volume registration timed out after 10 seconds", LogLevel.Warning);
                        // Continue anyway - this is not fatal
                    }
                    else
                    {
                        await registrationTask;
                    }
                }
                
                // 🔧 PHASE 2 FIX: Final atomic state transition to Ready
                if (!TrySetLoadingState(DeviceLoadingState.Ready))
                {
                    await HandleInitializationError("Failed to transition to Ready state");
                    return;
                }
                
                // Refresh mute states to ensure UI is in sync with actual device states
                _mainWindow.MuteService?.RefreshMuteStates();
                
                var totalTime = (DateTime.Now - startTime).TotalMilliseconds;
                _loggingService.Log($"[STATE] Device initialization completed successfully in {totalTime:F0}ms", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error during device initialization: {ex.Message}", LogLevel.Error);
                await HandleInitializationError($"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// PHASE 2 FIX: Centralized error handling with proper state recovery
        /// </summary>
        private async Task HandleInitializationError(string errorReason)
        {
            _loggingService.Log($"[STATE] Initialization error: {errorReason}", LogLevel.Error);
            
            // 🔧 PHASE 2 FIX: Atomic error recovery - reset to NotStarted
            lock (_stateLock)
            {
                var previousState = _loadingState;
                _loadingState = DeviceLoadingState.NotStarted;
                
                _loggingService.Log($"[STATE] Error recovery: {previousState} → NotStarted", LogLevel.Debug);
                
                // Update legacy flags
                AudioDeviceViewModel.IsLoadingDevices = false;
                _devicesLoaded = false;
                
                // Fire state change event
                StateChanged?.Invoke(this, new DeviceStateChangedEventArgs 
                { 
                    OldState = previousState, 
                    NewState = DeviceLoadingState.NotStarted, 
                    Timestamp = DateTime.Now 
                });
            }
            
            // Brief delay before potential retry
            await Task.Delay(1000);
        }

        public void RestoreFrozenDevicesToSavedLevels()
        {
            try
            {
                _loggingService.Log("Restoring frozen devices to saved levels after enumeration...");
                
                int restoredCount = 0;
                int totalFrozenCount = 0;
                
                // Check all device collections for frozen devices
                var allDevices = _mainWindow.Devices.Concat(_mainWindow.PlaybackDevices).ToList();
                
                foreach (var device in allDevices)
                {
                    if (device.IsSelected) // Frozen device
                    {
                        totalFrozenCount++;
                        
                        _loggingService.Log($"Processing frozen device: {device.Name} - Current: {device.VolumeDb}dB, Frozen: {device.FrozenVolumeDb:F1}dB");
                        
                        // Get the native device for volume operations
                        var nativeDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(device.Id);
                        if (nativeDevice != null)
                        {
                            try
                            {
                                // Get current actual volume from device (not UI display)
                                float currentActualVolume = nativeDevice.GetVolumeLevel();
                                
                                // Check if current volume differs from frozen level
                                float volumeDifference = Math.Abs(currentActualVolume - device.FrozenVolumeDb);
                                if (volumeDifference > 0.1f) // Only restore if significant difference
                                {
                                    // Only restore if not paused
                                    if (!_mainWindow.PauseManager.IsPaused)
                                    {
                                        // Restore to frozen level
                                        nativeDevice.SetVolumeLevel(device.FrozenVolumeDb);
                                    
                                        // Update UI to match
                                        device.VolumeDb = device.FrozenVolumeDb.ToString("F1");
                                        
                                        _loggingService.Log($"Restored {device.Name}: {currentActualVolume:F1}dB → {device.FrozenVolumeDb:F1}dB");
                                        restoredCount++;
                                    }
                                    else
                                    {
                                        _loggingService.Log($"Skipped restoring {device.Name} - application is paused");
                                    }
                                }
                                else
                                {
                                    _loggingService.Log($"Device {device.Name} already at frozen level ({currentActualVolume:F1}dB)");
                                }
                            }
                            catch (Exception ex)
                            {
                                _loggingService.Log($"Error restoring frozen device {device.Name}: {ex.Message}");
                            }
                        }
                        else
                        {
                            _loggingService.Log($"Warning: Native device not found for frozen device {device.Name}");
                        }
                    }
                }
                
                _loggingService.Log($"Frozen device restoration complete: {restoredCount}/{totalFrozenCount} devices restored");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error during frozen device restoration: {ex.Message}");
            }
        }

        public void RestoreSelectionsFromPersistentState()
        {
            if (!TrySetLoadingState(DeviceLoadingState.RestoringSavedState))
            {
                _loggingService.Log("Cannot restore selections - invalid state transition", LogLevel.Debug);
                return;
            }
            
            try
            {
                // Load settings without triggering selection events
                _mainWindow._settingsManager?.LoadDebugMode();
                _mainWindow._settingsManager?.LoadLogPanelHeight();
                _mainWindow._settingsManager?.LoadSelection();
                _mainWindow._frozenVolumeManager?.LoadFrozenVolumes();
                
                _loggingService.Log("[STATE] Selection restoration completed", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error restoring selections: {ex.Message}", LogLevel.Debug);
            }
        }

        public void ProcessDeviceSelectionChange(AudioDeviceViewModel device, bool isSelected)
        {
            try
            {
                // Update tab headers immediately
                _mainWindow._tabManager?.UpdateTabHeaders();
                
                if (isSelected)
                {
                    // Capture current volume for newly selected device
                    _mainWindow._frozenVolumeManager?.CaptureCurrentVolumeAsFrozen(device);
                    _loggingService.Log($"Device frozen: {device.Name}", LogLevel.Debug);
                }
                else
                {
                    // Clear frozen volume when unfreezing to prevent re-freeze issues
                    device.FrozenVolumeDb = 0.0f;
                    _loggingService.Log($"Device unfrozen: {device.Name}", LogLevel.Debug);
                }
                
                // Save selection state to registry - always allow for user-initiated actions
                // Registry protection was designed for system processes, not user interactions
                // Only pass frozen volume when freezing, not when unfreezing
                float? frozenVolume = isSelected ? device.FrozenVolumeDb : null;
                _mainWindow._debouncedRegistryManager?.ScheduleDeviceUpdate(
                    device.Id, isSelected, frozenVolume, Settings.RegistrySaveType.SingleDevice);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error processing device selection change: {ex.Message}", LogLevel.Debug);
            }
        }

        public void SaveDeviceStates()
        {
            _mainWindow._settingsManager?.SaveSelection();
            _mainWindow._frozenVolumeManager?.SaveFrozenVolumes(suppressLog: false);
        }

        /// <summary>
        /// Sets the devices loaded flag to enable registry saving for selection changes
        /// Called by DeviceManager when device enumeration and initialization is complete
        /// </summary>
        public void SetDevicesLoaded(bool loaded)
        {
            _devicesLoaded = loaded;
            _loggingService.Log($"DeviceStateManager: _devicesLoaded set to {loaded}", LogLevel.Debug);
        }
        
        /// <summary>
        /// Resets the device state manager to initial state for complete device reset
        /// </summary>
        public void Reset()
        {
            lock (_stateLock)
            {
                _loadingState = DeviceLoadingState.NotStarted;
                _devicesLoaded = false;
                _loggingService.Log("DeviceStateManager reset to initial state", LogLevel.Debug);
            }
        }
    }
}