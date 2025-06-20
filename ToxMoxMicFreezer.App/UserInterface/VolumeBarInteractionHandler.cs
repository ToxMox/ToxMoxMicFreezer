// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Handles all volume bar interactions including mouse events, drag operations, 
    /// double-click detection, and volume text editing
    /// </summary>
    public class VolumeBarInteractionHandler
    {
        private readonly MainWindow _mainWindow;

        // Volume bar interaction fields
        private bool _isDraggingVolumeBar = false;
        private AudioDeviceViewModel? _draggingDevice = null;
        
        // Performance optimization: Cache native device during drag to avoid dictionary lookups
        private NAudioAudioDevice? _cachedDragDevice = null;
        private float _lastDisplayedVolume = float.MinValue;
        
        // Volume change throttling to prevent feedback loop
        private DateTime _lastVolumeSet = DateTime.MinValue;
        private const int VOLUME_SET_THROTTLE_MS = 16; // 60 FPS maximum (~16.67ms)
        private float _pendingVolumeDb = float.NaN;
        private System.Timers.Timer? _volumeThrottleTimer;
        
        // Track devices being manually edited to temporarily pause volume monitoring
        private readonly HashSet<string> _devicesBeingEdited = new();

        public VolumeBarInteractionHandler(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }


        
        /// <summary>
        /// Handles volume bar mouse left button down events - simple click to set volume and drag
        /// </summary>
        public void OnVolumeBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioDeviceViewModel device)
            {
                // Setup drag state BEFORE volume update to ensure ExecuteVolumeChange() works
                _devicesBeingEdited.Add(device.Id);
                _cachedDragDevice = _mainWindow._deviceManager?.GetCachedNativeDevice(device.Id);
                _isDraggingVolumeBar = true;
                _draggingDevice = device;
                _lastDisplayedVolume = float.MinValue;
                
                // Pause volume notifications for this device to prevent feedback loop
                _mainWindow._volumeChangeManager?.PauseVolumeNotifications(device.Id);
                
                // Process the click position - set volume at click location
                var clickPosition = e.GetPosition(border);
                UpdateVolumeFromMousePosition(border, clickPosition, device);
                
                // Capture mouse for dragging
                border.CaptureMouse();
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles volume bar mouse move events for dragging
        /// </summary>
        public void OnVolumeBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingVolumeBar && _draggingDevice != null && sender is Border border)
            {
                // Simple dragging - update volume based on mouse position
                UpdateVolumeFromMousePosition(border, e.GetPosition(border), _draggingDevice);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles volume bar mouse left button up events
        /// </summary>
        public void OnVolumeBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingVolumeBar && sender is Border border)
            {
                // Final UI update to ensure accuracy after drag completion
                if (_draggingDevice != null && _cachedDragDevice != null)
                {
                    try
                    {
                        float finalVolume = _cachedDragDevice.GetVolumeLevel();
                        _draggingDevice.VolumeDb = finalVolume.ToString("F1");
                    }
                    catch
                    {
                        // Ignore errors in final update - user will see previous value
                    }
                }
                
                // Remove device from editing set to re-enable volume change notifications
                if (_draggingDevice != null)
                {
                    _devicesBeingEdited.Remove(_draggingDevice.Id);
                    
                    // Resume volume notifications for this device
                    _mainWindow._volumeChangeManager?.ResumeVolumeNotifications(_draggingDevice.Id);
                }
                
                // Clear drag state
                _cachedDragDevice = null;
                _lastDisplayedVolume = float.MinValue;
                _isDraggingVolumeBar = false;
                _draggingDevice = null;
                border.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles volume bar right-click events to set volume to 0dB
        /// </summary>
        public void OnVolumeBarMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioDeviceViewModel device)
            {
                try
                {
                    // Set volume to 0dB if the device supports it
                    float targetDb = 0.0f;
                    
                    // Clamp to device's supported range
                    targetDb = Math.Max(device.MinVolumeDb, Math.Min(device.MaxVolumeDb, targetDb));
                    
                    // Set the volume using cached device (SetDeviceVolume handles suppression)
                    SetDeviceVolume(device.Id, targetDb);
                    
                    // Update the display
                    device.VolumeDb = targetDb.ToString("F1");
                    
                    // If device is selected (frozen), update the frozen volume setting
                    if (device.IsSelected)
                    {
                        device.FrozenVolumeDb = targetDb;
                        _mainWindow._debouncedRegistryManager?.ScheduleDeviceUpdate(
                            device.Id, null, device.FrozenVolumeDb, Settings.RegistrySaveType.SingleDevice);
                    }
                    
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendLog($"Error setting volume to 0dB: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles volume text mouse left button down for editing
        /// </summary>
        public void OnVolumeTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement? element = sender as FrameworkElement;
            if (element != null)
            {
                var cell = FindParent<DataGridCell>(element);
                var dataGrid = cell != null ? FindParent<DataGrid>(cell) : null;
                
                if (dataGrid != null && cell != null)
                {
                    // Make sure the row is selected first
                    var row = FindParent<DataGridRow>(cell);
                    if (row != null)
                    {
                        row.IsSelected = true;
                        dataGrid.CurrentCell = new DataGridCellInfo(row.Item, cell.Column);
                        
                        // Add device to editing set to pause volume monitoring
                        if (row.Item is AudioDeviceViewModel device)
                        {
                            _devicesBeingEdited.Add(device.Id);
                            _mainWindow.AppendLog($"Started editing device: {device.Name} (ID: {device.Id})");
                        }
                    }
                    
                    // Enter edit mode
                    dataGrid.BeginEdit();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handles volume text box key down events
        /// </summary>
        public void OnVolumeTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (e.Key == Key.Enter)
                {
                    // Apply the change
                    ApplyVolumeEdit(textBox);
                    textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                    
                    // Remove device from editing set
                    var editCell = FindParent<DataGridCell>(textBox);
                    var editRow = editCell != null ? FindParent<DataGridRow>(editCell) : null;
                    if (editRow?.Item is AudioDeviceViewModel device)
                    {
                        _devicesBeingEdited.Remove(device.Id);
                        _mainWindow.AppendLog($"Finished editing device: {device.Name} (Enter pressed)");
                    }
                    
                    // Exit edit mode
                    var cell = FindParent<DataGridCell>(textBox);
                    var dataGrid = cell != null ? FindParent<DataGrid>(cell) : null;
                    dataGrid?.CommitEdit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // Cancel edit
                    var cell = FindParent<DataGridCell>(textBox);
                    var dataGrid = cell != null ? FindParent<DataGrid>(cell) : null;
                    dataGrid?.CancelEdit();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handles volume text box lost focus events
        /// </summary>
        public void OnVolumeTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                ApplyVolumeEdit(textBox);
                
                // Remove device from editing set
                var cell = FindParent<DataGridCell>(textBox);
                var row = cell != null ? FindParent<DataGridRow>(cell) : null;
                if (row?.Item is AudioDeviceViewModel device)
                {
                    _devicesBeingEdited.Remove(device.Id);
                    _mainWindow.AppendLog($"Finished editing device: {device.Name} (Lost focus)");
                }
            }
        }

        /// <summary>
        /// Handles volume text box loaded events
        /// </summary>
        public void OnVolumeTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        /// <summary>
        /// Checks if a device is currently being edited
        /// </summary>
        public bool IsDeviceBeingEdited(string deviceId)
        {
            return _devicesBeingEdited.Contains(deviceId);
        }

        /// <summary>
        /// Updates volume from mouse position during dragging with performance optimizations and throttling
        /// </summary>
        private void UpdateVolumeFromMousePosition(Border volumeBarBorder, System.Windows.Point mousePosition, AudioDeviceViewModel device)
        {
            try
            {
                // Calculate percentage based on mouse position
                double percentage = Math.Max(0, Math.Min(1, mousePosition.X / volumeBarBorder.ActualWidth));
                
                // Convert percentage to dB value using LOGARITHMIC AUDIO TAPER for professional audio control
                float targetVolumeDb = VolumeCalculationHelper.LogarithmicPercentageToDb(percentage, device.MinVolumeDb, device.MaxVolumeDb);
                
                // IMMEDIATE UI UPDATE (no throttling) - provides responsive visual feedback
                if (Math.Abs(targetVolumeDb - _lastDisplayedVolume) > 0.1f)
                {
                    device.VolumeDb = targetVolumeDb.ToString("F1");
                    _lastDisplayedVolume = targetVolumeDb;
                }
                
                // THROTTLED HARDWARE CALL - prevents feedback loop by limiting COM calls to 60 FPS
                var now = DateTime.Now;
                _pendingVolumeDb = targetVolumeDb; // Always store latest value
                
                if ((now - _lastVolumeSet).TotalMilliseconds >= VOLUME_SET_THROTTLE_MS)
                {
                    // Execute immediately if enough time has passed
                    ExecuteVolumeChange();
                }
                else
                {
                    // Schedule delayed execution to maintain throttling
                    _volumeThrottleTimer?.Stop();
                    _volumeThrottleTimer = new System.Timers.Timer(VOLUME_SET_THROTTLE_MS) { AutoReset = false };
                    _volumeThrottleTimer.Elapsed += (s, e) => ExecuteVolumeChange();
                    _volumeThrottleTimer.Start();
                }
                
                // If device is selected (frozen), update the frozen volume setting
                if (device.IsSelected)
                {
                    device.FrozenVolumeDb = targetVolumeDb;
                    _mainWindow._debouncedRegistryManager?.ScheduleDeviceUpdate(
                        device.Id, null, targetVolumeDb, Settings.RegistrySaveType.ContinuousEdit);
                }
            }
            catch (COMException ex)
            {
                // Performance optimization: Use specific exception types and avoid string formatting in hot path
                // Defer detailed logging to reduce impact during high-frequency volume dragging
                _mainWindow.AppendLog($"COM error in volume update: {ex.HResult:X8}");
            }
            catch (Exception)
            {
                // Performance optimization: Minimal logging in hot path - avoid string formatting during drag
                _mainWindow.AppendLog("Volume update error during drag");
            }
        }
        
        /// <summary>
        /// Executes throttled volume change to hardware device
        /// </summary>
        private void ExecuteVolumeChange()
        {
            if (!float.IsNaN(_pendingVolumeDb) && _cachedDragDevice != null)
            {
                try
                {
                    // Performance optimization: Use cached native device to avoid dictionary lookups
                    _cachedDragDevice.SetVolumeLevel(_pendingVolumeDb);
                    
                    _lastVolumeSet = DateTime.Now;
                    _pendingVolumeDb = float.NaN;
                }
                catch (COMException ex)
                {
                    _mainWindow.AppendLog($"COM error in throttled volume change: {ex.HResult:X8}");
                }
                catch (Exception)
                {
                }
            }
            else if (!float.IsNaN(_pendingVolumeDb) && _draggingDevice != null)
            {
                // Fallback to original method if cache failed
                try
                {
                    SetDeviceVolumeInternal(_draggingDevice.Id, _pendingVolumeDb);
                    _lastVolumeSet = DateTime.Now;
                    _pendingVolumeDb = float.NaN;
                }
                catch (Exception)
                {
                    // Minimal logging in hot path
                }
            }
        }

        /// <summary>
        /// Sets the volume for a specific device with suppression
        /// </summary>
        private void SetDeviceVolume(string deviceId, float volumeDb)
        {
            try
            {
                // Add device to editing set to suppress volume change notifications
                _devicesBeingEdited.Add(deviceId);
                
                try
                {
                    SetDeviceVolumeInternal(deviceId, volumeDb);
                }
                finally
                {
                    // Remove device from editing set after operation
                    _devicesBeingEdited.Remove(deviceId);
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error setting device volume: {ex.Message}");
                _devicesBeingEdited.Remove(deviceId);
            }
        }

        /// <summary>
        /// Sets the volume for a specific device without suppression (internal method)
        /// Used for non-dragging scenarios where caching is not available
        /// </summary>
        private void SetDeviceVolumeInternal(string deviceId, float volumeDb)
        {
            try
            {
                // Use device manager lookup (slower but necessary for non-dragging scenarios)
                var device = _mainWindow._deviceManager?.GetCachedNativeDevice(deviceId);
                if (device == null)
                {
                    _mainWindow.AppendLog($"Device not found or inaccessible");
                    return;
                }

                // Intent-based approach: UI changes don't need feedback loop prevention
                // External change detection will know this is a UI-initiated change
                device.SetVolumeLevel(volumeDb);
                
            }
            catch (COMException ex)
            {
                // More specific exception handling - avoid string formatting in hot path
                _mainWindow.AppendLog($"COM error setting device volume: {ex.HResult:X8}");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error setting device volume: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies volume edit from text box input
        /// </summary>
        private void ApplyVolumeEdit(System.Windows.Controls.TextBox textBox)
        {
            try
            {
                if (textBox.DataContext is AudioDeviceViewModel device)
                {
                    string input = textBox.Text.Trim().ToLower();
                    
                    // Remove "db" suffix if present
                    if (input.EndsWith("db"))
                        input = input.Substring(0, input.Length - 2).Trim();
                    
                    // Parse the dB value
                    if (float.TryParse(input, out float volumeDb))
                    {
                        // Clamp to device range
                        volumeDb = Math.Max(device.MinVolumeDb, Math.Min(device.MaxVolumeDb, volumeDb));
                        
                        // Set the volume using cached device (SetDeviceVolume handles suppression)
                        SetDeviceVolume(device.Id, volumeDb);
                        
                        // Update the display
                        device.VolumeDb = volumeDb.ToString("F1");
                        
                        // If device is selected (frozen), update the frozen volume setting
                        if (device.IsSelected)
                        {
                            device.FrozenVolumeDb = volumeDb;
                            _mainWindow._debouncedRegistryManager?.ScheduleDeviceUpdate(
                                device.Id, null, device.FrozenVolumeDb, Settings.RegistrySaveType.SingleDevice);
                        }
                    }
                    else
                    {
                        _mainWindow.AppendLog($"Invalid volume value: {textBox.Text}");
                        // Reset to current value
                        textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error applying volume edit: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to find parent control of a specific type
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            return parent is T ? (T)parent : FindParent<T>(parent);
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Dispose()
        {
            // Resume notifications for any device that might still be paused
            if (_draggingDevice != null)
            {
                _mainWindow._volumeChangeManager?.ResumeVolumeNotifications(_draggingDevice.Id);
            }
            
            _volumeThrottleTimer?.Stop();
            _volumeThrottleTimer?.Dispose();
            _volumeThrottleTimer = null;
        }
        
    }
}