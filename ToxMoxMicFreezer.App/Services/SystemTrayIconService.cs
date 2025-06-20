// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for system tray icon management
    /// Handles icon loading, state updates, and visual changes
    /// </summary>
    public class SystemTrayIconService : ISystemTrayIconService
    {
        private readonly ILoggingService _loggingService;

        public SystemTrayIconService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Attempts to load the appropriate icon for the taskbar
        /// </summary>
        public bool TryLoadIcon(TaskbarIcon taskbarIcon)
        {
            if (taskbarIcon == null) return false;

            try
            {
                // Method 1: Pack URI (most reliable for WPF applications)
                try
                {
                    var iconUri = new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute);
                    var iconSource = new BitmapImage(iconUri);
                    iconSource.Freeze(); // Important for cross-thread usage
                    taskbarIcon.IconSource = iconSource;
                    return true;
                }
                catch (Exception)
                {
                }

                // Method 2: Assembly resource stream
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("ToxMoxMicFreezer.App.Assets.AppIcon.ico");
                    if (stream != null)
                    {
                        var iconSource = new BitmapImage();
                        iconSource.BeginInit();
                        iconSource.StreamSource = stream;
                        iconSource.CacheOption = BitmapCacheOption.OnLoad;
                        iconSource.EndInit();
                        iconSource.Freeze();
                        taskbarIcon.IconSource = iconSource;
                        return true;
                    }
                }
                catch (Exception)
                {
                }

                // Method 3: File system fallback
                try
                {
                    var exeDir = AppContext.BaseDirectory;
                    var iconPath = Path.Combine(exeDir, "Assets", "AppIcon.ico");
                    
                    if (File.Exists(iconPath))
                    {
                        var iconSource = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                        iconSource.Freeze();
                        taskbarIcon.IconSource = iconSource;
                        return true;
                    }
                    else
                    {
                        _loggingService.Log($"Icon file not found at: {iconPath}", LogLevel.Debug);
                    }
                }
                catch (Exception fileEx)
                {
                    _loggingService.Log($"File system loading failed: {fileEx.Message}", LogLevel.Debug);
                }

                _loggingService.Log("All icon loading methods failed", LogLevel.Warning);
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Critical error in icon loading: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Updates the taskbar icon state (normal/paused)
        /// </summary>
        public void UpdateIconState(TaskbarIcon taskbarIcon, bool isPaused)
        {
            if (taskbarIcon == null) return;

            try
            {
                var iconFileName = isPaused ? "AppIconPaused.ico" : "AppIcon.ico";
                
                // Try Pack URI first
                try
                {
                    var iconUri = new Uri($"pack://application:,,,/Assets/{iconFileName}", UriKind.Absolute);
                    var iconSource = new BitmapImage(iconUri);
                    iconSource.Freeze();
                    taskbarIcon.IconSource = iconSource;
                    
                    // Update tooltip to reflect state
                    taskbarIcon.ToolTipText = isPaused 
                        ? "ToxMox Mic Freezer+ (Paused)" 
                        : "ToxMox Mic Freezer+";
                        
                    _loggingService.Log($"Icon updated to {(isPaused ? "paused" : "normal")} state", LogLevel.Debug);
                    return;
                }
                catch (Exception ex)
                {
                    _loggingService.Log($"Failed to update icon state: {ex.Message}", LogLevel.Warning);
                }

                // Fallback: just update tooltip if icon update fails
                taskbarIcon.ToolTipText = isPaused 
                    ? "ToxMox Mic Freezer+ (Paused)" 
                    : "ToxMox Mic Freezer+";
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error updating icon state: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads the FontAwesome font family for icon rendering
        /// </summary>
        public System.Windows.Media.FontFamily? LoadFontAwesomeFont()
        {
            try
            {
                // Try to load FontAwesome font from multiple locations
                var fontLocations = new[]
                {
                    "/Fonts/#Font Awesome 6 Free Solid",
                    "/Fonts/#FontAwesome",
                    "/Assets/Fonts/#Font Awesome 6 Free Solid"
                };

                foreach (var location in fontLocations)
                {
                    try
                    {
                        var fontFamily = new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"), location);
                        _loggingService.Log($"FontAwesome loaded from: {location}", LogLevel.Debug);
                        return fontFamily;
                    }
                    catch (Exception ex)
                    {
                        _loggingService.Log($"Failed to load font from {location}: {ex.Message}", LogLevel.Debug);
                    }
                }

                _loggingService.Log("FontAwesome font not available, using system default", LogLevel.Warning);
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error loading FontAwesome font: {ex.Message}", LogLevel.Error);
                return null;
            }
        }
    }
}