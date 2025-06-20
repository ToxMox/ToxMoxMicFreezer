// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Manages window operations, title bar controls, window icons, and window resizing
    /// </summary>
    public class WindowManager
    {
        private readonly MainWindow _mainWindow;

        public WindowManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Sets up the window icon from embedded resources
        /// </summary>
        public void SetupWindowIcon()
        {
            try
            {
                // The ApplicationIcon property in the csproj should handle this automatically
                // But let's also set it manually for good measure
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("ToxMoxMicFreezer.App.Assets.AppIcon.ico"))
                {
                    if (stream != null)
                    {
                        using (var icon = new System.Drawing.Icon(stream))
                        {
                            // Convert to ImageSource for WPF
                            var bitmap = icon.ToBitmap();
                            using (var memory = new System.IO.MemoryStream())
                            {
                                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                memory.Position = 0;
                                
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.StreamSource = memory;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();
                                
                                _mainWindow.Icon = bitmapImage;
                                _mainWindow.AppendLog("Window icon set from embedded resource");
                            }
                        }
                    }
                    else
                    {
                        _mainWindow.AppendLog("Embedded icon resource not found, using fallback");
                        var appIcon = App.GetAppIcon();
                        if (appIcon != null)
                        {
                            var bitmap = appIcon.ToBitmap();
                            using (var memory = new System.IO.MemoryStream())
                            {
                                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                memory.Position = 0;
                                
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.StreamSource = memory;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();
                                
                                _mainWindow.Icon = bitmapImage;
                                _mainWindow.AppendLog("Fallback window icon set");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error setting window icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up the title bar icon brush
        /// </summary>
        public void SetupTitleBarIcon()
        {
            try 
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("ToxMoxMicFreezer.App.Assets.AppIcon.ico"))
                {
                    BitmapImage bitmapImage;
                    
                    if (stream != null)
                    {
                        using (var icon = new System.Drawing.Icon(stream))
                        {
                            var bitmap = icon.ToBitmap();
                            using (var memory = new System.IO.MemoryStream())
                            {
                                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                memory.Position = 0;
                                
                                bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.StreamSource = memory;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();
                            }
                        }
                        _mainWindow.AppendLog("Title bar icon loaded from embedded resource");
                    }
                    else
                    {
                        // Fallback to generated icon
                        var appIcon = App.GetAppIcon();
                        var bitmap = appIcon.ToBitmap();
                        using (var memory = new System.IO.MemoryStream())
                        {
                            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                            memory.Position = 0;
                            
                            bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                            bitmapImage.Freeze();
                        }
                        _mainWindow.AppendLog("Title bar icon using fallback");
                    }
                    
                    var iconBrush = _mainWindow.FindName("AppIconBrush") as ImageBrush;
                    if (iconBrush != null)
                    {
                        iconBrush.ImageSource = bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error setting title bar icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles title bar mouse events for window dragging and maximize on double-click
        /// </summary>
        public void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                _mainWindow.DragMove();
            }
        }

        /// <summary>
        /// Minimizes the window based on user preferences
        /// </summary>
        public void MinimizeWindow()
        {
            // Check user preference for minimize behavior
            bool minimizeToTray = ((App)System.Windows.Application.Current).GetMinimizeToTraySetting();
            
            if (minimizeToTray)
            {
                // Hide the window (minimize to tray)
                _mainWindow.WindowState = WindowState.Minimized;
                _mainWindow.Hide();
                _mainWindow.AppendLog("App minimized to tray.");
            }
            else
            {
                // Normal minimize to taskbar
                _mainWindow.WindowState = WindowState.Minimized;
                _mainWindow.AppendLog("App minimized.");
            }
        }

        /// <summary>
        /// Toggles window maximize/restore state
        /// </summary>
        public void ToggleMaximize()
        {
            if (_mainWindow.WindowState == WindowState.Maximized)
            {
                _mainWindow.WindowState = WindowState.Normal;
                UpdateMaximizeIcon(false);
            }
            else
            {
                _mainWindow.WindowState = WindowState.Maximized;
                UpdateMaximizeIcon(true);
            }
        }

        /// <summary>
        /// Updates the maximize icon based on window state
        /// </summary>
        public void UpdateMaximizeIcon(bool isMaximized)
        {
            var textBlock = _mainWindow.FindName("MaximizeIcon") as TextBlock;
            if (textBlock != null)
            {
                // Use Font Awesome icons
                textBlock.Text = isMaximized ? "\uf2d2" : "\uf2d0"; // restore : maximize
            }
        }

        /// <summary>
        /// Shows the close options dialog and handles the result
        /// </summary>
        public void ShowCloseDialog()
        {
            // Create a custom dialog window
            var dialogWindow = new Window
            {
                Title = "ToxMox's Mic Freezer+",
                Width = 300,
                Height = 150,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = (SolidColorBrush)System.Windows.Application.Current.Resources["ApplicationBackgroundBrush"],
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = _mainWindow,
                ResizeMode = ResizeMode.NoResize,
                BorderBrush = (SolidColorBrush)System.Windows.Application.Current.Resources["AccentControlElevationBorderBrush"],
                BorderThickness = new Thickness(1)
            };

            // Create dialog content
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var titleBar = new Border
            {
                Height = 32,
                Background = (SolidColorBrush)System.Windows.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                Padding = new Thickness(10, 0, 10, 0)
            };

            var titleText = new TextBlock
            {
                Text = "Close Options",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5, 0, 0, 0)
            };

            titleBar.Child = titleText;
            grid.Children.Add(titleBar);
            Grid.SetRow(titleBar, 0);

            var messagePanel = new StackPanel
            {
                Margin = new Thickness(20, 20, 20, 10)
            };

            var messageText = new TextBlock
            {
                Text = "Would you like to minimize to system tray or exit the application?",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
                Margin = new Thickness(0, 0, 0, 10)
            };

            messagePanel.Children.Add(messageText);
            grid.Children.Add(messagePanel);
            Grid.SetRow(messagePanel, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20)
            };

            var minimizeButton = new System.Windows.Controls.Button
            {
                Content = "Minimize to Tray",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0),
                Background = (SolidColorBrush)System.Windows.Application.Current.Resources["SecondaryButtonBackgroundBrush"],
                Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
                BorderThickness = new Thickness(0)
            };

            var exitButton = new System.Windows.Controls.Button
            {
                Content = "Exit",
                Padding = new Thickness(15, 5, 15, 5),
                Background = (SolidColorBrush)System.Windows.Application.Current.Resources["PrimaryButtonBackgroundBrush"],
                Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
                BorderThickness = new Thickness(0)
            };

            minimizeButton.Click += (s, args) =>
            {
                dialogWindow.DialogResult = false;
                dialogWindow.Close();
            };

            exitButton.Click += (s, args) =>
            {
                dialogWindow.DialogResult = true;
                dialogWindow.Close();
            };

            buttonPanel.Children.Add(minimizeButton);
            buttonPanel.Children.Add(exitButton);
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            dialogWindow.Content = grid;

            // Show dialog and handle result
            var result = dialogWindow.ShowDialog();

            if (result == true)
            {
                // Exit the application
                _mainWindow._isExit = true;
                _mainWindow.Close();
            }
            else
            {
                // Minimize to system tray
                _mainWindow.WindowState = WindowState.Minimized;
                _mainWindow.Hide();
            }
        }

        /// <summary>
        /// Handles window resize operations using Windows API
        /// </summary>
        public void StartWindowResize(string direction)
        {
            if (_mainWindow.WindowState == WindowState.Maximized)
                return;
            
            ResizeDirection resizeDir = ResizeDirection.Bottom;
            
            switch (direction)
            {
                case "Left": resizeDir = ResizeDirection.Left; break;
                case "Right": resizeDir = ResizeDirection.Right; break;
                case "Top": resizeDir = ResizeDirection.Top; break;
                case "Bottom": resizeDir = ResizeDirection.Bottom; break;
                case "TopLeft": resizeDir = ResizeDirection.TopLeft; break;
                case "TopRight": resizeDir = ResizeDirection.TopRight; break;
                case "BottomLeft": resizeDir = ResizeDirection.BottomLeft; break;
                case "BottomRight": resizeDir = ResizeDirection.BottomRight; break;
            }
            
            ResizeWindow(resizeDir);
        }

        /// <summary>
        /// Handles resize grip drag operations
        /// </summary>
        public void OnResizeGripDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (_mainWindow.WindowState == WindowState.Maximized)
            {
                return;
            }
            
            double newWidth = Math.Max(_mainWindow.MinWidth, _mainWindow.Width + e.HorizontalChange);
            double newHeight = Math.Max(_mainWindow.MinHeight, _mainWindow.Height + e.VerticalChange);
            
            _mainWindow.Width = newWidth;
            _mainWindow.Height = newHeight;
        }

        /// <summary>
        /// Saves current window settings
        /// </summary>
        public void SaveWindowSettings()
        {
            try
            {
                var isMax = _mainWindow.WindowState == WindowState.Maximized;
                
                // Get the correct bounds based on window state
                var bounds = isMax ? _mainWindow.RestoreBounds : new Rect(_mainWindow.Left, _mainWindow.Top, _mainWindow.Width, _mainWindow.Height);
                
                // Delegate to SettingsManager
                _mainWindow._settingsManager?.SaveWindowPosition(bounds.Left, bounds.Top, bounds.Width, bounds.Height, _mainWindow.WindowState);
            }
            catch
            {
                // Silently fail if we can't save to registry
            }
        }

        /// <summary>
        /// Loads and restores window position and size from saved settings
        /// </summary>
        public void LoadWindowSettings()
        {
            try
            {
                if (_mainWindow._settingsManager == null)
                {
                    _mainWindow.AppendLog("Settings manager not available for window position loading");
                    return;
                }

                var (left, top, width, height, state) = _mainWindow._settingsManager.LoadWindowPosition();

                // Validate the loaded values to ensure they're reasonable
                var workingArea = SystemParameters.WorkArea;
                
                // Ensure the window is at least partially visible on screen
                left = Math.Max(0, Math.Min(left, workingArea.Width - 100));
                top = Math.Max(0, Math.Min(top, workingArea.Height - 50));
                
                // Ensure reasonable window dimensions
                width = Math.Max(_mainWindow.MinWidth, Math.Min(width, workingArea.Width));
                height = Math.Max(_mainWindow.MinHeight, Math.Min(height, workingArea.Height));
                
                // Apply the loaded settings
                _mainWindow.Left = left;
                _mainWindow.Top = top;
                _mainWindow.Width = width;
                _mainWindow.Height = height;
                _mainWindow.WindowState = state;
                
                // Update maximize icon based on loaded state
                UpdateMaximizeIcon(state == WindowState.Maximized);
                
                // Update corner radius based on loaded state
                var mainBorder = _mainWindow.FindName("MainBorder") as System.Windows.Controls.Border;
                if (mainBorder != null)
                {
                    mainBorder.CornerRadius = state == WindowState.Maximized 
                        ? new System.Windows.CornerRadius(0) 
                        : new System.Windows.CornerRadius(8);
                }
                
                _mainWindow.AppendLog($"Window position restored: {left:F0}, {top:F0}, {width:F0}x{height:F0}, State: {state}");
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error loading window position: {ex.Message}");
                // Fall back to default positioning if loading fails
                _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>
        /// Uses native Windows functionality to resize the window
        /// </summary>
        private void ResizeWindow(ResizeDirection direction)
        {
            // Use native Windows functionality to resize the window
            SendMessage(new WindowInteropHelper(_mainWindow).Handle, 0x112, (IntPtr)(61440 + direction), IntPtr.Zero);
        }

        private enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}