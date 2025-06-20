// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using H.NotifyIcon;
using ToxMoxMicFreezer.App.Services;

namespace ToxMoxMicFreezer.App.SystemTray
{
    /// <summary>
    /// Manages the system tray icon and context menu functionality
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly App _app;
        private readonly ISystemTrayIconService _iconService;
        private readonly ISystemTrayMenuBuilder _menuBuilder;
        private readonly ILoggingService _loggingService;
        private bool _disposed = false;

        public SystemTrayManager(App app, ISystemTrayIconService iconService, ISystemTrayMenuBuilder menuBuilder, ILoggingService loggingService)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
            _menuBuilder = menuBuilder ?? throw new ArgumentNullException(nameof(menuBuilder));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Initializes and shows the system tray icon using H.NotifyIcon only
        /// </summary>
        public void Initialize()
        {
            
            try
            {
                // Ensure we're on UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    _taskbarIcon = new TaskbarIcon();
                    
                    // Configure basic properties
                    _taskbarIcon.ToolTipText = "ToxMox Mic Freezer+";
                    _taskbarIcon.Visibility = Visibility.Visible;
                    
                    // Load icon using service
                    bool iconLoaded = _iconService.TryLoadIcon(_taskbarIcon);
                    
                    if (iconLoaded)
                    {
                        // Create taskbar icon
                        try
                        {
                            _taskbarIcon.ForceCreate(enablesEfficiencyMode: false);
                        }
                        catch (Exception forceEx)
                        {
                            _loggingService.Log($"ForceCreate failed: {forceEx.Message}", LogLevel.Warning);
                        }
                        
                        // Add event handlers
                        _taskbarIcon.TrayMouseDoubleClick += (s, e) => {
                            _app.ShowMainWindow();
                        };
                        
                        // Create context menu using service
                        try
                        {
                            var contextMenu = _menuBuilder.CreateMainContextMenu(_app);
                            _taskbarIcon.ContextMenu = contextMenu;
                        }
                        catch (Exception menuEx)
                        {
                            _loggingService.Log($"Context menu creation failed: {menuEx.Message}", LogLevel.Warning);
                        }
                    }
                    else
                    {
                        _loggingService.Log("Icon loading failed completely", LogLevel.Error);
                    }
                });
                
            }
            catch (Exception ex)
            {
                _loggingService.Log($"H.NotifyIcon initialization failed: {ex.Message}", LogLevel.Error);
                throw;
            }
        }



        /// <summary>
        /// Updates the tray icon state (normal/paused)
        /// </summary>
        public void UpdateTrayIcon(bool isPaused)
        {
            if (_taskbarIcon == null) return;

            try
            {
                // Ensure UI thread execution for TaskbarIcon updates
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    _iconService.UpdateIconState(_taskbarIcon, isPaused);
                });
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Failed to update tray icon: {ex.Message}", LogLevel.Error);
            }
        }        /// <summary>
        /// Hides the tray icon
        /// </summary>
        public void Hide()
        {
            _taskbarIcon?.Dispose();
        }

        /// <summary>
        /// Shows the tray icon
        /// </summary>
        public void Show()
        {
            if (_taskbarIcon == null)
            {
                // Reinitialize if needed
                Initialize();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _taskbarIcon?.Dispose();
                _taskbarIcon = null;
                _disposed = true;
            }
        }
    }
}