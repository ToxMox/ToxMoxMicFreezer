// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;

namespace ToxMoxMicFreezer.App.UserInterface
{
    /// <summary>
    /// Helper class for professional audio volume calculations using device native dB ranges
    /// Provides perceptually linear volume control by using actual decibel scaling
    /// </summary>
    public static class VolumeCalculationHelper
    {
        /// <summary>
        /// Convert dB value to percentage for volume bar display using device native dB range
        /// Provides perceptually linear scaling by using actual decibel values (which are inherently logarithmic)
        /// </summary>
        /// <param name="db">Current volume in dB</param>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Percentage (0.0 to 1.0)</returns>
        public static double DbToLinearPercentage(float db, float minDb, float maxDb)
        {
            if (maxDb == minDb) return 0; // Avoid division by zero
            
            // Professional audio scaling: direct proportion in dB domain
            // The dB scale itself provides the logarithmic perceptual linearity
            return Math.Max(0, Math.Min(1, (db - minDb) / (maxDb - minDb)));
        }

        /// <summary>
        /// Convert dB value to percentage using advanced audio scaling based on device characteristics
        /// Automatically detects device type and applies appropriate scaling:
        /// - Standard Attenuation (maxDb == 0): Windows aural taper
        /// - Hybrid Devices (minDb < 0, maxDb > 0): 80/20 split (standard taper + linear gain)
        /// - Pure Gain Devices (minDb >= 0, maxDb > 0): Linear mapping
        /// </summary>
        /// <param name="db">Current volume in dB</param>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Percentage (0.0 to 1.0) using device-appropriate scaling</returns>
        public static double DbToLogarithmicPercentage(float db, float minDb, float maxDb)
        {
            if (maxDb == minDb) return 0; // Avoid division by zero
            
            // Category 1: Standard Attenuation Devices (maxDb == 0.0f)
            if (Math.Abs(maxDb - 0.0f) < 0.01f)
            {
                return StandardDbToPercent(db, minDb, maxDb);
            }
            // Category 2: Hybrid Devices (minDb < 0.0f and maxDb > 0.0f)
            else if (minDb < 0.0f && maxDb > 0.0f)
            {
                return HybridDbToPercent(db, minDb, maxDb);
            }
            // Category 3: Pure Gain Devices (minDb >= 0.0f and maxDb > 0.0f)
            else if (minDb >= 0.0f && maxDb > 0.0f)
            {
                return LinearDbToPercent(db, minDb, maxDb);
            }
            // Fallback to linear for any other case
            else
            {
                return LinearDbToPercent(db, minDb, maxDb);
            }
        }
        
        /// <summary>
        /// Convert percentage to dB value for volume setting using device native dB range
        /// Provides perceptually linear conversion by using actual decibel values
        /// </summary>
        /// <param name="percentage">Percentage (0.0 to 1.0)</param>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Volume level in dB</returns>
        public static float LinearPercentageToDb(double percentage, float minDb, float maxDb)
        {
            // Professional audio scaling: direct proportion in dB domain
            // The dB scale itself provides the logarithmic perceptual linearity
            return minDb + (float)(percentage * (maxDb - minDb));
        }

        /// <summary>
        /// Convert percentage to dB value using advanced audio scaling based on device characteristics
        /// Automatically detects device type and applies appropriate scaling:
        /// - Standard Attenuation (maxDb == 0): Windows aural taper
        /// - Hybrid Devices (minDb < 0, maxDb > 0): 80/20 split (standard taper + linear gain)
        /// - Pure Gain Devices (minDb >= 0, maxDb > 0): Linear mapping
        /// </summary>
        /// <param name="percentage">Percentage (0.0 to 1.0) from UI slider</param>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Volume level in dB</returns>
        public static float LogarithmicPercentageToDb(double percentage, float minDb, float maxDb)
        {
            // Category 1: Standard Attenuation Devices (maxDb == 0.0f)
            if (Math.Abs(maxDb - 0.0f) < 0.01f)
            {
                return StandardPercentToDb(percentage, minDb, maxDb);
            }
            // Category 2: Hybrid Devices (minDb < 0.0f and maxDb > 0.0f)
            else if (minDb < 0.0f && maxDb > 0.0f)
            {
                return HybridPercentToDb(percentage, minDb, maxDb);
            }
            // Category 3: Pure Gain Devices (minDb >= 0.0f and maxDb > 0.0f)
            else if (minDb >= 0.0f && maxDb > 0.0f)
            {
                return LinearPercentToDb(percentage, minDb, maxDb);
            }
            // Fallback to linear for any other case
            else
            {
                return LinearPercentToDb(percentage, minDb, maxDb);
            }
        }
        
