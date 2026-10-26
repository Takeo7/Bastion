using System;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// The compressor before the speakers in 0.10.1 (audio/engine.ts: threshold −14 dB, knee 10, ratio 4, attack 4 ms,
    /// release 250 ms) as Chromium's DynamicsCompressorNode runs it (DynamicsCompressorKernel): 6 ms of look-ahead, the
    /// knee curve, the adaptive release, and its make-up gain. Stereo, in steps of 32 frames.
    /// </summary>
    internal sealed class Compressor
    {
        private const int Division = 32;
        private const int MaxPreDelay = 1024;
        private const float PiOverTwo = (float)(Math.PI / 2);

        private readonly float sampleRate;
        private readonly float threshold;
        private readonly float knee;
        private readonly float ratio;
        private readonly float attack;
        private readonly float release;
        private readonly float[][] delay = { new float[MaxPreDelay], new float[MaxPreDelay] };
        private int readIndex;
        private int writeIndex;
        private float detectorAverage;
        private float compressorGain = 1;
        private float maxAttackCompressionDiffDb = -1;
        // The static curve (UpdateStaticCurveParameters).
        private float linearThreshold;
        private float kneeThresholdDb;
        private float kneeThreshold;
        private float ykneeThresholdDb;
        private readonly float k;
        private readonly float slope;

        public Compressor(float sampleRate, float threshold = -14, float knee = 10, float ratio = 4, float attack = 0.004f, float release = 0.25f, float preDelay = 0.006f)
        {
            this.sampleRate = sampleRate;
            this.threshold = threshold;
            this.knee = knee;
            this.ratio = ratio;
            this.attack = attack;
            this.release = release;
            var frames = Math.Min((int)(preDelay * sampleRate), MaxPreDelay - 1);
            writeIndex = frames;
            linearThreshold = DbToLinear(threshold);
            slope = 1 / ratio;
            k = KAtSlope(1 / ratio);
            kneeThresholdDb = threshold + knee;
            kneeThreshold = DbToLinear(kneeThresholdDb);
            ykneeThresholdDb = LinearToDb(KneeCurve(kneeThreshold, k));
        }

        private static float DbToLinear(float db) => (float)Math.Pow(10, 0.05f * db);

        private static float LinearToDb(float x) => (float)(20 * Math.Log10(x));

        private float KneeCurve(float x, float kk)
        {
            if (x < linearThreshold) return x;
            return linearThreshold + (1 - (float)Math.Exp(-kk * (x - linearThreshold))) / kk;
        }

        private float Saturate(float x, float kk)
        {
            if (x < kneeThreshold) return KneeCurve(x, kk);
            var xDb = LinearToDb(x);
            var yDb = ykneeThresholdDb + slope * (xDb - kneeThresholdDb);
            return DbToLinear(yDb);
        }

        private float SlopeAt(float x, float kk)
        {
            if (x < linearThreshold) return 1;
            var x2 = x * 1.001f;
            var xDb = LinearToDb(x);
            var x2Db = LinearToDb(x2);
            var yDb = LinearToDb(KneeCurve(x, kk));
            var y2Db = LinearToDb(KneeCurve(x2, kk));
            return (y2Db - yDb) / (x2Db - xDb);
        }

        private float KAtSlope(float desired)
        {
            var x = DbToLinear(threshold + knee);
            float minK = 0.1f, maxK = 10000, kk = 5;
            for (var i = 0; i < 15; i++)
            {
                if (SlopeAt(x, kk) < desired) maxK = kk;
                else minK = kk;
                kk = (float)Math.Sqrt(minK * maxK);
            }
            return kk;
        }

        /// <summary>`frames` frames of `left` and `right`, compressed in place (Chromium's steps of 32; a last one shorter if need be).</summary>
        public void Run(float[] left, float[] right, int frames)
        {
            var fullRangeGain = Saturate(1, k);
            var makeup = (float)Math.Pow(1 / fullRangeGain, 0.6f);
            var masterLinearGain = makeup;
            var attackFrames = Math.Max(0.001f, attack) * sampleRate;
            var releaseFrames = sampleRate * release;
            var satReleaseFrames = 0.0025f * sampleRate;
            // The adaptive release: a curve through four points, from the release zones 0.09, 0.16, 0.42, 0.98.
            float y1 = releaseFrames * 0.09f, y2 = releaseFrames * 0.16f, y3 = releaseFrames * 0.42f, y4 = releaseFrames * 0.98f;
            var kA = 0.9999999999999998f * y1 + 1.8432219684323923e-16f * y2 - 1.9373394351676423e-16f * y3 + 8.824516011816245e-18f * y4;
            var kB = -1.5788320352845888f * y1 + 2.3305837032074286f * y2 - 0.9141194204840429f * y3 + 0.1623677525612032f * y4;
            var kC = 0.5334142869106424f * y1 - 1.272736789213631f * y2 + 0.9258856042207512f * y3 - 0.18656310191776226f * y4;
            var kD = 0.08783463138207234f * y1 - 0.1694162967925622f * y2 + 0.08588057951595272f * y3 - 0.00429891410546283f * y4;
            var kE = -0.042416883008123074f * y1 + 0.1115693827987602f * y2 - 0.09764676325265872f * y3 + 0.028494263462021576f * y4;
            var frame = 0;
            while (frame < frames)
            {
                if (float.IsNaN(detectorAverage) || float.IsInfinity(detectorAverage)) detectorAverage = 1;
                var desiredGain = detectorAverage;
                var scaledDesiredGain = (float)Math.Asin(desiredGain) / PiOverTwo;
                float envelopeRate;
                var releasing = scaledDesiredGain > compressorGain;
                var compressionDiffDb = LinearToDb(compressorGain / scaledDesiredGain);
                if (releasing)
                {
                    maxAttackCompressionDiffDb = -1;
                    if (float.IsNaN(compressionDiffDb) || float.IsInfinity(compressionDiffDb)) compressionDiffDb = -1;
                    var x = Math.Max(-12f, Math.Min(0f, compressionDiffDb));
                    x = 0.25f * (x + 12);
                    var x2 = x * x;
                    var x3 = x2 * x;
                    var x4 = x2 * x2;
                    var adaptive = kA + kB * x + kC * x2 + kD * x3 + kE * x4;
                    envelopeRate = DbToLinear(5 / adaptive);
                }
                else
                {
                    if (float.IsNaN(compressionDiffDb) || float.IsInfinity(compressionDiffDb)) compressionDiffDb = 1;
                    if (maxAttackCompressionDiffDb == -1 || maxAttackCompressionDiffDb < compressionDiffDb) maxAttackCompressionDiffDb = compressionDiffDb;
                    var effective = Math.Max(0.5f, maxAttackCompressionDiffDb);
                    var x = 0.25f / effective;
                    envelopeRate = 1 - (float)Math.Pow(x, 1 / attackFrames);
                }
                var steps = Math.Min(Division, frames - frame);
                for (var i = 0; i < steps; i++)
                {
                    var l = left[frame];
                    var r = right[frame];
                    delay[0][writeIndex] = l;
                    delay[1][writeIndex] = r;
                    var input = Math.Max(Math.Abs(l), Math.Abs(r));
                    var shaped = Saturate(input, k);
                    var attenuation = input <= 0.0001f ? 1 : shaped / input;
                    var attenuationDb = Math.Max(2f, -LinearToDb(attenuation));
                    var satReleaseRate = DbToLinear(attenuationDb / satReleaseFrames) - 1;
                    var rate = attenuation > detectorAverage ? satReleaseRate : 1;
                    detectorAverage += (attenuation - detectorAverage) * rate;
                    detectorAverage = Math.Min(1f, detectorAverage);
                    if (float.IsNaN(detectorAverage) || float.IsInfinity(detectorAverage)) detectorAverage = 1;
                    if (envelopeRate < 1) compressorGain += (scaledDesiredGain - compressorGain) * envelopeRate;
                    else compressorGain = Math.Min(1f, compressorGain * envelopeRate);
                    var warped = (float)Math.Sin(PiOverTwo * compressorGain);
                    var total = masterLinearGain * warped;
                    left[frame] = delay[0][readIndex] * total;
                    right[frame] = delay[1][readIndex] * total;
                    frame++;
                    readIndex = (readIndex + 1) & (MaxPreDelay - 1);
                    writeIndex = (writeIndex + 1) & (MaxPreDelay - 1);
                }
                if (Math.Abs(detectorAverage) < 1.175494e-38f) detectorAverage = 0;
                if (Math.Abs(compressorGain) < 1.175494e-38f) compressorGain = 0;
            }
        }
    }
}
