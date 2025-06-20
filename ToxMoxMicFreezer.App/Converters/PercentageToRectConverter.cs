// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ToxMoxMicFreezer.App.Converters
{
    /// <summary>
    /// Converts percentage and container width to a Rectangle for clipping meter gradients
    /// Used to create a fixed-position gradient that gets revealed by a moving clip mask
    /// </summary>
    public class PercentageToRectConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts percentage fill and container width to a clipping rectangle
        /// </summary>
        /// <param name="values">Array containing [0] percentage (double), [1] container width (double)</param>
        /// <param name="targetType">Target type (Rect)</param>
        /// <param name="parameter">Converter parameter (unused)</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Rect defining the clip area for the gradient meter</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Validate input parameters
            if (values.Length < 2 || 
                !double.TryParse(values[0]?.ToString(), out double percentage) ||
                !double.TryParse(values[1]?.ToString(), out double containerWidth))
            {
                // Return empty rectangle if conversion fails
                return new Rect(0, 0, 0, 6);
            }

            // Clamp percentage to valid range
            percentage = Math.Max(0.0, Math.Min(1.0, percentage));
            
            // Calculate fill width based on percentage
            double fillWidth = percentage * containerWidth;
            
            // Return clipping rectangle - height of 6 matches meter bar height
            // x=0, y=0 starts from top-left, width varies with level, height is fixed
            return new Rect(0, 0, fillWidth, 6);
        }

        /// <summary>
        /// ConvertBack is not implemented as this converter is one-way only
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // One-way converter
            return new object[] { System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing };
        }
    }
}