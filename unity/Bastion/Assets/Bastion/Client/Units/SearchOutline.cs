using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The line round what a soldier would search (Arturo's #12 of 5/11: «que no se resalte la casilla sino el cuerpo o el
    /// coche»; UI's CombatController.ShowSearchMarks, its rucksack over it): the body lying on each tile asked
    /// (Looting.BodyAt) or the car over it (its boot's tile, Looting.SearchSpot), Bastion/Mark's line round it in UI's
    /// colour, lit where a click searches it, else far. A body takes the line as a pass more over its own renderers
    /// (UnitViews.BodyRenderers), as the target's mark does; a car is joined into the map's still batch, so a copy of it
    /// stands over it with the line alone (MapView.CarCopy).
    /// </summary>
    public static class SearchOutline
    {
        /// <summary>The line's width (pixels; the target's 1.5): a body on the ground and a car under the game's camera want more.</summary>
        const float Line = 2;

        sealed class Shown
        {
            public Material material;
            public List<Renderer> renderers;
            public GameObject copy;
        }

        static readonly Dictionary<string, Shown> shown = new Dictionary<string, Shown>();
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        /// <summary>
        /// The lines round the bodies and cars on `marks`' tiles (each `lit` or `far`), and none round any other: what was
        /// shown and is still asked stays as it is. An empty list (or none), every line off.
        /// </summary>
        public static void Show(GameState s, IReadOnlyList<(Pos at, bool lit)> marks, Color lit, Color far)
        {
            var wanted = new Dictionary<string, (string body, int car, Color colour)>();
            if (s != null && marks != null)
                foreach (var (at, on) in marks)
                {
                    if (at == null) continue;
                    var body = Looting.BodyAt(s, at);
                    int car = body == null ? CarAt(s, at) : -1;
                    if (body == null && car < 0) continue;
                    string key = body != null ? "u " + body.Id : "c " + car;
                    // (Asked both ways, lit.)
                    if (wanted.TryGetValue(key, out var was) && was.colour == lit) continue;
                    wanted[key] = (body?.Id, car, on ? lit : far);
                }
            foreach (string key in shown.Keys.Where(k => !wanted.ContainsKey(k)).ToList()) Off(key);
            foreach (var pair in wanted)
            {
                var material = MaterialOf(pair.Value.colour);
                if (material == null) return;
                if (shown.TryGetValue(pair.Key, out var now))
                {
                    // (Its body or the map made again since, it is put on again.)
                    bool there = now.copy != null || (now.renderers != null && now.renderers.Count > 0 && now.renderers.All(r => r != null));
                    if (now.material == material && there) continue;
                    Off(pair.Key);
                }
                var (body, car, _) = pair.Value;
                if (body != null)
                {
                    var renderers = UnitViews.Current?.BodyRenderers(body);
                    if (renderers == null || renderers.Count == 0) continue;
                    Pass(renderers, material, true);
                    shown[pair.Key] = new Shown { material = material, renderers = renderers };
                }
                else
                {
                    var copy = MapView.Current?.CarCopy(car);
                    if (copy == null) continue;
                    foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    shown[pair.Key] = new Shown { material = material, copy = copy };
                }
            }
        }

        /// <summary>The anchor of the car over ground tile `at` (Decor.ObjectAt: its nose's tile), or −1.</summary>
        static int CarAt(GameState s, Pos at)
        {
            if (at.Z != 0 || s.Decor == null || at.X < 0 || at.Y < 0 || at.X >= s.Width || at.Y >= s.Height) return -1;
            var thing = Decor.ObjectAt(s.Decor, s.Width, at.Y * s.Width + at.X);
            return thing != null && thing.Kind == DecorKinds.CarFront ? thing.Anchor : -1;
        }

        static void Off(string key)
        {
            if (!shown.TryGetValue(key, out var now)) return;
            shown.Remove(key);
            if (now.renderers != null) Pass(now.renderers, now.material, false);
            if (now.copy == null) return;
            if (Application.isPlaying) Object.Destroy(now.copy);
            else Object.DestroyImmediate(now.copy);
        }

        /// <summary>Bastion/Mark with its line alone, in `colour` (sRGB, as UI's), made the first time.</summary>
        static Material MaterialOf(Color colour)
        {
            if (materials.TryGetValue(colour, out var m) && m != null) return m;
            m = KitMaterials.New(KitMaterials.Mark, "Registrar");
            if (m == null) return null;
            m.SetFloat("_Mark", 1);
            m.SetFloat("_Tint", 0);
            m.SetFloat("_Base", 0);
            m.SetFloat("_Rim", 0);
            m.SetFloat("_Line", Line);
            m.SetColor("_LineColor", colour);
            return materials[colour] = m;
        }

        /// <summary>`material` as a pass more over each of `renderers` (`on`), or taken off (UnitViews.Pass's way).</summary>
        static void Pass(List<Renderer> renderers, Material material, bool on)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var list = r.sharedMaterials.Where(x => x != material).ToList();
                if (on) list.Add(material);
                r.sharedMaterials = list.ToArray();
            }
        }
    }
}
