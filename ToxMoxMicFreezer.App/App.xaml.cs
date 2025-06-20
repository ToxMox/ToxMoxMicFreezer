// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui;
using ToxMoxMicFreezer.App.Helpers;
using ToxMoxMicFreezer.App.FontAwesome;
using ToxMoxMicFreezer.App.SystemTray;
using System.Windows.Forms;
using System.Drawing.Text;

namespace ToxMoxMicFreezer.App
{


    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string RegKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string RegValuePrefix = "Window";
        // Cache the icon in static memory so it's only created once
        private static System.Drawing.Icon? _cachedAppIcon = null;
        public SystemTrayManager? _systemTrayManager = null;

        public App()
        {
            // Theme setup will be handled by App.xaml directly
            // Don't create any files on disk
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if --generateicon parameter was passed
            if (e.Args.Length > 0 && e.Args[0] == "--generateicon")
            {
                // Generate the icon file for the build process only
                GenerateIconFile();
                Shutdown();
                return;
            }

            try
            {
                // Create persistent tray icon FIRST, before creating any windows
                InitializeSystemTray();

                // Check if user wants to start minimized to tray
                bool startMinimized = GetStartMinimizedSetting();

                // Create the main window
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;

                // Show window based on start minimized setting
                if (startMinimized)
                {
                    // Don't show the window, it will stay hidden
                    mainWindow.WindowState = WindowState.Minimized;
                    // Force initialization even when minimized to ensure volume monitoring works
                    mainWindow.ForceInitialization();
                }
                else
                {
                    // Show window normally
                    mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start application: {ex.Message}", "ToxMoxMicFreezer", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // Create the icon file but only when explicitly requested during build
        private void GenerateIconFile()
        {
            try
            {
                // Only generate the file during build
                string iconDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                string iconPath = Path.Combine(iconDir, "AppIcon.ico");
                
                // Create parent directory if needed
                if (!Directory.Exists(iconDir))
                {
                    Directory.CreateDirectory(iconDir);
                }
                
                // Generate the icon
                var icon = ToxMoxMicFreezer.App.Helpers.IconHelper.CreateSimpleIcon(
                    System.Drawing.ColorTranslator.FromHtml("#352242"),  // Background - purple
                    System.Drawing.ColorTranslator.FromHtml("#F1D1FF"),  // Foreground - light pink
                    256); // Large size for high-quality icon
                
                if (icon != null)
                {
                    ToxMoxMicFreezer.App.Helpers.IconHelper.SaveIconToFile(icon, iconPath);
                    Console.WriteLine($"Icon generated: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating icon: {ex.Message}");
            }
        }

        // This method provides a shared icon without touching the file system
        public static System.Drawing.Icon GetAppIcon()
        {
            if (_cachedAppIcon == null)
            {
                try
                {
                    // Load the actual AppIcon.ico file from embedded resources
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("ToxMoxMicFreezer.App.Assets.AppIcon.ico");
                    if (stream != null)
                    {
                        _cachedAppIcon = new System.Drawing.Icon(stream);
                        System.Diagnostics.Debug.WriteLine("App icon loaded from embedded AppIcon.ico");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: AppIcon.ico embedded resource not found");
                        throw new FileNotFoundException("AppIcon.ico embedded resource not found");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR loading app icon: {ex.Message}");
                    throw; // No fallback - let it fail if AppIcon.ico can't be loaded
                }
            }
            return _cachedAppIcon;
        }

        // Get app icon as Image for menu use
        public static System.Drawing.Image? GetAppIconAsImage()
        {
            try
            {
                // Try to load the embedded AppIcon.ico directly
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "ToxMoxMicFreezer.App.Assets.AppIcon.ico";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var icon = new System.Drawing.Icon(stream);
                    var image = icon.ToBitmap();
                    // Add debugging to verify icon loading
                    System.Diagnostics.Debug.WriteLine($"System tray icon loaded from embedded resource: {image.Width}x{image.Height}");
                    return image;
                }
                
                // Fallback to GetAppIcon method
                var fallbackIcon = GetAppIcon();
                if (fallbackIcon != null)
                {
                    var image = fallbackIcon.ToBitmap();
                    System.Diagnostics.Debug.WriteLine($"System tray icon loaded from fallback: {image.Width}x{image.Height}");
                    return image;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system tray icon: {ex.Message}");
                return null;
            }
        }

        private void InitializeSystemTray()
        {
            try
            {
                // Create required services for SystemTrayManager
                var loggingService = new ToxMoxMicFreezer.App.Services.LoggingService();
                var iconService = new ToxMoxMicFreezer.App.Services.SystemTrayIconService(loggingService);
                var menuBuilder = new ToxMoxMicFreezer.App.Services.SystemTrayMenuBuilder(iconService, loggingService);
                
                _systemTrayManager = new SystemTrayManager(this, iconService, menuBuilder, loggingService);
                _systemTrayManager.Initialize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow != null)
            {
                if (!MainWindow.IsVisible)
                {
                    MainWindow.Show();
                }
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        }

        public static bool GetStartMinimizedSetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("StartMinimizedToTray");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return false; // Default to false
        }

        public bool GetMinimizeToTraySetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("MinimizeToTray");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return false; // Default to false
        }

        public static void SaveMinimizeToTraySetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("MinimizeToTray", enabled ? 1 : 0);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static void SaveStartMinimizedSetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("StartMinimizedToTray", enabled ? 1 : 0);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static void SaveVolumeBarTheme(string theme)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("VolumeBarTheme", theme);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static string GetVolumeBarTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("VolumeBarTheme");
                    if (value is string stringValue)
                    {
                        return stringValue;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return "LightPurple"; // Default theme
        }

        public static bool GetHideFixedVolumeDevicesSetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("HideFixedVolumeDevices");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return false; // Default to false
        }

        public static void SaveHideFixedVolumeDevicesSetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("HideFixedVolumeDevices", enabled ? 1 : 0);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static void SavePopupNotificationsSetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("NotificationsEnabled", enabled ? 1 : 0);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public bool GetNotificationsEnabledSetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("NotificationsEnabled");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return true; // Default to enabled
        }


        public static void SaveNotificationsEnabledSetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("NotificationsEnabled", enabled ? 1 : 0);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static int GetNotificationDurationSetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("NotificationDuration");
                    if (value is int intValue)
                    {
                        return intValue;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return 3000; // Default 3 seconds
        }

        public static void SaveNotificationDurationSetting(int duration)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("NotificationDuration", duration);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static void UpdateTrayIconForPauseState(bool isPaused)
        {
            if (Current is App app)
            {
                app._systemTrayManager?.UpdateTrayIcon(isPaused);
            }
        }

        public static System.Drawing.Icon CreatePausedIcon()
        {
            try
            {
                // Get the original icon from embedded resource
                System.Drawing.Icon? originalIcon = null;
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using (var stream = assembly.GetManifestResourceStream("ToxMoxMicFreezer.App.Assets.AppIcon.ico"))
                    {
                        if (stream != null)
                        {
                            originalIcon = new System.Drawing.Icon(stream);
                        }
                    }
                }
                catch
                {
                    // Fallback to generated icon
                }
                
                if (originalIcon == null)
                {
                    System.Diagnostics.Debug.WriteLine("CRITICAL ERROR: Could not load AppIcon.ico for paused icon");
                    throw new FileNotFoundException("AppIcon.ico required for paused icon creation");
                }

                // Create a bitmap from the icon (use 32x32 for better visibility)
                var iconSize = 32;
                using (var originalBitmap = new System.Drawing.Bitmap(originalIcon.ToBitmap(), iconSize, iconSize))
                {
                    // Create a new bitmap with the same size
                    var pausedBitmap = new System.Drawing.Bitmap(iconSize, iconSize);
                    
                    using (var graphics = System.Drawing.Graphics.FromImage(pausedBitmap))
                    {
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        
                        // Draw the original icon
                        graphics.DrawImage(originalBitmap, 0, 0, iconSize, iconSize);
                        
                        // Create a much larger and more visible pause overlay
                        var overlaySize = Math.Max(12, iconSize / 2); // Half the icon size
                        var overlayX = iconSize - overlaySize - 1;
                        var overlayY = iconSize - overlaySize - 1;
                        var barWidth = Math.Max(3, overlaySize / 5); // Thicker bars
                        var barSpacing = Math.Max(2, overlaySize / 8);
                        
                        // Draw a more prominent background circle
                        using (var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 255, 69, 0))) // Orange background
                        {
                            graphics.FillEllipse(bgBrush, overlayX - 2, overlayY - 2, overlaySize + 4, overlaySize + 4);
                        }
                        
                        // Add a white border around the circle
                        using (var borderPen = new System.Drawing.Pen(System.Drawing.Color.White, 1))
                        {
                            graphics.DrawEllipse(borderPen, overlayX - 2, overlayY - 2, overlaySize + 4, overlaySize + 4);
                        }
                        
                        // Draw much more visible pause bars
                        using (var pauseBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            var bar1X = overlayX + overlaySize / 2 - barWidth - barSpacing / 2;
                            var bar2X = overlayX + overlaySize / 2 + barSpacing / 2;
                            var barY = overlayY + 3;
                            var barHeight = overlaySize - 6;
                            
                            graphics.FillRectangle(pauseBrush, bar1X, barY, barWidth, barHeight);
                            graphics.FillRectangle(pauseBrush, bar2X, barY, barWidth, barHeight);
                        }
                    }
                    
                    // Convert back to icon using IntPtr method (more reliable)
                    var iconHandle = pausedBitmap.GetHicon();
                    var icon = System.Drawing.Icon.FromHandle(iconHandle);
                    
                    // Create a copy to prevent handle issues
                    var iconCopy = new System.Drawing.Icon(icon, icon.Size);
                    
                    // Clean up the handle
                    DestroyIcon(iconHandle);
                    
                    return iconCopy;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR creating paused icon: {ex.Message}");
                throw; // No fallback - if we can't create paused icon from AppIcon.ico, fail
            }
        }

