// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;
using System.Windows.Controls;

namespace ToxMoxMicFreezer.App.UserControls
{
    /// <summary>
    /// UserControl for the log panel
    /// Replaces the log panel content in MainWindow.xaml
    /// </summary>
    public partial class LogPanelUserControl : System.Windows.Controls.UserControl
    {
        private MainWindow? _mainWindow;

        public LogPanelUserControl()
        {
            InitializeComponent();
            
            // Find the MainWindow reference for event delegation
            Loaded += (s, e) => {
                _mainWindow = FindMainWindow();
            };
        }

        #region Public Control Properties for MainWindow Access

        public System.Windows.Controls.TextBlock LogHeaderTextControl => LogHeaderText;
        public System.Windows.Controls.Button LogResumeButtonControl => LogResumeButton;
        public System.Windows.Controls.Button LogClearButtonControl => LogClearButton;
        public System.Windows.Controls.ScrollViewer LogScrollViewerControl => LogScrollViewer;
        public System.Windows.Controls.TextBox LogTextBoxControl => LogTextBox;

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

        #region Log Event Handlers

        private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnLogScrollViewerScrollChanged(sender, e);
        }

        private void LogResumeButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnLogResumeButtonClick(sender, e);
        }

        private void LogClearButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnLogClearButtonClick(sender, e);
        }

        #endregion
    }
}