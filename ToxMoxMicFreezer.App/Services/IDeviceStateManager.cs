// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Threading.Tasks;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    public enum DeviceLoadingState
    {
        NotStarted,
        Enumerating,
        RestoringSavedState,
        RegisteringVolumes,
        Ready
    }

    public class DeviceStateChangedEventArgs : EventArgs
    {
        public DeviceLoadingState OldState { get; set; }
        public DeviceLoadingState NewState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public interface IDeviceStateManager
    {
        DeviceLoadingState CurrentState { get; }
        bool IsLoadingDevices { get; }
        
        bool IsOperationAllowed(DeviceLoadingState requiredState);
        bool TrySetLoadingState(DeviceLoadingState newState);
        
        Task InitializeDevicesAsync();
        void RestoreFrozenDevicesToSavedLevels();
        void RestoreSelectionsFromPersistentState();
        void ProcessDeviceSelectionChange(AudioDeviceViewModel device, bool isSelected);
        void SaveDeviceStates();
        void SetDevicesLoaded(bool loaded);
        void Reset();
        
        event EventHandler<DeviceStateChangedEventArgs> StateChanged;
    }
}