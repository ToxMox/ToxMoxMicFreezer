using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Wpf.Ui;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Text.Json;
using System.Text;
using System.Windows.Media.Animation;

namespace ToxMoxMicFreezer.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IMMNotificationClient
{
    public ObservableCollection<AudioDeviceViewModel> Devices { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> DevicesLeft { get; set; } = new();
    public ObservableCollection<AudioDeviceViewModel> DevicesRight { get; set; } = new();
    private MMDeviceEnumerator _enumerator = null!;
    private CancellationTokenSource? _cts;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _isExit;
    private const string AppName = "ToxMoxMicFreezer";
    private const string RegKeyPath = @"SOFTWARE\ToxMoxMicFreezer";
    private const string RegValuePrefix = "Window";
    private const string RegValueName = "SelectedDevices";
    private Dictionary<string, AudioDeviceViewModel> _deviceCache = new();
    private bool _devicesLoaded = false;
    private readonly object _deviceLock = new object();
    private DateTime _lastDeviceRefresh = DateTime.MinValue;
    private bool _deviceRefreshPending = false;
    private HashSet<string> _persistentSelectionState = new HashSet<string>();

    public MainWindow()
    {
        // Set DataContext before loading UI
        DataContext = this;
        
        // Initialize the UI components from XAML
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing UI: {ex.Message}", "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        // Initialize enumerator
        _enumerator = new MMDeviceEnumerator();
        
        // Set up minimal UI first so window can show faster
        SetupTitleBarIcon();
        
        // Show the window immediately before loading audio devices
        Loaded += (s, e) => InitializeAfterWindowLoaded();
        
        AppendLog("MainWindow initialized.");
    }

    private void InitializeAfterWindowLoaded()
    {
        // Add handlers for window size/position changes
        SizeChanged += Window_SizeChanged;
        LocationChanged += Window_LocationChanged;

        // Load audio devices without blocking UI
        Task.Run(() => BasicLoadAudioDevices());
    }
    
    // Simple device loading method that avoids UI thread locking
    private void BasicLoadAudioDevices()
    {
        try
        {
            var startTime = DateTime.Now;
            var deviceList = new List<AudioDeviceViewModel>();
            
            // Store currently active device IDs for comparison
            var currentActiveDevices = new HashSet<string>();
            string baseDeviceId = string.Empty;
            
            using (var tempEnumerator = new MMDeviceEnumerator())
            {
                // Get all endpoints in ALL states, not just active ones
                var allDevices = tempEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All);
                var activeDevices = tempEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                foreach (var device in activeDevices)
                {
                    try
                    {
                        string deviceId = device.ID;
                        currentActiveDevices.Add(deviceId);
                        baseDeviceId = GetBaseDeviceId(deviceId);
                        
                        string friendlyName = device.FriendlyName;
                        string name = friendlyName;
                        string label = string.Empty;
                        
                        // Parse the name
                        int openParen = friendlyName.LastIndexOf("(");
                        int closeParen = friendlyName.LastIndexOf(")");
                        
                        if (openParen > 0 && closeParen > openParen)
                        {
                            name = friendlyName.Substring(0, openParen).Trim();
                            label = friendlyName.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                        }
                        
                        var vm = new AudioDeviceViewModel
                        {
                            Id = deviceId,
                            Name = name,
                            Label = label,
                            VolumeDb = device.AudioEndpointVolume.MasterVolumeLevel.ToString("F1"),
                            // Set selection state based on our persistent set
                            IsSelected = _persistentSelectionState.Contains(deviceId)
                        };
                        
                        deviceList.Add(vm);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing device: {ex.Message}");
                    }
                }
                
                // Now check the device cache to identify devices that should be removed
                Dispatcher.Invoke(() =>
                {
                    lock (_deviceLock)
                    {
                        var devicesToRemove = new List<string>();
                        
                        // Find devices in our cache that are no longer active and have the same base ID
                        foreach (var cachedDevice in _deviceCache.Values)
                        {
                            if (!currentActiveDevices.Contains(cachedDevice.Id))
                            {
                                string cachedBaseId = GetBaseDeviceId(cachedDevice.Id);
                                
                                // If this device shares a base ID with any currently disconnected devices, remove it
                                if (cachedBaseId == baseDeviceId || !cachedBaseId.Contains(baseDeviceId))
                                {
                                    devicesToRemove.Add(cachedDevice.Id);
                                }
                            }
                        }
                        
                        // Remove each device identified
                        foreach (var deviceId in devicesToRemove)
                        {
                            var device = _deviceCache[deviceId];
                            if (device != null)
                            {
                                Devices.Remove(device);
                                DevicesLeft.Remove(device);
                                DevicesRight.Remove(device);
                                _deviceCache.Remove(deviceId);
                            }
                        }
                        
                        if (devicesToRemove.Count > 0)
                        {
                            AppendLog($"Removed {devicesToRemove.Count} cached devices");
                        }
                    }
                });
            }
            
            // Sort the devices
            deviceList = deviceList.OrderBy(d => d.Label.ToLowerInvariant())
                                   .ThenBy(d => d.Name.ToLowerInvariant())
                                   .ToList();
            
            // Update the UI on the UI thread
            Dispatcher.Invoke(() => {
                try
                {
                    // Add new devices and update existing ones
                    foreach (var device in deviceList)
                    {
                        var existingDevice = Devices.FirstOrDefault(d => d.Id == device.Id);
                        if (existingDevice == null)
                        {
                            // New device - add it and maintain sorting
                            var vm = new AudioDeviceViewModel
                            {
                                Id = device.Id,
                                Name = device.Name,
                                Label = device.Label,
                                VolumeDb = device.VolumeDb,
                                IsSelected = _persistentSelectionState.Contains(device.Id)
                            };
                            
                            // Add to device cache
                            _deviceCache[device.Id] = vm;
                            
                            // Instead of just adding to the end, find the correct position for insertion
                            int insertIndex = 0;
                            for (int i = 0; i < Devices.Count; i++)
                            {
                                var compareResult = string.Compare(vm.Label.ToLowerInvariant(), 
                                                                 Devices[i].Label.ToLowerInvariant(), 
                                                                 StringComparison.Ordinal);
                                if (compareResult > 0 || (compareResult == 0 && 
                                                         string.Compare(vm.Name.ToLowerInvariant(), 
                                                                      Devices[i].Name.ToLowerInvariant(), 
                                                                      StringComparison.Ordinal) > 0))
                                {
                                    insertIndex = i + 1;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            
                            // Insert at the correct position
                            Devices.Insert(insertIndex, vm);
                        }
                        else
                        {
                            // Update existing device
                            existingDevice.Name = device.Name;
                            existingDevice.Label = device.Label;
                            existingDevice.VolumeDb = device.VolumeDb;
                        }
                    }
                    
                    // Rebalance the DataGrids
                    RebalanceDeviceGrids();
                    
                    // First time only, load selection from registry
                    if (!_devicesLoaded)
                    {
                        LoadSelection();
                        
                        // Set up the rest of the application
                        SetupNotifyIcon();
                        StartVolumeMonitor();
                        InitializeStartupToggle();
                        
                        // Now it's safe to register for notifications
                        try
                        {
                            _enumerator.RegisterEndpointNotificationCallback(this);
                            AppendLog("Registered for device notifications");
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"Failed to register for notifications: {ex.Message}");
                        }
                        
                        _devicesLoaded = true;
                    }
                    
                    // Save selection to persist changes
                    SaveSelection();
                    
                    var elapsed = DateTime.Now - startTime;
                    AppendLog($"Loaded {Devices.Count} audio devices in {elapsed.TotalMilliseconds:F0}ms");
                }
                catch (Exception ex)
                {
                    AppendLog($"Error updating UI: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => AppendLog($"Device load error: {ex.Message}"));
        }
    }
    
    // Simplify device notification handlers
    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        Dispatcher.BeginInvoke(() => {
            AppendLog($"Device state changed: {deviceId}");
            Task.Run(() => BasicLoadAudioDevices());
        });
    }
    
    public void OnDeviceAdded(string deviceId)
    {
        Dispatcher.BeginInvoke(() => {
            AppendLog($"Device added: {deviceId}");
            Task.Run(() => BasicLoadAudioDevices());
        });
    }
    
    public void OnDeviceRemoved(string deviceId)
    {
        Dispatcher.BeginInvoke(() => {
            AppendLog($"Device removed: {deviceId}");
            Task.Run(() => BasicLoadAudioDevices());
        });
    }
    
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string deviceId)
    {
        // We don't need to do anything special for default device changes
    }
    
    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        // If volume changes, we'll handle it in the volume monitor
    }
    
    private void AddOrUpdateDevice(string deviceId)
    {
        Task.Run(() => {
            try
            {
                lock (_deviceLock)
                {
                    // Create a new enumerator instance for each device request to avoid COM interface issues
                    using (var tempEnumerator = new MMDeviceEnumerator())
                    {
                        try
                        {
                            var device = tempEnumerator.GetDevice(deviceId);
                            
                            // Only process capture devices that are active
                            if (device.DataFlow == DataFlow.Capture && device.State == DeviceState.Active)
                            {
                                string friendlyName = device.FriendlyName;
                                string name = friendlyName;
                                string label = string.Empty;
                                
                                // Parse the device name
                                int openParen = friendlyName.LastIndexOf("(");
                                int closeParen = friendlyName.LastIndexOf(")");
                                
                                if (openParen > 0 && closeParen > openParen)
                                {
                                    name = friendlyName.Substring(0, openParen).Trim();
                                    label = friendlyName.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                                }
                                
                                Dispatcher.Invoke(() => {
                                    // Check if we already have this device
                                    var existingDevice = Devices.FirstOrDefault(d => d.Id == deviceId);
                                    
                                    if (existingDevice == null)
                                    {
                                        // New device - add it and maintain sorting
                                        var vm = new AudioDeviceViewModel
                                        {
                                            Id = deviceId,
                                            Name = name,
                                            Label = label,
                                            VolumeDb = device.AudioEndpointVolume.MasterVolumeLevel.ToString("F1"),
                                            IsSelected = _persistentSelectionState.Contains(deviceId)
                                        };
                                        
                                        // Add to device cache
                                        _deviceCache[deviceId] = vm;
                                        
                                        // Instead of just adding to the end, find the correct position for insertion
                                        int insertIndex = 0;
                                        for (int i = 0; i < Devices.Count; i++)
                                        {
                                            var compareResult = string.Compare(vm.Label.ToLowerInvariant(), 
                                                                             Devices[i].Label.ToLowerInvariant(), 
                                                                             StringComparison.Ordinal);
                                            if (compareResult > 0 || (compareResult == 0 && 
                                                                     string.Compare(vm.Name.ToLowerInvariant(), 
                                                                                  Devices[i].Name.ToLowerInvariant(), 
                                                                                  StringComparison.Ordinal) > 0))
                                            {
                                                insertIndex = i + 1;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                        
                                        // Insert at the correct position
                                        Devices.Insert(insertIndex, vm);
                                        
                                        // Rebalance the left and right grids
                                        RebalanceDeviceGrids();
                                        
                                        AppendLog($"Added device: {name} {(string.IsNullOrEmpty(label) ? "" : $"({label})")}");
                                    }
                                    else
                                    {
                                        // Update existing device
                                        existingDevice.Name = name;
                                        existingDevice.Label = label;
                                        existingDevice.VolumeDb = device.AudioEndpointVolume.MasterVolumeLevel.ToString("F1");
                                    }
                                });
                            }
                        }
                        catch (Exception deviceEx)
                        {
                            // Instead of reporting an error, try refreshing all devices when a device change is detected
                            Dispatcher.Invoke(() => {
                                AppendLog($"Device change detected, refreshing device list...");
                                Task.Run(() => BasicLoadAudioDevices());
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendLog($"Error during device update: {ex.Message}"));
            }
        });
    }
    
    private void RemoveDevice(string deviceId)
    {
        Dispatcher.Invoke(() => {
            try
            {
                lock (_deviceLock)
                {
                    var device = Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        Devices.Remove(device);
                        DevicesLeft.Remove(device);
                        DevicesRight.Remove(device);
                        _deviceCache.Remove(deviceId);
                        
                        // Rebalance the left and right grids
                        RebalanceDeviceGrids();
                        
                        AppendLog($"Removed device: {device.Name} {(string.IsNullOrEmpty(device.Label) ? "" : $"({device.Label})")}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error removing device {deviceId}: {ex.Message}");
            }
        });
    }
    
    private void RebalanceDeviceGrids()
    {
        DevicesLeft.Clear();
        DevicesRight.Clear();
        
        for (int i = 0; i < Devices.Count; i++)
        {
            if (i % 2 == 0)
                DevicesLeft.Add(Devices[i]);
            else
                DevicesRight.Add(Devices[i]);
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // Unregister notification client when closing
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
        }
        catch { /* Ignore errors during shutdown */ }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Minimize to system tray instead of just minimizing
        WindowState = WindowState.Minimized;
        Hide();
        
        // Make sure the notify icon is visible
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = true;
        }
        
        AppendLog("App minimized to tray.");
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            var textBlock = this.FindName("MaximizeIcon") as System.Windows.Controls.TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = "□";
            }
        }
        else
        {
            WindowState = WindowState.Maximized;
            var textBlock = this.FindName("MaximizeIcon") as System.Windows.Controls.TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = "❐";
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Create a custom dialog window
        var dialogWindow = new System.Windows.Window
        {
            Title = "ToxMoxMicFreezer",
            Width = 300,
            Height = 150,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["ApplicationBackgroundBrush"],
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            BorderBrush = (SolidColorBrush)System.Windows.Application.Current.Resources["AccentControlElevationBorderBrush"],
            BorderThickness = new Thickness(1)
        };

        // Create dialog content
        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

        var titleBar = new Border
        {
            Height = 32,
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Padding = new Thickness(10, 0, 10, 0)
        };

        var titleText = new System.Windows.Controls.TextBlock
        {
            Text = "Close Options",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(5, 0, 0, 0)
        };

        titleBar.Child = titleText;
        grid.Children.Add(titleBar);
        Grid.SetRow(titleBar, 0);

        var messagePanel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(20, 20, 20, 10)
        };

        var messageText = new System.Windows.Controls.TextBlock
        {
            Text = "Would you like to minimize to system tray or exit the application?",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
            Margin = new Thickness(0, 0, 0, 10)
        };

        messagePanel.Children.Add(messageText);
        grid.Children.Add(messagePanel);
        Grid.SetRow(messagePanel, 1);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 20)
        };

        var minimizeButton = new System.Windows.Controls.Button
        {
            Content = "Minimize to Tray",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 0, 10, 0),
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["SecondaryButtonBackgroundBrush"],
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
            BorderThickness = new Thickness(0)
        };

        var exitButton = new System.Windows.Controls.Button
        {
            Content = "Exit",
            Padding = new Thickness(15, 5, 15, 5),
            Background = (SolidColorBrush)System.Windows.Application.Current.Resources["PrimaryButtonBackgroundBrush"],
            Foreground = (SolidColorBrush)System.Windows.Application.Current.Resources["TextFillColorPrimaryBrush"],
            BorderThickness = new Thickness(0)
        };

        minimizeButton.Click += (s, args) =>
        {
            dialogWindow.DialogResult = false;
            dialogWindow.Close();
        };

        exitButton.Click += (s, args) =>
        {
            dialogWindow.DialogResult = true;
            dialogWindow.Close();
        };

        buttonPanel.Children.Add(minimizeButton);
        buttonPanel.Children.Add(exitButton);
        grid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 2);

        dialogWindow.Content = grid;

        // Show dialog and handle result
        var result = dialogWindow.ShowDialog();

        if (result == true)
        {
            // Exit the application
            _isExit = true;
            Close();
        }
        else
        {
            // Minimize to system tray
            WindowState = WindowState.Minimized;
            Hide();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }
    }

    private void SetupTitleBarIcon()
    {
        try 
        {
            // Get the cached icon from the App class
            var appIcon = App.GetAppIcon();
            if (appIcon != null)
            {
                // Convert icon to bitmap using a more efficient method
                var bitmap = appIcon.ToBitmap();
                
                // Process the icon on a background thread
                Task.Run(() => {
                    try {
                        using (var memory = new System.IO.MemoryStream())
                        {
                            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                            memory.Position = 0;
                            
                            // Create bitmap image
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                            bitmapImage.Freeze(); // Important for cross-thread usage
                            
                            // Update UI on the UI thread
                            Dispatcher.Invoke(() => {
                                var iconBrush = FindName("AppIconBrush") as ImageBrush;
                                if (iconBrush != null)
                                {
                                    iconBrush.ImageSource = bitmapImage;
                                }
                            });
                        }
                    }
                    catch (Exception ex) {
                        Dispatcher.Invoke(() => AppendLog($"Error in icon processing: {ex.Message}"));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error setting title bar icon: {ex.Message}");
        }
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        
        // Use the shared app icon
        try 
        {
            _notifyIcon.Icon = App.GetAppIcon();
        }
        catch
        {
            // Fallback to system icon
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        
        _notifyIcon.Visible = false;
        _notifyIcon.Text = AppName;
        _notifyIcon.DoubleClick += (s, e) => ShowFromTray();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (s, e) => ShowFromTray());
        menu.Items.Add("Exit", null, (s, e) => { _isExit = true; Dispatcher.Invoke(Close); });
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void LoadSelection()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
            if (key != null)
            {
                var selectedIds = key.GetValue(RegValueName) as string[];
                if (selectedIds != null)
                {
                    // Store selected device IDs in our persistent set
                    _persistentSelectionState = new HashSet<string>(selectedIds);
                    
                    // Apply selection states to current devices
                    foreach (var dev in Devices)
                    {
                        dev.IsSelected = _persistentSelectionState.Contains(dev.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to load device selection: {ex.Message}");
        }
    }

    private void SaveSelection()
    {
        try
        {
            // Save currently selected device IDs
            var currentSelectedIds = Devices.Where(d => d.IsSelected).Select(d => d.Id).ToList();
            
            // Add these to our persistent set
            foreach (var id in currentSelectedIds)
            {
                _persistentSelectionState.Add(id);
            }
            
            // Remove any IDs that are explicitly deselected
            var explicitlyDeselected = Devices.Where(d => !d.IsSelected).Select(d => d.Id).ToList();
            foreach (var id in explicitlyDeselected)
            {
                _persistentSelectionState.Remove(id);
            }
            
            // Save the entire persistent set
            using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
            key.SetValue(RegValueName, _persistentSelectionState.ToArray(), RegistryValueKind.MultiString);
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to save device selection: {ex.Message}");
        }
    }

    private void StartVolumeMonitor()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using (var tempEnumerator = new MMDeviceEnumerator())
                    {
                        var devices = tempEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                        
                        // Create a safe copy of the devices to avoid collection modification issues
                        List<AudioDeviceViewModel> selectedDevices;
                        lock (_deviceLock)
                        {
                            selectedDevices = Devices.Where(d => d.IsSelected).ToList();
                        }
                        
                        foreach (var dev in selectedDevices)
                        {
                            try
                            {
                                var naudioDev = devices.FirstOrDefault(d => d.ID == dev.Id);
                                if (naudioDev != null)
                                {
                                    dev.VolumeDb = naudioDev.AudioEndpointVolume.MasterVolumeLevel.ToString("F1");
                                    if (dev.IsSelected)
                                    {
                                        float maxDb = naudioDev.AudioEndpointVolume.VolumeRange.MaxDecibels;
                                        float minDb = naudioDev.AudioEndpointVolume.VolumeRange.MinDecibels;
                                        
                                        // Find the exact 0dB value or the closest value below 0dB that the device supports
                                        float targetDb = 0.0f;
                                        
                                        // Some devices don't support exactly 0dB, so find the closest value ≤ 0dB
                                        if (maxDb < 0.0f)
                                        {
                                            // If max is below 0, use the max
                                            targetDb = maxDb;
                                        }
                                        else if (minDb > 0.0f)
                                        {
                                            // If min is above 0, use the min
                                            targetDb = minDb;
                                        }
                                        else
                                        {
                                            // NAudio doesn't expose VolumeStepIncrement directly
                                            // Calculate a reasonable step size based on the volume range
                                            float volumeRange = maxDb - minDb;
                                            float increment = volumeRange / 100.0f; // 100 steps across the range
                                            
                                            // Ensure the increment is reasonable (between 0.05 and 0.5 dB)
                                            if (increment < 0.05f)
                                                increment = 0.05f;
                                            else if (increment > 0.5f)
                                                increment = 0.5f;
                                            
                                            // Find the highest value less than or equal to 0dB
                                            // Start from min and step up to find the closest value to 0dB without exceeding it
                                            targetDb = minDb;
                                            for (float db = minDb; db <= maxDb; db += increment)
                                            {
                                                if (db <= 0.0f && db > targetDb)
                                                {
                                                    targetDb = db;
                                                }
                                                if (db > 0.0f)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                        
                                        // Only change the volume if it's not already at our target
                                        if (Math.Abs(naudioDev.AudioEndpointVolume.MasterVolumeLevel - targetDb) > 0.01f)
                                        {
                                            naudioDev.AudioEndpointVolume.MasterVolumeLevel = targetDb;
                                            AppendLog($"Locked {dev.Name} to {targetDb:F1} dB");
                                        }
                                    }
                                }
                            }
                            catch (Exception deviceEx)
                            {
                                // Ignore errors for individual devices
                            }
                        }
                        
                        Dispatcher.Invoke(() => SaveSelection());
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => AppendLog($"Error: {ex.Message}"));
                }
                await Task.Delay(1000);
            }
        }, _cts.Token);
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var logTextBox = this.FindName("LogTextBox") as System.Windows.Controls.TextBox;
            if (logTextBox != null)
            {
                logTextBox.AppendText($"[{DateTime.Now:T}] {message}\n");
                logTextBox.ScrollToEnd();
            }
        });
    }

    private void InitializeStartupToggle()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            if (key != null)
            {
                bool isInStartup = key.GetValue(AppName) != null;
                
                var startupToggle = this.FindName("StartupToggle") as Wpf.Ui.Controls.ToggleSwitch;
                if (startupToggle != null)
                {
                    startupToggle.IsChecked = isInStartup;
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error checking startup status: {ex.Message}");
        }
    }

    private void StartupToggle_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule?.FileName == null)
            {
                AppendLog("Error: Could not get executable path");
                return;
            }
            string exePath = mainModule.FileName;
            
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key == null)
            {
                AppendLog("Error: Could not access registry key");
                return;
            }

            key.SetValue(AppName, exePath);
            AppendLog("Added to startup.");
        }
        catch (Exception ex)
        {
            AppendLog($"Startup error: {ex.Message}");
        }
    }

