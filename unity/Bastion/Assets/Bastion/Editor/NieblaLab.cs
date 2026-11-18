using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Grid = Bastion.Engine.Grid;

namespace Bastion.EditorTools
{
    /// <summary>
    /// T18 (Arturo, 18/11: «la actualización de visión y de niebla que se haga en cada casilla y no al final del
    /// movimiento»): what Fog.Show costs a tile, as UI will call it on each tile of a walk (Gameplay's Events.MoveStep). On
    /// a recorded map (BASTION_MAP, city-7 unless said), its first soldier stepped tile by tile along the open ground beside
    /// it (BASTION_PASOS steps, 12), the state's Seq on each time as an event would put it, Fog.Show timed each step (after
    /// two to warm up), whole and with sightOnly (what a walk's tile asks); the least, the mean and the most in the log
    /// ([niebla]). Edit mode.
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.NieblaLab.Coste -quit
    /// </summary>
    public static class NieblaLab
    {
        public static void Coste()
        {
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            int steps = int.TryParse(Environment.GetEnvironmentVariable("BASTION_PASOS"), out int n) ? n : 12;
            var state = MapLab.Golden(name);
            MapLab.Map(state, name);
            var fog = new GameObject("Niebla").AddComponent<Fog>();
            fog.Reset(state);
            fog.Show(state, true);
            // Its way: each next tile one of the eight beside the last, free and on the ground, not one it has been on; the first
            // soldier with room for the whole way.
            Unit walker = null;
            var way = new System.Collections.Generic.List<Pos>();
            foreach (var u in state.Units.Where(u => u.Alive && u.Team == Teams.Xcom))
            {
                way = new System.Collections.Generic.List<Pos> { u.Pos };
                for (int k = 0; k < steps + 2; k++)
                {
                    var last = way[way.Count - 1];
                    var next = Grid.Dirs8.Select(d => new Pos { X = last.X + d.X, Y = last.Y + d.Y, Z = 0 })
                        .FirstOrDefault(p => Grid.InBounds(state, p.X, p.Y) && Flights.CanStopAt(state, p) && Grid.UnitAt(state, p) == null && !way.Any(w => w.X == p.X && w.Y == p.Y));
                    if (next == null) break;
                    way.Add(next);
                }
                walker = u;
                if (way.Count >= steps + 3) break;
            }
            if (walker == null) return;
            // Four walks along the same way, from its start, with the state's Seq on at each tile as an event would put it, each
            // timed from its third tile: Fog.Show whole (as it is at a walk's end), Fog.Show with sightOnly (a walk's tile), and
            // the engine's two parts of a whole Show, the squad's view and what it sees, on a state new to them.
            var times = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<double>>();
            void Walk(string what, Action each)
            {
                walker.Pos = way[0];
                state.Seq++;
                fog.Show(state, true);
                var l = times[what] = new System.Collections.Generic.List<double>();
                var clock = new Stopwatch();
                for (int k = 1; k < way.Count; k++)
                {
                    walker.Pos = way[k];
                    state.Seq++;
                    clock.Restart();
                    each();
                    clock.Stop();
                    if (k > 2) l.Add(clock.Elapsed.TotalMilliseconds);
                }
            }
            Walk("Fog.Show whole", () => fog.Show(state));
            Walk("Fog.Show sight only", () => fog.Show(state, false, true));
            Walk("Los.TeamView", () => Los.TeamView(state, Teams.Xcom));
            Walk("Los.TeamVisibility", () => Los.TeamVisibility(state, Teams.Xcom));
            if (times.Values.First().Count == 0)
            {
                Debug.Log($"[niebla] {name}: nobody of the squad can walk");
                return;
            }
            foreach (var (what, l) in times)
                Debug.Log(FormattableString.Invariant($"[niebla] {name}, {walker.Id} walked {l.Count} tiles: {what} {l.Min():0.00} / {l.Average():0.00} / {l.Max():0.00} ms (least / mean / most)"));
        }
    }
}
