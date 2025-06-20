// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;

namespace ToxMoxMicFreezer.App.FontAwesome
{
    /// <summary>
    /// Provides FontAwesome icon creation and font management functionality
    /// </summary>
    public static class FontAwesomeIconProvider
    {
        private static FontFamily? _fontAwesomeFontFamily = null;
        private static PrivateFontCollection? _privateFontCollection = null;
        private static bool _fontInstalled = false;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>
        /// Creates a FontAwesome font with the specified size
        /// </summary>
        public static Font CreateFontAwesomeFont(float size)
        {
            try
            {
                if (_fontAwesomeFontFamily == null)
                {
                    // Load the font from embedded resource
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    System.Diagnostics.Debug.WriteLine($"Assembly: {assembly.FullName}");
                    
                    // Try to find the correct FontAwesome resource name
                    var resourceNames = assembly.GetManifestResourceNames();
                    var fontAwesomeResource = resourceNames.FirstOrDefault(r => r.Contains("fontawesome-solid")) 
                                           ?? resourceNames.FirstOrDefault(r => r.Contains("fontawesome") && r.Contains("solid"))
                                           ?? "ToxMoxMicFreezer.App.Fonts.fontawesome-solid.otf"; // fallback to original
                    
                    System.Diagnostics.Debug.WriteLine($"Using FontAwesome resource: {fontAwesomeResource}");
                    
                    using (var stream = assembly.GetManifestResourceStream(fontAwesomeResource))
                    {
                        if (stream != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"FontAwesome stream found, length: {stream.Length}");
                            
                            var fontData = new byte[stream.Length];
                            stream.ReadExactly(fontData, 0, fontData.Length);
                            
                            var handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
                            var pointer = handle.AddrOfPinnedObject();
                            
                            _privateFontCollection = new PrivateFontCollection();
                            _privateFontCollection.AddMemoryFont(pointer, fontData.Length);
                            
                            if (_privateFontCollection.Families.Length > 0)
                            {
                                _fontAwesomeFontFamily = _privateFontCollection.Families[0];
                                System.Diagnostics.Debug.WriteLine($"FontAwesome family loaded: {_fontAwesomeFontFamily.Name}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("No font families found in PrivateFontCollection");
                            }
                            
                            handle.Free();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("FontAwesome stream is NULL - resource not found!");
                            
                            // List all embedded resources to debug
                            var allResourceNames = assembly.GetManifestResourceNames();
                            System.Diagnostics.Debug.WriteLine("Available embedded resources:");
                            foreach (var name in allResourceNames)
                            {
                                System.Diagnostics.Debug.WriteLine($"  - {name}");
                            }
                        }
                    }
                }
                
                if (_fontAwesomeFontFamily != null)
                {
                    var font = new Font(_fontAwesomeFontFamily, size);
                    System.Diagnostics.Debug.WriteLine($"Created FontAwesome font: {font.FontFamily.Name}, size: {size}");
                    return font;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("FontAwesome family is null, falling back to Segoe UI");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FontAwesome font creation exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            System.Diagnostics.Debug.WriteLine($"Returning fallback font: Segoe UI, size: {size}");
            return new Font("Segoe UI", size);
        }

        /// <summary>
        /// Creates a FontAwesome icon as a system icon
        /// </summary>
        public static Icon CreateFontAwesomeIcon(string unicodeChar, float size = 24f, Color? color = null)
        {
            color ??= Color.FromArgb(241, 241, 241);
            
            try
            {
                using var bitmap = new Bitmap((int)size, (int)size);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    
                    using (var font = CreateFontAwesomeFont(size * 0.75f))
                    using (var brush = new SolidBrush(color.Value))
                    {
                        var textSize = g.MeasureString(unicodeChar, font);
                        var position = new PointF((bitmap.Width - textSize.Width) / 2, (bitmap.Height - textSize.Height) / 2);
                        g.DrawString(unicodeChar, font, brush, position);
                    }
                }
                
                // Convert bitmap to icon
                var hIcon = bitmap.GetHicon();
                var icon = Icon.FromHandle(hIcon);
                
                // Create a copy to avoid handle disposal issues
                var iconCopy = new Icon(icon, icon.Size);
                
                // Clean up the handle
                DestroyIcon(hIcon);
                
                return iconCopy;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create FontAwesome icon: {ex.Message}");
                // Create a simple fallback icon
                return CreateSimpleFallbackIcon(unicodeChar, size, color.Value);
            }
        }

        /// <summary>
        /// Enhanced FontAwesome menu icons with baseline alignment and standardized sizing
        /// </summary>
        public static Image CreateFontAwesomeMenuIcon(string unicodeChar, Color? color = null)
        {
            const int size = 16; // Standardized size matching IconSize constant
            const float fontSize = 10.5f; // Match text font size for consistent baseline
            var iconColor = color ?? Color.FromArgb(241, 241, 241); // Default white
            
            // Ensure font is loaded
            EnsureFontAwesomeInstalled();
            
            try
            {
                // Method 1: Try with PrivateFontCollection font
                if (_fontAwesomeFontFamily != null)
                {
                    return CreateIconWithFont(_fontAwesomeFontFamily, unicodeChar, size, fontSize, iconColor);
                }
                
                // Method 2: Try with system-installed FontAwesome (if available)
                try
                {
                    using (var testFont = new Font("Font Awesome 6 Free Solid", fontSize))
                    {
                        System.Diagnostics.Debug.WriteLine("Found system-installed FontAwesome");
                        return CreateIconWithFont(testFont.FontFamily, unicodeChar, size, fontSize, iconColor);
                    }
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("No system-installed FontAwesome found");
                }
                
                // Method 3: Try alternative font names
                string[] fontNames = { 
                    "FontAwesome", 
                    "Font Awesome 6 Free", 
                    "Font Awesome 6 Free Solid", 
                    "Font Awesome 5 Free Solid",
                    "FontAwesome Solid"
                };
                
                foreach (var fontName in fontNames)
                {
                    try
                    {
                        using (var testFont = new Font(fontName, fontSize))
                        {
                            System.Diagnostics.Debug.WriteLine($"Found FontAwesome with name: {fontName}");
                            return CreateIconWithFont(testFont.FontFamily, unicodeChar, size, fontSize, iconColor);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                throw new InvalidOperationException($"No FontAwesome font found for {unicodeChar}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FontAwesome icon creation failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensures FontAwesome font is installed and available
        /// </summary>
        private static void EnsureFontAwesomeInstalled()
        {
            if (_fontInstalled) return;
            
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                // Find the correct FontAwesome resource name
                var resourceNames = assembly.GetManifestResourceNames();
                var fontAwesomeResource = resourceNames.FirstOrDefault(r => r.Contains("fontawesome-solid")) 
                                       ?? resourceNames.FirstOrDefault(r => r.Contains("fontawesome") && r.Contains("solid"))
                                       ?? "ToxMoxMicFreezer.App.Fonts.fontawesome-solid.otf";
                
                using (var stream = assembly.GetManifestResourceStream(fontAwesomeResource))
                {
                    if (stream != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found FontAwesome resource, length: {stream.Length}");
                        
                        // Method 1: Try PrivateFontCollection approach
                        var fontData = new byte[stream.Length];
                        stream.ReadExactly(fontData, 0, fontData.Length);
                        
                        var handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
                        var pointer = handle.AddrOfPinnedObject();
                        
                        if (_privateFontCollection == null)
                            _privateFontCollection = new PrivateFontCollection();
                        
                        _privateFontCollection.AddMemoryFont(pointer, fontData.Length);
                        
                        if (_privateFontCollection.Families.Length > 0)
                        {
                            _fontAwesomeFontFamily = _privateFontCollection.Families[0];
                            System.Diagnostics.Debug.WriteLine($"Loaded FontAwesome family: {_fontAwesomeFontFamily.Name}");
                            _fontInstalled = true;
                        }
                        
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Font installation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates an icon with the specified font family
        /// </summary>
        private static Image CreateIconWithFont(FontFamily fontFamily, string unicodeChar, int size, float fontSize, Color iconColor)
        {
            // Create standard 16x16 icon that Windows Forms expects
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                
                // Use consistent font size matching text for proper baseline alignment
                using (var font = new Font(fontFamily, fontSize, FontStyle.Regular))
                using (var brush = new SolidBrush(iconColor))
                {
                    var stringFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    
                    var rect = new RectangleF(0, 0, 16, 16);
                    g.DrawString(unicodeChar, font, brush, rect, stringFormat);
                    
                    System.Diagnostics.Debug.WriteLine($"Successfully rendered {unicodeChar} with font {fontFamily.Name}");
                }
            }
            return bitmap;
        }

        /// <summary>
        /// Creates a simple fallback icon when FontAwesome is not available
        /// </summary>
        private static Icon CreateSimpleFallbackIcon(string unicodeChar, float size, Color color)
        {
            try
            {
                using var bitmap = CreateSimpleFallbackBitmap(unicodeChar, (int)size, color);
                var hIcon = bitmap.GetHicon();
                var icon = Icon.FromHandle(hIcon);
                var iconCopy = new Icon(icon, icon.Size);
                DestroyIcon(hIcon);
                return iconCopy;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// Creates a simple fallback icon as an image
        /// </summary>
        public static Image? CreateSimpleFallbackIconImage(string unicodeChar)
        {
            try
            {
                return CreateSimpleFallbackBitmap(unicodeChar, 24, Color.FromArgb(241, 241, 241));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a simple geometric fallback bitmap
        /// </summary>
        private static Bitmap CreateSimpleFallbackBitmap(string unicodeChar, int iconSize, Color color)
        {
            var bitmap = new Bitmap(iconSize, iconSize);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Draw a simple geometric shape based on the icon type
                using (var brush = new SolidBrush(color))
                {
                    // Simple fallback shapes
                    switch (unicodeChar)
                    {
                        case "\uf2d2": // Show
                            g.FillEllipse(brush, iconSize/3, iconSize/3, iconSize/3, iconSize/3);
                            break;
                        case "\uf04c": // Pause
                            var barWidth = iconSize/8;
                            var barHeight = iconSize/2;
                            var spacing = iconSize/6;
                            var startX = (iconSize - barWidth*2 - spacing) / 2;
                            var startY = (iconSize - barHeight) / 2;
                            g.FillRectangle(brush, startX, startY, barWidth, barHeight);
                            g.FillRectangle(brush, startX + barWidth + spacing, startY, barWidth, barHeight);
                            break;
                        case "\uf04b": // Play
                            var playSize = iconSize/2;
                            var playStartX = (iconSize - playSize) / 2;
                            var playStartY = (iconSize - playSize) / 2;
                            var points = new Point[] { 
                                new(playStartX, playStartY), 
                                new(playStartX + playSize, playStartY + playSize/2), 
                                new(playStartX, playStartY + playSize) 
                            };
                            g.FillPolygon(brush, points);
                            break;
                        case "\uf013": // Settings
                            g.FillEllipse(brush, iconSize/3, iconSize/3, iconSize/3, iconSize/3);
                            break;
                        default:
                            g.FillRectangle(brush, iconSize/3, iconSize/3, iconSize/3, iconSize/3);
                            break;
                    }
                }
            }
            return bitmap;
        }

        /// <summary>
        /// Cleanup resources when the application shuts down
        /// </summary>
        public static void Cleanup()
        {
            _privateFontCollection?.Dispose();
            _privateFontCollection = null;
            _fontAwesomeFontFamily = null;
            _fontInstalled = false;
        }
    }
}