// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Globalization;
using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for volume calculations and mouse position to volume conversions
    /// Handles the mathematical aspects of volume bar interactions
    /// </summary>
    public class VolumeCalculationService : IVolumeCalculationService
    {
        private readonly ILoggingService _loggingService;
        
        // Volume range constants
        private const float MIN_VOLUME_DB = -60f;
        private const float MAX_VOLUME_DB = 12f;
        private const float VOLUME_RANGE = MAX_VOLUME_DB - MIN_VOLUME_DB; // 72dB total range

        public VolumeCalculationService(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Calculates volume in dB from mouse position within a volume bar
        /// </summary>
        public float CalculateVolumeFromPosition(System.Windows.Point mousePosition, double volumeBarWidth)
        {
            try
            {
                // Convert mouse position to percentage (0.0 to 1.0)
                double percentage = Math.Max(0.0, Math.Min(1.0, mousePosition.X / volumeBarWidth));
                
                // Convert percentage to dB value
                float volumeDb = MIN_VOLUME_DB + (float)(percentage * VOLUME_RANGE);
                
                // Clamp to valid range
                return ClampVolume(volumeDb);
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error calculating volume from position: {ex.Message}", LogLevel.Warning);
                return 0f; // Default to 0dB on error
            }
        }

        /// <summary>
        /// Calculates the display position for a given volume
        /// </summary>
        public double CalculatePositionFromVolume(float volumeDb, double volumeBarWidth)
        {
            try
            {
                // Clamp volume to valid range
                float clampedVolume = ClampVolume(volumeDb);
                
                // Convert dB to percentage
                double percentage = (clampedVolume - MIN_VOLUME_DB) / VOLUME_RANGE;
                
                // Convert percentage to position
                return percentage * volumeBarWidth;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error calculating position from volume: {ex.Message}", LogLevel.Warning);
                return 0.0; // Default to start position on error
            }
        }

        /// <summary>
        /// Validates and clamps volume to acceptable range
        /// </summary>
        public float ClampVolume(float volumeDb)
        {
            return Math.Max(MIN_VOLUME_DB, Math.Min(MAX_VOLUME_DB, volumeDb));
        }

        /// <summary>
        /// Converts volume text input to dB value
        /// </summary>
        public float? ParseVolumeText(string volumeText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(volumeText))
                    return null;

                // Remove common suffixes and whitespace
                var cleanText = volumeText.Trim().ToLower()
                    .Replace("db", "")
                    .Replace("%", "")
                    .Trim();

                // Try to parse as float
                if (float.TryParse(cleanText, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                {
                    return ClampVolume(result);
                }

                _loggingService.Log($"Invalid volume text format: {volumeText}", LogLevel.Debug);
                return null;
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error parsing volume text '{volumeText}': {ex.Message}", LogLevel.Warning);
                return null;
            }
        }
    }
}