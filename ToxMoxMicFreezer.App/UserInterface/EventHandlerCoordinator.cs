// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Coordinates miscellaneous event handlers for UI interactions, device management, and window events
    /// </summary>
    public class EventHandlerCoordinator
    {
        private readonly MainWindow _mainWindow;

        public EventHandlerCoordinator(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }


        /// <summary>
        /// Handles debug refresh button click for manual device re-enumeration
        /// </summary>
        public void OnDebugRefreshButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.AppendLog($"[DEBUG] Manual device re-enumeration triggered by user");
            _mainWindow.AppendLog($"[DEBUG] Current cache sizes - Device: {_mainWindow._deviceManager?.DeviceCache.Count ?? 0}, Native: {_mainWindow._deviceManager?.NativeDeviceCache.Count ?? 0}");
            _mainWindow.AppendLog($"[DEBUG] Current UI collections - Recording: {_mainWindow.Devices.Count}, Playback: {_mainWindow.PlaybackDevices.Count}");
            Task.Run(() => _mainWindow._deviceManager?.BasicLoadAudioDevices());
        }

        /// <summary>
        /// Handles exit button click
        /// </summary>
        public void OnExitButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow._isExit = true;
            _mainWindow.Close();
        }

        /// <summary>
        /// Handles window closing event
        /// </summary>
        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (!_mainWindow._isExit)
            {
                e.Cancel = true;
                _mainWindow.Hide();
                _mainWindow.AppendLog("App minimized to tray.");
            }
            else
            {
                if (_mainWindow._cts != null)
                {
                    _mainWindow._cts.Cancel();
                }
                
                // Clean up the orchestrator service (including meter update timer)
                _mainWindow._mainWindowOrchestrator?.Cleanup();
            }
        }

        /// <summary>
        /// Handles window state change events
        /// </summary>
        public void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                bool minimizeToTray = ((App)System.Windows.Application.Current).GetMinimizeToTraySetting();
                if (minimizeToTray)
                {
                    _mainWindow.Hide();
                    _mainWindow.AppendLog("App minimized to tray.");
                }
            }
            else
            {
                // Update maximize icon when window state changes
                _mainWindow._windowManager?.UpdateMaximizeIcon(_mainWindow.WindowState == WindowState.Maximized);
                
                // Update corner radius based on window state
                var mainBorder = _mainWindow.FindName("MainBorder") as System.Windows.Controls.Border;
                if (mainBorder != null)
                {
                    mainBorder.CornerRadius = _mainWindow.WindowState == WindowState.Maximized 
                        ? new System.Windows.CornerRadius(0) 
                        : new System.Windows.CornerRadius(8);
                }
            }
        }

        /// <summary>
        /// Handles pause button click - toggles pause state
        /// </summary>
        public void OnPauseButtonClick(object sender, RoutedEventArgs e)
        {
            if (_mainWindow.PauseManager.IsPaused)
            {
                _mainWindow.PauseManager.Resume();
            }
            else
            {
                _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(15), "15 minutes");
            }
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles pause for 5 minutes button click
        /// </summary>
        public void OnPause5MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(5), "5 minutes");
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles pause for 15 minutes button click
        /// </summary>
        public void OnPause15MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(15), "15 minutes");
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles pause for 30 minutes button click
        /// </summary>
        public void OnPause30MinClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(30), "30 minutes");
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles pause for 1 hour button click
        /// </summary>
        public void OnPause1HourClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.PauseFor(TimeSpan.FromHours(1), "1 hour");
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles pause indefinitely button click
        /// </summary>
        public void OnPauseIndefiniteClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.PauseIndefinitely();
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles resume button click
        /// </summary>
        public void OnResumeButtonClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.PauseManager.Resume();
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles extend pause button click
        /// </summary>
        public void OnExtendPauseClick(object sender, RoutedEventArgs e)
        {
            if (_mainWindow.PauseManager.IsPaused && _mainWindow.PauseManager.PauseEndTime.HasValue)
            {
                var newDuration = _mainWindow.PauseManager.PauseEndTime.Value.Add(TimeSpan.FromMinutes(5)) - DateTime.Now;
                _mainWindow.PauseManager.PauseFor(newDuration, _mainWindow.PauseManager.PauseReason + " (+5 min)");
            }
            else if (_mainWindow.PauseManager.IsPaused)
            {
                _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(5), "5 minutes");
            }
            _mainWindow.UpdatePauseUI();
        }

        /// <summary>
        /// Handles scroll viewer scroll changed events
        /// </summary>
        public void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // The ScrollViewer will automatically synchronize both DataGrids since they're inside it
            // This handler is primarily used for adding custom scroll behavior if needed in the future
        }

        /// <summary>
        /// Handles main scroll viewer preview mouse wheel events
        /// </summary>
        public void OnMainScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Forward the mouse wheel event to the ScrollViewer
            ScrollViewer? scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                if (e.Delta < 0)
                {
                    scrollViewer.LineDown();
                }
                else
                {
                    scrollViewer.LineUp();
                }
            }
            
            // Mark the event as handled so it doesn't get routed to parent containers
            e.Handled = true;
        }
    }
}