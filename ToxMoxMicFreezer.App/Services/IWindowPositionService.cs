// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing window position and state persistence
    /// Handles saving and loading window dimensions and position
    /// </summary>
    public interface IWindowPositionService
    {
        /// <summary>
        /// Saves current window position and state to registry
        /// </summary>
        /// <param name="left">Window left position</param>
        /// <param name="top">Window top position</param>
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// <param name="windowState">Window state (Normal, Minimized, Maximized)</param>
        /// <returns>True if saved successfully</returns>
        bool SaveWindowPosition(double left, double top, double width, double height, WindowState windowState);

        /// <summary>
        /// Loads window position and state from registry
        /// </summary>
        /// <returns>Tuple containing window position and state values</returns>
        (double left, double top, double width, double height, WindowState state) LoadWindowPosition();

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        /// <param name="height">Log panel height</param>
        /// <returns>True if saved successfully</returns>
        bool SaveLogPanelHeight(double height);

        /// <summary>
        /// Loads log panel height from registry
        /// </summary>
        /// <returns>Log panel height or default value</returns>
        double LoadLogPanelHeight();

        /// <summary>
        /// Applies loaded log panel height to the main window grid
        /// </summary>
        /// <param name="height">Height to apply</param>
        void ApplyLogPanelHeight(double height);
    }
}