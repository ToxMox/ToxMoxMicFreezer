// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for managing audio device collections
    /// Handles ObservableCollection updates and device organization
    /// </summary>
    public interface IDeviceCollectionService
    {
        /// <summary>
        /// Updates the recording device collection with new devices
        /// </summary>
        /// <param name="deviceList">List of new recording devices</param>
        /// <param name="recordingDevices">Recording device collection to update</param>
        /// <param name="deviceCache">Device cache for storing view models</param>
        /// <param name="nativeDeviceCache">Native device cache for cleanup</param>
        /// <param name="persistentSelectionState">Set of persistently selected device IDs</param>
        void UpdateRecordingDevices(
            List<AudioDeviceViewModel> deviceList,
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState);

        /// <summary>
        /// Updates the playback device collection with new devices
        /// </summary>
        /// <param name="deviceList">List of new playback devices</param>
        /// <param name="playbackDevices">Playback device collection to update</param>
        /// <param name="deviceCache">Device cache for storing view models</param>
        /// <param name="nativeDeviceCache">Native device cache for cleanup</param>
        void UpdatePlaybackDevices(
            List<AudioDeviceViewModel> deviceList,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache);

        /// <summary>
        /// Removes stale devices from cache and collections
        /// </summary>
        /// <param name="currentActiveDevices">Set of currently active device IDs</param>
        /// <param name="deviceCache">Device cache for view models</param>
        /// <param name="nativeDeviceCache">Native device cache for cleanup</param>
        /// <param name="recordingDevices">Recording device collection</param>
        /// <param name="playbackDevices">Playback device collection</param>
        /// <returns>Number of devices removed</returns>
        int RemoveStaleDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices);
    }
}