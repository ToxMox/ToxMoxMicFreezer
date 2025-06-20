// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 CrunchFocus LLC

using System;
using NAudio.Wave;
using ToxMoxMicFreezer.App.Models;

namespace ToxMoxMicFreezer.App.Services
{
    /// <summary>
    /// Efficient audio buffer processor for calculating peak and RMS levels from raw audio samples
    /// Optimized for different sample formats commonly used in Windows audio
    /// </summary>
    public class AudioBufferProcessor : IAudioBufferProcessor
    {
        private readonly ILoggingService? _loggingService;

        public AudioBufferProcessor(ILoggingService? loggingService = null)
        {
            _loggingService = loggingService;
        }

        /// <summary>
        /// Calculate peak level from raw audio buffer with format-specific processing
        /// </summary>
        public float CalculatePeakLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat)
        {
            if (buffer == null || bytesRecorded <= 0)
                return 0.0f;

            try
            {
                return sampleFormat switch
                {
                    AudioSampleFormat.Float32 => CalculatePeakFromFloat32(buffer, bytesRecorded),
                    AudioSampleFormat.Int16 => CalculatePeakFromInt16(buffer, bytesRecorded),
                    AudioSampleFormat.Int32 => CalculatePeakFromInt32(buffer, bytesRecorded),
                    _ => 0.0f
                };
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error calculating peak level: {ex.Message}", LogLevel.Debug);
                return 0.0f;
            }
        }

