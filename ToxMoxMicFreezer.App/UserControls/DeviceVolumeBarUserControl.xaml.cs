// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ToxMoxMicFreezer.App.Models;
using ToxMoxMicFreezer.App.Services;

namespace ToxMoxMicFreezer.App.UserControls
{
    /// <summary>
    /// Simplified UserControl for displaying device volume bars with 2px meter bars that are always visible
    /// Simplified from complex grid-based approach to clean, efficient implementation
    /// </summary>
    public partial class DeviceVolumeBarUserControl : System.Windows.Controls.UserControl
    {
        // Dependency property for the device collection
        public static readonly DependencyProperty DeviceCollectionProperty =
            DependencyProperty.Register(nameof(DeviceCollection), 
                                      typeof(ObservableCollection<AudioDeviceViewModel>), 
                                      typeof(DeviceVolumeBarUserControl), 
                                      new PropertyMetadata(null));

        // Dependency property for margin configuration
        public static readonly DependencyProperty GridMarginProperty =
            DependencyProperty.Register(nameof(GridMargin), 
                                      typeof(Thickness), 
                                      typeof(DeviceVolumeBarUserControl), 
                                      new PropertyMetadata(new Thickness(0)));

        // Dependency property to indicate if this is used in favorites tab
        public static readonly DependencyProperty IsFavoritesTabProperty =
            DependencyProperty.Register(nameof(IsFavoritesTab), 
                                      typeof(bool), 
                                      typeof(DeviceVolumeBarUserControl), 
                                      new PropertyMetadata(false));

        // Dependency property for column type (for favorites drag & drop)
        public static readonly DependencyProperty FavoriteColumnProperty =
            DependencyProperty.Register(nameof(FavoriteColumn), 
                                      typeof(FavoriteColumnType?), 
                                      typeof(DeviceVolumeBarUserControl), 
                                      new PropertyMetadata(null));

        /// <summary>
        /// The device collection to display in the DataGrid
        /// </summary>
        public ObservableCollection<AudioDeviceViewModel> DeviceCollection
        {
            get { return (ObservableCollection<AudioDeviceViewModel>)GetValue(DeviceCollectionProperty); }
            set { SetValue(DeviceCollectionProperty, value); }
        }

        /// <summary>
        /// Margin for the DataGrid (for left/right column spacing)
        /// </summary>
        public Thickness GridMargin
        {
            get { return (Thickness)GetValue(GridMarginProperty); }
            set { SetValue(GridMarginProperty, value); }
        }

        /// <summary>
        /// Indicates if this control is used in the favorites tab
        /// </summary>
        public bool IsFavoritesTab
        {
            get { return (bool)GetValue(IsFavoritesTabProperty); }
            set { SetValue(IsFavoritesTabProperty, value); }
        }

        /// <summary>
        /// The column type for favorites drag & drop
        /// </summary>
        public FavoriteColumnType? FavoriteColumn
        {
            get { return (FavoriteColumnType?)GetValue(FavoriteColumnProperty); }
            set { SetValue(FavoriteColumnProperty, value); }
        }

        private MainWindow? _mainWindow;
        private bool _disposed = false;
        private System.Windows.Point? _dragStartPoint;