        #region Advanced Audio Scaling Implementation
        
        // Windows aural taper curve data points - matches the standard Windows volume curve
        private static readonly float[] s_dbCurvePoints = new float[] {
            1.0f, 0.6095f, 0.3006f, 0.0903f, 0.021f, 0.0f
        };
        
        /// <summary>
        /// Category 1: Standard Attenuation Devices (maxDb == 0.0f)
        /// Converts dB to percentage using Windows aural taper for familiar user experience
        /// </summary>
        private static double StandardDbToPercent(float db, float minDb, float maxDb)
        {
            if (db <= minDb) return 0.0;
            if (db >= maxDb) return 1.0;
            
            // Convert dB to scalar (linear amplitude)
            double scalar = Math.Pow(10.0, db / 20.0);
            
            // Find the appropriate curve segment for interpolation
            // This is a simplified approximation of the Windows curve
            // For perfect accuracy, would need full EarTrumpet interpolation logic
            double percentage = Math.Pow(scalar, 1.0 / 2.2);
            
            return Math.Max(0.0, Math.Min(1.0, percentage));
        }
        
        /// <summary>
        /// Category 1: Standard Attenuation Devices (maxDb == 0.0f)
        /// Converts percentage to dB using Windows aural taper
        /// </summary>
        private static float StandardPercentToDb(double percentage, float minDb, float maxDb)
        {
            if (percentage <= 0) return minDb;
            if (percentage >= 1.0) return maxDb;
            
            // Apply power curve approximation of Windows taper
            double scalar = Math.Pow(percentage, 2.2);
            
            // Convert scalar to dB
            float dbValue = (float)(20.0 * Math.Log10(Math.Max(1e-10, scalar)));
            
            // Clamp to device range
            return Math.Max(minDb, Math.Min(maxDb, dbValue));
        }
        
        /// <summary>
        /// Category 2: Hybrid Devices (minDb < 0.0f and maxDb > 0.0f)
        /// Converts dB to percentage using 80/20 split (standard taper + linear gain)
        /// </summary>
        private static double HybridDbToPercent(float db, float minDb, float maxDb)
        {
            const double GainZoneStart = 0.8; // 80% as decimal
            
            if (db <= 0.0f)
            {
                // Attenuation zone (minDb to 0dB) maps to 0-80% of slider
                // Use standard Windows taper scaled to 0-80% range
                double standardPercent = StandardDbToPercent(db, minDb, 0.0f);
                return standardPercent * GainZoneStart;
            }
            else
            {
                // Gain zone (0dB to maxDb) maps to 80-100% of slider
                // Use linear mapping for predictable gain control
                double gainPercent = db / maxDb;
                return GainZoneStart + gainPercent * (1.0 - GainZoneStart);
            }
        }
        
        /// <summary>
        /// Category 2: Hybrid Devices (minDb < 0.0f and maxDb > 0.0f)
        /// Converts percentage to dB using 80/20 split (standard taper + linear gain)
        /// </summary>
        private static float HybridPercentToDb(double percentage, float minDb, float maxDb)
        {
            const double GainZoneStart = 0.8; // 80% as decimal
            
            if (percentage < GainZoneStart)
            {
                // Attenuation zone: 0-80% maps to minDb-0dB using standard taper
                double scaledPercent = percentage / GainZoneStart;
                return StandardPercentToDb(scaledPercent, minDb, 0.0f);
            }
            else
            {
                // Gain zone: 80-100% maps to 0dB-maxDb using linear mapping
                double gainPercent = (percentage - GainZoneStart) / (1.0 - GainZoneStart);
                return (float)(gainPercent * maxDb);
            }
        }
        
