// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

namespace ToxMoxMicFreezer.App.Models
{
    /// <summary>
    /// Represents peak levels for stereo audio channels
    /// </summary>
    public struct StereoPeakLevels
    {
        /// <summary>
        /// Peak level for the left channel (0.0 to 1.0)
        /// </summary>
        public float Left { get; set; }

        /// <summary>
        /// Peak level for the right channel (0.0 to 1.0)
        /// </summary>
        public float Right { get; set; }

        /// <summary>
        /// Creates a new StereoPeakLevels instance
        /// </summary>
        public StereoPeakLevels(float left, float right)
        {
            Left = left;
            Right = right;
        }

        /// <summary>
        /// Creates a mono StereoPeakLevels where both channels have the same value
        /// </summary>
        public static StereoPeakLevels Mono(float level) => new StereoPeakLevels(level, level);
    }
}