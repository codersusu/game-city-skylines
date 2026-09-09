using System;
using System.IO;
using UnityEngine;

namespace Seabright
{
    /// <summary>Opt-in, bounded capture of the real listener mix. No microphone is used.</summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioOutputProbe : MonoBehaviour
    {
        readonly object gate = new object();
        float[] samples;
        int written, channelCount, sampleRate;
        bool capturing;

        public void Begin(float seconds)
        {
            int rate = AudioSettings.outputSampleRate;
            lock (gate)
            {
                sampleRate = rate;
                // Eight channels is the maximum supported capture layout. The actual
                // channel count comes from the audio thread, never an assumed stereo mix.
                samples = new float[Mathf.CeilToInt(Mathf.Clamp(seconds, 1, 40) * rate) * 8];
                written = channelCount = 0;
                maximumFrames = Mathf.CeilToInt(Mathf.Clamp(seconds, 1, 40) * rate);
                capturing = true;
            }
        }

        int maximumFrames;
        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                if (!capturing || samples == null || channels < 1 || channels > 8) return;
                if (channelCount == 0) channelCount = channels;
                if (channelCount != channels) { capturing = false; return; }
                int count = Math.Min(data.Length, maximumFrames * channels - written);
                if (count > 0) { Array.Copy(data, 0, samples, written, count); written += count; }
                if (written >= maximumFrames * channels) capturing = false;
            }
        }

        public Capture SaveWav(string path)
        {
            float[] pcm;
            int channels, rate;
            lock (gate)
            {
                capturing = false;
                channels = channelCount;
                rate = sampleRate;
                pcm = new float[written];
                if (written > 0) Array.Copy(samples, pcm, written);
            }
            var result = new Capture { filename = Path.GetFileName(path), sampleRate = rate, channels = channels, sampleCount = pcm.Length };
            if (pcm.Length == 0 || channels < 1 || rate < 1) return result;
            double power = 0;
            foreach (float sample in pcm)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) { result.nonFiniteSamples++; continue; }
                power += sample * (double)sample;
                result.peak = Mathf.Max(result.peak, Mathf.Abs(sample));
                if (Mathf.Abs(sample) >= .9999f) result.clippedSamples++;
            }
            result.rms = (float)Math.Sqrt(power / pcm.Length);
            result.durationSeconds = pcm.Length / (float)(rate * channels);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
                writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length * 2);
                foreach (float sample in pcm)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(float.IsNaN(sample) ? 0 : sample, -1, 1) * 32767));
            }
            return result;
        }

        [Serializable] public sealed class Capture
        {
            public string filename;
            public int sampleRate, channels, sampleCount, clippedSamples, nonFiniteSamples;
            public float durationSeconds, rms, peak;
        }
    }
}
