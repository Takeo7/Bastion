using System;
using Bastion.Client.Audio.Mix;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// A stream into the mix (the music, the beds, the ambience: minutes of stereo, too long to hold): Unity plays its clip
    /// from disk on this object's AudioSource, and this filter hands each block to the Mixer and leaves silence in its
    /// place. Unity runs the sources' filters before the listener's in each block (SoundPlayer's, which mixes): the block
    /// read is the one just written.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    internal sealed class Tap : MonoBehaviour, IFeed
    {
        private const int Blocks = 4;
        private float[] left = new float[4096 * Blocks];
        private float[] right = new float[4096 * Blocks];
        private long written;
        private long read;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var frames = data.Length / channels;
            if (left.Length < frames * Blocks)
            {
                left = new float[frames * Blocks];
                right = new float[frames * Blocks];
            }
            var size = left.Length;
            for (var i = 0; i < frames; i++)
            {
                var at = (int)((written + i) % size);
                left[at] = data[i * channels];
                right[at] = channels > 1 ? data[i * channels + 1] : data[i * channels];
            }
            Array.Clear(data, 0, data.Length);
            written += frames;
        }

        /// <summary>The frames written and not yet read, oldest first; silence where there are none (not started, or ended).</summary>
        public void Read(float[] l, float[] r, int frames)
        {
            var size = left.Length;
            // Too far behind (the mix missed blocks): from the newest frames.
            if (written - read > size - frames) read = written - Math.Min(written, frames);
            for (var i = 0; i < frames; i++)
            {
                if (read >= written)
                {
                    l[i] = r[i] = 0;
                    continue;
                }
                var at = (int)(read % size);
                l[i] = left[at];
                r[i] = right[at];
                read++;
            }
        }
    }
}
