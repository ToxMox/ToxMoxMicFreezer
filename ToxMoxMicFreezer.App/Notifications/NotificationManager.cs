// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows.Threading;
using CommunityToolkit.WinUI.Notifications;
using ToxMoxMicFreezer.App.Services;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Notifications
{
    /// <summary>
    /// Tracks notification phase and timing for each device
    /// </summary>
    public class NotificationPhaseInfo
    {
        public int Phase { get; set; } = 1;
        public DateTime LastNotification { get; set; } = DateTime.MinValue;
        public int NotificationCount { get; set; } = 0;
        public DateTime LastActivity { get; set; } = DateTime.Now;
        public List<VolumeChangeEvent> PendingEvents { get; set; } = new();
        public System.Timers.Timer? GroupingTimer { get; set; }
    }

    /// <summary>
    /// Manages all notification functionality including toast notifications, 
    /// popup muting, and notification throttling with sophisticated 4-phase system
    /// </summary>
    public class NotificationManager : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private readonly ILoggingService? _loggingService;
        private readonly object _notificationLock = new object();
        
        // Notification throttling system
        private readonly Dictionary<string, NotificationPhaseInfo> _deviceNotificationPhases = new();
        private readonly HashSet<string> _mutedDevices = new();
        private readonly Dictionary<string, DateTime> _mutedDeviceExpiry = new();
        
        // Global popup muting system
        private DateTime _popupsMutedUntil = DateTime.MinValue;
        private bool _popupsMutedIndefinitely = false;
        private string _muteReason = "";
        private System.Timers.Timer? _muteTimer;
        
        private bool _disposed = false;
        
        // Public properties for timing information (similar to PauseManager)
        public DateTime? MuteEndTime => _popupsMutedIndefinitely ? null : (_popupsMutedUntil == DateTime.MinValue ? null : _popupsMutedUntil);
        public TimeSpan? TimeRemaining => MuteEndTime?.Subtract(DateTime.Now);
        public string MuteReason => _muteReason;
        public bool IsMuted => _popupsMutedIndefinitely || DateTime.Now < _popupsMutedUntil;
        
        // Event for mute state changes
        public event Action<bool>? MuteStateChanged;

        public NotificationManager(MainWindow mainWindow, ILoggingService? loggingService = null)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _loggingService = loggingService;
            _dispatcher = mainWindow.Dispatcher;
            
            // Register for toast notification activations
            ToastNotificationManagerCompat.OnActivated += OnToastNotificationActivated;
        }

        /// <summary>
        /// Handles volume change events and shows appropriate notifications
        /// </summary>
        public void OnExternalVolumeChange(VolumeChangeEvent volumeEvent)
        {
            try
            {
                if (_mainWindow.PauseManager.IsPaused)
                {
                    return; // Don't show notifications when paused
                }

                if (!((App)System.Windows.Application.Current).GetNotificationsEnabledSetting())
                {
                    return; // Notifications disabled
                }

                _dispatcher.BeginInvoke(() =>
                {
                    // Check if this device is frozen/monitored - search ALL device collections
                    var device = _mainWindow.Devices.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected) ??
                                 _mainWindow.PlaybackDevices.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected) ??
                                 _mainWindow.RecordingDevicesLeft.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected) ??
                                 _mainWindow.RecordingDevicesRight.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected) ??
                                 _mainWindow.PlaybackDevicesLeft.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected) ??
                                 _mainWindow.PlaybackDevicesRight.FirstOrDefault(d => d.Name == volumeEvent.DeviceName && d.IsSelected);
                    
                    if (device != null)
                    {
                        // Get the full device name for notifications
                        var fullDeviceName = GetFullDeviceName(device);
                        var enhancedEvent = new VolumeChangeEvent
                        {
                            DeviceId = volumeEvent.DeviceId,
                            DeviceName = fullDeviceName,
                            PreviousVolume = volumeEvent.PreviousVolume,
                            NewVolume = volumeEvent.NewVolume,
                            IsDeviceFrozen = volumeEvent.IsDeviceFrozen,
                            IsExternalChange = volumeEvent.IsExternalChange,
                            Timestamp = volumeEvent.Timestamp
                        };
                        ShowVolumeChangeNotification(enhancedEvent);
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error in OnExternalVolumeChange: {ex.Message}");
            }
        }

        private void ShowVolumeChangeNotification(VolumeChangeEvent volumeEvent)
        {
            // Check if popups are globally muted
            if (ArePopupsMuted())
            {
                return; // Silently skip when popups are globally muted
            }

            lock (_notificationLock)
            {
                // Use device name as key for grouping
                var deviceKey = volumeEvent.DeviceName;
                
                // Check if device notifications are muted
                CleanupExpiredMutedApps();
                if (_mutedDevices.Contains(deviceKey))
                {
                    return; // Silently skip muted devices without logging
                }

                // Get or create phase info for this device
                if (!_deviceNotificationPhases.TryGetValue(deviceKey, out var phaseInfo))
                {
                    phaseInfo = new NotificationPhaseInfo();
                    _deviceNotificationPhases[deviceKey] = phaseInfo;
                }

                // Update activity time
                phaseInfo.LastActivity = DateTime.Now;

                // Check if we need to reset phase due to inactivity (30 seconds)
                if ((DateTime.Now - phaseInfo.LastNotification).TotalSeconds > 30)
                {
                    phaseInfo.Phase = 1;
                    phaseInfo.NotificationCount = 0;
                    phaseInfo.PendingEvents.Clear();
                    phaseInfo.GroupingTimer?.Stop();
                }

                // Add this event to pending events
                phaseInfo.PendingEvents.Add(volumeEvent);
                phaseInfo.NotificationCount++;

                // Phase 1: Show first notification immediately
                if (phaseInfo.Phase == 1 && phaseInfo.NotificationCount == 1)
                {
                    // Show first notification immediately
                    ShowToastNotification(volumeEvent, phaseInfo);
                    return;
                }

                // Phase 1: Start grouping timer for subsequent notifications
                if (phaseInfo.Phase == 1 && phaseInfo.NotificationCount > 1)
                {
                    StartGroupingTimer(deviceKey, phaseInfo, 2000, 2000); // 2s delay, 2s window for better grouping
                    return;
                }

                // Phase 2 and beyond: Always use grouping timer
                if (phaseInfo.Phase >= 2)
                {
                    var delay = Math.Min(5000 + (phaseInfo.Phase - 2) * 2000, 15000); // Max 15s delay
                    var window = Math.Min(10000 + (phaseInfo.Phase - 2) * 5000, 30000); // Max 30s window
                    StartGroupingTimer(deviceKey, phaseInfo, delay, window);
                }

                // Update phase based on notification count
                if (phaseInfo.NotificationCount >= GetPhaseThreshold(phaseInfo.Phase))
                {
                    phaseInfo.Phase++;
                    _loggingService?.Log($"Advanced {deviceKey} to notification phase {phaseInfo.Phase}");
                }
            }
        }

        private int GetPhaseThreshold(int currentPhase)
        {
            return currentPhase switch
            {
                1 => 3, // After 3 notifications, move to phase 2
                2 => 8, // After 8 total notifications, move to phase 3
                3 => 15, // After 15 total notifications, move to phase 4
                _ => int.MaxValue // Stay in final phase
            };
        }

        private void StartGroupingTimer(string deviceKey, NotificationPhaseInfo phaseInfo, int delayMs, int windowMs)
        {
            // Stop existing timer if running
            phaseInfo.GroupingTimer?.Stop();
            phaseInfo.GroupingTimer?.Dispose();

            // Create new timer for this grouping window
            phaseInfo.GroupingTimer = new System.Timers.Timer(delayMs);
            phaseInfo.GroupingTimer.Elapsed += (sender, e) =>
            {
                _dispatcher.BeginInvoke(() =>
                {
                    lock (_notificationLock)
                    {
                        ShowGroupedNotification(deviceKey, phaseInfo);
                        phaseInfo.PendingEvents.Clear();
                    }
                });
            };
            phaseInfo.GroupingTimer.AutoReset = false;
            phaseInfo.GroupingTimer.Start();
        }

        private void ShowToastNotification(VolumeChangeEvent volumeEvent, NotificationPhaseInfo phaseInfo)
        {
            try
            {
                var title = "External Volume Change Detected";
                var message = $"🎤 {volumeEvent.DeviceName}:\nRestored to {volumeEvent.NewVolume:F1}dB";

                var muteDuration = GetMuteDuration(phaseInfo.Phase);
                var muteButtonText = $"Mute Popups {muteDuration}min";
                var pauseButtonText = "Unfreeze 15min";

                var toastBuilder = new ToastContentBuilder()
                    .AddAppLogoOverride(GetAppIconUri())
                    .AddText(title)
                    .AddText(message)
                    .AddButton(new ToastButton()
                        .SetContent(muteButtonText)
                        .AddArgument("action", "mute")
                        .AddArgument("app", "global")
                        .AddArgument("duration", muteDuration.ToString()))
                    .AddButton(new ToastButton()
                        .SetContent(pauseButtonText)
                        .AddArgument("action", "pause")
                        .AddArgument("duration", "15"));

                toastBuilder.Show();
                phaseInfo.LastNotification = DateTime.Now;
                _loggingService?.Log($"Toast notification shown for {volumeEvent.DeviceName}");
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Failed to show toast notification: {ex.Message}");
                ShowFallbackNotification(volumeEvent);
            }
        }

        private void ShowGroupedNotification(string deviceKey, NotificationPhaseInfo phaseInfo)
        {
            try
            {
                var events = phaseInfo.PendingEvents;
                var firstEvent = events.First();
                var lastEvent = events.Last();
                
                var title = events.Count == 1 
                    ? "External Volume Change Detected"
                    : $"{events.Count}x External Volume Changes Detected";
                    
                var message = events.Count == 1 
                    ? $"🎤 {firstEvent.DeviceName}:\nRestored to {firstEvent.NewVolume:F1}dB"
                    : $"🎤 {lastEvent.DeviceName}:\nRestored to {lastEvent.NewVolume:F1}dB";

                var muteDuration = GetMuteDuration(phaseInfo.Phase);
                var muteButtonText = $"Mute Popups {muteDuration}min";
                var pauseButtonText = "Unfreeze 15min";

                var toastBuilder = new ToastContentBuilder()
                    .AddAppLogoOverride(GetAppIconUri())
                    .AddText(title)
                    .AddText(message)
                    .AddButton(new ToastButton()
                        .SetContent(muteButtonText)
                        .AddArgument("action", "mute")
                        .AddArgument("app", "global")
                        .AddArgument("duration", muteDuration.ToString()))
                    .AddButton(new ToastButton()
                        .SetContent(pauseButtonText)
                        .AddArgument("action", "pause")
                        .AddArgument("duration", "15"));

                toastBuilder.Show();
                phaseInfo.LastNotification = DateTime.Now;
                _loggingService?.Log($"Grouped toast notification shown for {deviceKey} ({events.Count} events)");
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Failed to show grouped notification: {ex.Message}");
                ShowFallbackNotification(phaseInfo.PendingEvents.Last());
            }
        }

        private void ShowFallbackNotification(VolumeChangeEvent volumeEvent)
        {
            try
            {
                // Fallback to logging when toast notifications fail
                _loggingService?.Log($"Volume notification: {volumeEvent.DeviceName} restored to {volumeEvent.NewVolume:F1}dB");
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Failed to show fallback notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles toast notification button clicks
        /// </summary>
        private void OnToastNotificationActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                _dispatcher.BeginInvoke(() =>
                {
                    var args = ToastArguments.Parse(e.Argument);
                    var action = args.Get("action");
                    
                    if (action == "mute")
                    {
                        var deviceName = args.Get("app");
                        var durationStr = args.Get("duration");
                        
                        if (!string.IsNullOrEmpty(deviceName) && int.TryParse(durationStr, out int durationMinutes))
                        {
                            // Use global muting instead of device-specific muting to match system tray behavior
                            MutePopupsFor(TimeSpan.FromMinutes(durationMinutes), $"{durationMinutes} minutes");
                            _loggingService?.Log($"Popup notifications muted for {durationMinutes} minutes via notification button");
                            
                            // Update the notification mute banner
                            _mainWindow.UpdateNotificationMuteUI();
                        }
                    }
                    else if (action == "pause")
                    {
                        var durationStr = args.Get("duration");
                        if (int.TryParse(durationStr, out int pauseDuration))
                        {
                            _mainWindow.PauseManager.PauseFor(TimeSpan.FromMinutes(pauseDuration), $"{pauseDuration} minutes");
                            _loggingService?.Log($"Volume freezing paused for {pauseDuration} minutes via notification button");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error handling toast notification activation: {ex.Message}");
            }
        }

        /// <summary>
        /// Mutes notifications for a specific device for the specified duration
        /// </summary>
        public void MuteDeviceNotifications(string deviceName, int durationMinutes)
        {
            lock (_notificationLock)
            {
                _mutedDevices.Add(deviceName);
                _mutedDeviceExpiry[deviceName] = DateTime.Now.AddMinutes(durationMinutes);
                _loggingService?.Log($"Muted notifications for {deviceName} for {durationMinutes} minutes");
            }
        }

        /// <summary>
        /// Mutes all popup notifications for the specified duration
        /// </summary>
        public void MutePopupsFor(TimeSpan duration, string reason)
        {
            _popupsMutedUntil = DateTime.Now.Add(duration);
            _popupsMutedIndefinitely = false;
            _muteReason = reason;
            
            // Stop existing timer
            _muteTimer?.Stop();
            _muteTimer?.Dispose();
            
            // Create automatic expiration timer
            _muteTimer = new System.Timers.Timer(duration.TotalMilliseconds);
            _muteTimer.Elapsed += (s, e) => UnmutePopups();
            _muteTimer.AutoReset = false;
            _muteTimer.Start();
            
            _loggingService?.Log($"Popup notifications muted for {reason}");
            MuteStateChanged?.Invoke(true);
            SaveMuteState();
        }

        /// <summary>
        /// Mutes all popup notifications indefinitely
        /// </summary>
        public void MutePopupsIndefinitely()
        {
            _popupsMutedIndefinitely = true;
            _popupsMutedUntil = DateTime.MinValue;
            _muteReason = "Manual";
            
            // Stop timer for indefinite muting
            _muteTimer?.Stop();
            _muteTimer?.Dispose();
            _muteTimer = null;
            
            _loggingService?.Log("Popup notifications muted until manually enabled");
            MuteStateChanged?.Invoke(true);
            SaveMuteState();
        }

        /// <summary>
        /// Unmutes all popup notifications
        /// </summary>
        public void UnmutePopups()
        {
            _popupsMutedIndefinitely = false;
            _popupsMutedUntil = DateTime.MinValue;
            _muteReason = "";
            
            // Stop and dispose timer
            _muteTimer?.Stop();
            _muteTimer?.Dispose();
            _muteTimer = null;
            
            _loggingService?.Log("Popup notifications enabled");
            MuteStateChanged?.Invoke(false);
            ClearMuteState();
        }

        /// <summary>
        /// Checks if popup notifications are currently muted
        /// </summary>
        public bool ArePopupsMuted()
        {
            return _popupsMutedIndefinitely || DateTime.Now < _popupsMutedUntil;
        }

        /// <summary>
        /// Checks if popup notifications are muted specifically from tray actions
        /// </summary>
        public bool ArePopupsMutedFromTray()
        {
            // Only return true if muted from tray actions, not from UI settings
            return (_popupsMutedIndefinitely || DateTime.Now < _popupsMutedUntil) && ((App)System.Windows.Application.Current).GetNotificationsEnabledSetting();
        }

        /// <summary>
        /// Cleans up expired app-specific notification mutes
        /// </summary>
        private void CleanupExpiredMutedApps()
        {
            var now = DateTime.Now;
            var expiredDevices = _mutedDeviceExpiry.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();
            
            foreach (var device in expiredDevices)
            {
                _mutedDevices.Remove(device);
                _mutedDeviceExpiry.Remove(device);
                _loggingService?.Log($"Unmuted notifications for {device}");
            }
        }

        /// <summary>
        /// Gets the app icon URI for toast notifications
        /// </summary>
        private System.Uri GetAppIconUri()
        {
            try
            {
                // Try to use the embedded icon and save it to a temp file for toast notifications
                var iconPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ToxMoxMicFreezer_icon.ico");
                
                if (!System.IO.File.Exists(iconPath))
                {
                    // Extract icon from resources and save to temp file
                    var iconStream = System.Windows.Application.GetResourceStream(new System.Uri("pack://application:,,,/Assets/AppIcon.ico"));
                    if (iconStream != null)
                    {
                        using var fileStream = System.IO.File.Create(iconPath);
                        iconStream.Stream.CopyTo(fileStream);
                    }
                }
                
                return new System.Uri($"file:///{iconPath}");
            }
            catch
            {
                // Fallback to a default Windows icon path
                return new System.Uri("ms-appx:///Assets/AppIcon.ico");
            }
        }

        /// <summary>
        /// Gets appropriate mute duration based on notification phase
        /// </summary>
        private int GetMuteDuration(int phase)
        {
            return phase switch
            {
                1 => 5,   // 5 minutes for first phase
                2 => 15,  // 15 minutes for second phase
                3 => 30,  // 30 minutes for third phase
                _ => 60   // 60 minutes for phase 4+
            };
        }

        /// <summary>
        /// Truncates app name for display in notification buttons
        /// </summary>
        private string TruncateAppName(string appName)
        {
            const int maxLength = 12;
            return appName.Length > maxLength ? appName.Substring(0, maxLength) + "..." : appName;
        }

        /// <summary>
        /// Gets the full device name including label (e.g., "Music (BEACN Studio)")
        /// </summary>
        private string GetFullDeviceName(AudioDeviceViewModel device)
        {
            if (!string.IsNullOrEmpty(device.Label))
            {
                return $"{device.Name} ({device.Label})";
            }
            return device.Name;
        }

        /// <summary>
        /// Checks if the mute has expired and handles expiration
        /// </summary>
        /// <returns>True if mute was expired and handled</returns>
        public bool CheckAndHandleExpiration()
        {
            if (IsMuted && MuteEndTime.HasValue && DateTime.Now >= MuteEndTime.Value)
            {
                UnmutePopups();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Saves mute state to registry for persistence across application restarts
        /// </summary>
        private void SaveMuteState()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                key.SetValue("NotificationMuteState", IsMuted ? 1 : 0);
                if (MuteEndTime.HasValue)
                {
                    key.SetValue("NotificationMuteEndTime", MuteEndTime.Value.ToBinary());
                }
                else
                {
                    key.DeleteValue("NotificationMuteEndTime", false);
                }
                key.SetValue("NotificationMuteReason", MuteReason);
            }
            catch
            {
                // Ignore registry errors
            }
        }

        /// <summary>
        /// Clears mute state from registry
        /// </summary>
        private void ClearMuteState()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                key.DeleteValue("NotificationMuteState", false);
                key.DeleteValue("NotificationMuteEndTime", false);
                key.DeleteValue("NotificationMuteReason", false);
            }
            catch
            {
                // Ignore registry errors
            }
        }

        /// <summary>
        /// Loads mute state from registry on application startup
        /// </summary>
        public void LoadMuteState()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                if (key != null)
                {
                    var muteState = key.GetValue("NotificationMuteState");
                    if (muteState != null && (int)muteState == 1)
                    {
                        var muteEndTimeBinary = key.GetValue("NotificationMuteEndTime");
                        var muteReason = key.GetValue("NotificationMuteReason")?.ToString() ?? "";
                        
                        if (muteEndTimeBinary != null)
                        {
                            var endTime = DateTime.FromBinary((long)muteEndTimeBinary);
                            if (DateTime.Now < endTime)
                            {
                                var remaining = endTime - DateTime.Now;
                                MutePopupsFor(remaining, muteReason);
                            }
                            else
                            {
                                ClearMuteState(); // Expired mute
                            }
                        }
                        else
                        {
                            MutePopupsIndefinitely(); // Indefinite mute
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }
        }

        /// <summary>
        /// Dispose of notification resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Cleanup notification timers
                lock (_notificationLock)
                {
                    foreach (var phaseInfo in _deviceNotificationPhases.Values)
                    {
                        phaseInfo.GroupingTimer?.Stop();
                        phaseInfo.GroupingTimer?.Dispose();
                        phaseInfo.GroupingTimer = null;
                    }
                    _deviceNotificationPhases.Clear();
                }

                // Cleanup mute timer
                _muteTimer?.Stop();
                _muteTimer?.Dispose();
                _muteTimer = null;

                // Unregister toast notification handler
                try
                {
                    ToastNotificationManagerCompat.OnActivated -= OnToastNotificationActivated;
                }
                catch { /* Ignore errors during shutdown */ }

                _disposed = true;
            }
        }
    }
}