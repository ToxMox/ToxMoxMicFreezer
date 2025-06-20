// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for handling volume text box interactions and editing
    /// Manages text input, validation, and conversion for volume editing
    /// </summary>
    public class VolumeTextEditingService : IVolumeTextEditingService
    {
        private readonly ILoggingService _loggingService;
        private readonly IVolumeCalculationService _volumeCalculationService;
        private readonly IVolumeApplicationService _volumeApplicationService;
        private readonly IVolumeDragStateService _dragStateService;

        public VolumeTextEditingService(
            ILoggingService loggingService,
            IVolumeCalculationService volumeCalculationService,
            IVolumeApplicationService volumeApplicationService,
            IVolumeDragStateService dragStateService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _volumeCalculationService = volumeCalculationService ?? throw new ArgumentNullException(nameof(volumeCalculationService));
            _volumeApplicationService = volumeApplicationService ?? throw new ArgumentNullException(nameof(volumeApplicationService));
            _dragStateService = dragStateService ?? throw new ArgumentNullException(nameof(dragStateService));
        }

        /// <summary>
        /// Handles mouse click on volume text to start editing
        /// </summary>
        public void HandleVolumeTextClick(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device)
        {
            try
            {
                // Mark device as being edited
                _dragStateService.StartEditingDevice(device.Id);
                
                // Select all text for easy replacement
                textBox.SelectAll();
                textBox.Focus();
                
                _loggingService.Log($"Started text editing for device: {device.Name}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error handling volume text click: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Handles key press events in volume text box
        /// </summary>
        public void HandleVolumeTextKeyDown(System.Windows.Controls.TextBox textBox, System.Windows.Input.KeyEventArgs e, AudioDeviceViewModel device)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    // Apply the edit and remove focus
                    ApplyVolumeEdit(textBox, device);
                    
                    // Remove focus from textbox
                    var parent = FindParent<System.Windows.Controls.Panel>(textBox);
                    if (parent != null)
                    {
                        parent.Focus();
                    }
                    
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // Cancel edit - restore original value
                    var volumeValue = float.TryParse(device.VolumeDb, out float vol) ? vol : 0f;
                    textBox.Text = FormatVolumeForDisplay(volumeValue);
                    
                    // Remove focus from textbox
                    var parent = FindParent<System.Windows.Controls.Panel>(textBox);
                    if (parent != null)
                    {
                        parent.Focus();
                    }
                    
                    // Stop editing the device
                    _dragStateService.StopEditingDevice(device.Id);
                    
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error handling volume text key down: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Handles text box losing focus to apply changes
        /// </summary>
        public void HandleVolumeTextLostFocus(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device)
        {
            try
            {
                // Apply any pending edits
                ApplyVolumeEdit(textBox, device);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error handling volume text lost focus: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Handles text box loaded event for initialization
        /// </summary>
        public void HandleVolumeTextLoaded(System.Windows.Controls.TextBox textBox)
        {
            try
            {
                // Set initial formatting and behavior
                textBox.TextAlignment = TextAlignment.Center;
                
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error handling volume text loaded: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Applies a volume edit from text input
        /// </summary>
        public bool ApplyVolumeEdit(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device)
        {
            try
            {
                var volumeText = textBox.Text?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(volumeText))
                {
                    // Restore original value if empty
                    var volumeValue = float.TryParse(device.VolumeDb, out float vol) ? vol : 0f;
                    textBox.Text = FormatVolumeForDisplay(volumeValue);
                    _dragStateService.StopEditingDevice(device.Id);
                    return false;
                }

                // Parse the volume text
                var parsedVolume = _volumeCalculationService.ParseVolumeText(volumeText);
                if (parsedVolume.HasValue)
                {
                    // Apply the volume change
                    bool success = _volumeApplicationService.ApplyVolumeChange(device, parsedVolume.Value);
                    
                    if (success)
                    {
                        // Update text box with formatted value
                        textBox.Text = FormatVolumeForDisplay(parsedVolume.Value);
                        _loggingService.Log($"Applied volume edit for {device.Name}: {parsedVolume.Value:F1}dB", LogLevel.Debug);
                    }
                    else
                    {
                        // Restore original value on failure
                        var volumeValue = float.TryParse(device.VolumeDb, out float vol) ? vol : 0f;
                        textBox.Text = FormatVolumeForDisplay(volumeValue);
                    }
                    
                    _dragStateService.StopEditingDevice(device.Id);
                    return success;
                }
                else
                {
                    // Invalid input - restore original value
                    var volumeValue = float.TryParse(device.VolumeDb, out float vol) ? vol : 0f;
                    textBox.Text = FormatVolumeForDisplay(volumeValue);
                    _dragStateService.StopEditingDevice(device.Id);
                    
                    _loggingService.Log($"Invalid volume text input: {volumeText}", LogLevel.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error applying volume edit: {ex.Message}", LogLevel.Error);
                
                // Restore original value on error
                var volumeValue = float.TryParse(device.VolumeDb, out float vol) ? vol : 0f;
                textBox.Text = FormatVolumeForDisplay(volumeValue);
                _dragStateService.StopEditingDevice(device.Id);
                return false;
            }
        }

        /// <summary>
        /// Validates volume text input
        /// </summary>
        public bool IsValidVolumeText(string volumeText)
        {
            return _volumeCalculationService.ParseVolumeText(volumeText).HasValue;
        }

        /// <summary>
        /// Formats a volume value for display in text box
        /// </summary>
        public string FormatVolumeForDisplay(float volumeDb)
        {
            return $"{volumeDb:F1}";
        }

        /// <summary>
        /// Finds a parent element of specific type in the visual tree
        /// </summary>
        public T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            try
            {
                var parent = VisualTreeHelper.GetParent(child);
                
                while (parent != null)
                {
                    if (parent is T typedParent)
                        return typedParent;
                    
                    parent = VisualTreeHelper.GetParent(parent);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error finding parent element: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }
    }
}