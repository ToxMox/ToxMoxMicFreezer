// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for handling settings toggle events
    /// Manages application setting changes through UI toggle controls
    /// </summary>
    public interface ISettingsToggleEventService
    {
        /// <summary>
        /// Handles startup toggle checked event
        /// </summary>
        void OnStartupToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles startup toggle unchecked event
        /// </summary>
        void OnStartupToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles minimize to tray toggle checked event
        /// </summary>
        void OnMinimizeToTrayToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles minimize to tray toggle unchecked event
        /// </summary>
        void OnMinimizeToTrayToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles start minimized toggle checked event
        /// </summary>
        void OnStartMinimizedToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles start minimized toggle unchecked event
        /// </summary>
        void OnStartMinimizedToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles hide fixed volume devices toggle checked event
        /// </summary>
        void OnHideFixedVolumeToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles hide fixed volume devices toggle unchecked event
        /// </summary>
        void OnHideFixedVolumeToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles popup notifications toggle checked event
        /// </summary>
        void OnPopupNotificationsToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles popup notifications toggle unchecked event
        /// </summary>
        void OnPopupNotificationsToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles audio metering toggle checked event
        /// </summary>
        void OnAudioMeteringToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles audio metering toggle unchecked event
        /// </summary>
        void OnAudioMeteringToggleUnchecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles debug logging toggle checked event
        /// </summary>
        void OnDebugLoggingToggleChecked(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles debug logging toggle unchecked event
        /// </summary>
        void OnDebugLoggingToggleUnchecked(object sender, RoutedEventArgs e);
    }
}