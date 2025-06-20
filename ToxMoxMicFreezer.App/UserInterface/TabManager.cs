// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Documents;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Manages device distribution between tabs and tab header updates
    /// </summary>
    public class TabManager
    {
        private readonly MainWindow _mainWindow;

        public TabManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Rebalances device distribution between tab grids and updates headers
        /// </summary>
        public void RebalanceTabDeviceGrids()
        {
            // Recording devices: filter and distribute main Devices collection to tab collections
            var filteredRecording = GetFilteredDevices(_mainWindow.Devices);
            DistributeDevices(filteredRecording, _mainWindow.RecordingDevicesLeft, _mainWindow.RecordingDevicesRight);
            
            // Playback devices: filter and distribute main PlaybackDevices collection to tab collections
            var filteredPlayback = GetFilteredDevices(_mainWindow.PlaybackDevices);
            DistributeDevices(filteredPlayback, _mainWindow.PlaybackDevicesLeft, _mainWindow.PlaybackDevicesRight);
            
            // Favorites: combine recording and playback devices that are favorited
            var allDevices = _mainWindow.Devices.Concat(_mainWindow.PlaybackDevices);
            var favoriteDevices = allDevices.Where(d => d.IsFavorite).ToList();
            
            // Use column-aware distribution for favorites
            DistributeFavoritesByColumn(favoriteDevices, _mainWindow.FavoriteDevicesLeft, _mainWindow.FavoriteDevicesRight);
            
            // Update tab headers
            UpdateTabHeaders();
        }

        /// <summary>
        /// Filters devices by removing blank entries and applying user settings
        /// </summary>
        public IEnumerable<AudioDeviceViewModel> GetFilteredDevices(IEnumerable<AudioDeviceViewModel> sourceDevices)
        {
            // Filter out blank devices
            var filtered = sourceDevices.Where(d => !string.IsNullOrWhiteSpace(d.Name) && !string.IsNullOrWhiteSpace(d.Label));
            
            // Apply HideFixedVolumeDevices setting
            var hideFixedToggle = _mainWindow.SettingsPanel?.HideFixedVolumeToggleControl;
            if (hideFixedToggle?.IsChecked == true)
            {
                filtered = filtered.Where(d => !d.IsVolumeFixed);
            }
            
            return filtered;
        }

        /// <summary>
        /// Distributes devices alternately between left and right collections
        /// </summary>
        public void DistributeDevices(IEnumerable<AudioDeviceViewModel> sourceDevices, 
                                     ObservableCollection<AudioDeviceViewModel> leftCollection, 
                                     ObservableCollection<AudioDeviceViewModel> rightCollection)
        {
            leftCollection.Clear();
            rightCollection.Clear();
            
            var deviceList = sourceDevices.ToList();
            
            // Use alternating distribution like the original single view
            for (int i = 0; i < deviceList.Count; i++)
            {
                if (i % 2 == 0)
                    leftCollection.Add(deviceList[i]);
                else
                    rightCollection.Add(deviceList[i]);
            }
        }

        /// <summary>
        /// Distributes favorite devices based on their saved column and position
        /// </summary>
        public void DistributeFavoritesByColumn(List<AudioDeviceViewModel> favoriteDevices,
                                               ObservableCollection<AudioDeviceViewModel> leftCollection,
                                               ObservableCollection<AudioDeviceViewModel> rightCollection)
        {
            leftCollection.Clear();
            rightCollection.Clear();

            // Separate devices by column
            var leftDevices = favoriteDevices.Where(d => d.FavoriteColumn == Models.FavoriteColumnType.Left)
                                           .OrderBy(d => d.FavoritePosition)
                                           .ToList();
            
            var rightDevices = favoriteDevices.Where(d => d.FavoriteColumn == Models.FavoriteColumnType.Right)
                                            .OrderBy(d => d.FavoritePosition)
                                            .ToList();

            // Add to collections
            foreach (var device in leftDevices)
            {
                leftCollection.Add(device);
            }

            foreach (var device in rightDevices)
            {
                rightCollection.Add(device);
            }

            // Handle any devices without column assignment (shouldn't happen, but safety check)
            var unassignedDevices = favoriteDevices.Except(leftDevices).Except(rightDevices).ToList();
            if (unassignedDevices.Any())
            {
                _mainWindow.AppendLog($"Found {unassignedDevices.Count} favorites without column assignment, distributing...");
                
                // Add to least populated column
                foreach (var device in unassignedDevices)
                {
                    if (leftCollection.Count <= rightCollection.Count)
                    {
                        device.FavoriteColumn = Models.FavoriteColumnType.Left;
                        device.FavoritePosition = leftCollection.Count;
                        leftCollection.Add(device);
                    }
                    else
                    {
                        device.FavoriteColumn = Models.FavoriteColumnType.Right;
                        device.FavoritePosition = rightCollection.Count;
                        rightCollection.Add(device);
                    }
                }
            }
        }

        /// <summary>
        /// Updates tab headers with monitoring counts
        /// </summary>
        public void UpdateTabHeaders()
        {
            try
            {
                // Ensure UI operations are on the UI thread
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    // Update Recording Devices tab header
                    var recordingMonitoringRun = _mainWindow.FindName("RecordingMonitoringText") as Run;
                    if (recordingMonitoringRun != null)
                    {
                        var recordingMonitoring = _mainWindow.Devices.Count(d => d.IsSelected);
                        var recordingTotal = _mainWindow.Devices.Count;
                        recordingMonitoringRun.Text = $"Monitoring {recordingMonitoring} of {recordingTotal}";
                    }
                    
                    // Update Playback Devices tab header
                    var playbackMonitoringRun = _mainWindow.FindName("PlaybackMonitoringText") as Run;
                    if (playbackMonitoringRun != null)
                    {
                        var playbackMonitoring = _mainWindow.PlaybackDevices.Count(d => d.IsSelected);
                        var playbackTotal = _mainWindow.PlaybackDevices.Count;
                        playbackMonitoringRun.Text = $"Monitoring {playbackMonitoring} of {playbackTotal}";
                    }
                    
                    // Update Favorites tab header
                    var favoritesCountRun = _mainWindow.FindName("FavoritesCountText") as Run;
                    if (favoritesCountRun != null)
                    {
                        var allDevices = _mainWindow.Devices.Concat(_mainWindow.PlaybackDevices);
                        var favoritesCount = allDevices.Count(d => d.IsFavorite);
                        favoritesCountRun.Text = favoritesCount.ToString();
                    }
                });
            }
            catch (Exception ex)
            {
                _mainWindow.AppendLog($"Error updating tab headers: {ex.Message}");
            }
        }
    }
}