// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows.Controls;

namespace ToxMoxMicFreezer.App.UserControls
{
    /// <summary>
    /// Semi-transparent overlay shown during device reset operations
    /// Provides clear visual feedback that device changes are being processed
    /// </summary>
    public partial class DeviceResetOverlayUserControl : System.Windows.Controls.UserControl
    {
        public DeviceResetOverlayUserControl()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Updates the overlay message to show current reset stage
        /// </summary>
        public void SetMessage(string mainMessage, string? subMessage = null)
        {
            MainMessage.Text = mainMessage;
            
            if (!string.IsNullOrEmpty(subMessage))
            {
                SubMessage.Text = subMessage;
            }
        }
    }
}