        /// <summary>
        /// Category 3: Pure Gain Devices (minDb >= 0.0f and maxDb > 0.0f)
        /// Converts dB to percentage using linear mapping for predictable gain control
        /// </summary>
        private static double LinearDbToPercent(float db, float minDb, float maxDb)
        {
            if (maxDb == minDb) return 0.0;
            
            // Simple linear interpolation across the entire dB range
            double percentage = (db - minDb) / (maxDb - minDb);
            return Math.Max(0.0, Math.Min(1.0, percentage));
        }
        
        /// <summary>
        /// Category 3: Pure Gain Devices (minDb >= 0.0f and maxDb > 0.0f)
        /// Converts percentage to dB using linear mapping
        /// </summary>
        private static float LinearPercentToDb(double percentage, float minDb, float maxDb)
        {
            if (percentage <= 0) return minDb;
            if (percentage >= 1.0) return maxDb;
            
            // Simple linear interpolation across the entire dB range
            return (float)(minDb + (percentage * (maxDb - minDb)));
        }
        
        #endregion
        
        /// <summary>
        /// Calculate where 0dB falls within the device's range using professional dB scaling
        /// </summary>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Position of 0dB marker (0.0 to 1.0)</returns>
        public static double GetZeroDbMarkerPosition(float minDb, float maxDb)
        {
            if (maxDb == minDb) return 0; // Avoid division by zero
            
            // Calculate 0dB position using professional dB scaling
            double zeroPosition = DbToLinearPercentage(0.0f, minDb, maxDb);
            
            // Clamp the position to valid range
            return Math.Max(0, Math.Min(1, zeroPosition));
        }

        /// <summary>
        /// Calculate where 0dB falls within the device's range using advanced audio scaling
        /// Uses the same device-type detection as the main scaling methods
        /// </summary>
        /// <param name="minDb">Minimum volume range in dB</param>
        /// <param name="maxDb">Maximum volume range in dB</param>
        /// <returns>Position of 0dB marker (0.0 to 1.0) using device-appropriate scaling</returns>
        public static double GetZeroDbMarkerPositionLogarithmic(float minDb, float maxDb)
        {
            if (maxDb == minDb) return 0; // Avoid division by zero
            
            // Use the advanced scaling to find 0dB position
            double zeroPosition = DbToLogarithmicPercentage(0.0f, minDb, maxDb);
            
            // Clamp the position to valid range
            return Math.Max(0, Math.Min(1, zeroPosition));
        }
        
        /// <summary>
        /// Convert linear amplitude (0.0-1.0) to dBFS for professional audio metering
        /// </summary>
        /// <param name="linearAmplitude">Linear amplitude from audio buffer (0.0 to 1.0)</param>
        /// <param name="floorDb">Minimum dB floor (default -60dB)</param>
        /// <returns>dBFS value</returns>
        public static float LinearAmplitudeToDbFS(float linearAmplitude, float floorDb = -60.0f)
        {
            if (linearAmplitude <= 0.0f)
            {
                return floorDb; // Return floor instead of negative infinity
            }
            
            // Convert to dBFS: 20 * log10(amplitude) 
            float dbfs = 20.0f * (float)Math.Log10(linearAmplitude);
            
            // Clamp to floor to prevent extremely negative values
            return Math.Max(floorDb, dbfs);
        }
        