        public DeviceVolumeBarUserControl()
        {
            InitializeComponent();
            
            // Subscribe to audio metering setting changes for simple show/hide
            App.AudioMeteringSettingChanged += OnAudioMeteringSettingChanged;
            
            // Set up drag & drop event handlers
            DeviceDataGrid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            DeviceDataGrid.PreviewMouseMove += OnPreviewMouseMove;
            DeviceDataGrid.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            DeviceDataGrid.DragEnter += OnDragEnter;
            DeviceDataGrid.DragOver += OnDragOver;
            DeviceDataGrid.Drop += OnDrop;
            DeviceDataGrid.DragLeave += OnDragLeave;
            
            // Find the MainWindow reference for event delegation
            Loaded += (s, e) => {
                _mainWindow = FindMainWindow();
                DeviceDataGrid.Margin = GridMargin;
                
                // Ensure meters are always visible
                EnsureMetersAlwaysVisible();
            };

            // Handle cleanup on unload
            Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Audio metering setting change handler - ensure meters stay always visible
        /// </summary>
        private void OnAudioMeteringSettingChanged(object? sender, EventArgs e)
        {
            if (_disposed) return;

            Dispatcher.BeginInvoke(new Action(() => {
                EnsureMetersAlwaysVisible();
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Ensure 2px meter bars are always visible - no collapsing or hiding
        /// Audio metering setting will only control whether bars show data or stay empty
        /// </summary>
        private void EnsureMetersAlwaysVisible()
        {
            if (_disposed) return;

            try
            {
                // Find all AudioMeterContainer elements and ensure they're always visible
                foreach (var item in DeviceDataGrid.Items)
                {
                    var container = DeviceDataGrid.ItemContainerGenerator.ContainerFromItem(item);
                    if (container is DataGridRow row)
                    {
                        var meterContainer = FindChildByName<Border>(row, "AudioMeterContainer");
                        if (meterContainer != null)
                        {
                            // ALWAYS visible - no more collapsing based on settings
                            meterContainer.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring meter visibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to find child controls by name in the visual tree
        /// </summary>
        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private MainWindow? FindMainWindow()
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is MainWindow mainWindow)
                    return mainWindow;
                current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        #region Volume Bar Event Handlers (delegated to MainWindow)

        private void VolumeBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeBarMouseLeftButtonDown(sender, e);
        }

        private void VolumeBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeBarMouseMove(sender, e);
        }

        private void VolumeBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeBarMouseLeftButtonUp(sender, e);
        }

        private void VolumeBar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeBarMouseRightButtonDown(sender, e);
        }

        #endregion

        #region Volume Text Event Handlers (delegated to MainWindow)

        private void VolumeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeTextMouseLeftButtonDown(sender, e);
        }

        private void VolumeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeTextBoxKeyDown(sender, e);
        }

        private void VolumeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeTextBoxLostFocus(sender, e);
        }

        private void VolumeTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            _mainWindow?._uiEventRouter?.OnVolumeTextBoxLoaded(sender, e);
        }

        #endregion

        #region Mute Button Event Handlers

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox muteButton && muteButton.DataContext is AudioDeviceViewModel device)
            {
                // Use the MainWindow's MuteService to toggle mute
                _mainWindow?.MuteService?.ToggleMute(device.Id);
            }
        }

        #endregion

