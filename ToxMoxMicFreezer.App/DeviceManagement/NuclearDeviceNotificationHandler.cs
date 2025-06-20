// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Timers;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ToxMoxMicFreezer.App.DeviceManagement
{
    /// <summary>
    /// NUCLEAR DEVICE HANDLER: Uses process restart instead of disposal/cleanup
    /// Eliminates ALL COM threading and disposal issues by letting OS handle cleanup
    /// </summary>
    public class NuclearDeviceNotificationHandler : IMMNotificationClient, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private readonly ProcessRestartHandler _restartHandler;
        
        // Simple debounce system - just wait for changes to stop
        private System.Timers.Timer? _debounceTimer;
        private bool _isRestarting = false;
        private const int DEBOUNCE_DELAY_MS = 2000; // 2 second delay for changes to settle
        
        // NAudio enumerator for notifications
        private MMDeviceEnumerator? _notificationEnumerator;
        private bool _disposed = false;
        
        public NuclearDeviceNotificationHandler(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
            _restartHandler = new ProcessRestartHandler(mainWindow);
        }
        
        /// <summary>
        /// Initialize and register for device notifications
        /// </summary>
        public bool Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NuclearDeviceNotificationHandler));
                
            try
            {
                _mainWindow.AppendLog("Initializing NUCLEAR device notifications (process restart on changes)...");
                
                // Create NAudio enumerator for notifications
                _notificationEnumerator = new MMDeviceEnumerator();
                
                // Register this handler as notification callback
                _notificationEnumerator.RegisterEndpointNotificationCallback(this);
                
                _mainWindow.AppendLog("NUCLEAR device notifications registered successfully");
                return true;
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Failed to initialize NUCLEAR device notifications: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Triggers NUCLEAR restart with debouncing - waits for changes to stop before restarting
        /// </summary>
        private void TriggerNuclearRestart(string reason)
        {
            if (_disposed || _isRestarting) return;
            
            _dispatcher.BeginInvoke(() => {
                _mainWindow.AppendLog($"Device change detected: {reason} - starting NUCLEAR restart debounce timer");
                
                // Show overlay immediately on first change
                if (_debounceTimer == null)
                {
                    _mainWindow.ShowDeviceResetOverlay($"Device changes detected ({reason}) - will restart");
                }
                
                // Reset debounce timer - this implements "wait until changes stop"
                _debounceTimer?.Stop();
                _debounceTimer?.Dispose();
                
                _debounceTimer = new System.Timers.Timer(DEBOUNCE_DELAY_MS);
                _debounceTimer.Elapsed += OnDebounceComplete;
                _debounceTimer.AutoReset = false;
                _debounceTimer.Start();
            });
        }
        
        /// <summary>
        /// Called when debounce timer completes - no more device changes for 2 seconds
        /// EXECUTE NUCLEAR RESTART
        /// </summary>
        private void OnDebounceComplete(object? sender, ElapsedEventArgs e)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            
            if (_isRestarting) return;
            _isRestarting = true;
            
            // NUCLEAR OPTION: Restart entire process
            _restartHandler.RestartProcess("Device changes settled");
        }
        
        // IMMNotificationClient implementation - all trigger NUCLEAR restart
        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            TriggerNuclearRestart($"State Change: {deviceId} → {newState}");
        }
        
        public void OnDeviceAdded(string deviceId)
        {
            TriggerNuclearRestart($"Device Added: {deviceId}");
        }
        
        public void OnDeviceRemoved(string deviceId)
        {
            TriggerNuclearRestart($"Device Removed: {deviceId}");
        }
        
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string deviceId)
        {
            // Default device changes are irrelevant to volume management - ignored
        }
        
        public void OnPropertyValueChanged(string deviceId, NAudio.CoreAudioApi.PropertyKey key)
        {
            // Property changes don't require restart - volume changes are handled elsewhere
            // Intentionally ignored to prevent restart spam
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                _mainWindow.AppendLog("Disposing NUCLEAR device notification handler (minimal cleanup)...");
                
                // Stop debounce timer
                _debounceTimer?.Stop();
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                
                // Minimal COM cleanup - if this fails, process restart will handle it
                try
                {
                    _notificationEnumerator?.UnregisterEndpointNotificationCallback(this);
                    _notificationEnumerator?.Dispose();
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendLog($"Error in minimal COM cleanup (will be handled by restart): {ex.Message}");
                }
                
                _restartHandler?.Dispose();
                
                _mainWindow.AppendLog("NUCLEAR device notification handler disposed");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error disposing NUCLEAR handler (will be handled by restart): {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}