// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace ToxMoxMicFreezer.App
{
    // PropertyKeys for NAudio device property access
    public static class CoreAudioPropertyKeys
    {
        public static readonly NAudio.CoreAudioApi.PropertyKey PKEY_Device_DeviceDesc = new NAudio.CoreAudioApi.PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
        public static readonly NAudio.CoreAudioApi.PropertyKey PKEY_Device_FriendlyName = new NAudio.CoreAudioApi.PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    }

    public class NAudioAudioDevice : IDisposable
    {
        private MMDevice? _mmDevice;
        private bool _disposed;
        
        // PHASE 4 FIX: Cache volume range to avoid repeated COM calls during volume operations
        private (float min, float max, float step)? _cachedVolumeRange;

        public string Id { get; }
        public string FriendlyName { get; }
        public string DeviceDescription { get; }
        public uint State { get; }
        public uint DataFlow { get; }

        public bool IsActive => (State & (uint)NAudio.CoreAudioApi.DeviceState.Active) != 0;
        public bool IsRender => DataFlow == (uint)NAudio.CoreAudioApi.DataFlow.Render;
        public bool IsCapture => DataFlow == (uint)NAudio.CoreAudioApi.DataFlow.Capture;
        
        // Expose AudioEndpointVolume for volume change callbacks
        public AudioEndpointVolume? AudioEndpointVolume => _mmDevice?.AudioEndpointVolume;
        
        // Expose the underlying MMDevice for logging purposes
        public MMDevice? MMDevice => _mmDevice;
        
        // Get the number of audio channels for this device
        public int GetChannelCount()
        {
            if (_disposed || _mmDevice == null) return 1; // Default to mono if can't determine
            
            string deviceType = _mmDevice.DataFlow == NAudio.CoreAudioApi.DataFlow.Capture ? "Recording" : "Playback";
            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ========== Getting channel count for {deviceType} device: {FriendlyName} ==========");
            
            try
            {
                // Method 1: Try AudioEndpointVolume channel count (most direct and reliable)
                try
                {
                    var endpointVolume = _mmDevice.AudioEndpointVolume;
                    if (endpointVolume != null)
                    {
                        int channelCount = endpointVolume.Channels.Count;
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 1 - AudioEndpointVolume.Channels.Count: {channelCount} channels");
                        
                        if (channelCount >= 1 && channelCount <= 32)
                        {
                            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Using Method 1 result: {channelCount} channels");
                            return (int)channelCount;
                        }
                    }
                }
                catch (Exception volEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 1 - Error getting volume channel count: {volEx.Message}");
                }
                
                // Method 2: Try to get from AudioClient MixFormat with WAVEFORMATEXTENSIBLE support
                var audioClient = _mmDevice.AudioClient;
                if (audioClient?.MixFormat != null)
                {
                    var mixFormat = audioClient.MixFormat;
                    int baseChannels = mixFormat.Channels;
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 2 - AudioClient.MixFormat: {baseChannels} channels (base)");
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Sample Rate: {mixFormat.SampleRate} Hz");
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Bits Per Sample: {mixFormat.BitsPerSample}");
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Block Align: {mixFormat.BlockAlign}");
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Extra Size: {mixFormat.ExtraSize} bytes");
                    
                    // Check if this is WAVEFORMATEXTENSIBLE (ExtraSize >= 22)
                    if (mixFormat.ExtraSize >= 22 && mixFormat.Encoding == NAudio.Wave.WaveFormatEncoding.Extensible)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   WAVEFORMATEXTENSIBLE detected (will parse in Method 5)");
                    }
                    
                    // Fall back to base channel count if reasonable
                    if (baseChannels >= 1 && baseChannels <= 32)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Using Method 2 base channel count: {baseChannels} channels");
                        return baseChannels;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 2 - AudioClient.MixFormat is null");
                }
                
                // Method 3: Try device properties for physical speaker configuration
                try
                {
                    // PKEY_AudioEndpoint_PhysicalSpeakers - indicates actual speaker configuration
                    var speakerConfigKey = new PropertyKey(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 3);
                    if (_mmDevice.Properties.Contains(speakerConfigKey))
                    {
                        var speakerConfig = _mmDevice.Properties[speakerConfigKey];
                        if (speakerConfig?.Value != null)
                        {
                            uint speakers = Convert.ToUInt32(speakerConfig.Value);
                            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 3 - Physical Speakers Config: 0x{speakers:X8}");
                            
                            // Count bits to determine number of speakers/channels
                            int channelCount = CountChannelsFromSpeakerMask(speakers);
                            if (channelCount > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Physical speaker configuration indicates {channelCount} channels");
                                return channelCount;
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 3 - Physical Speakers property not found");
                    }
                }
                catch (Exception propEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 3 - Error reading speaker config: {propEx.Message}");
                }
                
                // Method 4: For recording devices, try creating a temporary capture
                if (_mmDevice.DataFlow == NAudio.CoreAudioApi.DataFlow.Capture)
                {
                    try
                    {
                        using (var tempCapture = new WasapiCapture(_mmDevice))
                        {
                            int channels = tempCapture.WaveFormat.Channels;
                            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 4 - WasapiCapture.WaveFormat: {channels} channels");
                            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Using Method 4 result: {channels} channels");
                            return channels;
                        }
                    }
                    catch (Exception captureEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 4 - Failed to create temporary capture: {captureEx.Message}");
                    }
                }
                
                // Method 5: Try AudioEngine device format property
                try
                {
                    // PKEY_AudioEngine_DeviceFormat - internal device format
                    var deviceFormatKey = new PropertyKey(new Guid("f19f064d-082c-4e27-bc73-6882a1bb8e4c"), 0);
                    if (_mmDevice.Properties.Contains(deviceFormatKey))
                    {
                        var formatProp = _mmDevice.Properties[deviceFormatKey];
                        if (formatProp?.Value != null && formatProp.Value is byte[] formatBytes && formatBytes.Length >= 18)
                        {
                            // Check for WAVEFORMATEXTENSIBLE (size check)
                            if (formatBytes.Length >= 40) // WAVEFORMATEXTENSIBLE is larger
                            {
                                // WAVEFORMATEXTENSIBLE: dwChannelMask is at offset 20
                                uint channelMask = BitConverter.ToUInt32(formatBytes, 20);
                                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 5 - AudioEngine DeviceFormat (WAVEFORMATEXTENSIBLE)");
                                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Channel Mask: 0x{channelMask:X8}");
                                
                                int maskChannels = CountChannelsFromSpeakerMask(channelMask);
                                if (maskChannels > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Using Method 5 WAVEFORMATEXTENSIBLE result: {maskChannels} channels");
                                    return maskChannels;
                                }
                            }
                            
                            // Fall back to standard WAVEFORMATEX
                            int channels = BitConverter.ToInt16(formatBytes, 2);
                            System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 5 - AudioEngine DeviceFormat (WAVEFORMATEX): {channels} channels");
                            
                            if (channels >= 1 && channels <= 32)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✓ Using Method 5 result: {channels} channels");
                                return channels;
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 5 - AudioEngine DeviceFormat property not found");
                    }
                }
                catch (Exception formatEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Method 5 - Error reading device format: {formatEx.Message}");
                }
                
                // Default based on device type
                int defaultChannels = _mmDevice.DataFlow == NAudio.CoreAudioApi.DataFlow.Capture ? 2 : 2; // Default to stereo for both
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ✗ All methods failed, using default: {defaultChannels} channels");
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] ========== Channel detection complete ==========");
                return defaultChannels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] CRITICAL ERROR getting channel count for {FriendlyName}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Stack trace: {ex.StackTrace}");
                // Default to stereo
                return 2;
            }
        }

        // Helper method to count channels from speaker mask
        private int CountChannelsFromSpeakerMask(uint speakerMask)
        {
            int count = 0;
            
            
            // Count set bits
            for (int i = 0; i < 32; i++)
            {
                if ((speakerMask & (1u << i)) != 0)
                {
                    count++;
                }
            }
            
            // Common configurations with detailed logging
            if (speakerMask == 0x3) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Stereo (2.0) configuration detected");
            else if (speakerMask == 0x7) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   2.1 configuration detected (L+R+LFE)");
            else if (speakerMask == 0xF) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Quadraphonic (4.0) configuration detected");
            else if (speakerMask == 0x3F) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   5.1 surround configuration detected");
            else if (speakerMask == 0x63F) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   7.1 surround configuration detected");
            else if (speakerMask == 0xFF) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   7.1 surround (alternate) configuration detected");
            else if (speakerMask == 0x6FF) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   7.1.2 Atmos configuration detected");
            else if (speakerMask == 0x7FF) 
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   7.1.4 Atmos configuration detected");
            else if (count == 8)
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   8-channel configuration detected (mask: 0x{speakerMask:X})");
            else
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice]   Custom {count}-channel configuration (mask: 0x{speakerMask:X})");
            
            return count;
        }

        internal NAudioAudioDevice(MMDevice device)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Creating wrapper for device: {device?.ID ?? "null"}");
                
                _mmDevice = device ?? throw new ArgumentNullException(nameof(device));

                Id = device.ID;
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Device ID: {Id}");
                
                FriendlyName = device.FriendlyName ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] FriendlyName: {FriendlyName}");

                // Attempt to get PKEY_Device_DeviceDesc
                try
                {
                    if (device.Properties.Contains(CoreAudioPropertyKeys.PKEY_Device_DeviceDesc))
                    {
                        DeviceDescription = device.Properties[CoreAudioPropertyKeys.PKEY_Device_DeviceDesc].Value as string ?? string.Empty;
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] DeviceDescription from PKEY: {DeviceDescription}");
                    }
                    else
                    {
                        DeviceDescription = FriendlyName;
                        System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] PKEY_Device_DeviceDesc not found, using FriendlyName as fallback");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error getting PKEY_Device_DeviceDesc for {Id}: {ex.Message}");
                    DeviceDescription = FriendlyName;
                }
                
                State = (uint)device.State;
                DataFlow = (uint)device.DataFlow;
                
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Device created successfully - State: {State}, DataFlow: {DataFlow}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Exception in constructor: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public float GetVolumeLevel()
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return 0f;
            try
            {
                return _mmDevice.AudioEndpointVolume.MasterVolumeLevel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error GetVolumeLevel for {Id}: {ex.Message}");
                return 0f;
            }
        }

        public void SetVolumeLevel(float levelDb)
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return;
            try
            {
                var range = GetVolumeRange();
                if (levelDb < range.min) levelDb = range.min;
                if (levelDb > range.max) levelDb = range.max;
                
                _mmDevice.AudioEndpointVolume.MasterVolumeLevel = levelDb;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error SetVolumeLevel for {Id}: {ex.Message}");
            }
        }

        public (float min, float max, float increment) GetVolumeRange()
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return (0f, 0f, 0f);
            
            // Return cached range if available
            if (_cachedVolumeRange.HasValue)
                return _cachedVolumeRange.Value;
                
            try
            {
                var volRange = _mmDevice.AudioEndpointVolume.VolumeRange;
                // NAudio doesn't expose StepDecibels, use a reasonable default
                var range = (volRange.MinDecibels, volRange.MaxDecibels, 1.0f);
                
                // Cache the result for future calls
                _cachedVolumeRange = range;
                
                return range;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error GetVolumeRange for {Id}: {ex.Message}");
                return (0f, 0f, 0f);
            }
        }

        public float GetPeakValue()
        {
            if (_disposed || _mmDevice?.AudioMeterInformation == null) return 0f;
            try
            {
                return _mmDevice.AudioMeterInformation.MasterPeakValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error GetPeakValue for {Id}: {ex.Message}");
                return 0f;
            }
        }

        public bool GetMuteState()
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return false;
            try
            {
                return _mmDevice.AudioEndpointVolume.Mute;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error GetMuteState for {Id}: {ex.Message}");
                return false;
            }
        }

        public void SetMuteState(bool muted)
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return;
            try
            {
                _mmDevice.AudioEndpointVolume.Mute = muted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error SetMuteState for {Id}: {ex.Message}");
            }
        }

        public bool ToggleMute()
        {
            if (_disposed || _mmDevice?.AudioEndpointVolume == null) return false;
            try
            {
                bool newState = !_mmDevice.AudioEndpointVolume.Mute;
                _mmDevice.AudioEndpointVolume.Mute = newState;
                return newState;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Error ToggleMute for {Id}: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_mmDevice != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[NAudioAudioDevice] Disposing MMDevice for {Id}");
                    _mmDevice.Dispose();
                    _mmDevice = null;
                }
            }
            _disposed = true;
        }
    }

    public class NAudioDeviceEnumerator : IDisposable
    {
        private bool _disposed;

        public NAudioDeviceEnumerator()
        {
            System.Diagnostics.Debug.WriteLine("[NAudioDeviceEnumerator] Created - NAudio handles COM initialization internally");
        }

        public List<NAudioAudioDevice> EnumerateAudioDevices(uint dataFlow, uint stateMask = (uint)NAudio.CoreAudioApi.DeviceState.Active)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NAudioDeviceEnumerator));

            var audioDevices = new List<NAudioAudioDevice>();
            MMDeviceEnumerator? enumerator = null;

            try
            {
                var naudioDataFlow = (NAudio.CoreAudioApi.DataFlow)dataFlow;
                var naudioStateMask = (NAudio.CoreAudioApi.DeviceState)stateMask;
                
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Starting NAudio enumeration - DataFlow: {naudioDataFlow}, StateMask: {naudioStateMask}");
                
                enumerator = new MMDeviceEnumerator();
                
                foreach (var device in enumerator.EnumerateAudioEndPoints(naudioDataFlow, naudioStateMask))
                {
                    try
                    {
                        var wrappedDevice = new NAudioAudioDevice(device);
                        audioDevices.Add(wrappedDevice);
                        System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Successfully wrapped device: {wrappedDevice.FriendlyName} (ID: {wrappedDevice.Id})");
                    }
                    catch (Exception deviceEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Failed to wrap device {device?.ID ?? "unknown"}: {deviceEx.GetType().Name}: {deviceEx.Message}");
                        device?.Dispose(); // Dispose the NAudio device if wrapping failed
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Exception during enumeration: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Stack trace: {ex.StackTrace}");
                
                // Clean up any devices already added if an error occurs mid-enumeration
                foreach (var ad in audioDevices) ad.Dispose();
                audioDevices.Clear();
            }
            finally
            {
                enumerator?.Dispose();
            }
            
            System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] NAudio enumeration complete, returning {audioDevices.Count} devices");
            return audioDevices;
        }

        public NAudioAudioDevice? GetDevice(string deviceId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NAudioDeviceEnumerator));
            if (string.IsNullOrEmpty(deviceId))
                return null;

            MMDeviceEnumerator? enumerator = null;
            MMDevice? device = null;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Getting device by ID: {deviceId}");
                
                enumerator = new MMDeviceEnumerator();
                device = enumerator.GetDevice(deviceId);
                
                if (device != null)
                {
                    var wrappedDevice = new NAudioAudioDevice(device);
                    System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Successfully retrieved and wrapped device: {wrappedDevice.FriendlyName}");
                    return wrappedDevice;
                }
                
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Device {deviceId} not found");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NAudioDeviceEnumerator] Error getting device {deviceId}: {ex.GetType().Name}: {ex.Message}");
                device?.Dispose();
                return null;
            }
            finally
            {
                enumerator?.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            System.Diagnostics.Debug.WriteLine("[NAudioDeviceEnumerator] Disposing - no persistent COM objects held");
            _disposed = true;
        }
    }
}