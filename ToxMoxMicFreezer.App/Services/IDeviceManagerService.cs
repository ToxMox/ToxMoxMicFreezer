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
    /// Coordinating service that manages all device-related operations
    /// Orchestrates device enumeration, collection management, and event handling
    /// </summary>
    public interface IDeviceManagerService : IDisposable
    {
        /// <summary>
        /// Performs complete device enumeration and collection updates
        /// </summary>
        /// <param name="recordingDevices">Recording device collection</param>
        /// <param name="playbackDevices">Playback device collection</param>
        /// <param name="deviceCache">Device cache for view models</param>
        /// <param name="nativeDeviceCache">Native device cache</param>
        /// <param name="persistentSelectionState">Set of persistently selected device IDs</param>
        /// <returns>Tuple of (recording count, playback count, removed count)</returns>
        (int recordingCount, int playbackCount, int removedCount) EnumerateAndUpdateDevices(
            ObservableCollection<AudioDeviceViewModel> recordingDevices,
            ObservableCollection<AudioDeviceViewModel> playbackDevices,
            ConcurrentDictionary<string, AudioDeviceViewModel> deviceCache,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState);

        /// <summary>
        /// Gets a native audio device by ID
        /// </summary>
        NAudioAudioDevice? GetCachedNativeDevice(string deviceId);
    }
}