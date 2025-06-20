// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Globalization;
using System.Windows.Data;

namespace ToxMoxMicFreezer.App
{
    public class ThumbSizeConverter : IMultiValueConverter
    {
        public static readonly ThumbSizeConverter Instance = new ThumbSizeConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 3 || values[0] == null || values[1] == null)
                {
                    return values[0] ?? 100.0;
                }

                if (!double.TryParse(values[0].ToString(), out double viewportSize) ||
                    !double.TryParse(values[1].ToString(), out double maximum))
                {
                    return values[0];
                }

                if (maximum <= 0 || viewportSize <= 0)
                {
                    return viewportSize;
                }

                // Calculate what the normal thumb ratio would be
                double normalRatio = viewportSize / maximum;
                
                // Set minimum thumb size to 40% of track height
                double minimumThumbRatio = 0.4;
                double minimumViewportForThisRatio = maximum * minimumThumbRatio;
                
                // If the normal viewport would create a thumb smaller than our minimum,
                // artificially increase the viewport size to maintain minimum thumb size
                if (normalRatio < minimumThumbRatio)
                {
                    return minimumViewportForThisRatio;
                }
                
                // Otherwise, use the normal viewport size
                return viewportSize;
            }
            catch
            {
                return values[0] ?? 100.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // One-way converter
            return new object[] { System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing };
        }
    }
}