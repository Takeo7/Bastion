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
    /// stands over it with the line alone (MapView.CarCopy). And what else a soldier opens, searches or pries (Arturo's #3
    /// of 7/11: «las cajas, puertas, etc., que no sean una casilla en el suelo»): a loot box, a door's leaves, the furniture
    /// piled against a window, a doorway's boards, each mesh drawn again with the line alone (MapView.Outline). TopOf: how
    /// high each stands, for UI's icon over it.
    /// </summary>
    public static class SearchOutline
    {
        /// <summary>The line's width (pixels; the target's 1.5): a body on the ground and a car under the game's camera want more.</summary>
        const float Line = 2;

        sealed class Shown
        {
            public Material material;
            public List<Renderer> renderers;
            public List<GameObject> copies;
        }

        static readonly Dictionary<string, Shown> shown = new Dictionary<string, Shown>();
        /// <summary>What was asked last (Show), to outline again what the map makes again (KeepUp).</summary>
        static readonly bool Probe = System.Environment.GetEnvironmentVariable("BASTION_SONDA_CONTORNO") == "1";
        static GameState asked;
        static List<(Pos at, bool lit)> askedMarks;
        static Color askedLit, askedFar;
        static readonly Dictionary<(Color, bool), Material> materials = new Dictionary<(Color, bool), Material>();

        /// <summary>
        /// The lines round the bodies and cars on `marks`' tiles (each `lit` or `far`), and none round any other: what was
        /// shown and is still asked stays as it is. An empty list (or none), every line off.
        /// </summary>
        public static void Show(GameState s, IReadOnlyList<(Pos at, bool lit)> marks, Color lit, Color far)
        {
            (asked, askedMarks, askedLit, askedFar) = (s, marks?.ToList(), lit, far);
            if (Probe) Debug.Log($"[contorno] {Time.time:0.00} s, asked {marks?.Count ?? 0}: {string.Join(" ", (marks ?? new List<(Pos, bool)>()).Select(m => $"{m.at?.X},{m.at?.Y},{m.at?.Z}"))}");
            var wanted = new Dictionary<string, (string body, int car, Pos thing, Color colour)>();
            if (s != null && marks != null)
                foreach (var (at, on) in marks)
                {
                    if (at == null) continue;
                    var body = Looting.BodyAt(s, at);
                    int car = body == null ? CarAt(s, at) : -1;
                    // (Anything else, by its tile: what MapView draws there, if anything.)
                    string key = body != null ? "u " + body.Id : car >= 0 ? "c " + car : $"t {at.Z},{at.X},{at.Y}";
                    // (Asked both ways, lit.)
                    if (wanted.TryGetValue(key, out var was) && was.colour == lit) continue;
                    wanted[key] = (body?.Id, car, body == null && car < 0 ? at : null, on ? lit : far);
                }
            foreach (string key in shown.Keys.Where(k => !wanted.ContainsKey(k)).ToList()) Off(key);
            foreach (var pair in wanted)
            {
                // (A door's leaf is cut low by the cutaway, or dark in an unfound building: its line seen through, round all of it.)
                var at = pair.Value.thing;
                string kind = at != null && s != null ? Bastion.Engine.Grid.KindAt(s, at) : null;
                var material = MaterialOf(pair.Value.colour, kind == TileKinds.Door || kind == TileKinds.DoorOpen);
                if (material == null) return;
                if (shown.TryGetValue(pair.Key, out var now))
                {
                    // (Its body or the map made again since, it is put on again.)
                    bool there = (now.copies != null && now.copies.Count > 0 && now.copies.All(c => c != null)) || (now.renderers != null && now.renderers.Count > 0 && now.renderers.All(r => r != null));
                    if (now.material == material && there) continue;
                    Off(pair.Key);
                }
                var (body, car, thing, _) = pair.Value;
                if (body != null)
                {
                    var renderers = UnitViews.Current?.BodyRenderers(body);
                    if (renderers == null || renderers.Count == 0) continue;
                    Pass(renderers, material, true);
                    shown[pair.Key] = new Shown { material = material, renderers = renderers };
                }
                else if (car >= 0)
                {
                    var copy = MapView.Current?.CarCopy(car);
                    if (copy == null) continue;
                    foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    shown[pair.Key] = new Shown { material = material, copies = new List<GameObject> { copy } };
                }
                else
                {
                    var copies = MapView.Current?.Outline(thing, material);
                    if (Probe) Debug.Log($"[contorno] {pair.Key}: {copies?.Count ?? -1} copies (map {MapView.Current != null}), {(material != null ? material.GetFloat("_LineTest") : -1)}");
                    if (copies == null || copies.Count == 0) continue;
                    shown[pair.Key] = new Shown { material = material, copies = copies };
                }
            }
        }

        /// <summary>
        /// Each frame (MapView): what is outlined and was made again since (a building's walls laid again as the cutaway
        /// moves take a door's leaf and its line's copy with them; a body's figure made again), outlined again as last asked.
        /// </summary>
        public static void KeepUp()
        {
            // (BASTION_SONDA_CONTORNO=1, for a lab: each line's copies as they are, twice a second.)
            if (Probe && Time.frameCount % 5 == 0)
                foreach (var pair in shown)
                    foreach (var c in pair.Value.copies ?? new List<GameObject>())
                    {
                        var r = c != null ? c.GetComponent<Renderer>() : null;
                        Debug.Log(c == null ? $"[contorno] {pair.Key}: gone" : $"[contorno] {pair.Key}: {c.name} under {c.transform.parent?.name}, active {c.activeInHierarchy}, drawn {r?.enabled} seen {r?.isVisible}, layer {c.layer}, at {r?.bounds.center} size {r?.bounds.size}, {r?.sharedMaterial?.shader?.name}");
                    }
            if (shown.Count == 0 || askedMarks == null) return;
            foreach (var now in shown.Values)
                if ((now.copies != null && now.copies.Any(c => c == null)) || (now.renderers != null && now.renderers.Any(r => r == null)))
                {
                    Show(asked, askedMarks, askedLit, askedFar);
                    return;
                }
        }

        /// <summary>
        /// How high the top of what a mark on `at` outlines stands (m, the world's), for UI's icon over it: a body's, or what
        /// the map draws there (MapView.TopOf: a car, a box, a door, the boards); null if nothing.
        /// </summary>
        public static float? TopOf(GameState s, Pos at)
        {
            if (s == null || at == null) return null;
            var body = Looting.BodyAt(s, at);
            if (body == null) return MapView.Current?.TopOf(at);
            var renderers = UnitViews.Current?.BodyRenderers(body.Id)?.Where(r => r != null).ToList();
            return renderers != null && renderers.Count > 0 ? renderers.Max(r => r.bounds.max.y) : (float?)null;
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
            foreach (var copy in now.copies ?? new List<GameObject>())
            {
                if (copy == null) continue;
                if (Application.isPlaying) Object.Destroy(copy);
                else Object.DestroyImmediate(copy);
            }
        }

        /// <summary>Bastion/Mark with its line alone, in `colour` (sRGB, as UI's), seen `through` what is before it or not, made the first time.</summary>
        static Material MaterialOf(Color colour, bool through = false)
        {
            if (materials.TryGetValue((colour, through), out var m) && m != null) return m;
            m = KitMaterials.New(KitMaterials.Mark, "Registrar");
            if (m == null) return null;
            m.SetFloat("_Mark", 1);
            m.SetFloat("_Tint", 0);
            m.SetFloat("_Base", 0);
            m.SetFloat("_Rim", 0);
            m.SetFloat("_Line", Line);
            m.SetColor("_LineColor", colour);
            if (through)
            {
                m.SetFloat("_InsideTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                m.SetFloat("_InsideMark", (float)UnityEngine.Rendering.StencilOp.Replace);
                m.SetFloat("_LineTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                m.SetFloat("_LineStencil", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
            }
            return materials[(colour, through)] = m;
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
