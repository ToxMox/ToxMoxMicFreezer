// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ToxMoxMicFreezer.App.Services;

namespace ToxMoxMicFreezer.App.Models
{
    /// <summary>
    /// Device type enumeration
    /// </summary>
    public enum AudioDeviceType
    {
        Recording,
        Playback
    }

    /// <summary>
    /// Column type for favorites tab
    /// </summary>
    public enum FavoriteColumnType
    {
        Left,
        Right
    }

    /// <summary>
    /// View model for audio device representation in the UI
    /// Handles device properties, volume state, and selection management
    /// </summary>
    public class AudioDeviceViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public AudioDeviceType DeviceType { get; set; } = AudioDeviceType.Recording;
        
        // Favorites properties
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set 
            { 
                _isFavorite = value; 
                OnPropertyChanged(nameof(IsFavorite));
                
                // Fire event for favorites change to trigger UI updates
                if (!IsLoadingDevices)
                {
                    FavoritesChanged?.Invoke(this, value);
                }
            }
        }
        
        public int FavoriteOrder { get; set; } = int.MaxValue; // Default to end of list
        
        // New favorites drag & drop properties
        private FavoriteColumnType _favoriteColumn = FavoriteColumnType.Left;
        public FavoriteColumnType FavoriteColumn
        {
            get => _favoriteColumn;
            set
            {
                _favoriteColumn = value;
                OnPropertyChanged(nameof(FavoriteColumn));
            }
        }
        
        private int _favoritePosition = 0;
        public int FavoritePosition
        {
            get => _favoritePosition;
            set
            {
                _favoritePosition = value;
                OnPropertyChanged(nameof(FavoritePosition));
            }
        }
        
        // Cached fingerprint for performance optimization - avoids recalculating during volume operations
        private string? _cachedFingerprint = null;
        public string? CachedFingerprint 
        { 
            get => _cachedFingerprint; 
            set => _cachedFingerprint = value; 
        }
        
        // Removed complex logarithmic scaling methods - replaced with simple linear scaling
        // in UserInterface.VolumeCalculationHelper for better performance and responsiveness
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { 
                _isSelected = value; 
                OnPropertyChanged(nameof(IsSelected)); 
                
                // Only fire SelectionChanged event for user interactions, not during device loading/restoration
                if (!IsLoadingDevices)
                {
                    SelectionChanged?.Invoke(this, value);
                }
            }
        }
        
        private string _volumeDb = "0.0";
        public string VolumeDb
        {
            get => _volumeDb;
            set { 
                _volumeDb = value; 
                OnPropertyChanged(nameof(VolumeDb)); 
                OnPropertyChanged(nameof(VolumeBarFillPercentage)); 
                OnPropertyChanged(nameof(CurrentVolumeDbValue));
                OnPropertyChanged(nameof(IsCurrentVolumeInPositiveRange));
                OnPropertyChanged(nameof(IsCurrentVolumeInDangerZone));
            }
        }
        
        // Volume range properties for the device
        private float _minVolumeDb = -60.0f;
        private float _maxVolumeDb = 6.0f;
        
        public float MinVolumeDb 
        { 
            get => _minVolumeDb; 
            set 
            { 
                _minVolumeDb = value; 
                OnPropertyChanged(nameof(MinVolumeDb)); 
                OnPropertyChanged(nameof(VolumeBarFillPercentage)); 
                OnPropertyChanged(nameof(ZeroDbMarkerPosition));
                OnPropertyChanged(nameof(HasVolumeRange));
                OnPropertyChanged(nameof(IsVolumeFixed));
            } 
        }
        
        public float MaxVolumeDb 
        { 
            get => _maxVolumeDb; 
            set 
            { 
                _maxVolumeDb = value; 
                OnPropertyChanged(nameof(MaxVolumeDb)); 
                OnPropertyChanged(nameof(VolumeBarFillPercentage)); 
                OnPropertyChanged(nameof(ZeroDbMarkerPosition));
                OnPropertyChanged(nameof(HasVolumeRange));
                OnPropertyChanged(nameof(IsVolumeFixed));
                OnPropertyChanged(nameof(IsGainDevice));
                OnPropertyChanged(nameof(IsCurrentVolumeInDangerZone));
            } 
        }
        
        // Mute state properties - uses real OS-level mute via AudioEndpointVolume
        private bool _isMuted = false;
        public bool IsMuted
        {
            get => _isMuted;
            set 
            { 
                _isMuted = value; 
                OnPropertyChanged(nameof(IsMuted)); 
                OnPropertyChanged(nameof(MuteIconGlyph));
            }
        }
        
        /// <summary>
        /// FontAwesome glyph for mute button: muted (speaker-slash) or unmuted (volume-up)
        /// </summary>
        public string MuteIconGlyph => IsMuted ? "\uf6a9" : "\uf028"; // muted : unmuted
        
        // Frozen volume setting - stores the volume to lock to when selected
        public float FrozenVolumeDb { get; set; } = 0.0f;
        
        // Volume throttling state properties to track hardware call timing
        public bool IsVolumeThrottled { get; set; } = false;
        public DateTime LastHardwareVolumeChange { get; set; } = DateTime.MinValue;
        
        // Audio metering properties for real-time peak level monitoring
        private float _peakLevel = 0.0f;
        public float PeakLevel
        {
            get => _peakLevel;
            set { 
                _peakLevel = value; 
                OnPropertyChanged(nameof(PeakLevel)); 
                OnPropertyChanged(nameof(MeterBarFillPercentage));
                OnPropertyChanged(nameof(MeterGradientBrush));
            }
        }

        // Stereo audio metering properties
        private float _leftPeakLevel = 0.0f;
        public float LeftPeakLevel
        {
            get => _leftPeakLevel;
            set { 
                _leftPeakLevel = value; 
                OnPropertyChanged(nameof(LeftPeakLevel)); 
                OnPropertyChanged(nameof(LeftMeterBarFillPercentage));
                OnPropertyChanged(nameof(LeftMeterGradientBrush));
            }
        }

        private float _rightPeakLevel = 0.0f;
        public float RightPeakLevel
        {
            get => _rightPeakLevel;
            set { 
                _rightPeakLevel = value; 
                OnPropertyChanged(nameof(RightPeakLevel)); 
                OnPropertyChanged(nameof(RightMeterBarFillPercentage));
                OnPropertyChanged(nameof(RightMeterGradientBrush));
            }
        }

        // Channel count for determining mono/stereo display
        private int _channels = 1;
        private bool _isMultiChannel = false; // Set once during initial enumeration
        
        public int Channels
        {
            get => _channels;
            set { 
                _channels = value; 
                OnPropertyChanged(nameof(Channels)); 
                OnPropertyChanged(nameof(IsStereo));
                // Note: IsMultiChannel is NOT updated here - it's set once during enumeration
            }
        }

        public bool IsStereo => _channels > 1;
        
        // IsMultiChannel is set once during device enumeration and never changes
        // This prevents audio metering from changing the multi-channel status
        public bool IsMultiChannel 
        { 
            get => _isMultiChannel;
            set 
            {
                _isMultiChannel = value;
                OnPropertyChanged(nameof(IsMultiChannel));
            }
        }
        
        // Property to check if device has adjustable volume
        public bool HasVolumeRange => Math.Abs(MaxVolumeDb - MinVolumeDb) > 0.1f; // More than 0.1dB range
        public bool IsVolumeFixed => !HasVolumeRange;
        
        // Calculated property for volume bar fill percentage using logarithmic audio taper
        public double VolumeBarFillPercentage
        {
            get
            {
                if (float.TryParse(_volumeDb, out float currentVolume))
                {
                    if (MaxVolumeDb == MinVolumeDb) return 0; // Avoid division by zero
                    return UserInterface.VolumeCalculationHelper.DbToLogarithmicPercentage(currentVolume, MinVolumeDb, MaxVolumeDb);
                }
                return 0;
            }
        }
        
        // Calculated property for 0dB marker position using logarithmic audio taper (percentage from left)
        public double ZeroDbMarkerPosition
        {
            get
            {
                if (MaxVolumeDb == MinVolumeDb) return 0; // Avoid division by zero
                
                // Calculate where 0dB falls within the device's range using logarithmic audio taper
                return UserInterface.VolumeCalculationHelper.GetZeroDbMarkerPositionLogarithmic(MinVolumeDb, MaxVolumeDb);
            }
        }
        
        // Properties for gain device detection and positive range handling
        public bool IsGainDevice => MaxVolumeDb > 0.1f; // Device supports amplification above 0dB
        
        public float CurrentVolumeDbValue
        {
            get
            {
                if (float.TryParse(_volumeDb, out float currentVolume))
                    return currentVolume;
                return 0.0f;
            }
        }
        
        public bool IsCurrentVolumeInPositiveRange => CurrentVolumeDbValue > 0.0f;
        
        public bool IsCurrentVolumeInDangerZone => IsGainDevice && IsCurrentVolumeInPositiveRange;
        
        // Calculated property for audio meter bar fill percentage using professional dBFS metering
        public double MeterBarFillPercentage
        {
            get
            {
                if (PeakLevel <= 0.0f) return 0.0;
                
                // Convert linear peak level (0.0-1.0) to dBFS using professional audio standards
                // Uses -60dB floor for meaningful dynamic range visualization
                float peakDbfs = UserInterface.VolumeCalculationHelper.LinearAmplitudeToDbFS(PeakLevel, -60.0f);
                
                // Convert dBFS to meter percentage (0dB = 100%, -60dB = 0%)
                return UserInterface.VolumeCalculationHelper.DbFSToMeterPercentage(peakDbfs, -60.0f);
            }
        }
        
        // Stereo meter bar fill percentages
        public double LeftMeterBarFillPercentage
        {
            get
            {
                if (LeftPeakLevel <= 0.0f) return 0.0;
                
                // Convert linear peak level (0.0-1.0) to dBFS using professional audio standards
                float peakDbfs = UserInterface.VolumeCalculationHelper.LinearAmplitudeToDbFS(LeftPeakLevel, -60.0f);
                
                // Convert dBFS to meter percentage (0dB = 100%, -60dB = 0%)
                return UserInterface.VolumeCalculationHelper.DbFSToMeterPercentage(peakDbfs, -60.0f);
            }
        }

        public double RightMeterBarFillPercentage
        {
            get
            {
                if (RightPeakLevel <= 0.0f) return 0.0;
                
                // Convert linear peak level (0.0-1.0) to dBFS using professional audio standards
                float peakDbfs = UserInterface.VolumeCalculationHelper.LinearAmplitudeToDbFS(RightPeakLevel, -60.0f);
                
                // Convert dBFS to meter percentage (0dB = 100%, -60dB = 0%)
                return UserInterface.VolumeCalculationHelper.DbFSToMeterPercentage(peakDbfs, -60.0f);
            }
        }

        /// <summary>
        /// Dynamic gradient brush for volume meter based on current peak level and dBFS zones
        /// Green → Yellow → Red gradient that updates in real-time with audio levels
        /// </summary>
        public System.Windows.Media.Brush MeterGradientBrush
        {
            get
            {
                // Create a horizontal linear gradient brush
                var gradientBrush = new System.Windows.Media.LinearGradientBrush();
                gradientBrush.StartPoint = new System.Windows.Point(0, 0);
                gradientBrush.EndPoint = new System.Windows.Point(1, 0);
                
                // Calculate zone percentages in the -60dB to 0dB range
                // Professional audio dBFS zone boundaries: Green (-60 to -6dB), Yellow (-6 to -3dB), Red (-3 to 0dB)
                double safeZonePercent = (-6.0 + 60.0) / 60.0;      // 90% (0% to 90%)
                double cautionZonePercent = (-3.0 + 60.0) / 60.0;   // 95% (90% to 95%)
                double dangerZonePercent = 1.0;                     // 100% (95% to 100%)
                
                // Add gradient stops for professional color zones
                gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(0, 180, 0), 0.0));                    // Start: Green
                gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(0, 180, 0), safeZonePercent));        // 90%: Green
                gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(255, 165, 0), cautionZonePercent));   // 95%: Yellow/Orange
                gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(220, 20, 20), dangerZonePercent));    // 100%: Red
                
                // Freeze the brush for performance (it's read-only)
                gradientBrush.Freeze();
                
                return gradientBrush;
            }
        }

        // Stereo gradient brushes (using same gradient as mono)
        public System.Windows.Media.Brush LeftMeterGradientBrush => MeterGradientBrush;
        public System.Windows.Media.Brush RightMeterGradientBrush => MeterGradientBrush;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        
        // Static event for selection changes - maintains existing pattern
        public static Action<AudioDeviceViewModel, bool>? SelectionChanged;
        public static Action<AudioDeviceViewModel, bool>? FavoritesChanged;
        public static bool IsLoadingDevices { get; set; } = false;
    }
}