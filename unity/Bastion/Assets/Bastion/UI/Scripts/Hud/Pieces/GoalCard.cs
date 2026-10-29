using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The objective's card under «Menú» (hud.ts setMission; docs/UNITY-UI.md §2.7): the mission's kind, the objective
    /// as it goes (in full on hover), the turns left, a siege's next wave, the Saqueo's horde, «VIENEN MÁS». A click opens
    /// the arrival card again. Under it, a siege's compass (#34), turned with the camera every frame; and with it, its
    /// four letters at the screen's edges, each on the side its quarter of the map lies (Ana, 28/10: «una N en el lado
    /// que sea el norte, una S en el sur»), the next wave's in red as on the compass.
    /// </summary>
    public sealed class GoalCard
    {
        readonly VisualElement host;
        readonly TooltipLayer tips;
        readonly VisualElement compass;
        readonly VisualElement rose;
        readonly Label[] dirs = new Label[4];
        /// <summary>The letters at the screen's edges (on the edge marks' layer, under the panels).</summary>
        readonly VisualElement edgeLayer;
        readonly Label[] edgeDirs = new Label[4];
        string key;
        string compassKey;
        float turn = float.NaN;
        IVisualElementScheduledItem beat;

        public GoalCard(VisualElement host, VisualElement compassHost, TooltipLayer tips, System.Action reopen, VisualElement edges = null)
        {
            edgeLayer = edges;
            this.host = host;
            this.tips = tips;
            compass = compassHost;
            tips.Attach(host, HudText.Mission.DockTip);
            host.RegisterCallback<ClickEvent>(_ => reopen?.Invoke());

            rose = Ui.El("compass-rose", compass);
            rose.pickingMode = PickingMode.Ignore;
            rose.Add(new CompassNeedle());
            var names = new[] { "n", "e", "s", "o" };
            for (var i = 0; i < 4; i++)
            {
                dirs[i] = Ui.Text(HudText.Compass.Letters[i], "compass-dir compass-dir--" + names[i], rose);
                dirs[i].pickingMode = PickingMode.Ignore;
            }
            Ui.Show(compass, false);
            if (edgeLayer != null)
                for (var i = 0; i < 4; i++)
                {
                    edgeDirs[i] = Ui.Text(HudText.Compass.Letters[i], "cardinal cardinal--" + names[i], edgeLayer);
                    edgeDirs[i].pickingMode = PickingMode.Ignore;
                    Ui.Show(edgeDirs[i], false);
                }
        }

        public void Set(MissionView m)
        {
            var k = Ui.Key(m);
            if (k == key) return;
            key = k;
            host.Clear();
            if (m == null) return;
            var kind = Ui.Text(m.Label.ToUpperInvariant(), "mission-kind font-title", host);
            tips.Attach(kind, m.Area);
            var goal = Ui.Text(m.Objective, "mission-goal", host);
            tips.Attach(goal, string.IsNullOrEmpty(m.ObjectiveTip) ? m.Objective : m.ObjectiveTip);
            if (m.TurnsLeft >= 0) Ui.Text(HudText.Mission.Turns(m.TurnsLeft), m.TurnsLeft <= 3 ? "timer timer--urgent" : "timer", host);
            if (!string.IsNullOrEmpty(m.WaveText)) tips.Attach(Ui.Text(m.WaveText, m.WaveUrgent ? "timer timer--urgent" : "timer", host), m.WaveTip);
            if (m.HordeFill >= 0)
            {
                var horde = Ui.El("horde" + (m.HordeFill >= 1 ? " horde--full" : ""), host);
                Ui.Text(HudText.Mission.Horde, "horde__label", horde);
                var bar = Ui.El("horde__bar well", horde);
                Ui.El("horde__fill", bar).style.width = Length.Percent(Mathf.Clamp01(m.HordeFill) * 100);
                tips.Attach(horde, m.HordeTitle);
            }
            if (!string.IsNullOrEmpty(m.Alert)) Ui.Text(m.Alert, "timer timer--urgent", host);
        }

        /// <summary>Hidden while the arrival card is up (it shrinks into here when closed).</summary>
        public void SetWaiting(bool waiting)
        {
            host.EnableInClassList("goal-dock--waiting", waiting);
        }

        /// <summary>A siege's compass: shown, and the sides the next wave comes in by in red, beating.</summary>
        public void SetCompass(CompassView v)
        {
            var k = Ui.Key(v);
            if (k == compassKey) return;
            compassKey = k;
            var shown = v != null && v.Shown;
            Ui.Show(compass, shown);
            foreach (var e in edgeDirs)
                if (e != null) Ui.Show(e, shown);
            turn = float.NaN;
            beat?.Pause();
            if (!shown) return;
            var by = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var hot = v.Hot.Contains(i);
                dirs[i].EnableInClassList("compass-dir--hot", hot);
                edgeDirs[i]?.EnableInClassList("cardinal--hot", hot);
                if (hot) by.Add(HudText.Compass.Sides[i]);
            }
            tips.Attach(compass, by.Count > 0 ? HudText.Compass.Next(Ui.List(by)) : HudText.Compass.About);
            // USS has no @keyframes: the hot letters beat from here, twice a second.
            if (by.Count == 0) return;
            var on = false;
            beat = compass.schedule.Execute(() =>
            {
                on = !on;
                for (var i = 0; i < 4; i++)
                    if (v.Hot.Contains(i))
                    {
                        dirs[i].style.opacity = on ? 1f : 0.55f;
                        if (edgeDirs[i] != null) edgeDirs[i].style.opacity = on ? 1f : 0.55f;
                    }
            }).Every(450);
        }

        /// <summary>Its N where the map's north is on the screen (radians, clockwise from up); the letters stay upright.</summary>
        public void TurnCompass(float radians)
        {
            if (compass.style.display == DisplayStyle.None) return;
            // (The edge letters each frame: the screen's size, and their own once laid out.)
            PlaceEdges(radians);
            if (Mathf.Abs(radians - turn) < 0.003f) return;
            turn = radians;
            var degrees = radians * Mathf.Rad2Deg;
            rose.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree));
            foreach (var d in dirs) d.style.rotate = new Rotate(new Angle(-degrees, AngleUnit.Degree));
        }

        /// <summary>How far in from the screen's edges its letters keep: clear of the turn strip above and the bar below.</summary>
        const float EdgeSide = 26, EdgeTop = 70, EdgeBottom = 120;

        /// <summary>
        /// Each letter where a line from the screen's middle, the way its quarter lies (N at `radians` clockwise from up, E a
        /// quarter on, and so on), meets the screen's edges taken in a little.
        /// </summary>
        void PlaceEdges(float radians)
        {
            if (edgeLayer == null) return;
            var size = edgeLayer.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0) return;
            var c = new Vector2(size.x / 2, (EdgeTop + size.y - EdgeBottom) / 2);
            var half = new Vector2(size.x / 2 - EdgeSide, (size.y - EdgeBottom - EdgeTop) / 2);
            for (var i = 0; i < 4; i++)
            {
                var e = edgeDirs[i];
                if (e == null) continue;
                var a = radians + i * Mathf.PI / 2;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                var t = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? half.x / Mathf.Abs(dir.x) : float.MaxValue, Mathf.Abs(dir.y) > 1e-4f ? half.y / Mathf.Abs(dir.y) : float.MaxValue);
                var at = c + dir * t;
                var w = float.IsNaN(e.layout.width) ? 0 : e.layout.width;
                var h = float.IsNaN(e.layout.height) ? 0 : e.layout.height;
                e.style.translate = new Translate(at.x - w / 2, at.y - h / 2);
            }
        }
    }
}
