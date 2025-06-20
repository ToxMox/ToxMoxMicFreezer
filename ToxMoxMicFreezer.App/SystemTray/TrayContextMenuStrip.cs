// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ToxMoxMicFreezer.App.SystemTray
{
    /// <summary>
    /// Custom ContextMenuStrip with taskbar-aware positioning
    /// </summary>
    public class TrayContextMenuStrip : ContextMenuStrip
    {
        // Win32 API declarations for taskbar detection
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Shows the context menu with taskbar-aware positioning
        /// </summary>
        public new void Show(Point position)
        {
            try
            {
                Point optimalPosition = CalculateOptimalPosition(position);
                base.Show(optimalPosition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to calculate optimal position: {ex.Message}");
                // Fallback to default positioning
                base.Show(position);
            }
        }

        /// <summary>
        /// Calculates optimal menu position based on system tray location
        /// </summary>
        private Point CalculateOptimalPosition(Point clickPosition)
        {
            // Get notification area (system tray) bounds
            var trayBounds = GetNotificationAreaBounds();
            var taskbarBounds = GetTaskbarBounds();
            
            if (taskbarBounds == Rectangle.Empty)
            {
                // Fallback if taskbar detection fails
                return clickPosition;
            }

            // Get screen bounds
            Rectangle screenBounds = Screen.FromPoint(clickPosition).Bounds;
            
            // Determine taskbar orientation
            TaskbarPosition taskbarPos = GetTaskbarPosition(taskbarBounds, screenBounds);

            // Calculate menu position relative to system tray area, not full taskbar
            Point menuPosition = clickPosition;

            switch (taskbarPos)
            {
                case TaskbarPosition.Bottom:
                    // Taskbar at bottom - show menu above system tray
                    menuPosition.X = clickPosition.X - (this.Width / 2);
                    menuPosition.Y = taskbarBounds.Top - this.Height - 2;
                    break;

                case TaskbarPosition.Top:
                    // Taskbar at top - show menu below system tray
                    menuPosition.X = clickPosition.X - (this.Width / 2);
                    menuPosition.Y = taskbarBounds.Bottom + 2;
                    break;

                case TaskbarPosition.Left:
                    // Taskbar on left - show menu to the right of click position
                    menuPosition.X = clickPosition.X + 10;
                    menuPosition.Y = clickPosition.Y - (this.Height / 2);
                    break;

                case TaskbarPosition.Right:
                    // Taskbar on right - show menu to the left of click position
                    menuPosition.X = clickPosition.X - this.Width - 10;
                    menuPosition.Y = clickPosition.Y - (this.Height / 2);
                    break;
            }

            // Ensure menu stays within screen bounds
            if (menuPosition.X < screenBounds.Left)
                menuPosition.X = screenBounds.Left;
            if (menuPosition.Y < screenBounds.Top)
                menuPosition.Y = screenBounds.Top;
            if (menuPosition.X + this.Width > screenBounds.Right)
                menuPosition.X = screenBounds.Right - this.Width;
            if (menuPosition.Y + this.Height > screenBounds.Bottom)
                menuPosition.Y = screenBounds.Bottom - this.Height;

            return menuPosition;
        }

        /// <summary>
        /// Gets the notification area (system tray) bounds
        /// </summary>
        private Rectangle GetNotificationAreaBounds()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null!);
                if (taskbarHandle != IntPtr.Zero)
                {
                    // Try to find the notification area within the taskbar
                    IntPtr trayNotifyWnd = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null!);
                    if (trayNotifyWnd != IntPtr.Zero)
                    {
                        if (GetWindowRect(trayNotifyWnd, out RECT rect))
                        {
                            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get notification area bounds: {ex.Message}");
            }

            return Rectangle.Empty;
        }

        /// <summary>
        /// Gets the taskbar window bounds
        /// </summary>
        private Rectangle GetTaskbarBounds()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null!);
                if (taskbarHandle != IntPtr.Zero)
                {
                    if (GetWindowRect(taskbarHandle, out RECT rect))
                    {
                        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get taskbar bounds: {ex.Message}");
            }

            return Rectangle.Empty;
        }

        /// <summary>
        /// Determines taskbar position based on its bounds relative to screen
        /// </summary>
        private TaskbarPosition GetTaskbarPosition(Rectangle taskbarBounds, Rectangle screenBounds)
        {
            // Check if taskbar spans full width (horizontal)
            if (taskbarBounds.Width >= screenBounds.Width * 0.8)
            {
                // Horizontal taskbar
                if (taskbarBounds.Top <= screenBounds.Top + 10)
                    return TaskbarPosition.Top;
                else
                    return TaskbarPosition.Bottom;
            }
            else
            {
                // Vertical taskbar
                if (taskbarBounds.Left <= screenBounds.Left + 10)
                    return TaskbarPosition.Left;
                else
                    return TaskbarPosition.Right;
            }
        }

        private enum TaskbarPosition
        {
            Bottom,
            Top,
            Left,
            Right
        }
    }
}