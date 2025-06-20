// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing window position and state persistence
    /// Handles saving and loading window dimensions and position
    /// </summary>
    public class WindowPositionService : IWindowPositionService
    {
        private readonly IRegistryService _registryService;
        private readonly ILoggingService _loggingService;
        private readonly MainWindow _mainWindow;
        
        private const string AppKeyPath = @"SOFTWARE\ToxMoxMicFreezer";

        public WindowPositionService(MainWindow mainWindow, IRegistryService registryService, ILoggingService loggingService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Saves current window position and state to registry
        /// </summary>
        public bool SaveWindowPosition(double left, double top, double width, double height, WindowState windowState)
        {
            try
            {
                bool success = true;
                success &= _registryService.SetValue(AppKeyPath, "WindowLeft", left, RegistryValueKind.String);
                success &= _registryService.SetValue(AppKeyPath, "WindowTop", top, RegistryValueKind.String);
                success &= _registryService.SetValue(AppKeyPath, "WindowWidth", width, RegistryValueKind.String);
                success &= _registryService.SetValue(AppKeyPath, "WindowHeight", height, RegistryValueKind.String);
                success &= _registryService.SetValue(AppKeyPath, "WindowState", windowState.ToString(), RegistryValueKind.String);
                
                return success;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error saving window position: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Loads window position and state from registry
        /// </summary>
        public (double left, double top, double width, double height, WindowState state) LoadWindowPosition()
        {
            try
            {
                double left = _registryService.GetValue(AppKeyPath, "WindowLeft", 100.0);
                double top = _registryService.GetValue(AppKeyPath, "WindowTop", 100.0);
                double width = _registryService.GetValue(AppKeyPath, "WindowWidth", 800.0);
                double height = _registryService.GetValue(AppKeyPath, "WindowHeight", 600.0);
                
                string stateStr = _registryService.GetValue(AppKeyPath, "WindowState", "Normal");
                WindowState state = Enum.TryParse<WindowState>(stateStr, out var parsedState) ? parsedState : WindowState.Normal;
                
                return (left, top, width, height, state);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading window position: {ex.Message}", LogLevel.Warning);
                return (100.0, 100.0, 800.0, 600.0, WindowState.Normal);
            }
        }

        /// <summary>
        /// Saves log panel height to registry
        /// </summary>
        public bool SaveLogPanelHeight(double height)
        {
            try
            {
                bool success = _registryService.SetValue(AppKeyPath, "LogPanelHeight", (int)Math.Round(height), RegistryValueKind.DWord);
                return success;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error saving log panel height: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Loads log panel height from registry
        /// </summary>
        public double LoadLogPanelHeight()
        {
            try
            {
                int height = _registryService.GetValue(AppKeyPath, "LogPanelHeight", 150);
                
                // Validate height range
                if (height >= 100 && height <= 400)
                {
                    return height;
                }
                else
                {
                    return 150;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading log panel height: {ex.Message}", LogLevel.Warning);
                return 150; // Default height
            }
        }

        /// <summary>
        /// Applies loaded log panel height to the main window grid
        /// </summary>
        public void ApplyLogPanelHeight(double height)
        {
            try
            {
                // Update the row definition height
                var mainGrid = _mainWindow.Content as Grid;
                if (mainGrid?.RowDefinitions.Count > 5)
                {
                    mainGrid.RowDefinitions[5].Height = new GridLength(height);
                }
                else
                {
                    _loggingService.Log("Cannot apply log panel height - main grid not found or insufficient rows", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error applying log panel height: {ex.Message}", LogLevel.Error);
            }
        }
    }
}