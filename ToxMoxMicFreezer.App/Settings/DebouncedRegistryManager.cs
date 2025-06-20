// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using LogLevel = ToxMoxMicFreezer.App.Services.LogLevel;

namespace ToxMoxMicFreezer.App.Settings
{
    /// <summary>
    /// Registry save type enum to control debouncing behavior
    /// </summary>
    public enum RegistrySaveType
    {
        Immediate,      // App shutdown, device enumeration - no debounce
        SingleDevice,   // One device changed (snowflake clicks) - no debounce for user actions
        ContinuousEdit, // Volume bar dragging (heavy debounce) - 500ms debounce
        BatchUpdate     // Multiple devices changed simultaneously - 200ms debounce
    }

    /// <summary>
    /// Pending device update data structure
    /// </summary>
    public class PendingDeviceUpdate
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool? IsSelected { get; set; }
        public float? FrozenVolume { get; set; }
        public DateTime LastUpdated { get; set; }
        public RegistrySaveType SaveType { get; set; }
    }

    /// <summary>
    /// Manages debounced registry operations to prevent registry I/O spam during user interactions
    /// </summary>
    public class DebouncedRegistryManager : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly System.Timers.Timer _continuousEditTimer;  // 500ms for volume drags
        private readonly System.Timers.Timer _singleDeviceTimer;    // 100ms for snowflake clicks
        private readonly System.Timers.Timer _batchUpdateTimer;     // 200ms for multiple changes
        private readonly Dictionary<string, PendingDeviceUpdate> _pendingUpdates = new();
        private readonly object _updateLock = new object();
        private bool _disposed = false;

        public DebouncedRegistryManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            
            // Initialize timers with their respective debounce intervals
            _continuousEditTimer = new System.Timers.Timer(500) { AutoReset = false };
            _continuousEditTimer.Elapsed += OnContinuousEditTimer;
            
            _singleDeviceTimer = new System.Timers.Timer(100) { AutoReset = false };
            _singleDeviceTimer.Elapsed += OnSingleDeviceTimer;
            
            _batchUpdateTimer = new System.Timers.Timer(200) { AutoReset = false };
            _batchUpdateTimer.Elapsed += OnBatchUpdateTimer;
        }

        /// <summary>
        /// Schedule a device update with the specified save type and debouncing behavior
        /// </summary>
        public void ScheduleDeviceUpdate(string deviceId, bool? isSelected, float? frozenVolume, RegistrySaveType saveType)
        {
            if (_disposed) return;
            
            try
            {
                lock (_updateLock)
                {
                    // Handle immediate saves (app shutdown, device enumeration)
                    if (saveType == RegistrySaveType.Immediate)
                    {
                        FlushPendingUpdates();
                        return;
                    }

                    // Update or create pending update
                    if (_pendingUpdates.TryGetValue(deviceId, out var existingUpdate))
                    {
                        // Update existing entry
                        if (isSelected.HasValue) existingUpdate.IsSelected = isSelected;
                        if (frozenVolume.HasValue) existingUpdate.FrozenVolume = frozenVolume;
                        existingUpdate.LastUpdated = DateTime.Now;
                        existingUpdate.SaveType = saveType; // Use latest save type
                    }
                    else
                    {
                        // Create new entry
                        _pendingUpdates[deviceId] = new PendingDeviceUpdate
                        {
                            DeviceId = deviceId,
                            IsSelected = isSelected,
                            FrozenVolume = frozenVolume,
                            LastUpdated = DateTime.Now,
                            SaveType = saveType
                        };
                    }

                    // Schedule appropriate timer based on save type
                    ScheduleTimerForSaveType(saveType);
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error scheduling device update: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Force immediate save of all pending updates (for app shutdown)
        /// </summary>
        public void SaveImmediately()
        {
            if (_disposed) return;
            
            try
            {
                lock (_updateLock)
                {
                    // Stop all timers
                    _continuousEditTimer.Stop();
                    _singleDeviceTimer.Stop();
                    _batchUpdateTimer.Stop();
                    
                    // Flush all pending updates
                    FlushPendingUpdates();
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error during immediate save: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Schedule the appropriate timer based on save type
        /// </summary>
        private void ScheduleTimerForSaveType(RegistrySaveType saveType)
        {
            switch (saveType)
            {
                case RegistrySaveType.ContinuousEdit:
                    // Reset continuous edit timer (500ms)
                    _continuousEditTimer.Stop();
                    _continuousEditTimer.Start();
                    break;
                    
                case RegistrySaveType.SingleDevice:
                    // Execute immediately for snowflake clicks - no debouncing needed
                    FlushPendingUpdates();
                    break;
                    
                case RegistrySaveType.BatchUpdate:
                    // Reset batch update timer (200ms)
                    _batchUpdateTimer.Stop();
                    _batchUpdateTimer.Start();
                    break;
            }
        }

        /// <summary>
        /// Handle continuous edit timer (for volume bar dragging)
        /// </summary>
        private void OnContinuousEditTimer(object? sender, ElapsedEventArgs e)
        {
            lock (_updateLock)
            {
                FlushPendingUpdates();
            }
        }

        /// <summary>
        /// Handle single device timer (for snowflake clicks)
        /// </summary>
        private void OnSingleDeviceTimer(object? sender, ElapsedEventArgs e)
        {
            lock (_updateLock)
            {
                FlushPendingUpdates();
            }
        }

        /// <summary>
        /// Handle batch update timer (for multiple simultaneous changes)
        /// </summary>
        private void OnBatchUpdateTimer(object? sender, ElapsedEventArgs e)
        {
            lock (_updateLock)
            {
                FlushPendingUpdates();
            }
        }

        /// <summary>
        /// Flush all pending updates to the registry using single-device operations
        /// </summary>
        private void FlushPendingUpdates()
        {
            if (_pendingUpdates.Count == 0) return;
            
            try
            {
                var singleDeviceManager = new SingleDeviceRegistryManager();
                int updateCount = 0;
                
                foreach (var update in _pendingUpdates.Values)
                {
                    try
                    {
                        // Update device selection if changed
                        if (update.IsSelected.HasValue)
                        {
                            singleDeviceManager.UpdateDeviceSelection(update.DeviceId, update.IsSelected.Value);
                            updateCount++;
                        }
                        
                        // Handle frozen volume updates (save or remove)
                        if (update.FrozenVolume.HasValue)
                        {
                            // Save frozen volume when provided (UI adjustments or new freezes)
                            singleDeviceManager.UpdateDeviceFrozenVolume(update.DeviceId, update.FrozenVolume.Value);
                            updateCount++;
                        }
                        else if (update.IsSelected.HasValue && !update.IsSelected.Value)
                        {
                            // Remove frozen volume only when explicitly unfreezing
                            singleDeviceManager.RemoveDeviceFrozenVolume(update.DeviceId);
                            updateCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _mainWindow.AppendLog($"Error updating device {update.DeviceId}: {ex.Message}", LogLevel.Debug);
                    }
                }
                
                _pendingUpdates.Clear();
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error flushing pending updates: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Dispose resources and flush any pending updates
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Save any pending updates immediately
                    SaveImmediately();
                    
                    // Dispose timers
                    _continuousEditTimer?.Dispose();
                    _singleDeviceTimer?.Dispose();
                    _batchUpdateTimer?.Dispose();
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendLog($"Error disposing DebouncedRegistryManager: {ex.Message}", LogLevel.Debug);
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}