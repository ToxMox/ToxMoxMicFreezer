// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Central UI event router that delegates events to appropriate manager classes
    /// Follows Single Responsibility Principle by coordinating event handling
    /// </summary>
    public class UIEventRouter : IUIEventRouter
    {
        private readonly MainWindow _mainWindow;
        private readonly ILoggingService _loggingService;
        private readonly IWindowEventService _windowEventService;
        private readonly ISettingsToggleEventService _settingsToggleEventService;
        private readonly IVolumeBarEventService _volumeBarEventService;

        public UIEventRouter(MainWindow mainWindow, ILoggingService loggingService, IWindowEventService windowEventService, ISettingsToggleEventService settingsToggleEventService, IVolumeBarEventService volumeBarEventService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _windowEventService = windowEventService ?? throw new ArgumentNullException(nameof(windowEventService));
            _settingsToggleEventService = settingsToggleEventService ?? throw new ArgumentNullException(nameof(settingsToggleEventService));
            _volumeBarEventService = volumeBarEventService ?? throw new ArgumentNullException(nameof(volumeBarEventService));
        }

        #region Window Management Events

        public void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _windowEventService.OnTitleBarMouseLeftButtonDown(sender, e);
        }

        public void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            _windowEventService.OnMinimizeButtonClick(sender, e);
        }

        public void OnMaximizeButtonClick(object sender, RoutedEventArgs e)
        {
            _windowEventService.OnMaximizeButtonClick(sender, e);
        }

        public void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            _windowEventService.OnCloseButtonClick(sender, e);
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _windowEventService.OnWindowClosing(sender, e);
        }

        public void OnWindowStateChanged(object sender, EventArgs e)
        {
            _windowEventService.OnWindowStateChanged(sender, e);
        }

        public void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _windowEventService.OnWindowSizeChanged(sender, e);
        }

        public void OnWindowLocationChanged(object sender, EventArgs e)
        {
            _windowEventService.OnWindowLocationChanged(sender, e);
        }

        #endregion

        #region Button Click Events

        public void OnDebugRefreshButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnDebugRefreshButtonClick(sender, e);
        }

        public void OnPauseButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPauseButtonClick(sender, e);
        }

        public void OnResumeButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnResumeButtonClick(sender, e);
        }

        public void OnPause5MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPause5MinClick(sender, e);
        }

        public void OnPause15MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPause15MinClick(sender, e);
        }

        public void OnPause30MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPause30MinClick(sender, e);
        }

        public void OnPause1HourClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPause1HourClick(sender, e);
        }

        public void OnPauseIndefiniteClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnPauseIndefiniteClick(sender, e);
        }

        public void OnExtendPauseClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnExtendPauseClick(sender, e);
        }

        public void OnExitButtonClick(object sender, RoutedEventArgs e)
        {
            _windowEventService.OnExitButtonClick(sender, e);
        }

        #endregion

        #region Volume Bar Interactions

        public void OnVolumeBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarEventService.OnVolumeBarMouseLeftButtonDown(sender, e);
        }

        public void OnVolumeBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _volumeBarEventService.OnVolumeBarMouseMove(sender, e);
        }

        public void OnVolumeBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _volumeBarEventService.OnVolumeBarMouseLeftButtonUp(sender, e);
        }

        public void OnVolumeBarMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarEventService.OnVolumeBarMouseRightButtonDown(sender, e);
        }

        #endregion

        #region Volume Text Editing

        public void OnVolumeTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _volumeBarEventService.OnVolumeTextMouseLeftButtonDown(sender, e);
        }

        public void OnVolumeTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _volumeBarEventService.OnVolumeTextBoxKeyDown(sender, e);
        }

        public void OnVolumeTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            _volumeBarEventService.OnVolumeTextBoxLostFocus(sender, e);
        }

        public void OnVolumeTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            _volumeBarEventService.OnVolumeTextBoxLoaded(sender, e);
        }

        #endregion

        #region Settings Toggles

        public void OnStartupToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnStartupToggleChecked(sender, e);
        }

        public void OnStartupToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnStartupToggleUnchecked(sender, e);
        }

        public void OnMinimizeToTrayToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnMinimizeToTrayToggleChecked(sender, e);
        }

        public void OnMinimizeToTrayToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnMinimizeToTrayToggleUnchecked(sender, e);
        }

        public void OnStartMinimizedToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnStartMinimizedToggleChecked(sender, e);
        }

        public void OnStartMinimizedToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnStartMinimizedToggleUnchecked(sender, e);
        }

        public void OnHideFixedVolumeToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnHideFixedVolumeToggleChecked(sender, e);
        }

        public void OnHideFixedVolumeToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnHideFixedVolumeToggleUnchecked(sender, e);
        }

        public void OnPopupNotificationsToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnPopupNotificationsToggleChecked(sender, e);
        }

        public void OnPopupNotificationsToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnPopupNotificationsToggleUnchecked(sender, e);
        }

        public void OnAudioMeteringToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnAudioMeteringToggleChecked(sender, e);
        }

        public void OnAudioMeteringToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnAudioMeteringToggleUnchecked(sender, e);
        }

        public void OnDebugLoggingToggleChecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnDebugLoggingToggleChecked(sender, e);
        }

        public void OnDebugLoggingToggleUnchecked(object sender, RoutedEventArgs e)
        {
            _settingsToggleEventService.OnDebugLoggingToggleUnchecked(sender, e);
        }

        #endregion


        public void OnSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            // Toggle settings popup
            if (_mainWindow.SettingsPopup != null)
            {
                _mainWindow.SettingsPopup.IsOpen = !_mainWindow.SettingsPopup.IsOpen;
            }
        }

        #region Scroll and Mouse Events

        public void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnScrollViewerScrollChanged(sender, e);
        }

        public void OnMainScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _mainWindow._eventHandlerCoordinator?.OnMainScrollViewerPreviewMouseWheel(sender, e);
        }

        public void OnLogScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                _loggingService.HandleScrollChanged(scrollViewer, e);
                
                // Update UI based on auto-scroll state
                if (_loggingService.IsAutoScrollEnabled)
                {
                    _mainWindow.LogPanel.LogHeaderTextControl.Text = "Live Log:";
                    _mainWindow.LogPanel.LogResumeButtonControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _mainWindow.LogPanel.LogHeaderTextControl.Text = "Log: (paused)";
                    _mainWindow.LogPanel.LogResumeButtonControl.Visibility = Visibility.Visible;
                }
            }
        }

        public void OnLogResumeButtonClick(object sender, RoutedEventArgs e)
        {
            _loggingService.SetAutoScroll(true);
        }

        public void OnLogClearButtonClick(object sender, RoutedEventArgs e)
        {
            _loggingService.Clear();
        }

        #endregion

        #region Resize Events

        public void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e)
        {
            _windowEventService.OnResizeGripDragDelta(sender, e);
        }

        public void OnResizeSidePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _windowEventService.OnResizeSidePreviewMouseLeftButtonDown(sender, e);
        }

        public void OnLogPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            // Save the new height to registry when user finishes resizing
            try
            {
                var mainGrid = _mainWindow.Content as Grid;
                if (mainGrid?.RowDefinitions.Count > 5)
                {
                    var logPanelHeight = mainGrid.RowDefinitions[5].ActualHeight;
                    _mainWindow._settingsManager?.SaveLogPanelHeight(logPanelHeight);
                }
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error saving log panel size: {ex.Message}");
            }
        }

        #endregion
    }
}