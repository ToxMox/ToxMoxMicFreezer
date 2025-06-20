// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Windows;
using System.Windows.Threading;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for detecting double-clicks and managing click intent detection
    /// Handles the timing and logic to distinguish between single clicks and double clicks
    /// </summary>
    public class DoubleClickDetectionService : IDoubleClickDetectionService, IDisposable
    {
        private readonly ILoggingService _loggingService;
        private readonly Dispatcher _dispatcher;
        
        // Intent detection timer for double-click vs single-click
        private System.Timers.Timer? _intentDetectionTimer;
        private const int INTENT_DETECTION_MS = 200; // Wait 200ms to determine user intent
        
        // Double-click detection state
        private DateTime _lastClickTime = DateTime.MinValue;
        private AudioDeviceViewModel? _lastClickedDevice = null;
        
        // Pending intent state
        private bool _waitingForIntent = false;
        private System.Windows.Point _pendingClickPosition;
        private AudioDeviceViewModel? _pendingClickDevice;

        public DoubleClickDetectionService(ILoggingService loggingService, Dispatcher dispatcher)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            
            // Initialize intent detection timer
            _intentDetectionTimer = new System.Timers.Timer(INTENT_DETECTION_MS) { AutoReset = false };
            _intentDetectionTimer.Elapsed += OnIntentDetectionTimer;
        }

        /// <summary>
        /// Event fired when a single click intent is detected (after delay)
        /// </summary>
        public event Action<System.Windows.Point, AudioDeviceViewModel>? SingleClickDetected;

        /// <summary>
        /// Event fired when a double click is detected immediately
        /// </summary>
        public event Action<AudioDeviceViewModel>? DoubleClickDetected;

        /// <summary>
        /// Gets whether click intent detection is currently active
        /// </summary>
        public bool IsWaitingForIntent => _waitingForIntent;

        /// <summary>
        /// Gets the current intent detection delay in milliseconds
        /// </summary>
        public int IntentDetectionDelayMs => INTENT_DETECTION_MS;

        /// <summary>
        /// Processes a mouse click and determines if it's a single or double click
        /// </summary>
        public bool ProcessClick(AudioDeviceViewModel device, System.Windows.Point mousePosition)
        {
            try
            {
                var now = DateTime.Now;
                var timeSinceLastClick = now - _lastClickTime;
                
                // Check for double-click (second click within delay time on same device)
                if (timeSinceLastClick.TotalMilliseconds < INTENT_DETECTION_MS && _lastClickedDevice == device)
                {
                    // Double-click detected - cancel intent detection and fire double-click event
                    CancelPendingIntent();
                    
                    _loggingService.Log($"Double-click detected on device: {device.Name}", LogLevel.Debug);
                    DoubleClickDetected?.Invoke(device);
                    
                    // Reset click tracking
                    _lastClickTime = DateTime.MinValue;
                    _lastClickedDevice = null;
                    
                    return true; // This was a double-click
                }
                
                // Single click or first click - start intent detection
                _lastClickTime = now;
                _lastClickedDevice = device;
                
                // Store pending action data
                _pendingClickPosition = mousePosition;
                _pendingClickDevice = device;
                _waitingForIntent = true;
                
                // Start intent detection timer
                _intentDetectionTimer?.Stop();
                _intentDetectionTimer?.Start();
                
                _loggingService.Log($"Single click detected, starting intent detection for device: {device.Name}", LogLevel.Debug);
                
                return false; // This was not a double-click (yet)
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error processing click: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Cancels any pending click intent detection
        /// </summary>
        public void CancelPendingIntent()
        {
            _intentDetectionTimer?.Stop();
            _waitingForIntent = false;
            _pendingClickDevice = null;
        }

        /// <summary>
        /// Timer event handler for intent detection - fires single click event after delay
        /// </summary>
        private void OnIntentDetectionTimer(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Execute on UI thread
            _dispatcher.Invoke(() =>
            {
                if (_waitingForIntent && _pendingClickDevice != null)
                {
                    _loggingService.Log($"Single click intent confirmed for device: {_pendingClickDevice.Name}", LogLevel.Debug);
                    
                    // Fire single click event
                    SingleClickDetected?.Invoke(_pendingClickPosition, _pendingClickDevice);
                }
                
                // Clear intent detection state
                _waitingForIntent = false;
                _pendingClickDevice = null;
            });
        }

        /// <summary>
        /// Disposes of the service and cleans up resources
        /// </summary>
        public void Dispose()
        {
            _intentDetectionTimer?.Stop();
            _intentDetectionTimer?.Dispose();
            _intentDetectionTimer = null;
        }
    }
}