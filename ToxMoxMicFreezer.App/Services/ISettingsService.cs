// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Coordinating service that manages all settings-related operations
    /// Orchestrates registry, theme, startup, and device selection services
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Initializes all UI toggle controls with their saved settings
        /// </summary>
        void InitializeAllToggles();

        /// <summary>
        /// Loads and sets debug mode from registry
        /// </summary>
        void LoadDebugMode();

        /// <summary>
        /// Saves debug mode setting to registry
        /// </summary>
        void SaveDebugMode(LogLevel logLevel);

        /// <summary>
        /// Loads log panel height and applies it to the UI
        /// </summary>
        void LoadLogPanelHeight();

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        void SaveLogPanelHeight(double height);

        /// <summary>
        /// Loads device selection and frozen volume settings from registry
        /// </summary>
        void LoadSelection();

        /// <summary>
        /// Saves device selection and frozen volume settings to registry
        /// </summary>
        void SaveSelection();


        /// <summary>
        /// Sets whether the application starts with Windows
        /// </summary>
        void SetStartupEnabled(bool enabled);

        /// <summary>
        /// Sets whether the application starts minimized
        /// </summary>
        void SetStartMinimizedEnabled(bool enabled);

        /// <summary>
        /// Saves window position to registry
        /// </summary>
        void SaveWindowPosition(double left, double top, double width, double height, WindowState windowState);

        /// <summary>
        /// Loads window position from registry
        /// </summary>
        (double left, double top, double width, double height, WindowState state) LoadWindowPosition();

        /// <summary>
        /// Gets whether audio metering is enabled
        /// </summary>
        bool GetAudioMeteringEnabled();

        /// <summary>
        /// Sets whether audio metering is enabled
        /// </summary>
        void SetAudioMeteringEnabled(bool enabled);

    }
}