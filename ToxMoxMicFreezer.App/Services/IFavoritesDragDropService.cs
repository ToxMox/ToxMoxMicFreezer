// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Collections.ObjectModel;
using System.Windows;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service interface for handling drag and drop operations in the Favorites tab
    /// </summary>
    public interface IFavoritesDragDropService
    {
        /// <summary>
        /// Starts a drag operation for a device
        /// </summary>
        void StartDrag(AudioDeviceViewModel device);

        /// <summary>
        /// Gets the current dragging device
        /// </summary>
        AudioDeviceViewModel? GetDraggingDevice();

        /// <summary>
        /// Calculates the drop position based on drop point
        /// </summary>
        int GetDropPosition(System.Windows.Point dropPoint, FrameworkElement container, ObservableCollection<AudioDeviceViewModel> devices);

        /// <summary>
        /// Handles dropping a device into a new position
        /// </summary>
        void HandleDrop(AudioDeviceViewModel device, FavoriteColumnType targetColumn, int position, 
                       ObservableCollection<AudioDeviceViewModel> leftDevices, 
                       ObservableCollection<AudioDeviceViewModel> rightDevices);

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        void CancelDrag();

        /// <summary>
        /// Gets the least populated column for adding new favorites
        /// </summary>
        FavoriteColumnType GetLeastPopulatedColumn(ObservableCollection<AudioDeviceViewModel> leftDevices, 
                                                   ObservableCollection<AudioDeviceViewModel> rightDevices);
    }
}