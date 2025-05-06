using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui;
using ToxMoxMicFreezer.App.Helpers;

namespace ToxMoxMicFreezer.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string RegKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
        private const string RegValuePrefix = "Window";
        private Window? _mainWindow;
        // Cache the icon in static memory so it's only created once
        private static System.Drawing.Icon? _cachedAppIcon = null;

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
                // Create and show the main window directly
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
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
                var icon = IconHelper.CreateSimpleIcon(
                    System.Drawing.ColorTranslator.FromHtml("#352242"),  // Background - purple
                    System.Drawing.ColorTranslator.FromHtml("#F1D1FF"),  // Foreground - light pink
                    256); // Large size for high-quality icon
                
                if (icon != null)
                {
                    IconHelper.SaveIconToFile(icon, iconPath);
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
                _cachedAppIcon = IconHelper.CreateSimpleIcon(
                    System.Drawing.ColorTranslator.FromHtml("#352242"),  // Background - purple
                    System.Drawing.ColorTranslator.FromHtml("#F1D1FF"),  // Foreground - light pink
                    64); // Reasonable size for window icon
            }
            return _cachedAppIcon;
        }

        protected override void OnExit(ExitEventArgs e)
        {
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

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);
    }
}

