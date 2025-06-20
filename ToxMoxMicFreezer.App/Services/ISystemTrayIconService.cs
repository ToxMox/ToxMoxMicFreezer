// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows.Media;
using H.NotifyIcon;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for system tray icon management
    /// Handles icon loading, state updates, and visual changes
    /// </summary>
    public interface ISystemTrayIconService
    {
        /// <summary>
        /// Attempts to load the appropriate icon for the taskbar
        /// </summary>
        /// <param name="taskbarIcon">The taskbar icon to configure</param>
        /// <returns>True if icon was loaded successfully</returns>
        bool TryLoadIcon(TaskbarIcon taskbarIcon);

        /// <summary>
        /// Updates the taskbar icon state (normal/paused)
        /// </summary>
        /// <param name="taskbarIcon">The taskbar icon to update</param>
        /// <param name="isPaused">Whether the application is in paused state</param>
        void UpdateIconState(TaskbarIcon taskbarIcon, bool isPaused);

        /// <summary>
        /// Loads the FontAwesome font family for icon rendering
        /// </summary>
        /// <returns>FontAwesome FontFamily or null if not available</returns>
        System.Windows.Media.FontFamily? LoadFontAwesomeFont();
    }
}