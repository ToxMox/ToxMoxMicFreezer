// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service for handling drag and drop operations in the Favorites tab
    /// </summary>
    public class FavoritesDragDropService : IFavoritesDragDropService
    {
        private readonly ILoggingService _loggingService;
        private readonly IFavoritesService _favoritesService;
        private AudioDeviceViewModel? _draggingDevice;

        public FavoritesDragDropService(ILoggingService loggingService, IFavoritesService favoritesService)
        {
            _loggingService = loggingService;
            _favoritesService = favoritesService;
        }

        /// <summary>
        /// Starts a drag operation for a device
        /// </summary>
        public void StartDrag(AudioDeviceViewModel device)
        {
            _draggingDevice = device;
            _loggingService.Log($"Started dragging device: {device.Name} from {device.FavoriteColumn} column, position {device.FavoritePosition}");
        }

        /// <summary>
        /// Gets the current dragging device
        /// </summary>
        public AudioDeviceViewModel? GetDraggingDevice()
        {
            return _draggingDevice;
        }

        /// <summary>
        /// Calculates the drop position based on drop point
        /// </summary>
        public int GetDropPosition(System.Windows.Point dropPoint, FrameworkElement container, ObservableCollection<AudioDeviceViewModel> devices)
        {
            if (devices.Count == 0)
                return 0;

            // Try to find the DataGrid from the container
            var dataGrid = container as System.Windows.Controls.DataGrid ?? 
                          FindParent<System.Windows.Controls.DataGrid>(container);
            
            if (dataGrid != null)
            {
                // Use proper DataGrid row hit testing
                return GetDropRowIndex(dropPoint, dataGrid, devices.Count);
            }

            // Fallback to simple calculation if DataGrid not found
            double itemHeight = container.ActualHeight / Math.Max(1, devices.Count);
            int position = (int)(dropPoint.Y / itemHeight);
            
            // Clamp to valid range
            position = Math.Max(0, Math.Min(devices.Count, position));
            
            return position;
        }

        /// <summary>
        /// Gets the drop row index using proper DataGrid hit testing
        /// </summary>
        private int GetDropRowIndex(System.Windows.Point dropPoint, System.Windows.Controls.DataGrid dataGrid, int itemCount)
        {
            // Get the visual hit test result
            var hitTest = dataGrid.InputHitTest(dropPoint) as DependencyObject;
            
            // Walk up the visual tree to find a DataGridRow
            while (hitTest != null && !(hitTest is System.Windows.Controls.DataGridRow))
            {
                hitTest = System.Windows.Media.VisualTreeHelper.GetParent(hitTest);
            }
            
            if (hitTest is System.Windows.Controls.DataGridRow row)
            {
                // Get the index of the row
                var index = dataGrid.ItemContainerGenerator.IndexFromContainer(row);
                
                // Check if we're in the upper or lower half of the row
                var rowBounds = row.TransformToAncestor(dataGrid).TransformBounds(
                    new System.Windows.Rect(0, 0, row.ActualWidth, row.ActualHeight));
                
                var relativeY = dropPoint.Y - rowBounds.Top;
                var isLowerHalf = relativeY > rowBounds.Height / 2;
                
                // If in lower half, insert after this row
                if (isLowerHalf)
                {
                    index++;
                }
                
                return Math.Max(0, Math.Min(itemCount, index));
            }
            
            // If no row found, check if we're beyond the last item
            if (itemCount > 0)
            {
                var lastItem = dataGrid.ItemContainerGenerator.ContainerFromIndex(itemCount - 1) as System.Windows.Controls.DataGridRow;
                if (lastItem != null)
                {
                    var lastRowBounds = lastItem.TransformToAncestor(dataGrid).TransformBounds(
                        new System.Windows.Rect(0, 0, lastItem.ActualWidth, lastItem.ActualHeight));
                    
                    if (dropPoint.Y > lastRowBounds.Bottom)
                    {
                        return itemCount; // Insert at end
                    }
                }
            }
            
            // Default to beginning if can't determine position
            return 0;
        }

        /// <summary>
        /// Finds a parent of the specified type in the visual tree
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            
            while (parent != null && !(parent is T))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            
            return parent as T;
        }

        /// <summary>
        /// Handles dropping a device into a new position
        /// </summary>
        public void HandleDrop(AudioDeviceViewModel device, FavoriteColumnType targetColumn, int position,
                              ObservableCollection<AudioDeviceViewModel> leftDevices,
                              ObservableCollection<AudioDeviceViewModel> rightDevices)
        {
            if (device == null) return;

            var sourceColumn = device.FavoriteColumn;
            var sourcePosition = device.FavoritePosition;

            // Adjust position if dropping within the same column and after the original position
            // This accounts for the index shift when the item is removed
            if (sourceColumn == targetColumn && position > sourcePosition)
            {
                position--;
            }

            // Check if dropping to same position
            if (sourceColumn == targetColumn && sourcePosition == position)
            {
                _loggingService.Log("Device dropped at same position, no changes needed");
                CancelDrag();
                return;
            }

            _loggingService.Log($"Dropping device {device.Name} to {targetColumn} column at position {position}");

            // Remove from source collection
            var sourceCollection = sourceColumn == FavoriteColumnType.Left ? leftDevices : rightDevices;
            sourceCollection.Remove(device);

            // Update device properties
            device.FavoriteColumn = targetColumn;
            device.FavoritePosition = position;

            // Add to target collection
            var targetCollection = targetColumn == FavoriteColumnType.Left ? leftDevices : rightDevices;
            
            // Insert at correct position
            if (position >= targetCollection.Count)
            {
                targetCollection.Add(device);
            }
            else
            {
                targetCollection.Insert(position, device);
            }

            // Reindex positions in both columns
            ReindexColumnPositions(leftDevices, FavoriteColumnType.Left);
            ReindexColumnPositions(rightDevices, FavoriteColumnType.Right);

            // Save the updated positions
            var allDevices = leftDevices.Concat(rightDevices);
            _favoritesService.SaveFavorites(allDevices);

            CancelDrag();
        }

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        public void CancelDrag()
        {
            _draggingDevice = null;
        }

        /// <summary>
        /// Gets the least populated column for adding new favorites
        /// </summary>
        public FavoriteColumnType GetLeastPopulatedColumn(ObservableCollection<AudioDeviceViewModel> leftDevices,
                                                          ObservableCollection<AudioDeviceViewModel> rightDevices)
        {
            return leftDevices.Count <= rightDevices.Count ? FavoriteColumnType.Left : FavoriteColumnType.Right;
        }

        /// <summary>
        /// Reindexes positions for all devices in a column
        /// </summary>
        private void ReindexColumnPositions(ObservableCollection<AudioDeviceViewModel> devices, FavoriteColumnType column)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                devices[i].FavoriteColumn = column;
                devices[i].FavoritePosition = i;
            }
        }
    }
}