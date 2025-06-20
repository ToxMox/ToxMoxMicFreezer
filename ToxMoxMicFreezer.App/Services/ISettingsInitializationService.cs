// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using ToxMoxMicFreezer.App.Services;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for initializing UI settings controls
    /// Handles setting up all toggle controls with their saved states
    /// </summary>
    public interface ISettingsInitializationService
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
        /// <param name="logLevel">Log level to save</param>
        void SaveDebugMode(LogLevel logLevel);

        /// <summary>
        /// Loads log panel height and applies it to the UI
        /// </summary>
        void LoadLogPanelHeight();

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        /// <param name="height">Height to save</param>
        void SaveLogPanelHeight(double height);
    }
}