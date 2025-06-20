// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ToxMoxMicFreezer.App.SystemTray
{
    /// <summary>
    /// Simple dark theme renderer for system tray menus
    /// </summary>
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }
        
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            // Make submenu arrows white
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }
        
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Handle settings items with bold text for enabled items
            if (e.Item.Tag?.ToString() == "enabled")
            {
                // Make text bold and WHITE for enabled items
                var boldFont = new Font(e.Item.Font, FontStyle.Bold);
                e.TextFont = boldFont;
                e.TextColor = Color.White;
                
                base.OnRenderItemText(e);
                boldFont.Dispose();
            }
            else
            {
                // Normal text rendering in white
                e.TextColor = Color.White;
                base.OnRenderItemText(e);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            // Custom hover rendering - only draw purple border, no background
            if (e.Item.Selected)
            {
                // Draw only a purple border, no background fill
                using (var pen = new Pen(Color.FromArgb(132, 118, 162), 1)) // Purple border
                {
                    var borderRect = new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);
                    e.Graphics.DrawRectangle(pen, borderRect);
                }
            }
            // Don't call base method to avoid default background rendering
        }

        /// <summary>
        /// Gets the green checkmark icon as an image from embedded resources
        /// </summary>
        public static Image? GetGreenCheckIcon()
        {
            try
            {
                // For now, create a simple green checkmark using drawing
                // This avoids the embedded resource issue
                var size = 16;
                var bitmap = new Bitmap(size, size);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Draw a green checkmark
                using var pen = new Pen(Color.LimeGreen, 2.0f);
                
                // Draw checkmark lines
                graphics.DrawLine(pen, 3, 8, 6, 11);  // First part of checkmark
                graphics.DrawLine(pen, 6, 11, 12, 5); // Second part of checkmark
                
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetGreenCheckIcon: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Dark color table for system tray menus
    /// </summary>
    public class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color DarkBackground = Color.FromArgb(45, 45, 45);
        private static readonly Color PurpleBorder = Color.FromArgb(132, 118, 162); // Purple border #8476A2
        
        public override Color ToolStripDropDownBackground => DarkBackground;
        public override Color MenuBorder => PurpleBorder;
        
        // Override ALL possible selection/hover colors to be transparent
        public override Color MenuItemSelected => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Color.Transparent;
        public override Color MenuItemSelectedGradientEnd => Color.Transparent;
        public override Color MenuItemPressedGradientBegin => Color.Transparent;
        public override Color MenuItemPressedGradientEnd => Color.Transparent;
        public override Color MenuItemPressedGradientMiddle => Color.Transparent;
        public override Color ButtonSelectedHighlight => Color.Transparent;
        public override Color ButtonSelectedHighlightBorder => Color.Transparent;
        public override Color ButtonPressedHighlight => Color.Transparent;
        public override Color ButtonPressedHighlightBorder => Color.Transparent;
        public override Color ButtonSelectedGradientBegin => Color.Transparent;
        public override Color ButtonSelectedGradientEnd => Color.Transparent;
        public override Color ButtonSelectedGradientMiddle => Color.Transparent;
        public override Color ButtonPressedGradientBegin => Color.Transparent;
        public override Color ButtonPressedGradientEnd => Color.Transparent;
        public override Color ButtonPressedGradientMiddle => Color.Transparent;
        public override Color CheckBackground => Color.Transparent;
        public override Color CheckSelectedBackground => Color.Transparent;
        public override Color CheckPressedBackground => Color.Transparent;
        
        // Background colors
        public override Color MenuStripGradientBegin => DarkBackground;
        public override Color MenuStripGradientEnd => DarkBackground;
        public override Color ToolStripBorder => PurpleBorder;
        public override Color ToolStripGradientBegin => DarkBackground;
        public override Color ToolStripGradientEnd => DarkBackground;
        public override Color ToolStripGradientMiddle => DarkBackground;
        public override Color ImageMarginGradientBegin => DarkBackground;
        public override Color ImageMarginGradientEnd => DarkBackground;
        public override Color ImageMarginGradientMiddle => DarkBackground;
        
        // Border - set to transparent since we're handling it in OnRenderMenuItemBackground
        public override Color MenuItemBorder => Color.Transparent;
        
        // Separators
        public override Color SeparatorDark => Color.FromArgb(60, 60, 60);
        public override Color SeparatorLight => Color.FromArgb(40, 40, 40);
    }
}