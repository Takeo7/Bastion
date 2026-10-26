using System;
using System.Collections.Generic;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// A level that changes over time as WebAudio's AudioParam does (0.10.1's audio/engine.ts moves every level with them):
    /// set at a time, ramped to a value linearly or exponentially, or drawn towards a target with a time constant. Times are
    /// the mix's seconds (Mixer.Time). Read sample by sample, forwards only (At); the audio thread alone touches it.
    /// </summary>
    internal sealed class Param
    {
        private enum Kind { Set, Linear, Exponential, Target }

        private readonly struct Event
        {
            public readonly Kind kind;
            public readonly double time;
            public readonly float value;
            public readonly double tau;

            public Event(Kind kind, double time, float value, double tau)
            {
                this.kind = kind;
                this.time = time;
                this.value = value;
                this.tau = tau;
            }
        }

        /// <summary>Chromium's AudioParamTimeline: a target this close is reached, and stays.</summary>
        private const double TargetThreshold = 1.5e-6;
        private const double TargetZeroThreshold = 1e-20;

        /// <summary>What is still ahead, by time.</summary>
        private readonly List<Event> events = new List<Event>();
        private float value;
        private double clock;
        // The last event passed: where a ramp after it starts from.
        private double lastTime;
        private float lastValue;
        // A target being approached (SetTarget passed).
        private bool approaching;
        private float target;
        private double targetFrom;
        private float targetStart;
        private double tau;

        public Param(float value)
        {
            this.value = lastValue = value;
        }

        /// <summary>The value at the last time read (WebAudio's AudioParam.value).</summary>
        public float Value => value;

        /// <summary>Whether it stays as it is from here on: nothing ahead, no target being approached.</summary>
        public bool Still => events.Count == 0 && !approaching;

        public void Set(float v, double t) => Add(new Event(Kind.Set, t, v, 0));

        public void Linear(float v, double t) => Ramp(new Event(Kind.Linear, t, v, 0));

        public void Exponential(float v, double t) => Ramp(new Event(Kind.Exponential, t, v, 0));

        public void Target(float v, double t, double timeConstant) => Add(new Event(Kind.Target, t, v, timeConstant));

        /// <summary>Everything from `t` on is dropped (a ramp that ends there too); the value holds where it is.</summary>
        public void Cancel(double t)
        {
            events.RemoveAll((e) => e.time >= t);
        }

        /// <summary>The value at `t` (never before the last time read).</summary>
        public float At(double t)
        {
            clock = t;
            while (events.Count > 0 && events[0].time <= t)
            {
                Pass(events[0]);
                events.RemoveAt(0);
            }
            if (events.Count > 0 && events[0].kind != Kind.Set && events[0].kind != Kind.Target)
            {
                var e = events[0];
                var f = (t - lastTime) / (e.time - lastTime);
                if (e.kind == Kind.Linear) value = (float)(lastValue + (e.value - lastValue) * f);
                // An exponential ramp from or to nothing, or across zero, holds (WebAudio).
                else if (lastValue != 0 && e.value != 0 && (lastValue > 0) == (e.value > 0)) value = (float)(lastValue * Math.Pow(e.value / lastValue, f));
                else value = lastValue;
                return value;
            }
            if (approaching)
            {
                value = (float)(target + (targetStart - target) * Math.Exp(-(t - targetFrom) / tau));
                var off = Math.Abs(value - target);
                if (off < TargetThreshold * Math.Abs(target) || off < TargetZeroThreshold)
                {
                    value = target;
                    approaching = false;
                    lastValue = value;
                }
            }
            return value;
        }

        private void Pass(Event e)
        {
            switch (e.kind)
            {
                case Kind.Target:
                    approaching = e.tau > 0;
                    target = e.value;
                    targetFrom = e.time;
                    targetStart = value;
                    tau = e.tau;
                    if (!approaching) value = e.value;
                    break;
                default:
                    approaching = false;
                    value = e.value;
                    break;
            }
            lastTime = e.time;
            lastValue = value;
        }

        /// <summary>A ramp with nothing before it starts where the value is now (Chromium adds that start itself).</summary>
        private void Ramp(Event e)
        {
            if (events.Count == 0 && !approaching)
            {
                lastTime = clock;
                lastValue = value;
            }
            Add(e);
        }

        /// <summary>In its place by time; one of the same kind at the same time is replaced (WebAudio).</summary>
        private void Add(Event e)
        {
            var i = 0;
            while (i < events.Count && events[i].time <= e.time)
            {
                if (events[i].time == e.time && events[i].kind == e.kind)
                {
                    events[i] = e;
                    return;
                }
                i++;
            }
            events.Insert(i, e);
        }
    }
}