        #region Star Button Event Handlers

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox starButton && starButton.DataContext is AudioDeviceViewModel device)
            {
                // The binding will handle the property update
                // Just log the change for now
                _mainWindow?.AppendLog($"Device '{device.Name}' favorite status changed to: {device.IsFavorite}");
            }
        }

        #endregion

        #region Drag & Drop Event Handlers

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsFavoritesTab) return;

            // Check if the click is on a volume bar - if so, don't initiate drag & drop
            var originalSource = e.OriginalSource as DependencyObject;
            if (IsClickOnVolumeBar(originalSource))
            {
                // Don't store drag start point for volume bar clicks
                _dragStartPoint = null;
                return;
            }

            // Store the mouse position for potential drag operation
            _dragStartPoint = e.GetPosition(null);
        }

        private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsFavoritesTab || _dragStartPoint == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            // Additional safety check - ensure we're not dragging from a volume bar
            var originalSource = e.OriginalSource as DependencyObject;
            if (IsClickOnVolumeBar(originalSource))
            {
                _dragStartPoint = null;
                return;
            }

            System.Windows.Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint.Value - currentPosition;

            // Check if the mouse has moved enough to start a drag
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // Get the dragged device
                var device = GetDeviceFromPoint(e.GetPosition(DeviceDataGrid));
                if (device != null)
                {
                    
                    // Get drag drop service from MainWindow
                    var dragDropService = _mainWindow?.GetService<IFavoritesDragDropService>();
                    dragDropService?.StartDrag(device);

                    // Find and reduce opacity of the dragged row
                    var draggedRow = GetRowFromDevice(device);
                    if (draggedRow != null)
                    {
                        draggedRow.Opacity = 0.5;
                    }

                    // Create data object and start drag
                    System.Windows.DataObject dragData = new System.Windows.DataObject("FavoriteDevice", device);
                    DragDrop.DoDragDrop(DeviceDataGrid, dragData, System.Windows.DragDropEffects.Move);
                    
                    // Restore opacity after drag
                    if (draggedRow != null)
                    {
                        draggedRow.Opacity = 1.0;
                    }
                    
                }
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = null;
        }

        private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!IsFavoritesTab || !e.Data.GetDataPresent("FavoriteDevice"))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
        }

        private void OnDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!IsFavoritesTab || !e.Data.GetDataPresent("FavoriteDevice"))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                HideDropIndicator();
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            
            // Update drop indicator position
            UpdateDropIndicatorPosition(e.GetPosition(DeviceDataGrid));
        }

        private void OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (!IsFavoritesTab || !e.Data.GetDataPresent("FavoriteDevice"))
                return;

            var draggedDevice = e.Data.GetData("FavoriteDevice") as AudioDeviceViewModel;
            if (draggedDevice == null || FavoriteColumn == null)
                return;

            // Get drop position
            var dragDropService = _mainWindow?.GetService<IFavoritesDragDropService>();
            if (dragDropService != null && DeviceCollection != null)
            {
                System.Windows.Point dropPoint = e.GetPosition(DeviceDataGrid);
                int dropPosition = dragDropService.GetDropPosition(dropPoint, DeviceDataGrid, DeviceCollection);

                // Get left and right collections from MainWindow
                var leftCollection = _mainWindow?.FavoriteDevicesLeft;
                var rightCollection = _mainWindow?.FavoriteDevicesRight;

                if (leftCollection != null && rightCollection != null)
                {
                    // Handle the drop
                    dragDropService.HandleDrop(draggedDevice, FavoriteColumn.Value, dropPosition, leftCollection, rightCollection);
                }
            }
            
            // Hide drop indicator after drop
            HideDropIndicator();
        }

        private void OnDragLeave(object sender, System.Windows.DragEventArgs e)
        {
            // Hide drop indicator when leaving
            HideDropIndicator();
        }

        private AudioDeviceViewModel? GetDeviceFromPoint(System.Windows.Point point)
        {
            // Find the data grid row at the point
            var hitTest = DeviceDataGrid.InputHitTest(point) as DependencyObject;
            while (hitTest != null && !(hitTest is DataGridRow))
            {
                hitTest = System.Windows.Media.VisualTreeHelper.GetParent(hitTest);
            }

            if (hitTest is DataGridRow row)
            {
                return row.Item as AudioDeviceViewModel;
            }

            return null;
        }

        /// <summary>
        /// Updates the position of the drop indicator based on the drag position
        /// </summary>
        private void UpdateDropIndicatorPosition(System.Windows.Point dropPoint)
        {
            if (DeviceCollection == null || DeviceCollection.Count == 0)
            {
                HideDropIndicator();
                return;
            }

            // Get the drop indicator control
            var dropIndicator = FindChildByName<Border>(this, "DropIndicator");
            if (dropIndicator == null) return;

            // Ensure the DataGrid has generated its containers
            DeviceDataGrid.UpdateLayout();

            // Calculate drop position using the improved service method
            var dragDropService = _mainWindow?.GetService<IFavoritesDragDropService>();
            if (dragDropService == null) return;

            int dropIndex = dragDropService.GetDropPosition(dropPoint, DeviceDataGrid, DeviceCollection);
            
            // Find the Y position where the indicator should appear
            double indicatorY = 0;
            
            if (dropIndex >= DeviceCollection.Count)
            {
                // Position at the bottom of the last item
                var lastRow = DeviceDataGrid.ItemContainerGenerator.ContainerFromIndex(DeviceCollection.Count - 1) as DataGridRow;
                if (lastRow != null)
                {
                    var bounds = lastRow.TransformToAncestor(DeviceDataGrid).TransformBounds(
                        new System.Windows.Rect(0, 0, lastRow.ActualWidth, lastRow.ActualHeight));
                    indicatorY = bounds.Bottom;
                }
            }
            else if (dropIndex == 0)
            {
                // Position at the top of the first item
                indicatorY = 0;
            }
            else
            {
                // Position between items
                var row = DeviceDataGrid.ItemContainerGenerator.ContainerFromIndex(dropIndex) as DataGridRow;
                if (row != null)
                {
                    var bounds = row.TransformToAncestor(DeviceDataGrid).TransformBounds(
                        new System.Windows.Rect(0, 0, row.ActualWidth, row.ActualHeight));
                    indicatorY = bounds.Top;
                }
            }

            // Set the position and show the indicator
            dropIndicator.Margin = new Thickness(5, indicatorY - 1.5, 5, 0);
            dropIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the drop indicator
        /// </summary>
        private void HideDropIndicator()
        {
            var dropIndicator = FindChildByName<Border>(this, "DropIndicator");
            if (dropIndicator != null)
            {
                dropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Gets the DataGridRow for a given device
        /// </summary>
        private DataGridRow? GetRowFromDevice(AudioDeviceViewModel device)
        {
            var container = DeviceDataGrid.ItemContainerGenerator.ContainerFromItem(device);
            return container as DataGridRow;
        }

        /// <summary>
        /// Checks if a click originated from a volume bar element
        /// </summary>
        private bool IsClickOnVolumeBar(DependencyObject? element)
        {
            if (element == null) return false;

            // Walk up the visual tree to check if we hit a volume bar
            var current = element;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.Name == "VolumeBarContainer")
                {
                    return true;
                }
                
                // Stop at DataGrid level - we've gone too far up
                if (current == DeviceDataGrid)
                {
                    break;
                }
                
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            
            return false;
        }

        #endregion

        #region Disposal

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Unsubscribe from events
                App.AudioMeteringSettingChanged -= OnAudioMeteringSettingChanged;
                
                System.Diagnostics.Debug.WriteLine("DeviceVolumeBarUserControl: Disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing DeviceVolumeBarUserControl: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion
    }
}