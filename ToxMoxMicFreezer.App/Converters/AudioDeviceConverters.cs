// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ToxMoxMicFreezer.App.Converters
{
    /// <summary>
    /// Converts volume value to boolean indicating if it's at zero
    /// </summary>
    public class VolumeToZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string volumeStr)
            {
                // Check if the volume is 0.0
                if (float.TryParse(volumeStr, out float volume))
                {
                    return Math.Abs(volume) < 0.1f; // Returns true if volume is close to zero
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter
            return System.Windows.Data.Binding.DoNothing;
        }
    }


    /// <summary>
    /// Multi-value converter that uses percentage and container width to calculate position or width
    /// </summary>
    public class PercentageToWidthMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double percentage && values[1] is double containerWidth)
            {
                double result = percentage * containerWidth;
                
                // If targeting Thickness (for Margin), return as left margin
                if (targetType == typeof(Thickness))
                {
                    return new Thickness(result, 0, 0, 0);
                }
                
                // Otherwise return as width
                return result;
            }
            
            return targetType == typeof(Thickness) ? new Thickness(0) : 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // One-way converter
            return new object[] { System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing };
        }
    }

    /// <summary>
    /// Converts volume bar theme colors based on parameter type
    /// </summary>
    public class VolumeBarColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string colorType = parameter?.ToString() ?? "Fill";
            
            return colorType switch
            {
                "Fill" => "#C4B5FD",
                "Empty" => "#777777",
                "Marker" => "#FF0080",
                _ => "#DDD6FE"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter
            return System.Windows.Data.Binding.DoNothing;
        }
    }

}