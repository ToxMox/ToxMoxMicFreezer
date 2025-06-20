// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using NAudio.CoreAudioApi;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    public class VolumeChangeEvent
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public float PreviousVolume { get; set; }
        public float NewVolume { get; set; }
        public bool IsDeviceFrozen { get; set; }
        public bool IsExternalChange { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public interface IVolumeEnforcementService
    {
        void ProcessInternalVolumeChange(VolumeChangeEvent volumeEvent);
        void ProcessExternalVolumeChange(VolumeChangeEvent volumeEvent);
        float CalculateTargetVolume(NAudioAudioDevice device, AudioDeviceViewModel deviceViewModel);
        bool ShouldEnforceVolume(AudioDeviceViewModel device);
        void EnforceVolumeForDevice(string deviceId);
        
        event EventHandler<VolumeChangeEvent> VolumeEnforced;
    }
}