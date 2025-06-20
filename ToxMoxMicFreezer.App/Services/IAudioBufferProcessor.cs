// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Interface for processing raw audio buffers to calculate peak levels
    /// Provides efficient audio sample analysis for volume metering
    /// </summary>
    public interface IAudioBufferProcessor
    {
        /// <summary>
        /// Calculate peak level from raw audio buffer
        /// </summary>
        /// <param name="buffer">Raw audio buffer bytes</param>
        /// <param name="bytesRecorded">Number of valid bytes in buffer</param>
        /// <param name="sampleFormat">Audio sample format (16-bit, 32-bit float, etc.)</param>
        /// <returns>Peak level as float (0.0 to 1.0)</returns>
        float CalculatePeakLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat);

        /// <summary>
        /// Calculate RMS (Root Mean Square) level from raw audio buffer
        /// </summary>
        /// <param name="buffer">Raw audio buffer bytes</param>
        /// <param name="bytesRecorded">Number of valid bytes in buffer</param>
        /// <param name="sampleFormat">Audio sample format</param>
        /// <returns>RMS level as float (0.0 to 1.0)</returns>
        float CalculateRmsLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat);

        /// <summary>
        /// Calculate stereo peak levels from raw audio buffer
        /// </summary>
        /// <param name="buffer">Raw audio buffer bytes (interleaved stereo)</param>
        /// <param name="bytesRecorded">Number of valid bytes in buffer</param>
        /// <param name="sampleFormat">Audio sample format (16-bit, 32-bit float, etc.)</param>
        /// <param name="channels">Number of audio channels (1=mono, 2=stereo)</param>
        /// <returns>StereoPeakLevels containing left and right channel peaks</returns>
        StereoPeakLevels CalculateStereoPeakLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat, int channels);
    }

    /// <summary>
    /// Supported audio sample formats for buffer processing
    /// </summary>
    public enum AudioSampleFormat
    {
        /// <summary>16-bit signed integer samples</summary>
        Int16,
        /// <summary>32-bit signed integer samples</summary>
        Int32,
        /// <summary>32-bit floating point samples (preferred for WASAPI)</summary>
        Float32
    }
}