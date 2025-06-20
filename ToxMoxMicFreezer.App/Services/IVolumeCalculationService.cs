// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System.Windows;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Service responsible for volume calculations and mouse position to volume conversions
    /// Handles the mathematical aspects of volume bar interactions
    /// </summary>
    public interface IVolumeCalculationService
    {
        /// <summary>
        /// Calculates volume in dB from mouse position within a volume bar
        /// </summary>
        /// <param name="mousePosition">Mouse position relative to volume bar</param>
        /// <param name="volumeBarWidth">Width of the volume bar</param>
        /// <returns>Volume in dB (-60 to +12)</returns>
        float CalculateVolumeFromPosition(System.Windows.Point mousePosition, double volumeBarWidth);

        /// <summary>
        /// Calculates the display position for a given volume
        /// </summary>
        /// <param name="volumeDb">Volume in dB</param>
        /// <param name="volumeBarWidth">Width of the volume bar</param>
        /// <returns>Position within the volume bar</returns>
        double CalculatePositionFromVolume(float volumeDb, double volumeBarWidth);

        /// <summary>
        /// Validates and clamps volume to acceptable range
        /// </summary>
        /// <param name="volumeDb">Input volume in dB</param>
        /// <returns>Clamped volume within valid range</returns>
        float ClampVolume(float volumeDb);

        /// <summary>
        /// Converts volume text input to dB value
        /// </summary>
        /// <param name="volumeText">Text representation of volume</param>
        /// <returns>Volume in dB if valid, null if invalid</returns>
        float? ParseVolumeText(string volumeText);
    }
}