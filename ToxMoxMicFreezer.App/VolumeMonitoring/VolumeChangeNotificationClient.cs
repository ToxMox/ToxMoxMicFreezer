// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ToxMoxMicFreezer.App.VolumeMonitoring
{
    /// <summary>
    /// Listens for volume change events from Windows Core Audio API
    /// Provides real-time volume change notifications without polling
    /// </summary>
    public class VolumeChangeNotificationClient : IMMNotificationClient, IDisposable
    {
        private MMDeviceEnumerator? _notificationEnumerator;
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private bool _disposed = false;
        
        // Event fired when any device volume changes
        public event Action<string, float>? VolumeChanged;
        
        public VolumeChangeNotificationClient(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
        }
        
        /// <summary>
        /// Initialize and register for volume change notifications
        /// </summary>
        public bool Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VolumeChangeNotificationClient));
                
            try
            {
                _mainWindow.AppendLog("Initializing volume change notifications...");
                
                _notificationEnumerator = new MMDeviceEnumerator();
                _notificationEnumerator.RegisterEndpointNotificationCallback(this);
                
                _mainWindow.AppendLog("Volume change notifications registered successfully");
                return true;
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Failed to initialize volume change notifications: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Called when any device property changes - we filter for volume changes
        /// </summary>
        public void OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            if (_disposed) return;
            
            try
            {
                // Log all property changes initially to identify volume PropertyKey
                _dispatcher.BeginInvoke(() => {
                    _mainWindow.AppendLog($"Property changed: Device {deviceId}, Key: {key.formatId}:{key.propertyId}");
                });
                
                // Check if this might be a volume-related property change
                if (IsVolumeProperty(key))
                {
                    _dispatcher.BeginInvoke(() => {
                        _mainWindow.AppendLog($"VOLUME PROPERTY DETECTED: {deviceId}");
                    });
                    
                    try
                    {
                        // Get current volume from NAudio device cache
                        var device = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                        if (device != null)
                        {
                            float currentVolume = device.GetVolumeLevel();
                            
                            // Fire event on UI thread
                            _dispatcher.BeginInvoke(() => {
                                _mainWindow.AppendLog($"Volume change event: {deviceId} -> {currentVolume:F1}dB");
                                VolumeChanged?.Invoke(deviceId, currentVolume);
                            });
                        }
                        else
                        {
                            _dispatcher.BeginInvoke(() => {
                                _mainWindow.AppendLog($"Device not found in cache: {deviceId}");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _dispatcher.BeginInvoke(() => 
                            _mainWindow.AppendLog($"Error reading volume for {deviceId}: {ex.Message}"));
                    }
                }
            }
            catch (Exception ex)
            {
                _dispatcher.BeginInvoke(() => 
                    _mainWindow.AppendLog($"Error in volume change notification: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Check if this PropertyKey represents a volume change
        /// For now, return false for all so we just log without processing
        /// </summary>
        private bool IsVolumeProperty(PropertyKey key)
        {
            // DEBUGGING: Return false for all properties so we just log them
            // We'll analyze the logs to find the correct volume PropertyKey
            return false;
        }
        
        // Empty implementations for other IMMNotificationClient methods
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) 
        {
            // We don't need these for volume change notifications
        }
        
        public void OnDeviceAdded(string deviceId) 
        {
            // We don't need these for volume change notifications
        }
        
        public void OnDeviceRemoved(string deviceId) 
        {
            // We don't need these for volume change notifications
        }
        
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string deviceId) 
        {
            // We don't need these for volume change notifications
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                _mainWindow.AppendLog("Disposing volume change notifications...");
                
                if (_notificationEnumerator != null)
                {
                    try
                    {
                        _notificationEnumerator.UnregisterEndpointNotificationCallback(this);
                    }
                    catch (Exception ex)
                    {
                        _mainWindow.AppendLog($"Error unregistering volume change notifications: {ex.Message}");
                    }
                    
                    _notificationEnumerator.Dispose();
                    _notificationEnumerator = null;
                }
                
                _mainWindow.AppendLog("Volume change notifications disposed successfully");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error disposing volume change notifications: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}