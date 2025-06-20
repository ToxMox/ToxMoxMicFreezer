// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ToxMoxMicFreezer.App.UserInterface;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for handling window-related events and interactions
    /// Coordinates between UI events and appropriate manager classes for window operations
    /// </summary>
    public class WindowEventService : IWindowEventService
    {
        private readonly MainWindow _mainWindow;
        private readonly WindowManager _windowManager;
        private readonly EventHandlerCoordinator _eventHandlerCoordinator;
        private readonly ILoggingService _loggingService;

        public WindowEventService(
            MainWindow mainWindow, 
            WindowManager windowManager, 
            EventHandlerCoordinator eventHandlerCoordinator,
            ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _eventHandlerCoordinator = eventHandlerCoordinator ?? throw new ArgumentNullException(nameof(eventHandlerCoordinator));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Handles window size changes and updates minimum height calculations
        /// </summary>
        public void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_mainWindow.IsLoaded) // Only save after window is fully loaded
            {
                _windowManager.SaveWindowSettings();
                
                // Calculate dynamic minimum height
                CalculateAndSetMinimumHeight();
            }
        }

        /// <summary>
        /// Handles window location changes for settings persistence
        /// </summary>
        public void OnWindowLocationChanged(object? sender, EventArgs e)
        {
            if (_mainWindow.IsLoaded) // Only save after window is fully loaded
            {
                _windowManager.SaveWindowSettings();
            }
        }

        /// <summary>
        /// Handles window closing events and cleanup
        /// </summary>
        public void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            _eventHandlerCoordinator.OnWindowClosing(sender!, e);
        }

        /// <summary>
        /// Handles window state changes (minimized, maximized, normal)
        /// </summary>
        public void OnWindowStateChanged(object? sender, EventArgs e)
        {
            _eventHandlerCoordinator.OnWindowStateChanged(sender!, e);
        }

        /// <summary>
        /// Handles title bar mouse down for window dragging
        /// </summary>
        public void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _windowManager.OnTitleBarMouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// Handles resize grip drag operations
        /// </summary>
        public void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e)
        {
            _windowManager.OnResizeGripDragDelta(sender, e);
        }

        /// <summary>
        /// Handles window edge resize operations
        /// </summary>
        public void OnResizeSidePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindow.WindowState == WindowState.Maximized)
                return;
            
            var element = sender as FrameworkElement;
            if (element?.Tag == null)
                return;
            
            string? direction = element.Tag.ToString();
            if (!string.IsNullOrEmpty(direction))
            {
                _windowManager.StartWindowResize(direction);
            }
        }

        /// <summary>
        /// Handles border mouse clicks (prevents event propagation)
        /// </summary>
        public void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only handle the event to prevent it from propagating, but don't do any resizing here
            // Resizing is handled exclusively by the edge elements
            e.Handled = true;
        }

        /// <summary>
        /// Handles minimize button clicks
        /// </summary>
        public void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            _windowManager.MinimizeWindow();
        }

        /// <summary>
        /// Handles maximize/restore button clicks
        /// </summary>
        public void OnMaximizeButtonClick(object sender, RoutedEventArgs e)
        {
            _windowManager.ToggleMaximize();
        }

        /// <summary>
        /// Handles close button clicks
        /// </summary>
        public void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            _windowManager.ShowCloseDialog();
        }

        /// <summary>
        /// Handles exit button clicks (system tray context menu)
        /// </summary>
        public void OnExitButtonClick(object sender, RoutedEventArgs e)
        {
            _eventHandlerCoordinator.OnExitButtonClick(sender, e);
        }

        /// <summary>
        /// Calculates and sets the minimum window height based on UI elements
        /// </summary>
        public void CalculateAndSetMinimumHeight()
        {
            try
            {
                double calculatedMinHeight = 0;
                
                // Fixed heights from RowDefinitions
                calculatedMinHeight += 32; // Row 0: Title bar
                
                // Auto-sized rows - measure their actual rendered heights
                if (_mainWindow.PauseStatusBanner?.ActualHeight > 0)
                    calculatedMinHeight += _mainWindow.PauseStatusBanner.ActualHeight;
                
                // Row 2: Action buttons card - estimate based on content
                calculatedMinHeight += 80; // Estimated height for the CardControl
                
                // Row 3: DeviceTabView minimum
                calculatedMinHeight += 90; // MinHeight we set
                
                // Row 4: GridSplitter
                calculatedMinHeight += 8; // Height from XAML
                
                // Row 5: Log panel minimum
                calculatedMinHeight += 100; // MinHeight from XAML
                
                // Add margins and padding
                calculatedMinHeight += 40; // Window margins, border thickness, etc.
                
                // Set the new minimum height, but never go below current minimum
                double newMinHeight = Math.Max(calculatedMinHeight, 620); // Keep current 620 as absolute minimum
                
                if (Math.Abs(_mainWindow.MinHeight - newMinHeight) > 1) // Only update if significantly different
                {
                    _mainWindow.MinHeight = newMinHeight;
                    _loggingService.Log($"Dynamic MinHeight updated to {newMinHeight:F0}px");
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error calculating minimum height: {ex.Message}");
            }
        }
    }
}