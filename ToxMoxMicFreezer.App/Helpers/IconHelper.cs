// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ToxMoxMicFreezer.App.Helpers
{
    public static class IconHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        
        public static Icon CreateSimpleIcon(Color backgroundColor, Color foregroundColor, int size)
        {
            try
            {
                using (var bitmap = new Bitmap(size, size))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    // Fill background
                    using (var backgroundBrush = new SolidBrush(backgroundColor))
                    {
                        graphics.FillRectangle(backgroundBrush, 0, 0, size, size);
                    }

                    // Create a simple microphone-like icon
                    int centerX = size / 2;
                    int centerY = size / 2;
                    int micWidth = size / 4;
                    int micHeight = size / 3;

                    // Draw microphone capsule
                    using (var foregroundBrush = new SolidBrush(foregroundColor))
                    {
                        var micRect = new Rectangle(centerX - micWidth / 2, centerY - micHeight / 2 - size / 8, micWidth, micHeight);
                        graphics.FillEllipse(foregroundBrush, micRect);

                        // Draw microphone stand
                        var standRect = new Rectangle(centerX - 1, centerY + micHeight / 4, 3, size / 4);
                        graphics.FillRectangle(foregroundBrush, standRect);

                        // Draw base
                        var baseRect = new Rectangle(centerX - size / 8, centerY + micHeight / 2 + size / 8, size / 4, 3);
                        graphics.FillRectangle(foregroundBrush, baseRect);
                    }

                    // Convert to icon using proper handle method
                    var hIcon = bitmap.GetHicon();
                    var icon = Icon.FromHandle(hIcon);
                    
                    // Create a copy to avoid handle disposal issues
                    var iconCopy = new Icon(icon, icon.Size);
                    
                    // Clean up the handle
                    DestroyIcon(hIcon);
                    
                    return iconCopy;
                }
            }
            catch
            {
                // Fallback to a simple colored square
                return CreateFallbackIcon(backgroundColor, size);
            }
        }

        private static Icon CreateFallbackIcon(Color color, int size)
        {
            try
            {
                using (var bitmap = new Bitmap(size, size))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    using (var brush = new SolidBrush(color))
                    {
                        graphics.FillRectangle(brush, 0, 0, size, size);
                    }

                    // Convert to icon using proper handle method
                    var hIcon = bitmap.GetHicon();
                    var icon = Icon.FromHandle(hIcon);
                    
                    // Create a copy to avoid handle disposal issues
                    var iconCopy = new Icon(icon, icon.Size);
                    
                    // Clean up the handle
                    DestroyIcon(hIcon);
                    
                    return iconCopy;
                }
            }
            catch
            {
                // Ultimate fallback - return system application icon
                return SystemIcons.Application;
            }
        }

        public static void SaveIconToFile(Icon icon, string filePath)
        {
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    icon.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save icon to {filePath}: {ex.Message}", ex);
            }
        }
    }
}