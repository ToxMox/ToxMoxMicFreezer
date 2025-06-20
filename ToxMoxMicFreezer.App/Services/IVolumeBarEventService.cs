// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;
using System.Windows.Input;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for handling volume bar and volume text editing events
    /// Manages user interactions with volume controls in the device grids
    /// </summary>
    public interface IVolumeBarEventService
    {
        /// <summary>
        /// Handles volume bar mouse left button down events (start drag)
        /// </summary>
        void OnVolumeBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles volume bar mouse move events (during drag)
        /// </summary>
        void OnVolumeBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e);

        /// <summary>
        /// Handles volume bar mouse left button up events (end drag)
        /// </summary>
        void OnVolumeBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles volume bar mouse right button down events (set to 0dB)
        /// </summary>
        void OnVolumeBarMouseRightButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles volume text mouse left button down events (start editing)
        /// </summary>
        void OnVolumeTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles volume text box key down events (Enter/Escape handling)
        /// </summary>
        void OnVolumeTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e);

        /// <summary>
        /// Handles volume text box lost focus events (commit changes)
        /// </summary>
        void OnVolumeTextBoxLostFocus(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles volume text box loaded events (initial setup)
        /// </summary>
        void OnVolumeTextBoxLoaded(object sender, RoutedEventArgs e);
    }
}