// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ToxMoxMicFreezer.App.UserControls
{
    /// <summary>
    /// UserControl for the settings panel
    /// Replaces the settings popup content in MainWindow.xaml
    /// </summary>
    public partial class SettingsPanelUserControl : System.Windows.Controls.UserControl
    {
        private MainWindow? _mainWindow;

        public SettingsPanelUserControl()
        {
            InitializeComponent();
            
            // Find the MainWindow reference for event delegation
            Loaded += (s, e) => {
                _mainWindow = FindMainWindow();
            };
        }

        #region Public Control Properties for MainWindow Access

        public System.Windows.Controls.CheckBox StartupToggleControl => StartupToggle;
        public System.Windows.Controls.CheckBox MinimizeToTrayToggleControl => MinimizeToTrayToggle;
        public System.Windows.Controls.CheckBox StartMinimizedToggleControl => StartMinimizedToggle;
        public System.Windows.Controls.CheckBox HideFixedVolumeToggleControl => HideFixedVolumeToggle;
        public System.Windows.Controls.CheckBox PopupNotificationsToggleControl => PopupNotificationsToggle;
        public System.Windows.Controls.CheckBox AudioMeteringToggleControl => AudioMeteringToggle;
        public System.Windows.Controls.CheckBox DebugLoggingToggleControl => DebugLoggingToggle;

        #endregion

        private MainWindow? FindMainWindow()
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is MainWindow mainWindow)
                    return mainWindow;
                current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        #region Settings Toggle Event Handlers

        private void StartupToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnStartupToggleChecked(sender, e);
        }

        private void StartupToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnStartupToggleUnchecked(sender, e);
        }

        private void MinimizeToTrayToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnMinimizeToTrayToggleChecked(sender, e);
        }

        private void MinimizeToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnMinimizeToTrayToggleUnchecked(sender, e);
        }

        private void StartMinimizedToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnStartMinimizedToggleChecked(sender, e);
        }

        private void StartMinimizedToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnStartMinimizedToggleUnchecked(sender, e);
        }

        private void HideFixedVolumeToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnHideFixedVolumeToggleChecked(sender, e);
        }

        private void HideFixedVolumeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnHideFixedVolumeToggleUnchecked(sender, e);
        }

        private void PopupNotificationsToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnPopupNotificationsToggleChecked(sender, e);
        }

        private void PopupNotificationsToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnPopupNotificationsToggleUnchecked(sender, e);
        }

        private void AudioMeteringToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnAudioMeteringToggleChecked(sender, e);
        }

        private void AudioMeteringToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnAudioMeteringToggleUnchecked(sender, e);
        }

        private void DebugLoggingToggle_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnDebugLoggingToggleChecked(sender, e);
        }

        private void DebugLoggingToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnDebugLoggingToggleUnchecked(sender, e);
        }

        #endregion
    }
}