    private void StartupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key == null)
            {
                AppendLog("Error: Could not access registry key");
                return;
            }

            key.DeleteValue(AppName, false);
            AppendLog("Removed from startup.");
        }
        catch (Exception ex)
        {
            AppendLog($"Startup error: {ex.Message}");
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _isExit = true;
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExit)
        {
            e.Cancel = true;
            Hide();
            AppendLog("App minimized to tray.");
        }
        else
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            AppendLog("App minimized to tray.");
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        AppendLog("App restored from tray.");
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var dev in Devices)
            dev.IsSelected = true;
        SaveSelection();
        AppendLog("All devices selected.");
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var dev in Devices)
            dev.IsSelected = false;
        SaveSelection();
        AppendLog("All devices deselected.");
    }

    private void SaveWindowSettings()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
            var isMax = WindowState == WindowState.Maximized;
            
            // Get the correct bounds based on window state
            var bounds = isMax ? RestoreBounds : new Rect(Left, Top, Width, Height);
            
            key.SetValue($"{RegValuePrefix}Left", (int)bounds.Left);
            key.SetValue($"{RegValuePrefix}Top", (int)bounds.Top);
            key.SetValue($"{RegValuePrefix}Width", (int)bounds.Width);
            key.SetValue($"{RegValuePrefix}Height", (int)bounds.Height);
            key.SetValue($"{RegValuePrefix}IsMaximized", isMax ? 1 : 0);
        }
        catch
        {
            // Silently fail if we can't save to registry
        }
    }

    private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (IsLoaded) // Only save after window is fully loaded
        {
            SaveWindowSettings();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (IsLoaded) // Only save after window is fully loaded
        {
            SaveWindowSettings();
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // The ScrollViewer will automatically synchronize both DataGrids since they're inside it
        // This handler is primarily used for adding custom scroll behavior if needed in the future
    }

    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Forward the mouse wheel event to the ScrollViewer
        ScrollViewer scrollViewer = sender as ScrollViewer;
        if (scrollViewer != null)
        {
            if (e.Delta < 0)
            {
                scrollViewer.LineDown();
            }
            else
            {
                scrollViewer.LineUp();
            }
        }
        
        // Mark the event as handled so it doesn't get routed to parent containers
        e.Handled = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var popup = this.FindName("SettingsPopup") as System.Windows.Controls.Primitives.Popup;
        if (popup != null)
        {
            popup.IsOpen = !popup.IsOpen;
        }
        else
        {
            AppendLog("Settings popup not found.");
        }
    }

    // Add these methods for window resizing functionality
    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            return;
        }
        
        double newWidth = Math.Max(this.MinWidth, this.Width + e.HorizontalChange);
        double newHeight = Math.Max(this.MinHeight, this.Height + e.VerticalChange);
        
        this.Width = newWidth;
        this.Height = newHeight;
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle the event to prevent it from propagating, but don't do any resizing here
        // Resizing is handled exclusively by the edge elements
        e.Handled = true;
    }

    private void ResizeSide_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            return;
        
        var element = sender as FrameworkElement;
        if (element?.Tag == null)
            return;
        
        string direction = element.Tag.ToString();
        ResizeDirection resizeDir = ResizeDirection.Bottom;
        
        switch (direction)
        {
            case "Left": resizeDir = ResizeDirection.Left; break;
            case "Right": resizeDir = ResizeDirection.Right; break;
            case "Top": resizeDir = ResizeDirection.Top; break;
            case "Bottom": resizeDir = ResizeDirection.Bottom; break;
            case "TopLeft": resizeDir = ResizeDirection.TopLeft; break;
            case "TopRight": resizeDir = ResizeDirection.TopRight; break;
            case "BottomLeft": resizeDir = ResizeDirection.BottomLeft; break;
            case "BottomRight": resizeDir = ResizeDirection.BottomRight; break;
        }
        
        ResizeWindow(resizeDir);
    }

    private void ResizeWindow(ResizeDirection direction)
    {
        // Use native Windows functionality to resize the window
        SendMessage(new WindowInteropHelper(this).Handle, 0x112, (IntPtr)(61440 + direction), IntPtr.Zero);
    }

    private enum ResizeDirection
    {
        Left = 1,
        Right = 2,
        Top = 3,
        TopLeft = 4,
        TopRight = 5,
        Bottom = 6,
        BottomLeft = 7,
        BottomRight = 8,
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // Helper method to extract the base device ID (without the {x.x.x} endpoint part)
    private string GetBaseDeviceId(string fullDeviceId)
    {
        int lastSeparator = fullDeviceId.LastIndexOf('#');
        if (lastSeparator > 0)
        {
            return fullDeviceId.Substring(0, lastSeparator);
        }
        
        return fullDeviceId;
    }
}

public class AudioDeviceViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); SelectionChanged?.Invoke(); }
    }
    private string _volumeDb = "0.0";
    public string VolumeDb
    {
        get => _volumeDb;
        set { _volumeDb = value; OnPropertyChanged(nameof(VolumeDb)); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public static Action? SelectionChanged;
}

public class WindowSettings
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public WindowState WindowState { get; set; }
}

public class VolumeToZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string volumeStr)
        {
            // Check if the volume is 0.0
            if (float.TryParse(volumeStr, out float volume))
            {
                return Math.Abs(volume) < 0.1f; // Returns true if volume is close to zero
            }
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