        /// <summary>
        /// Calculate RMS level from raw audio buffer
        /// </summary>
        public float CalculateRmsLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat)
        {
            if (buffer == null || bytesRecorded <= 0)
                return 0.0f;

            try
            {
                return sampleFormat switch
                {
                    AudioSampleFormat.Float32 => CalculateRmsFromFloat32(buffer, bytesRecorded),
                    AudioSampleFormat.Int16 => CalculateRmsFromInt16(buffer, bytesRecorded),
                    AudioSampleFormat.Int32 => CalculateRmsFromInt32(buffer, bytesRecorded),
                    _ => 0.0f
                };
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error calculating RMS level: {ex.Message}", LogLevel.Debug);
                return 0.0f;
            }
        }

        /// <summary>
        /// Calculate peak from 32-bit float samples (WASAPI preferred format)
        /// </summary>
        private float CalculatePeakFromFloat32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            float max = 0.0f;

            // Process as 32-bit float samples
            int sampleCount = bytesRecorded / 4; // 4 bytes per float sample
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = Math.Abs(waveBuffer.FloatBuffer[i]);
                if (sample > max)
                    max = sample;
            }

            return Math.Min(max, 1.0f); // Clamp to maximum of 1.0
        }

        /// <summary>
        /// Calculate peak from 16-bit integer samples
        /// </summary>
        private float CalculatePeakFromInt16(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            short maxSample = 0;

            // Process as 16-bit signed integer samples
            int sampleCount = bytesRecorded / 2; // 2 bytes per short sample
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = Math.Abs(waveBuffer.ShortBuffer[i]);
                if (sample > maxSample)
                    maxSample = sample;
            }

            // Convert to float (0.0 to 1.0)
            return maxSample / 32768.0f; // short.MaxValue + 1
        }

        /// <summary>
        /// Calculate peak from 32-bit integer samples
        /// </summary>
        private float CalculatePeakFromInt32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int maxSample = 0;

            // Process as 32-bit signed integer samples
            int sampleCount = bytesRecorded / 4; // 4 bytes per int sample
            for (int i = 0; i < sampleCount; i++)
            {
                int sample = Math.Abs(waveBuffer.IntBuffer[i]);
                if (sample > maxSample)
                    maxSample = sample;
            }

            // Convert to float (0.0 to 1.0)
            return maxSample / 2147483648.0f; // int.MaxValue + 1
        }

        /// <summary>
        /// Calculate RMS from 32-bit float samples
        /// </summary>
        private float CalculateRmsFromFloat32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            double sum = 0.0;

            int sampleCount = bytesRecorded / 4;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = waveBuffer.FloatBuffer[i];
                sum += sample * sample;
            }

            if (sampleCount > 0)
            {
                double rms = Math.Sqrt(sum / sampleCount);
                return (float)Math.Min(rms, 1.0);
            }

            return 0.0f;
        }

        /// <summary>
        /// Calculate RMS from 16-bit integer samples
        /// </summary>
        private float CalculateRmsFromInt16(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            double sum = 0.0;

            int sampleCount = bytesRecorded / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = waveBuffer.ShortBuffer[i] / 32768.0f;
                sum += sample * sample;
            }

            if (sampleCount > 0)
            {
                double rms = Math.Sqrt(sum / sampleCount);
                return (float)Math.Min(rms, 1.0);
            }

            return 0.0f;
        }

        /// <summary>
        /// Calculate RMS from 32-bit integer samples
        /// </summary>
        private float CalculateRmsFromInt32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            double sum = 0.0;

            int sampleCount = bytesRecorded / 4;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = waveBuffer.IntBuffer[i] / 2147483648.0f;
                sum += sample * sample;
            }

            if (sampleCount > 0)
            {
                double rms = Math.Sqrt(sum / sampleCount);
                return (float)Math.Min(rms, 1.0);
            }

            return 0.0f;
        }

        /// <summary>
        /// Calculate stereo peak levels from raw audio buffer
        /// </summary>
        public StereoPeakLevels CalculateStereoPeakLevel(byte[] buffer, int bytesRecorded, AudioSampleFormat sampleFormat, int channels)
        {
            if (buffer == null || bytesRecorded <= 0)
                return new StereoPeakLevels(0.0f, 0.0f);

            // For mono, calculate single peak and return same value for both channels
            if (channels == 1)
            {
                float monoPeak = CalculatePeakLevel(buffer, bytesRecorded, sampleFormat);
                return StereoPeakLevels.Mono(monoPeak);
            }

            try
            {
                return sampleFormat switch
                {
                    AudioSampleFormat.Float32 => CalculateStereoPeakFromFloat32(buffer, bytesRecorded),
                    AudioSampleFormat.Int16 => CalculateStereoPeakFromInt16(buffer, bytesRecorded),
                    AudioSampleFormat.Int32 => CalculateStereoPeakFromInt32(buffer, bytesRecorded),
                    _ => new StereoPeakLevels(0.0f, 0.0f)
                };
            }
            catch (Exception ex)
            {
                _loggingService?.Log($"Error calculating stereo peak level: {ex.Message}", LogLevel.Debug);
                return new StereoPeakLevels(0.0f, 0.0f);
            }
        }

        /// <summary>
        /// Calculate stereo peak from 32-bit float samples (interleaved L,R,L,R...)
        /// </summary>
        private StereoPeakLevels CalculateStereoPeakFromFloat32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            float maxLeft = 0.0f;
            float maxRight = 0.0f;

            // Process as 32-bit float samples (interleaved stereo)
            int sampleCount = bytesRecorded / 4; // 4 bytes per float sample
            for (int i = 0; i < sampleCount; i += 2) // Skip by 2 for stereo
            {
                // Ensure we don't go out of bounds
                if (i + 1 < sampleCount)
                {
                    float leftSample = Math.Abs(waveBuffer.FloatBuffer[i]);
                    float rightSample = Math.Abs(waveBuffer.FloatBuffer[i + 1]);
                    
                    if (leftSample > maxLeft)
                        maxLeft = leftSample;
                    if (rightSample > maxRight)
                        maxRight = rightSample;
                }
            }

            return new StereoPeakLevels(
                Math.Min(maxLeft, 1.0f),
                Math.Min(maxRight, 1.0f)
            );
        }

        /// <summary>
        /// Calculate stereo peak from 16-bit integer samples (interleaved L,R,L,R...)
        /// </summary>
        private StereoPeakLevels CalculateStereoPeakFromInt16(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            short maxLeftSample = 0;
            short maxRightSample = 0;

            // Process as 16-bit signed integer samples (interleaved stereo)
            int sampleCount = bytesRecorded / 2; // 2 bytes per short sample
            for (int i = 0; i < sampleCount; i += 2) // Skip by 2 for stereo
            {
                // Ensure we don't go out of bounds
                if (i + 1 < sampleCount)
                {
                    short leftSample = Math.Abs(waveBuffer.ShortBuffer[i]);
                    short rightSample = Math.Abs(waveBuffer.ShortBuffer[i + 1]);
                    
                    if (leftSample > maxLeftSample)
                        maxLeftSample = leftSample;
                    if (rightSample > maxRightSample)
                        maxRightSample = rightSample;
                }
            }

            // Convert to float (0.0 to 1.0)
            return new StereoPeakLevels(
                maxLeftSample / 32768.0f,
                maxRightSample / 32768.0f
            );
        }

        /// <summary>
        /// Calculate stereo peak from 32-bit integer samples (interleaved L,R,L,R...)
        /// </summary>
        private StereoPeakLevels CalculateStereoPeakFromInt32(byte[] buffer, int bytesRecorded)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int maxLeftSample = 0;
            int maxRightSample = 0;

            // Process as 32-bit signed integer samples (interleaved stereo)
            int sampleCount = bytesRecorded / 4; // 4 bytes per int sample
            for (int i = 0; i < sampleCount; i += 2) // Skip by 2 for stereo
            {
                // Ensure we don't go out of bounds
                if (i + 1 < sampleCount)
                {
                    int leftSample = Math.Abs(waveBuffer.IntBuffer[i]);
                    int rightSample = Math.Abs(waveBuffer.IntBuffer[i + 1]);
                    
                    if (leftSample > maxLeftSample)
                        maxLeftSample = leftSample;
                    if (rightSample > maxRightSample)
                        maxRightSample = rightSample;
                }
            }

            // Convert to float (0.0 to 1.0)
            return new StereoPeakLevels(
                maxLeftSample / 2147483648.0f,
                maxRightSample / 2147483648.0f
            );
        }
    }
}