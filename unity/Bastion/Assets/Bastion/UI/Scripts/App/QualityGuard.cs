using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.UI.App
{
    /// <summary>
    /// Watches a battle's frame rate (settings.ts QualityGuard of the 0.10.1). It times the frames for a few seconds and,
    /// well under 45 FPS, takes the level down one (GameSettings.LowerQuality), then measures again; after the first
    /// seconds it keeps watching gently. An app out of focus, a cap under the bar and hitches don't count, and the
    /// average drops the fastest and slowest tenth of the frames. On real time, not the game's.
    /// </summary>
    public sealed class QualityGuard
    {
        /// <summary>Below this, a battle is not fluid: the guard steps in.</summary>
        const float SlowFps = 45;
        /// <summary>Later on, it takes two windows in a row under this.</summary>
        const float WatchFps = 38;
        /// <summary>Nothing counts in the first seconds (the map being built) nor right after a step.</summary>
        const double WarmUp = 2.5;
        const double Settle = 1.5;
        /// <summary>The first seconds of a battle are watched closely, in short windows; then in long ones.</summary>
        const double CloseWatch = 25;
        const double ShortWindow = 3;
        const double LongWindow = 8;
        /// <summary>A frame this long is a hitch, not the frame rate.</summary>
        const double Hitch = 0.25;

        readonly double born = Now;
        readonly Action onLower;
        readonly List<double> times = new List<double>();
        double resumeAt;
        double last;
        double spent;
        int slowWindows;

        /// <summary>`onLower`: a step was taken (applied already; the battle says so).</summary>
        public QualityGuard(Action onLower)
        {
            this.onLower = onLower;
            resumeAt = born + WarmUp;
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>Called with every frame drawn.</summary>
        public void Frame()
        {
            var now = Now;
            var cap = GameSettings.FpsCap;
            // Only while the player is at it (the lab's slow frames, BASTION_LENTO, out of focus too).
            if ((!Application.isFocused && GameSettings.SlowForTest == 0) || !GameSettings.AutoQuality || (cap > 0 && cap < SlowFps))
            {
                last = 0;
                return;
            }
            var dt = last > 0 ? now - last : 0;
            last = now;
            if (dt <= 0 || dt > Hitch || now < resumeAt) return;
            times.Add(dt);
            spent += dt;
            var close = now - born < CloseWatch;
            if (spent < (close ? ShortWindow : LongWindow)) return;
            times.Sort();
            var from = (int)Math.Floor(times.Count * 0.1);
            var to = (int)Math.Ceiling(times.Count * 0.9);
            var sum = 0.0;
            for (var i = from; i < to; i++) sum += times[i];
            var fps = (to - from) / sum;
            times.Clear();
            spent = 0;
            slowWindows = fps < (close ? SlowFps : WatchFps) ? slowWindows + 1 : 0;
            if (slowWindows < (close ? 1 : 2)) return;
            slowWindows = 0;
            if (!GameSettings.LowerQuality()) return;
            Debug.Log($"[ajuste] {fps:0} fps en combate: gráficos {GameSettings.LevelNames[GameSettings.ShownLevel]}");
            resumeAt = now + Settle;
            onLower?.Invoke();
        }
    }
}
