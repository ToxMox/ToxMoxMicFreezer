// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for persisting device selections and frozen volume states
    /// Handles loading and saving device preferences to registry
    /// </summary>
    public interface IDeviceSelectionPersistenceService
    {
        /// <summary>
        /// Loads device selection and frozen volume settings from registry
        /// </summary>
        /// <param name="recordingDevices">Recording device collection</param>
        /// <param name="playbackDevices">Playback device collection</param>
        /// <param name="persistentSelectionState">Persistent selection state set</param>
        void LoadSelection(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            HashSet<string> persistentSelectionState);

        /// <summary>
        /// Saves device selection and frozen volume settings to registry
        /// </summary>
        /// <param name="recordingDevices">Recording device collection</param>
        /// <param name="playbackDevices">Playback device collection</param>
        /// <param name="persistentSelectionState">Persistent selection state set</param>
        void SaveSelection(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            HashSet<string> persistentSelectionState);

        /// <summary>
        /// Loads frozen volume for a specific device
        /// </summary>
        /// <param name="device">Device to load frozen volume for</param>
        /// <returns>True if frozen volume was loaded</returns>
        bool LoadFrozenVolumeForDevice(AudioDeviceViewModel device);

        /// <summary>
        /// Saves frozen volume for a specific device
        /// </summary>
        /// <param name="device">Device to save frozen volume for</param>
        /// <returns>True if frozen volume was saved</returns>
        bool SaveFrozenVolumeForDevice(AudioDeviceViewModel device);

        /// <summary>
        /// Removes frozen volume entry for a device
        /// </summary>
        /// <param name="deviceId">Device ID to remove frozen volume for</param>
        /// <returns>True if entry was removed</returns>
        bool RemoveFrozenVolumeForDevice(string deviceId);
        
        /// <summary>
        /// PHASE 3 FIX: Clean up orphaned registry entries for devices that no longer exist
        /// Removes stale device fingerprints and frozen volume entries
        /// </summary>
        /// <param name="currentDevices">List of currently available devices</param>
        void CleanupOrphanedRegistryEntries(List<AudioDeviceViewModel> currentDevices);
    }
}