// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using System.Windows.Input;
using ToxMoxMicFreezer.App.UserInterface;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for handling volume bar and volume text editing events
    /// Coordinates between UI events and VolumeBarInteractionHandler for volume control operations
    /// </summary>
    public class VolumeBarEventService : IVolumeBarEventService
    {
        private readonly VolumeBarInteractionHandler _volumeBarHandler;
        private readonly ILoggingService _loggingService;

        public VolumeBarEventService(
            VolumeBarInteractionHandler volumeBarHandler,
            ILoggingService loggingService)
        {
            _volumeBarHandler = volumeBarHandler ?? throw new ArgumentNullException(nameof(volumeBarHandler));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Handles volume bar mouse left button down events (start drag)
        /// </summary>
        public void OnVolumeBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarHandler.OnVolumeBarMouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// Handles volume bar mouse move events (during drag)
        /// </summary>
        public void OnVolumeBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _volumeBarHandler.OnVolumeBarMouseMove(sender, e);
        }

        /// <summary>
        /// Handles volume bar mouse left button up events (end drag)
        /// </summary>
        public void OnVolumeBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _volumeBarHandler.OnVolumeBarMouseLeftButtonUp(sender, e);
        }

        /// <summary>
        /// Handles volume bar mouse right button down events (set to 0dB)
        /// </summary>
        public void OnVolumeBarMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarHandler.OnVolumeBarMouseRightButtonDown(sender, e);
        }

        /// <summary>
        /// Handles volume text mouse left button down events (start editing)
        /// </summary>
        public void OnVolumeTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarHandler.OnVolumeTextMouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// Handles volume text box key down events (Enter/Escape handling)
        /// </summary>
        public void OnVolumeTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _volumeBarHandler.OnVolumeTextBoxKeyDown(sender, e);
        }

        /// <summary>
        /// Handles volume text box lost focus events (commit changes)
        /// </summary>
        public void OnVolumeTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            _volumeBarHandler.OnVolumeTextBoxLostFocus(sender, e);
        }

        /// <summary>
        /// Handles volume text box loaded events (initial setup)
        /// </summary>
        public void OnVolumeTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            _volumeBarHandler.OnVolumeTextBoxLoaded(sender, e);
        }
    }
}