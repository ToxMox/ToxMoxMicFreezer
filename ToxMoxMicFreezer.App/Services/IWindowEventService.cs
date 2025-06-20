// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for handling window-related events and interactions
    /// Manages window positioning, resizing, state changes, and drag operations
    /// </summary>
    public interface IWindowEventService
    {
        /// <summary>
        /// Handles window size changes and updates minimum height calculations
        /// </summary>
        void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e);

        /// <summary>
        /// Handles window location changes for settings persistence
        /// </summary>
        void OnWindowLocationChanged(object? sender, EventArgs e);

        /// <summary>
        /// Handles window closing events and cleanup
        /// </summary>
        void OnWindowClosing(object? sender, CancelEventArgs e);

        /// <summary>
        /// Handles window state changes (minimized, maximized, normal)
        /// </summary>
        void OnWindowStateChanged(object? sender, EventArgs e);

        /// <summary>
        /// Handles title bar mouse down for window dragging
        /// </summary>
        void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles resize grip drag operations
        /// </summary>
        void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e);

        /// <summary>
        /// Handles window edge resize operations
        /// </summary>
        void OnResizeSidePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles border mouse clicks (prevents event propagation)
        /// </summary>
        void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e);

        /// <summary>
        /// Handles minimize button clicks
        /// </summary>
        void OnMinimizeButtonClick(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles maximize/restore button clicks
        /// </summary>
        void OnMaximizeButtonClick(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles close button clicks
        /// </summary>
        void OnCloseButtonClick(object sender, RoutedEventArgs e);

        /// <summary>
        /// Handles exit button clicks (system tray context menu)
        /// </summary>
        void OnExitButtonClick(object sender, RoutedEventArgs e);

        /// <summary>
        /// Calculates and sets the minimum window height based on UI elements
        /// </summary>
        void CalculateAndSetMinimumHeight();
    }
}