        public void PauseFromTray(TimeSpan? duration, string reason)
        {
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                if (duration.HasValue)
                {
                    mainWin.PauseManager.PauseFor(duration.Value, reason);
                }
                else
                {
                    mainWin.PauseManager.PauseIndefinitely();
                }
                mainWin.Dispatcher.BeginInvoke(() => {
                    mainWin.UpdatePauseUI();
                    UpdateTrayIconForPauseState(true);
                });
            }
        }

        public void MutePopupsFromTray(TimeSpan? duration, string reason)
        {
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                if (duration.HasValue)
                {
                    mainWin.MutePopupsFor(duration.Value, reason);
                }
                else
                {
                    mainWin.MutePopupsIndefinitely();
                }
                mainWin.UpdateNotificationMuteUI();
            }
        }

        public void ResumePopupsFromTray()
        {
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                mainWin.UnmutePopups();
                mainWin.UpdateNotificationMuteUI();
            }
        }

        public void ResumeFromTray()
        {
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                mainWin.PauseManager.Resume();
                mainWin.Dispatcher.BeginInvoke(() => {
                    mainWin.UpdatePauseUI();
                    UpdateTrayIconForPauseState(false);
                });
            }
        }

        public void ToggleStartupFromTray(bool enabled)
        {
            if (enabled)
            {
                AddToStartup();
            }
            else
            {
                RemoveFromStartup();
            }
        }

        private static void AddToStartup()
        {
            try
            {
                var mainModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (mainModule?.FileName == null)
                {
                    return;
                }
                string exePath = mainModule.FileName;
                
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    key.SetValue("ToxMoxMicFreezer", exePath);
                }
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        private static void RemoveFromStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    key.DeleteValue("ToxMoxMicFreezer", false);
                }
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public bool IsInStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (key != null)
                {
                    var value = key.GetValue("ToxMoxMicFreezer");
                    return value != null;
                }
            }
            catch
            {
                // If registry access fails, return false
            }
            return false;
        }

        public void ToggleMinimizeToTrayFromTray(bool enabled)
        {
            SaveMinimizeToTraySetting(enabled);
            
            // Update UI toggle if main window exists
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                mainWin.Dispatcher.BeginInvoke(() => {
                    var toggle = mainWin.FindName("MinimizeToTrayToggle") as System.Windows.Controls.CheckBox;
                    if (toggle != null)
                    {
                        toggle.IsChecked = enabled;
                    }
                });
            }
        }

        public void ToggleStartMinimizedFromTray(bool enabled)
        {
            SaveStartMinimizedSetting(enabled);
            
            // Update UI toggle if main window exists
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                mainWin.Dispatcher.BeginInvoke(() => {
                    var toggle = mainWin.FindName("StartMinimizedToggle") as System.Windows.Controls.CheckBox;
                    if (toggle != null)
                    {
                        toggle.IsChecked = enabled;
                    }
                });
            }
        }

        public void TogglePopupNotificationsFromTray(bool enabled)
        {
            SavePopupNotificationsSetting(enabled);
            
            // Update MainWindow popup state and UI toggle
            var app = Current as App;
            if (app?.MainWindow is MainWindow mainWin)
            {
                if (enabled)
                {
                    mainWin.UnmutePopups();
                }
                else
                {
                    mainWin.MutePopupsIndefinitely();
                }
                
                mainWin.Dispatcher.BeginInvoke(() => {
                    var toggle = mainWin.FindName("PopupNotificationsToggle") as System.Windows.Controls.CheckBox;
                    if (toggle != null)
                    {
                        toggle.IsChecked = enabled;
                    }
                    mainWin.UpdateNotificationMuteUI();
                });
            }
        }


        private static System.Drawing.Image? CreateHeaderIcon()
        {
            try
            {
                // Create a small version of the app icon for the header
                var bitmap = new System.Drawing.Bitmap(20, 20);
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    // Draw a simplified microphone icon using graphics
                    using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255)))
                    {
                        // Microphone body (rectangle)
                        g.FillRectangle(brush, 7, 4, 6, 10);
                        // Microphone stand (line)
                        g.FillRectangle(brush, 9, 14, 2, 4);
                        // Base
                        g.FillRectangle(brush, 6, 17, 8, 2);
                    }
                }
                return bitmap;
            }
            catch
            {
                // Return null if icon creation fails
                return null;
            }
        }




        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up persistent tray icon
            _systemTrayManager?.Dispose();
            _systemTrayManager = null;

            if (MainWindow is Window mainWindow)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                    var isMax = mainWindow.WindowState == WindowState.Maximized;
                    
                    // Always save the non-maximized window position
                    var bounds = isMax ? mainWindow.RestoreBounds : new Rect(mainWindow.Left, mainWindow.Top, mainWindow.Width, mainWindow.Height);
                    
                    key.SetValue($"{RegValuePrefix}Left", (int)bounds.Left);
                    key.SetValue($"{RegValuePrefix}Top", (int)bounds.Top);
                    key.SetValue($"{RegValuePrefix}Width", (int)bounds.Width);
                    key.SetValue($"{RegValuePrefix}Height", (int)bounds.Height);
                    key.SetValue($"{RegValuePrefix}IsMaximized", isMax ? 1 : 0);
                }
                catch
                {
                    // If registry access fails, just exit normally
                }
            }
            base.OnExit(e);
        }

        // Audio metering settings
        public static bool GetAudioMeteringEnabledSetting()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var value = key.GetValue("AudioMeteringEnabled");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // If registry access fails, use default
            }
            return true; // Default to enabled
        }

        public static void SaveAudioMeteringEnabledSetting(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                key.SetValue("AudioMeteringEnabled", enabled ? 1 : 0);
                
                // Fire event after successful save
                AudioMeteringSettingChanged?.Invoke(null, EventArgs.Empty);
            }
            catch
            {
                // Silently fail if registry access is denied
            }
        }

        public static event EventHandler? AudioMeteringSettingChanged;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

