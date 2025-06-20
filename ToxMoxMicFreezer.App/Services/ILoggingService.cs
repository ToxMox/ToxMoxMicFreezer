// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using NAudio.CoreAudioApi;

namespace ToxMoxMicFreezer.App.Services
{
    public class LogEventArgs : EventArgs
    {
        public required string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
    }

    public enum LogLevel
    {
        Info,    // Clean user-friendly messages (default)
        Debug,   // Verbose technical details for troubleshooting
        Warning, // Important issues that don't break functionality
        Error    // Critical issues that impact functionality
    }

    public interface ILoggingService : IDisposable
    {
        void Initialize(System.Windows.Controls.TextBox logTextBox, System.Windows.Controls.ScrollViewer logScrollViewer, System.Windows.Threading.Dispatcher dispatcher);
        void SetLogLevel(LogLevel level);
        void Log(string message, LogLevel level = LogLevel.Info);
        void LogVolumeChange(MMDevice device, float previousVolume, float currentVolume, string source);
        void SetAutoScroll(bool enabled);
        void Clear();
        void HandleScrollChanged(System.Windows.Controls.ScrollViewer scrollViewer, System.Windows.Controls.ScrollChangedEventArgs e);
        bool IsAutoScrollEnabled { get; }
        event EventHandler<LogEventArgs> LogAdded;
        event EventHandler AutoScrollChanged;
    }
}