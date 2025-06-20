// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ToxMoxMicFreezer.App.UserControls
{
    /// <summary>
    /// UserControl for the help panel
    /// Displays help and usage information in a popup
    /// </summary>
    public partial class HelpPanelUserControl : System.Windows.Controls.UserControl
    {
        public HelpPanelUserControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Gets the application version string
        /// </summary>
        public string AppVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    return $"v{version.Major}.{version.Minor}.{version.Build}";
                }
                return "v1.0.0";
            }
        }

        /// <summary>
        /// Handles hyperlink navigation requests by opening the URL in the default browser
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch
            {
                // Silently fail if unable to open the link
            }
        }
    }
}