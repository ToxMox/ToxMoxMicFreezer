// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using Microsoft.Win32;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Manages application pause state with timer-based and indefinite pause capabilities
    /// Handles pause state persistence to registry for resume across application restarts
    /// </summary>
    public class PauseManager : INotifyPropertyChanged
    {
        private bool _isPaused = false;
        private DateTime? _pauseEndTime = null;
        private string _pauseReason = "";
        private System.Timers.Timer? _pauseTimer;
        
        public bool IsPaused 
        { 
            get => _isPaused;
            private set
            {
                _isPaused = value;
                OnPropertyChanged();
            }
        }
        
        public DateTime? PauseEndTime 
        { 
            get => _pauseEndTime;
            private set
            {
                _pauseEndTime = value;
                OnPropertyChanged();
            }
        }
        
        public string PauseReason 
        { 
            get => _pauseReason;
            private set
            {
                _pauseReason = value;
                OnPropertyChanged();
            }
        }
        
        public TimeSpan? TimeRemaining => PauseEndTime?.Subtract(DateTime.Now);
        
        public event Action<bool>? PauseStateChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Pauses the application for a specific duration
        /// </summary>
        /// <param name="duration">How long to pause for</param>
        /// <param name="reason">Reason for the pause</param>
        public void PauseFor(TimeSpan duration, string reason)
        {
            IsPaused = true;
            PauseEndTime = DateTime.Now.Add(duration);
            PauseReason = reason;
            
            _pauseTimer?.Stop();
            _pauseTimer?.Dispose();
            
            _pauseTimer = new System.Timers.Timer(duration.TotalMilliseconds);
            _pauseTimer.Elapsed += (s, e) => Resume();
            _pauseTimer.AutoReset = false;
            _pauseTimer.Start();
            
            PauseStateChanged?.Invoke(true);
            SavePauseState();
        }
        
        /// <summary>
        /// Pauses the application indefinitely until manually resumed
        /// </summary>
        public void PauseIndefinitely()
        {
            IsPaused = true;
            PauseEndTime = null;
            PauseReason = "Manual";
            
            _pauseTimer?.Stop();
            _pauseTimer?.Dispose();
            _pauseTimer = null;
            
            PauseStateChanged?.Invoke(true);
            SavePauseState();
        }
        
        /// <summary>
        /// Resumes the application from pause state
        /// </summary>
        public void Resume()
        {
            IsPaused = false;
            PauseEndTime = null;
            PauseReason = "";
            
            _pauseTimer?.Stop();
            _pauseTimer?.Dispose();
            _pauseTimer = null;
            
            PauseStateChanged?.Invoke(false);
            ClearPauseState();
        }
        
        /// <summary>
        /// Checks if the pause has expired and handles expiration
        /// </summary>
        /// <returns>True if pause was expired and handled</returns>
        public bool CheckAndHandleExpiration()
        {
            if (IsPaused && PauseEndTime.HasValue && DateTime.Now >= PauseEndTime.Value)
            {
                Resume();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Saves pause state to registry for persistence across application restarts
        /// </summary>
        private void SavePauseState()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                key.SetValue("PauseState", IsPaused ? 1 : 0);
                if (PauseEndTime.HasValue)
                {
                    key.SetValue("PauseEndTime", PauseEndTime.Value.ToBinary());
                }
                else
                {
                    key.DeleteValue("PauseEndTime", false);
                }
                key.SetValue("PauseReason", PauseReason);
            }
            catch
            {
                // Ignore registry errors
            }
        }
        
        /// <summary>
        /// Clears pause state from registry
        /// </summary>
        private void ClearPauseState()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                key.DeleteValue("PauseState", false);
                key.DeleteValue("PauseEndTime", false);
                key.DeleteValue("PauseReason", false);
            }
            catch
            {
                // Ignore registry errors
            }
        }
        
        /// <summary>
        /// Loads pause state from registry on application startup
        /// </summary>
        public void LoadPauseState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ToxMoxMicFreezer");
                if (key != null)
                {
                    var pauseState = key.GetValue("PauseState");
                    if (pauseState != null && (int)pauseState == 1)
                    {
                        var pauseEndTimeBinary = key.GetValue("PauseEndTime");
                        var pauseReason = key.GetValue("PauseReason")?.ToString() ?? "";
                        
                        if (pauseEndTimeBinary != null)
                        {
                            var endTime = DateTime.FromBinary((long)pauseEndTimeBinary);
                            if (DateTime.Now < endTime)
                            {
                                var remaining = endTime - DateTime.Now;
                                PauseFor(remaining, pauseReason);
                            }
                            else
                            {
                                ClearPauseState(); // Expired pause
                            }
                        }
                        else
                        {
                            PauseIndefinitely(); // Indefinite pause
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }
        }
    }
}