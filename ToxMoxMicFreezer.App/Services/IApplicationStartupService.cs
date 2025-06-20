// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing Windows startup registry entries
    /// Handles adding/removing application from Windows startup
    /// </summary>
    public interface IApplicationStartupService
    {
        /// <summary>
        /// Checks if the application is currently in Windows startup
        /// </summary>
        /// <returns>True if application is set to start with Windows</returns>
        bool IsInStartup();

        /// <summary>
        /// Sets whether the application starts with Windows
        /// </summary>
        /// <param name="enabled">True to add to startup, false to remove</param>
        /// <returns>True if operation was successful</returns>
        bool SetStartupEnabled(bool enabled);

        /// <summary>
        /// Checks if the application is set to start minimized
        /// </summary>
        /// <returns>True if application starts minimized</returns>
        bool IsStartMinimizedEnabled();

        /// <summary>
        /// Sets whether the application starts minimized
        /// </summary>
        /// <param name="enabled">True to start minimized, false for normal startup</param>
        /// <returns>True if operation was successful</returns>
        bool SetStartMinimizedEnabled(bool enabled);
    }
}