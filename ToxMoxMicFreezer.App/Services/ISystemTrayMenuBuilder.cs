// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using System.Windows.Controls;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for building system tray context menus
    /// Handles menu structure creation and organization
    /// </summary>
    public interface ISystemTrayMenuBuilder
    {
        /// <summary>
        /// Creates the main WPF context menu for the system tray
        /// </summary>
        /// <param name="app">The application instance for callback actions</param>
        /// <returns>Configured ContextMenu</returns>
        ContextMenu CreateMainContextMenu(App app);

        /// <summary>
        /// Creates a standard WPF menu item with icon and text
        /// </summary>
        /// <param name="text">Menu item text</param>
        /// <param name="fontAwesomeIcon">FontAwesome unicode icon</param>
        /// <param name="clickHandler">Optional click handler</param>
        /// <returns>Configured MenuItem</returns>
        MenuItem CreateMenuItem(string text, string fontAwesomeIcon, Action? clickHandler = null);

        /// <summary>
        /// Creates the settings submenu with all configuration options
        /// </summary>
        /// <param name="app">The application instance for settings access</param>
        /// <returns>Configured settings MenuItem</returns>
        MenuItem CreateSettingsMenu(App app);

        /// <summary>
        /// Creates a settings menu item with state indicator
        /// </summary>
        /// <param name="text">Menu item text</param>
        /// <param name="iconGlyph">FontAwesome icon glyph</param>
        /// <param name="isEnabled">Current enabled state</param>
        /// <param name="clickHandler">Click handler action</param>
        /// <returns>Configured settings MenuItem</returns>
        MenuItem CreateSettingsMenuItem(string text, string iconGlyph, bool isEnabled, Action clickHandler);

        /// <summary>
        /// Creates the application icon header for the context menu
        /// </summary>
        /// <returns>UI element for menu header</returns>
        UIElement CreateAppIconHeader();
    }
}