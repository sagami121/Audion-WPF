using System;
using NAudio.Wave;

namespace Audion_WPF
{
    public sealed class LevelMeterSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private float _smoothedPeak;

        public LevelMeterSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public WaveFormat WaveFormat
        {
            get { return _source.WaveFormat; }
        }

        public float CurrentLevel
        {
            get { return _smoothedPeak; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var read = _source.Read(buffer, offset, count);
            float peak = 0f;

            for (var i = offset; i < offset + read; i++)
            {
                var sample = Math.Abs(buffer[i]);
                if (sample > peak)
                {
                    peak = sample;
                }
            }

            _smoothedPeak = (_smoothedPeak * 0.8f) + (peak * 0.2f);
            return read;
        }
    }
}
