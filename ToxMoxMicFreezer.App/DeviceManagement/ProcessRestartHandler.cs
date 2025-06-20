// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ToxMoxMicFreezer.App.DeviceManagement
{
    /// <summary>
    /// NUCLEAR OPTION: Handles device changes by restarting the entire process
    /// Eliminates ALL COM disposal/cleanup issues by letting the OS handle everything
    /// </summary>
    public class ProcessRestartHandler : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private bool _disposed = false;
        
        // Store window state for restoration
        private WindowState _lastWindowState;
        private double _lastLeft;
        private double _lastTop;
        private double _lastWidth;
        private double _lastHeight;
        
        public ProcessRestartHandler(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = mainWindow.Dispatcher;
        }
        
        /// <summary>
        /// Save current window state before restart
        /// </summary>
        private void SaveWindowState()
        {
            try
            {
                _lastWindowState = _mainWindow.WindowState;
                _lastLeft = _mainWindow.Left;
                _lastTop = _mainWindow.Top;
                _lastWidth = _mainWindow.Width;
                _lastHeight = _mainWindow.Height;
                
                // Save to registry for restoration using direct registry access
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ToxMoxMicFreezer\ProcessRestart");
                    if (key != null)
                    {
                        key.SetValue("LastWindowState", (int)_lastWindowState);
                        key.SetValue("LastWindowLeft", _lastLeft);
                        key.SetValue("LastWindowTop", _lastTop);
                        key.SetValue("LastWindowWidth", _lastWidth);
                        key.SetValue("LastWindowHeight", _lastHeight);
                        key.SetValue("RestartInProgress", "true");
                    }
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendLog($"Error saving restart state to registry: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error saving window state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NUCLEAR RESTART - restart entire process instead of cleanup
        /// </summary>
        public void RestartProcess(string reason)
        {
            if (_disposed) return;
            
            _dispatcher.Invoke(() => {
                try
                {
                    _mainWindow.AppendLog($"NUCLEAR RESTART: Device change detected ({reason}) - restarting entire process");
                    
                    // Show overlay immediately
                    _mainWindow.ShowDeviceResetOverlay($"Restarting application ({reason})");
                    
                    // Save window position/state for restoration
                    SaveWindowState();
                    
                    // Get current executable path
                    var currentProcess = Process.GetCurrentProcess();
                    var executablePath = currentProcess.MainModule?.FileName;
                    
                    if (string.IsNullOrEmpty(executablePath))
                    {
                        _mainWindow.AppendLog("ERROR: Could not determine executable path for restart");
                        _mainWindow.HideDeviceResetOverlay();
                        return;
                    }
                    
                    _mainWindow.AppendLog($"Restarting process: {executablePath}");
                    
                    // Start new process - let OS handle ALL cleanup
                    var startInfo = new ProcessStartInfo(executablePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(executablePath)
                    };
                    
                    // Launch new instance
                    Process.Start(startInfo);
                    
                    // Force exit current process - no cleanup, let OS handle everything
                    _mainWindow.AppendLog("Forcing process exit - OS will handle all COM cleanup");
                    
                    // Set flag to prevent normal shutdown procedures
                    _mainWindow._isExit = true;
                    
                    // NUCLEAR OPTION: Environment.Exit bypasses ALL cleanup
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendLog($"ERROR: Failed to restart process: {ex.Message}");
                    _mainWindow.HideDeviceResetOverlay();
                }
            });
        }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
}