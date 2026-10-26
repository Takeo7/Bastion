using System;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>A sound's samples, held as the 16 bits they were written in (Generado/*.wav): one or two channels, at their own rate.</summary>
    public sealed class Sample
    {
        public readonly short[] Data;
        public readonly int Channels;
        public readonly int Rate;
        public readonly int Frames;

        public Sample(short[] data, int channels, int rate)
        {
            if (channels != 1 && channels != 2) throw new ArgumentException("one or two channels", nameof(channels));
            Data = data;
            Channels = channels;
            Rate = rate;
            Frames = data.Length / channels;
        }

        /// <summary>From floats from −1 to 1 (Unity's AudioClip.GetData), interleaved.</summary>
        public static Sample FromFloats(float[] data, int channels, int rate)
        {
            var d = new short[data.Length];
            for (var i = 0; i < d.Length; i++) d[i] = (short)Math.Max(-32768, Math.Min(32767, Math.Round(data[i] * 32768)));
            return new Sample(d, channels, rate);
        }

        /// <summary>A 16-bit PCM WAV file's (Generado's: sonido.sh writes them so), or null if it isn't one.</summary>
        public static Sample? FromWav(byte[] b)
        {
            int at = 12, channels = 1, rate = 44100, bits = 16;
            while (at + 8 <= b.Length)
            {
                var id = System.Text.Encoding.ASCII.GetString(b, at, 4);
                var size = BitConverter.ToInt32(b, at + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, at + 10);
                    rate = BitConverter.ToInt32(b, at + 12);
                    bits = BitConverter.ToInt16(b, at + 22);
                }
                else if (id == "data")
                {
                    if (bits != 16 || (channels != 1 && channels != 2)) return null;
                    var d = new short[Math.Min(size, b.Length - at - 8) / 2];
                    Buffer.BlockCopy(b, at + 8, d, 0, d.Length * 2);
                    return new Sample(d, channels, rate);
                }
                at += 8 + size + (size & 1);
            }
            return null;
        }

        public double Seconds => (double)Frames / Rate;
    }
}
