// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for handling volume text box interactions and editing
    /// Manages text input, validation, and conversion for volume editing
    /// </summary>
    public interface IVolumeTextEditingService
    {
        /// <summary>
        /// Handles mouse click on volume text to start editing
        /// </summary>
        /// <param name="textBox">TextBox that was clicked</param>
        /// <param name="device">Device associated with the text box</param>
        void HandleVolumeTextClick(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device);

        /// <summary>
        /// Handles key press events in volume text box
        /// </summary>
        /// <param name="textBox">TextBox that received key press</param>
        /// <param name="e">Key event arguments</param>
        /// <param name="device">Device associated with the text box</param>
        void HandleVolumeTextKeyDown(System.Windows.Controls.TextBox textBox, System.Windows.Input.KeyEventArgs e, AudioDeviceViewModel device);

        /// <summary>
        /// Handles text box losing focus to apply changes
        /// </summary>
        /// <param name="textBox">TextBox that lost focus</param>
        /// <param name="device">Device associated with the text box</param>
        void HandleVolumeTextLostFocus(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device);

        /// <summary>
        /// Handles text box loaded event for initialization
        /// </summary>
        /// <param name="textBox">TextBox that was loaded</param>
        void HandleVolumeTextLoaded(System.Windows.Controls.TextBox textBox);

        /// <summary>
        /// Applies a volume edit from text input
        /// </summary>
        /// <param name="textBox">TextBox containing the volume text</param>
        /// <param name="device">Device to apply the volume to</param>
        /// <returns>True if volume was applied successfully</returns>
        bool ApplyVolumeEdit(System.Windows.Controls.TextBox textBox, AudioDeviceViewModel device);

        /// <summary>
        /// Validates volume text input
        /// </summary>
        /// <param name="volumeText">Text to validate</param>
        /// <returns>True if text represents a valid volume</returns>
        bool IsValidVolumeText(string volumeText);

        /// <summary>
        /// Formats a volume value for display in text box
        /// </summary>
        /// <param name="volumeDb">Volume in dB</param>
        /// <returns>Formatted text representation</returns>
        string FormatVolumeForDisplay(float volumeDb);

        /// <summary>
        /// Finds a parent element of specific type in the visual tree
        /// </summary>
        /// <typeparam name="T">Type of parent to find</typeparam>
        /// <param name="child">Child element to start search from</param>
        /// <returns>Parent element of type T, or null if not found</returns>
        T? FindParent<T>(DependencyObject child) where T : DependencyObject;
    }
}