        /// <summary>
        /// Convert dBFS to percentage for meter display using logarithmic scaling that matches grid markers
        /// </summary>
        /// <param name="dbfs">dBFS value</param>
        /// <param name="floorDb">Minimum dB floor (default -60dB)</param>
        /// <returns>Percentage (0.0 to 1.0) for meter display using logarithmic scaling</returns>
        public static double DbFSToMeterPercentage(float dbfs, float floorDb = -60.0f)
        {
            // Simple linear percentage calculation for simplified meter bars
            return Math.Max(0.0, Math.Min(1.0, (dbfs - floorDb) / (0.0f - floorDb)));
        }
        
        /// <summary>
        /// Determines which color zone a dBFS value falls into for professional audio metering
        /// </summary>
        /// <param name="dbfs">dBFS value</param>
        /// <returns>Color zone for the given dBFS level</returns>
        public static MeterColorZone GetMeterColorZone(float dbfs)
        {
            return dbfs switch
            {
                >= -3.0f => MeterColorZone.Danger,   // -3dB to 0dB: Red zone (high clipping risk)
                >= -6.0f => MeterColorZone.Caution,  // -6dB to -3dB: Yellow zone (approaching limits)
                _ => MeterColorZone.Safe              // -60dB to -6dB: Green zone (safe operating range)
            };
        }
        
        /// <summary>
        /// Gets the color zone boundaries for professional audio metering
        /// </summary>
        public static class MeterColorZones
        {
            public const float DangerThreshold = -3.0f;   // Red zone starts at -3dB
            public const float CautionThreshold = -6.0f;  // Yellow zone starts at -6dB
            public const float SafeThreshold = -60.0f;    // Green zone starts at -60dB (floor)
            public const float ClippingBoundary = 0.0f;   // Digital clipping occurs at 0dB
        }
        
        /// <summary>
        /// Calculates meter fill percentage with color zone information
        /// </summary>
        /// <param name="dbfs">dBFS value</param>
        /// <param name="floorDb">Minimum dB floor (default -60dB)</param>
        /// <returns>Tuple containing percentage and color zone</returns>
        public static (double Percentage, MeterColorZone Zone) GetMeterFillWithZone(float dbfs, float floorDb = -60.0f)
        {
            double percentage = DbFSToMeterPercentage(dbfs, floorDb);
            MeterColorZone zone = GetMeterColorZone(dbfs);
            return (percentage, zone);
        }
        
        /// <summary>
        /// Checks if a dBFS value indicates digital clipping
        /// </summary>
        /// <param name="dbfs">dBFS value</param>
        /// <param name="tolerance">Tolerance for clipping detection (default 0.1dB)</param>
        /// <returns>True if the signal is at or near clipping levels</returns>
        public static bool IsClipping(float dbfs, float tolerance = 0.1f)
        {
            return dbfs >= (0.0f - tolerance);
        }
        
        /// <summary>
        /// Gets a color representation for a meter color zone
        /// </summary>
        /// <param name="zone">The color zone</param>
        /// <returns>Color for the specified zone</returns>
        public static System.Windows.Media.Color GetZoneColor(MeterColorZone zone)
        {
            return zone switch
            {
                MeterColorZone.Safe => System.Windows.Media.Color.FromRgb(0, 180, 0),     // Green
                MeterColorZone.Caution => System.Windows.Media.Color.FromRgb(255, 165, 0), // Orange  
                MeterColorZone.Danger => System.Windows.Media.Color.FromRgb(220, 20, 20),  // Red
                _ => System.Windows.Media.Color.FromRgb(128, 128, 128)                     // Gray fallback
            };
        }
    }
    
    /// <summary>
    /// Color zones for professional audio metering display
    /// </summary>
    public enum MeterColorZone
    {
        /// <summary>
        /// Safe operating range (-60dB to -6dB) - Green zone
        /// </summary>
        Safe,
        
        /// <summary>
        /// Caution zone (-6dB to -3dB) - Yellow/Orange zone, approaching limits
        /// </summary>
        Caution,
        
        /// <summary>
        /// Danger zone (-3dB to 0dB) - Red zone, high risk of clipping
        /// </summary>
        Danger
    }
}