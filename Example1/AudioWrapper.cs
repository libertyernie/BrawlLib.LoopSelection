using BrawlLib.LoopSelection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Example1
{
    public class AudioWrapper : IAudioStream
    {
        private readonly byte[] _pcmDataSource;

        public AudioWrapper(byte[] pcmDataSource, int channels, int frequency)
        {
            _pcmDataSource = pcmDataSource;
            Channels = channels;
            Frequency = frequency;
        }

        public WaveFormatTag Format => WaveFormatTag.WAVE_FORMAT_PCM;

        public int BitsPerSample => 16;

        public int Samples => checked((int)(_pcmDataSource.Length / (2 * Channels)));

        public int Channels { get; }

        public int Frequency { get; }

        public bool IsLooping { get; set; }
        public int LoopStartSample { get; set; }
        public int LoopEndSample { get; set; }

        public int SamplePosition { get; set; }

        public void Dispose() { }

        public int ReadSamples(IntPtr destAddr, int numSamples)
        {
            int bytes_read = 2 * Channels * Math.Min(numSamples, Samples - SamplePosition);
            Marshal.Copy(_pcmDataSource, 2 * Channels * SamplePosition, destAddr, bytes_read);
            SamplePosition += bytes_read / (2 * Channels);
            return bytes_read;
        }

        public void Wrap()
        {
            SamplePosition = LoopStartSample;
        }
    }
}
