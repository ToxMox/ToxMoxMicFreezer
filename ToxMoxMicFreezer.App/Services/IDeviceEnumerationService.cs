// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for enumerating audio devices
    /// Handles device discovery and initial setup
    /// </summary>
    public interface IDeviceEnumerationService
    {
        /// <summary>
        /// Enumerates recording devices using NAudio
        /// </summary>
        /// <param name="currentActiveDevices">Set to track active device IDs</param>
        /// <param name="nativeDeviceCache">Cache for storing native device instances</param>
        /// <param name="persistentSelectionState">Set of persistently selected device IDs</param>
        /// <returns>List of recording device view models</returns>
        List<AudioDeviceViewModel> EnumerateRecordingDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState);

        /// <summary>
        /// Enumerates playback devices using NAudio
        /// </summary>
        /// <param name="currentActiveDevices">Set to track active device IDs</param>
        /// <param name="nativeDeviceCache">Cache for storing native device instances</param>
        /// <param name="persistentSelectionState">Set of persistently selected device IDs</param>
        /// <returns>List of playback device view models</returns>
        List<AudioDeviceViewModel> EnumeratePlaybackDevices(
            HashSet<string> currentActiveDevices,
            ConcurrentDictionary<string, NAudioAudioDevice> nativeDeviceCache,
            HashSet<string> persistentSelectionState);
    }
}