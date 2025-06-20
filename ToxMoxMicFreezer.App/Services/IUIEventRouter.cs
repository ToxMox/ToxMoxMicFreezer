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
    public interface IUIEventRouter
    {
        // Window management events
        void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e);
        void OnMinimizeButtonClick(object sender, RoutedEventArgs e);
        void OnMaximizeButtonClick(object sender, RoutedEventArgs e);
        void OnCloseButtonClick(object sender, RoutedEventArgs e);
        void OnWindowClosing(object sender, CancelEventArgs e);
        void OnWindowStateChanged(object sender, EventArgs e);
        void OnWindowSizeChanged(object sender, SizeChangedEventArgs e);
        void OnWindowLocationChanged(object sender, EventArgs e);
        
        // Button click events
        void OnDebugRefreshButtonClick(object sender, RoutedEventArgs e);
        void OnPauseButtonClick(object sender, RoutedEventArgs e);
        void OnResumeButtonClick(object sender, RoutedEventArgs e);
        void OnPause5MinClick(object sender, RoutedEventArgs e);
        void OnPause15MinClick(object sender, RoutedEventArgs e);
        void OnPause30MinClick(object sender, RoutedEventArgs e);
        void OnPause1HourClick(object sender, RoutedEventArgs e);
        void OnPauseIndefiniteClick(object sender, RoutedEventArgs e);
        void OnExtendPauseClick(object sender, RoutedEventArgs e);
        void OnExitButtonClick(object sender, RoutedEventArgs e);
        
        // Volume bar interactions
        void OnVolumeBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e);
        void OnVolumeBarMouseMove(object sender, System.Windows.Input.MouseEventArgs e);
        void OnVolumeBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e);
        void OnVolumeBarMouseRightButtonDown(object sender, MouseButtonEventArgs e);
        
        // Volume text editing
        void OnVolumeTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e);
        void OnVolumeTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e);
        void OnVolumeTextBoxLostFocus(object sender, RoutedEventArgs e);
        void OnVolumeTextBoxLoaded(object sender, RoutedEventArgs e);
        
        // Settings toggles
        void OnStartupToggleChecked(object sender, RoutedEventArgs e);
        void OnStartupToggleUnchecked(object sender, RoutedEventArgs e);
        void OnMinimizeToTrayToggleChecked(object sender, RoutedEventArgs e);
        void OnMinimizeToTrayToggleUnchecked(object sender, RoutedEventArgs e);
        void OnStartMinimizedToggleChecked(object sender, RoutedEventArgs e);
        void OnStartMinimizedToggleUnchecked(object sender, RoutedEventArgs e);
        void OnHideFixedVolumeToggleChecked(object sender, RoutedEventArgs e);
        void OnHideFixedVolumeToggleUnchecked(object sender, RoutedEventArgs e);
        void OnPopupNotificationsToggleChecked(object sender, RoutedEventArgs e);
        void OnPopupNotificationsToggleUnchecked(object sender, RoutedEventArgs e);
        void OnAudioMeteringToggleChecked(object sender, RoutedEventArgs e);
        void OnAudioMeteringToggleUnchecked(object sender, RoutedEventArgs e);
        void OnDebugLoggingToggleChecked(object sender, RoutedEventArgs e);
        void OnDebugLoggingToggleUnchecked(object sender, RoutedEventArgs e);
        
        void OnSettingsButtonClick(object sender, RoutedEventArgs e);
        
        // Scroll and mouse events
        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e);
        void OnMainScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e);
        void OnLogScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e);
        void OnLogResumeButtonClick(object sender, RoutedEventArgs e);
        void OnLogClearButtonClick(object sender, RoutedEventArgs e);
        
        // Resize events
        void OnResizeGripDragDelta(object sender, DragDeltaEventArgs e);
        void OnResizeSidePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e);
        void OnLogPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e);
    }
}