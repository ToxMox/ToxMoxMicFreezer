// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace ToxMoxMicFreezer.App.Services
{
    public class VolumeChangeBuffer
    {
        public string DeviceName { get; set; } = string.Empty;
        public float StartVolume { get; set; }
        public float EndVolume { get; set; }
        public int ChangeCount { get; set; }
        public DateTime LastChangeTime { get; set; }
        public System.Timers.Timer? GroupingTimer { get; set; }
        public bool IsExternal { get; set; }
    }

    public class LoggingService : ILoggingService
    {
        private readonly Dictionary<string, VolumeChangeBuffer> _logGroupingBuffers = new();
        private readonly object _logGroupingLock = new object();
        private readonly object _logScrollLock = new object();
        private bool _isAutoScrollEnabled = true;
        private LogLevel _currentLogLevel = LogLevel.Info;
        private const double LOG_BOTTOM_THRESHOLD = 5.0;

        // UI elements - will be set via dependency injection or setter
        private System.Windows.Controls.TextBox? _logTextBox;
        private System.Windows.Controls.ScrollViewer? _logScrollViewer;
        private System.Windows.Threading.Dispatcher? _dispatcher;

        public bool IsAutoScrollEnabled 
        { 
            get { lock (_logScrollLock) { return _isAutoScrollEnabled; } }
        }

        public event EventHandler<LogEventArgs>? LogAdded;
        public event EventHandler? AutoScrollChanged;

        public void Initialize(System.Windows.Controls.TextBox logTextBox, System.Windows.Controls.ScrollViewer logScrollViewer, System.Windows.Threading.Dispatcher dispatcher)
        {
            _logTextBox = logTextBox;
            _logScrollViewer = logScrollViewer;
            _dispatcher = dispatcher;
        }

        public void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            // Exit early if debug mode is disabled and this is a debug message
            if (level == LogLevel.Debug && _currentLogLevel != LogLevel.Debug)
                return;

            try
            {
                var timestamp = DateTime.Now.ToString("T");
                var timestampedMessage = $"[{timestamp}] {message}\n";

                _dispatcher?.BeginInvoke(() =>
                {
                    try
                    {
                        _logTextBox?.AppendText(timestampedMessage);
                        
                        // Only auto-scroll if enabled
                        lock (_logScrollLock)
                        {
                            if (_isAutoScrollEnabled)
                            {
                                _logScrollViewer?.ScrollToBottom();
                                _logTextBox?.ScrollToEnd();
                            }
                        }

                        // Raise event
                        LogAdded?.Invoke(this, new LogEventArgs 
                        { 
                            Message = message, 
                            Timestamp = DateTime.Now, 
                            Level = level 
                        });
                    }
                    catch
                    {
                        // Silent catch to prevent cascading errors in logging
                    }
                });
            }
            catch
            {
                // Silent catch for any timestamp/formatting errors
            }
        }

        public void LogVolumeChange(MMDevice device, float previousVolume, float currentVolume, string source)
        {
            var deviceId = device.ID;
            var deviceName = device.FriendlyName;
            var isExternal = source != "UI";

            lock (_logGroupingLock)
            {
                var now = DateTime.Now;
                
                if (!_logGroupingBuffers.TryGetValue(deviceId, out var buffer))
                {
                    buffer = new VolumeChangeBuffer
                    {
                        DeviceName = deviceName,
                        StartVolume = currentVolume,
                        EndVolume = currentVolume,
                        ChangeCount = 1,
                        LastChangeTime = now,
                        IsExternal = isExternal
                    };
                    _logGroupingBuffers[deviceId] = buffer;
                    
                    // Show immediately for first change using popup format
                    var changeType = isExternal ? "externally" : "via UI";
                    Log($"{deviceName} changed {changeType} (Set to {currentVolume:F1}dB)");
                    
                    // Start grouping timer for subsequent changes
                    StartLogGroupingTimer(deviceId, buffer);
                    return;
                }
                
                // Update existing buffer
                buffer.EndVolume = currentVolume;
                buffer.ChangeCount++;
                buffer.LastChangeTime = now;
                buffer.IsExternal = isExternal; // Use latest source
                
                // Performance optimization: Reset existing timer instead of disposing/recreating
                ResetLogGroupingTimer(deviceId, buffer);
            }
        }

        private void ResetLogGroupingTimer(string deviceId, VolumeChangeBuffer buffer)
        {
            if (buffer.GroupingTimer != null)
            {
                // Performance optimization: Reuse existing timer instead of dispose/create
                buffer.GroupingTimer.Stop();
                buffer.GroupingTimer.Start();
            }
            else
            {
                // Create new timer only if one doesn't exist
                StartLogGroupingTimer(deviceId, buffer);
            }
        }

        private void StartLogGroupingTimer(string deviceId, VolumeChangeBuffer buffer)
        {
            buffer.GroupingTimer = new System.Timers.Timer(2000); // 2 second cooldown
            buffer.GroupingTimer.Elapsed += (s, e) => FlushLogGroup(deviceId);
            buffer.GroupingTimer.AutoReset = false;
            buffer.GroupingTimer.Start();
        }

        private void FlushLogGroup(string deviceId)
        {
            lock (_logGroupingLock)
            {
                if (_logGroupingBuffers.TryGetValue(deviceId, out var buffer))
                {
                    if (buffer.ChangeCount > 1)
                    {
                        var changeType = buffer.IsExternal ? "externally" : "via UI";
                        Log($"{buffer.DeviceName} changed {changeType} ({buffer.ChangeCount}x) (Set to {buffer.EndVolume:F1}dB)");
                    }
                    
                    buffer.GroupingTimer?.Dispose();
                    _logGroupingBuffers.Remove(deviceId);
                }
            }
        }

        public void SetAutoScroll(bool enabled)
        {
            lock (_logScrollLock)
            {
                if (_isAutoScrollEnabled == enabled) return;
                
                _isAutoScrollEnabled = enabled;
                
                // Raise event
                AutoScrollChanged?.Invoke(this, EventArgs.Empty);
                
                if (enabled)
                {
                    // Scroll to bottom when enabling
                    _dispatcher?.BeginInvoke(() =>
                    {
                        _logScrollViewer?.ScrollToBottom();
                        _logTextBox?.ScrollToEnd();
                    });
                }
            }
        }

        public void Clear()
        {
            _dispatcher?.BeginInvoke(() =>
            {
                _logTextBox?.Clear();
                // After clearing, we're automatically at the bottom, so ensure auto-scroll is enabled
                SetAutoScroll(true);
            });
        }

        public void HandleScrollChanged(System.Windows.Controls.ScrollViewer scrollViewer, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (scrollViewer == null) return;
            
            lock (_logScrollLock)
            {
                // Only process scroll changes if there's scrollable content
                if (scrollViewer.ScrollableHeight <= 0) return;
                
                // Calculate distance from bottom
                double distanceFromBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
                
                // Check if user is near the bottom (within threshold)
                bool isNearBottom = distanceFromBottom <= LOG_BOTTOM_THRESHOLD;
                
                // Only change state if the scroll change was significant
                if (e.ExtentHeightChange == 0) // User-initiated scroll (not content change)
                {
                    if (isNearBottom && !_isAutoScrollEnabled)
                    {
                        // User scrolled back to bottom - resume auto-scroll
                        SetAutoScroll(true);
                    }
                    else if (!isNearBottom && _isAutoScrollEnabled)
                    {
                        // User scrolled up - pause auto-scroll
                        SetAutoScroll(false);
                    }
                }
            }
        }

        public void Dispose()
        {
            // Clean up all timers
            lock (_logGroupingLock)
            {
                foreach (var buffer in _logGroupingBuffers.Values)
                {
                    buffer.GroupingTimer?.Stop();
                    buffer.GroupingTimer?.Dispose();
                }
                _logGroupingBuffers.Clear();
            }
        }
    }
}