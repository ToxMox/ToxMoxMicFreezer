// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for building system tray context menus
    /// Handles menu structure creation and organization
    /// </summary>
    public class SystemTrayMenuBuilder : ISystemTrayMenuBuilder
    {
        private readonly ISystemTrayIconService _iconService;
        private readonly ILoggingService _loggingService;

        public SystemTrayMenuBuilder(ISystemTrayIconService iconService, ILoggingService loggingService)
        {
            _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Creates the main WPF context menu for the system tray
        /// </summary>
        public ContextMenu CreateMainContextMenu(App app)
        {
            var menu = new ContextMenu();
            
            // Header with app icon
            menu.Items.Add(CreateAppIconHeader());
            menu.Items.Add(new Separator());
            
            // Main menu items
            menu.Items.Add(CreateMenuItem("Show", "\uf2d2", () => app.ShowMainWindow()));
            
            // Pause submenu
            var pauseMenu = CreateMenuItem("Pause", "\uf04c");
            pauseMenu.Items.Add(CreateMenuItem("5 minutes", "\uf017", () => app.PauseFromTray(TimeSpan.FromMinutes(5), "5 minutes")));
            pauseMenu.Items.Add(CreateMenuItem("15 minutes", "\uf017", () => app.PauseFromTray(TimeSpan.FromMinutes(15), "15 minutes")));
            pauseMenu.Items.Add(CreateMenuItem("30 minutes", "\uf017", () => app.PauseFromTray(TimeSpan.FromMinutes(30), "30 minutes")));
            pauseMenu.Items.Add(CreateMenuItem("1 hour", "\uf017", () => app.PauseFromTray(TimeSpan.FromHours(1), "1 hour")));
            pauseMenu.Items.Add(CreateMenuItem("Until manually resumed", "\uf04d", () => app.PauseFromTray(null, "Manual")));
            menu.Items.Add(pauseMenu);
            
            // Resume option (will be shown/hidden dynamically)
            var resumeItem = CreateMenuItem("Resume", "\uf04b", () => app.ResumeFromTray());
            menu.Items.Add(resumeItem);
            
            // Mute Popups submenu
            var mutePopupsMenu = CreateMenuItem("Mute Popups", "\uf6a9");
            mutePopupsMenu.Items.Add(CreateMenuItem("5 minutes", "\uf017", () => app.MutePopupsFromTray(TimeSpan.FromMinutes(5), "5 minutes")));
            mutePopupsMenu.Items.Add(CreateMenuItem("15 minutes", "\uf017", () => app.MutePopupsFromTray(TimeSpan.FromMinutes(15), "15 minutes")));
            mutePopupsMenu.Items.Add(CreateMenuItem("30 minutes", "\uf017", () => app.MutePopupsFromTray(TimeSpan.FromMinutes(30), "30 minutes")));
            mutePopupsMenu.Items.Add(CreateMenuItem("1 hour", "\uf017", () => app.MutePopupsFromTray(TimeSpan.FromHours(1), "1 hour")));
            mutePopupsMenu.Items.Add(CreateMenuItem("Until manually enabled", "\uf256", () => app.MutePopupsFromTray(null, "Manual")));
            menu.Items.Add(mutePopupsMenu);
            
            // Resume Popups option (will be shown/hidden dynamically)
            var resumePopupsItem = CreateMenuItem("Resume Popups", "\uf0f3", () => app.ResumePopupsFromTray());
            menu.Items.Add(resumePopupsItem);
            
            // Settings submenu
            var settingsMenu = CreateSettingsMenu(app);
            menu.Items.Add(settingsMenu);
            
            menu.Items.Add(CreateMenuItem("Exit", "\uf00d", () => {
                try { 
                    if (app.MainWindow is MainWindow mainWin)
                    {
                        mainWin._isExit = true;
                        mainWin.Close();
                    }
                    else
                    {
                        app.Shutdown();
                    }
                } 
                catch { app.Shutdown(); }
            }));
            
            // Update menu visibility and settings state
            menu.Opened += (s, e) => {
                if (app.MainWindow is MainWindow mainWin)
                {
                    bool isPaused = mainWin.PauseManager.IsPaused;
                    bool arePopupsMuted = mainWin.ArePopupsMutedFromTray();
                    
                    // Always keep Pause menu visible (user requested)
                    pauseMenu.Visibility = Visibility.Visible;
                    resumeItem.Visibility = isPaused ? Visibility.Visible : Visibility.Collapsed;
                    
                    // Show Resume Popups only when popups are muted from tray
                    mutePopupsMenu.Visibility = arePopupsMuted ? Visibility.Collapsed : Visibility.Visible;
                    resumePopupsItem.Visibility = arePopupsMuted ? Visibility.Visible : Visibility.Collapsed;
                    
                    // Update settings menu checkmarks to reflect current state
                    UpdateSettingsMenuState(settingsMenu, app);
                }
            };
            
            return menu;
        }

        /// <summary>
        /// Creates a standard WPF menu item with icon and text
        /// </summary>
        public MenuItem CreateMenuItem(string text, string fontAwesomeIcon, Action? clickHandler = null)
        {
            var item = new MenuItem();
            
            // Create a Grid to hold icon and text with full width for better hit testing
            var grid = new Grid();
            grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Icon column - increased for wider icons
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text column (fills remaining)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Empty space column (fills remaining)
            
            // Add invisible Rectangle to capture hover/click events across full width
            var invisibleRect = new System.Windows.Shapes.Rectangle();
            invisibleRect.Fill = System.Windows.Media.Brushes.Transparent;
            invisibleRect.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            invisibleRect.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetColumnSpan(invisibleRect, 3); // Span all columns
            grid.Children.Add(invisibleRect);
            
            // Add FontAwesome icon
            var iconText = new TextBlock
            {
                Text = fontAwesomeIcon,
                FontFamily = _iconService.LoadFontAwesomeFont(),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);
            
            // Add menu text
            var textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);
            
            item.Header = grid;
            item.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
            item.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            item.MinWidth = 200; // Ensure consistent width for better hit testing
            item.Width = double.NaN; // Allow to stretch to full available width
            
            if (clickHandler != null)
            {
                item.Click += (s, e) => clickHandler();
            }
            
            _loggingService.Log($"Created WPF menu item: {text} with icon: {fontAwesomeIcon}", LogLevel.Debug);
            
            return item;
        }

        /// <summary>
        /// Creates the settings submenu with all configuration options
        /// </summary>
        public MenuItem CreateSettingsMenu(App app)
        {
            var settingsMenu = CreateMenuItem("Settings", "\uf013");
            
            var startupToggle = CreateSettingsMenuItem("Run at Startup", "\uf011", app.IsInStartup(), () => app.ToggleStartupFromTray(!app.IsInStartup()));
            var minimizeToTrayToggle = CreateSettingsMenuItem("Minimize to Tray", "\uf2d1", app.GetMinimizeToTraySetting(), () => app.ToggleMinimizeToTrayFromTray(!app.GetMinimizeToTraySetting()));
            var startMinimizedToggle = CreateSettingsMenuItem("Start Minimized to Tray", "\uf2d1", App.GetStartMinimizedSetting(), () => app.ToggleStartMinimizedFromTray(!App.GetStartMinimizedSetting()));
            var popupNotificationsToggle = CreateSettingsMenuItem("Popup Notifications", "\uf0f3", app.GetNotificationsEnabledSetting(), () => app.TogglePopupNotificationsFromTray(!app.GetNotificationsEnabledSetting()));
            
            settingsMenu.Items.Add(startupToggle);
            settingsMenu.Items.Add(minimizeToTrayToggle);
            settingsMenu.Items.Add(startMinimizedToggle);
            settingsMenu.Items.Add(popupNotificationsToggle);
            
            return settingsMenu;
        }

        /// <summary>
        /// Creates a settings menu item with state indicator
        /// </summary>
        public MenuItem CreateSettingsMenuItem(string text, string iconGlyph, bool isEnabled, Action clickHandler)
        {
            var item = new MenuItem();

            var panel = new DockPanel();

            var iconText = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = _iconService.LoadFontAwesomeFont(),
                FontSize = 14,
                Margin = new Thickness(6, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            DockPanel.SetDock(iconText, Dock.Left);
            panel.Children.Add(iconText);

            var mainText = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Name = "MainText" // Name for easy reference
            };
            DockPanel.SetDock(mainText, Dock.Left);
            panel.Children.Add(mainText);

            // Checkmark for enabled state
            var checkIcon = new TextBlock
            {
                Text = isEnabled ? "\uf00c" : "", // Show checkmark if enabled
                FontFamily = _iconService.LoadFontAwesomeFont(),
                FontSize = 14,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 0)),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 20,
                TextAlignment = TextAlignment.Center,
                Name = "CheckIcon" // Name for easy reference
            };
            DockPanel.SetDock(checkIcon, Dock.Right);
            panel.Children.Add(checkIcon);

            item.Header = panel;
            item.Click += (s, e) => clickHandler();

            return item;
        }

        /// <summary>
        /// Updates the checkmark state of settings menu items to reflect current settings
        /// </summary>
        private void UpdateSettingsMenuState(MenuItem settingsMenu, App app)
        {
            try
            {
                foreach (MenuItem item in settingsMenu.Items)
                {
                    if (item.Header is DockPanel panel)
                    {
                        // Find the main text to identify which setting this is
                        var mainTextBlock = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "MainText");
                        var checkIconBlock = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "CheckIcon");
                        
                        if (mainTextBlock != null && checkIconBlock != null)
                        {
                            bool isEnabled = mainTextBlock.Text switch
                            {
                                "Run at Startup" => app.IsInStartup(),
                                "Minimize to Tray" => app.GetMinimizeToTraySetting(),
                                "Start Minimized to Tray" => App.GetStartMinimizedSetting(),
                                "Popup Notifications" => app.GetNotificationsEnabledSetting(),
                                _ => false
                            };
                            
                            checkIconBlock.Text = isEnabled ? "\uf00c" : "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error updating settings menu state: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Creates the application icon header for the context menu
        /// </summary>
        public UIElement CreateAppIconHeader()
        {
            try
            {
                var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                
                // App icon
                var appIcon = new System.Windows.Controls.Image
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(8, 4, 8, 4)
                };
                
                try
                {
                    appIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
                }
                catch (Exception ex)
                {
                    _loggingService.Log($"Failed to load app icon for header: {ex.Message}", LogLevel.Debug);
                }
                
                headerPanel.Children.Add(appIcon);
                
                // App name
                var appName = new TextBlock
                {
                    Text = "ToxMox's Mic Freezer+",
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 8, 4)
                };
                headerPanel.Children.Add(appName);
                
                return headerPanel;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error creating app icon header: {ex.Message}", LogLevel.Error);
                
                // Fallback: simple text header
                return new TextBlock
                {
                    Text = "ToxMox's Mic Freezer+",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(8, 4, 8, 4)
                };
            }
        }
    }
}