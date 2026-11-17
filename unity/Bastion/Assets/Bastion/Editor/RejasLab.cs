using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bastion.Client.Effects;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>The caged dead behind a police cell's bars (H9 of the runs; UnitViews.Bars): their blows through the bars, both ways, filmed and measured.</summary>
    public static partial class CombatLab
    {
        /// <summary>
        /// One of the caged and a soldier across its bars, as the event player plays them (EventPlayer.PlayAttack: a beat,
        /// the blow awaited till it lands, 0.05 s, the hit), in the police station of Gameplay's sample (`muestras/comisaria.json`,
        /// BASTION_REJAS_MUESTRA another; its real cells, bars and walls, MapLab.Map): the first caged one (Unit.Caged) put by
        /// hand on the tile of its cell by its bars nearest it, the squad's first soldier two tiles across them in the
        /// corridor (Aim.AcrossBars), the rest of the units out, every tile of the ground floor found. BASTION_ACCION (a list,
        /// all of it unset): andar (from the back of its cell to its bars, turned as the state has it then), mordisco (its bite
        /// through the bars, the soldier hit), tajo (the soldier's blow through them, BASTION_HOJA: knife unless said),
        /// vigilancia (on watch with the blade, its blow as a reaction), silencioso (its back to the bars, unaware: the silent
        /// takedown through them, its death). Logs [rejas]: the bars found in the scene (their middles along them, their
        /// thickness), each beat's start, its blow and when it is over (s), and every frame how far into the bars each one's
        /// skin, bones (capsules) and blade go and how far past their middle line its trunk comes (cm), with a summary by beat.
        /// BASTION_REJAS_ANTES=1: as before (UnitViews.BarsOff). BASTION_TIRAS=&lt;folder&gt;: each frame from the game's camera
        /// (45° down, 24 and 9 tiles off, turned each of BASTION_REJAS_GIRO degrees from the south: 45,135 unless said, the
        /// game's first turn, from the cells' side, and the corridor's) 960×600, `juego24-45`…, and close 480×400: over the
        /// corridor at three quarters (`pasillo`), over the cell (`celda`), from above the bars' line (`lado`); and
        /// `estados.txt` (frame, time, beat). Shots' own camera: the first turn at BASTION_DIST tiles (24).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Rejas -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 190 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Rejas()
        {
            if (Resources.Load<FxArt>("FxArt") == null) FxArtMake.Run();
            string path = Environment.GetEnvironmentVariable("BASTION_REJAS_MUESTRA") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.dev-snapshots/gameplay/runs/muestras/comisaria.json"));
            var file = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            state = Seen(Bastion.Net.Json.Read<GameState>((file["state"] ?? file).ToString(Newtonsoft.Json.Formatting.None)));
            var caged = state.Units.First(u => u.Alive && u.Caged == true);
            soldier = state.Units.First(u => u.Alive && u.Team == Teams.Xcom);
            // The bars nearest it with its cell's floor on one side and the corridor's on the other.
            Pos front = null, across = null, bars = null;
            float best = float.MaxValue;
            bool Floor(Pos p) => Bastion.Engine.Grid.InBounds(state, p.X, p.Y) && Bastion.Engine.Grid.KindAt(state, p) == TileKinds.Floor;
            for (int y = 0; y < state.Height; y++)
                for (int x = 0; x < state.Width; x++)
                {
                    var b = new Pos { X = x, Y = y, Z = caged.Pos.Z };
                    if (Bastion.Engine.Grid.KindAt(state, b) != TileKinds.Bars) continue;
                    foreach (var d in Bastion.Engine.Grid.Dirs4)
                    {
                        Pos a = new Pos { X = x - d.X, Y = y - d.Y, Z = b.Z }, c = new Pos { X = x + d.X, Y = y + d.Y, Z = b.Z };
                        if (!Floor(a) || !Floor(c) || !Bastion.Engine.Aim.AcrossBars(state, a, c)) continue;
                        float far = Mathf.Abs(a.X - caged.Pos.X) + Mathf.Abs(a.Y - caged.Pos.Y) + 0.1f * (Mathf.Abs(c.X - caged.Pos.X) + Mathf.Abs(c.Y - caged.Pos.Y));
                        if (far < best) (best, front, across, bars) = (far, a, c, b);
                    }
                }
            if (front == null) throw new Exception($"[rejas] no bars by {caged.Id}");
            int dx = across.X - front.X, dy = across.Y - front.Y;
            double toward = Math.Atan2(dy, dx), away = Math.Atan2(-dy, -dx);
            var back = new Pos { X = front.X - dx / 2, Y = front.Y - dy / 2, Z = front.Z };
            string blade = Environment.GetEnvironmentVariable("BASTION_HOJA") ?? "knife";
            soldier.Template = "rookie";
            soldier.Overwatch = false;
            soldier.Hidden = null;
            soldier.Hunkered = false;
            soldier.Arms = new Arms { Primary = "assaultRifle", Melee = blade == "knife" ? null : blade };
            Put(soldier, across.X, across.Y, away);
            var acts = (Environment.GetEnvironmentVariable("BASTION_ACCION") ?? "andar,mordisco,tajo,vigilancia,silencioso").Split(',').Select(s => s.Trim()).ToList();
            // (From the back of its cell for «andar», side on; else at its bars facing the corridor.)
            if (acts[0] == "andar" && Floor(back)) Put(caged, back.X, back.Y, Math.Atan2(dx, -dy));
            else Put(caged, front.X, front.Y, toward);
            caged.Overwatch = false;
            caged.Hidden = null;
            caged.Pos.Z = front.Z;
            soldier.Pos.Z = front.Z;
            state.Units = new List<Unit> { soldier, caged };
            int area = state.Width * state.Height;
            state.Discovered = Enumerable.Range(0, area).ToList();
            target = caged;
            Debug.Log($"[rejas] {path}: {caged.Id} on ({front.X},{front.Y}), bars ({bars.X},{bars.Y}), {soldier.Id} on ({across.X},{across.Y}) with {blade}; beats {string.Join(",", acts)}{(UnitViews.BarsOff ? " (antes)" : "")}");
            MapLab.Lighting(true);
            // (The still things left as they were made, not joined: joined, their meshes can't be read for the bars' measure.
            // BASTION_REJAS_JUNTAR=1 as the game has them.)
            if (Environment.GetEnvironmentVariable("BASTION_REJAS_JUNTAR") != "1") MapBuilder.JoinStill = false;
            var view = MapLab.Map(state, "comisaria");
            view.ShowLevel(front.Z);
            if (Cutaway.Current != null) Cutaway.Current.Focus.selected = soldier.Id;
            units = new GameObject("Unidades").AddComponent<UnitViews>();
            fx = new GameObject("Efectos").AddComponent<Fx>();
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 800;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            var middle = MapSpace.TileToWorld(state, bars);
            var yaws = (Environment.GetEnvironmentVariable("BASTION_REJAS_GIRO") ?? "45,135").Split(',').Select(t => float.Parse(t, CultureInfo.InvariantCulture)).ToArray();
            float yaw = yaws[0];
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), NumberStyles.Float, CultureInfo.InvariantCulture, out float tiles) ? tiles : 24;
            MapSpace.Aim(camera, middle + Vector3.up * 0.9f, off, yaw);
            // (Across the bars from the dead one to the soldier, and along them: the measure's frame.)
            var normal = MapSpace.TileToWorld(state, across) - MapSpace.TileToWorld(state, front);
            normal.y = 0;
            normal.Normalize();
            var rows = new RejasFrame { middle = middle, normal = normal, along = Vector3.Cross(Vector3.up, normal) };
            string folder = Environment.GetEnvironmentVariable("BASTION_TIRAS");
            string beat = "espera";
            var worst = new Dictionary<string, RejasWorst>();
            bool done = false;
            var nearest = new Dictionary<string, float>();
            async Awaitable Watch()
            {
                int frame = 0;
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    if (rows.bars == null) BarsInScene(rows);
                    foreach (var (id, side) in new[] { (caged.Id, 1f), (soldier.Id, -1f) }) MeasureBars(rows, id, side, beat, worst);
                    // (How near what strikes comes to what it strikes, each frame: the least of the beat.)
                    if (Nearest(beat) is float near && (!nearest.TryGetValue(beat, out float was) || near < was)) nearest[beat] = near;
                    if (folder != null)
                    {
                        FilmBars(folder, frame, rows, yaws);
                        File.AppendAllText(Path.Combine(folder, "estados.txt"), FormattableString.Invariant($"{frame} {Time.time:0.000} {beat}\n"));
                        frame++;
                    }
                }
            }
            string F(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = Watch();
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    foreach (string act in acts)
                    {
                        float start = Time.time, hit = -1;
                        beat = act;
                        void Landed()
                        {
                            hit = Time.time;
                            Debug.Log($"[rejas] {act} {F(Time.time)} golpe a los {F(hit - start)} s {Reaches(act)}");
                            // (And the frames after it: called as the blow lands, the frame's pose is still the one before, its
                            // stroke a frame short at 10 fps. The bite's, as the one bitten is jerked to the bars.)
                            foreach (float after in new[] { 0.1f, 0.2f, 0.3f })
                                Later(after, () => Debug.Log($"[rejas] {act} {F(Time.time)} tras el golpe {Reaches(act)}"));
                        }
                        Debug.Log($"[rejas] {act} {F(Time.time)} empieza");
                        switch (act)
                        {
                            case "andar":
                                if (caged.Pos.X != front.X || caged.Pos.Y != front.Y)
                                {
                                    await units.MoveAlong(caged.Id, new[] { new Pos { X = front.X, Y = front.Y, Z = front.Z } }, 1);
                                    caged.Pos = new Pos { X = front.X, Y = front.Y, Z = front.Z };
                                }
                                // (Its TurnedEvent: to the corridor, as the state has it after its walk.)
                                caged.Facing = toward;
                                units.Sync(state);
                                await Awaitable.WaitForSecondsAsync(1.6f);
                                break;
                            case "mordisco":
                                // (EventPlayer.PlayAttack: a beat, the blow, 0.05 s, the hit.)
                                await Awaitable.WaitForSecondsAsync(0.2f);
                                await units.Lunge(caged.Id, soldier.Id, units.CanFeed(caged.Id, soldier.Id), () =>
                                {
                                    Landed();
                                    var chest = units.Chest(soldier.Id);
                                    fx.Slash(chest, Fx.Hex(0xe8f6ff), false);
                                    fx.Bite(chest, units.Feet(caged.Id));
                                });
                                await Awaitable.WaitForSecondsAsync(0.05f);
                                await units.HitReact(soldier.Id, 1);
                                await Awaitable.WaitForSecondsAsync(1.0f);
                                break;
                            case "tajo":
                            case "vigilancia":
                                if (act == "vigilancia")
                                {
                                    soldier.Overwatch = true;
                                    soldier.WatchMelee = true;
                                    units.Sync(state);
                                    await Awaitable.WaitForSecondsAsync(1.2f);
                                }
                                await Awaitable.WaitForSecondsAsync(0.2f);
                                await units.Lunge(soldier.Id, caged.Id, false, () =>
                                {
                                    Landed();
                                    fx.Slash(units.Chest(caged.Id), Fx.Hex(0xe8f6ff), false);
                                });
                                await Awaitable.WaitForSecondsAsync(0.05f);
                                await units.HitReact(caged.Id, 1);
                                await Awaitable.WaitForSecondsAsync(act == "vigilancia" ? 0.8f : 1.0f);
                                if (act == "vigilancia")
                                {
                                    soldier.Overwatch = false;
                                    soldier.WatchMelee = false;
                                    units.Sync(state);
                                    await Awaitable.WaitForSecondsAsync(0.8f);
                                }
                                break;
                            case "silencioso":
                                // (Its back to the bars, unaware: what the rules ask of a takedown, Abilities.TakedownRefusal.)
                                caged.Facing = away;
                                units.Sync(state);
                                await Awaitable.WaitForSecondsAsync(1.8f);
                                start = Time.time;
                                await Awaitable.WaitForSecondsAsync(0.2f);
                                await units.Takedown(soldier.Id, caged.Id, Landed);
                                Debug.Log($"[rejas] {act} {F(Time.time)} resuelto a los {F(Time.time - start)} s");
                                await Awaitable.WaitForSecondsAsync(0.05f);
                                units.Sync(state);
                                _ = units.HitReact(caged.Id, 1);
                                await Awaitable.WaitForSecondsAsync(0.1f);
                                units.Sync(state);
                                await units.Die(caged.Id);
                                await Awaitable.WaitForSecondsAsync(0.2f);
                                caged.Alive = false;
                                caged.Hp = 0;
                                units.Sync(state);
                                for (float w = 0; w < 3 && units.TakedownForLab(soldier.Id) != null; w += Time.deltaTime) await Awaitable.NextFrameAsync();
                                await Awaitable.WaitForSecondsAsync(0.8f);
                                break;
                        }
                        Debug.Log($"[rejas] {act} {F(Time.time)} acabado a los {F(Time.time - start)} s{(hit >= 0 ? $", el golpe a los {F(hit - start)} s" : "")}");
                    }
                    beat = "fin";
                    foreach (var pair in nearest) Debug.Log($"[rejas] lo más cerca en {pair.Key}: {Cm(pair.Value)} cm ({(pair.Key == "mordisco" ? "una mano del muerto del pecho del soldado" : pair.Key == "silencioso" ? "el filo de la nuca" : "la punta del pecho del muerto")}, del hueso)");
                    foreach (var pair in worst.OrderBy(p => p.Key))
                        Debug.Log($"[rejas] resumen {pair.Key}: piel {Cm(pair.Value.skin)} cm@{pair.Value.skinAt}, huesos {Cm(pair.Value.bones)} cm@{pair.Value.bonesAt}, hoja {Cm(pair.Value.blade)} cm@{pair.Value.bladeAt}, "
                            + $"tronco pasado {Cm(pair.Value.past)} cm@{pair.Value.pastAt}");
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
                finally
                {
                    done = true;
                }
            };
            EditorApplication.playModeStateChanged += Started;
        }

        static string Cm(float m) => (m * 100).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// This frame's (Rejas): in the bite, the dead one's nearer hand from the soldier's chest; in a blade's blow, its point
        /// from the dead one's chest; in the takedown, the blade from the nape (m, from the bones: the skin is a hand nearer);
        /// null else.
        /// </summary>
        static float? Nearest(string beat)
        {
            var dead = state.Units.First(u => u.Caged == true);
            var a = units.transform.Find(dead.Id)?.GetComponentInChildren<Animator>();
            if (a == null || !a.isHuman) return null;
            if (beat == "mordisco" && a.GetBoneTransform(HumanBodyBones.LeftHand) is Transform lh && a.GetBoneTransform(HumanBodyBones.RightHand) is Transform rh)
                return Mathf.Min(Vector3.Distance(lh.position, units.Chest(soldier.Id)), Vector3.Distance(rh.position, units.Chest(soldier.Id)));
            if ((beat == "tajo" || beat == "vigilancia") && units.BladeForLab(soldier.Id) is var (_, tip)) return Vector3.Distance(tip, units.Chest(dead.Id));
            if (beat == "silencioso" && units.TakedownForLab(soldier.Id) is var (_, _, nape, contact, _, lay, _) && lay > 0.5f) return Vector3.Distance(contact, nape);
            return null;
        }

        /// <summary>`then` `seconds` on (a lab's look a moment after the blow).</summary>
        static async void Later(float seconds, Action then)
        {
            await Awaitable.WaitForSecondsAsync(seconds);
            then();
        }

        /// <summary>
        /// What reaches what at a blow of the lab (Rejas, cm): the bite, the dead one's nearer hand from the soldier's chest; a
        /// blade's blow, its point from the dead one's chest and neck; the takedown, the left palm from where it goes on the
        /// head and the blade from the nape (UnitViews.TakedownForLab); how far apart the roots are.
        /// </summary>
        static string Reaches(string act)
        {
            var dead = state.Units.First(u => u.Caged == true);
            var a = units.transform.Find(dead.Id)?.GetComponentInChildren<Animator>();
            Transform Bone(HumanBodyBones b) => a != null && a.isHuman ? a.GetBoneTransform(b) : null;
            var parts = new List<string>();
            var chest = units.Chest(soldier.Id);
            if (act == "mordisco" && Bone(HumanBodyBones.LeftHand) is Transform lh && Bone(HumanBodyBones.RightHand) is Transform rh)
                parts.Add($"mano-pecho={Cm(Mathf.Min(Vector3.Distance(lh.position, chest), Vector3.Distance(rh.position, chest)))}");
            if ((act == "tajo" || act == "vigilancia") && units.BladeForLab(soldier.Id) is var (grip, tip))
            {
                parts.Add($"punta-pecho={Cm(Vector3.Distance(tip, units.Chest(dead.Id)))}");
                if (Bone(HumanBodyBones.Neck) is Transform neck) parts.Add($"punta-cuello={Cm(Vector3.Distance(tip, neck.position))}");
            }
            if (units.TakedownForLab(soldier.Id) is var (grabAt, palm, nape, contact, grab, lay, offset))
                parts.Add($"mano-izq={Cm(Vector3.Distance(palm, grabAt))} filo-nuca={Cm(Vector3.Distance(contact, nape))} agarre={grab:0.00} apoyo={lay:0.00} paso={Cm(offset.magnitude)}");
            parts.Add($"raíces a {Cm(Vector3.Distance(units.Feet(dead.Id), units.Feet(soldier.Id)))} una de otra");
            return string.Join(" ", parts);
        }

        /// <summary>The bars of the lab (Rejas): their middle on the floor, across them from the dead one to the soldier, along them; each bar found in the scene (along them, across, thick).</summary>
        sealed class RejasFrame
        {
            public Vector3 middle, normal, along;
            public List<(float along, float depth, float r)> bars;
            /// <summary>The crossbars: how high their middle is over the floor, half their height, how far across and half their depth.</summary>
            public List<(float high, float half, float depth, float deep)> rails;
            public float Depth(Vector3 p) => Vector3.Dot(p - middle, normal);
            public float Along(Vector3 p) => Vector3.Dot(p - middle, along);
        }

        /// <summary>The worst of a beat for one of them (Rejas): into the bars, its skin, its bones and its blade, and its trunk past their middle line (m), and where.</summary>
        sealed class RejasWorst
        {
            public float skin, bones, blade, past;
            public string skinAt = "-", bonesAt = "-", bladeAt = "-", pastAt = "-";
        }

        /// <summary>
        /// The bars as the scene draws them (Rejas): every vertex of the map's meshes within 9 cm of their middle line, along
        /// both tiles of the front and a tile more. Those between 1.2 and 1.45 m over the floor (between two crossbars) put
        /// into runs along them (a gap of 6 cm or more between two: a round bar's vertices are 3 cm apart): each run a bar (a
        /// post, if wider), its middle and half its width. Those in a gap (6 cm or less from the middle between two round bars
        /// a pitch apart) put into runs upwards: each a crossbar.
        /// </summary>
        static void BarsInScene(RejasFrame f)
        {
            var points = new List<(float along, float depth, float high)>();
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var mesh = mf.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                if (mesh == null || r == null || mf.GetComponentInParent<UnitViews>() != null) continue;
                var box = r.bounds;
                box.Expand(0.3f);
                if (box.SqrDistance(f.middle + Vector3.up * 1.2f) > 4) continue;
                Vector3[] vertices;
                try
                {
                    vertices = mesh.vertices;
                }
                catch (Exception)
                {
                    continue;
                }
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    float h = p.y - f.middle.y, d = f.Depth(p), a = f.Along(p);
                    if (h < 0.05f || h > 3.4f || Mathf.Abs(d) > 0.09f || Mathf.Abs(a) > 2.4f) continue;
                    points.Add((a, d, h));
                }
            }
            // (Runs of values: where the next is `apart` or more on, a new one.)
            List<(float lo, float hi, float depth, float deep)> Runs(List<(float key, float depth)> values, float apart)
            {
                values.Sort((p, q) => p.key.CompareTo(q.key));
                var runs = new List<(float lo, float hi, float depth, float deep)>();
                int from = 0;
                for (int i = 1; i <= values.Count; i++)
                {
                    if (i < values.Count && values[i].key - values[i - 1].key < apart) continue;
                    if (i > from)
                    {
                        float depth = 0, lo = float.MaxValue, hi = float.MinValue;
                        for (int k = from; k < i; k++)
                        {
                            depth += values[k].depth;
                            lo = Mathf.Min(lo, values[k].depth);
                            hi = Mathf.Max(hi, values[k].depth);
                        }
                        runs.Add((values[from].key, values[i - 1].key, depth / (i - from), (hi - lo) / 2));
                    }
                    from = i;
                }
                return runs;
            }
            f.bars = Runs(points.Where(p => p.high > 1.2f && p.high < 1.45f).Select(p => (p.along, p.depth)).ToList(), 0.06f)
                .Where(b => b.hi - b.lo < 0.25f).Select(b => ((b.lo + b.hi) / 2, b.depth, (b.hi - b.lo) / 2)).ToList();
            float pitch = f.bars.Count > 1 ? (f.bars[f.bars.Count - 1].along - f.bars[0].along) / (f.bars.Count - 1) : 0.3f;
            // (Gaps: half way between two bars next to each other.)
            var gaps = new List<float>();
            // (Only between two round bars a pitch apart: a wall's or a post's vertices on the line made runs of their own.)
            bool Round((float along, float depth, float r) b) => b.r > 0.025f && b.r < 0.04f;
            for (int i = 1; i < f.bars.Count; i++)
                if (Round(f.bars[i]) && Round(f.bars[i - 1]) && Mathf.Abs(f.bars[i].along - f.bars[i - 1].along - 0.3f) < 0.05f) gaps.Add((f.bars[i].along + f.bars[i - 1].along) / 2);
            f.rails = Runs(points.Where(p => gaps.Any(g => Mathf.Abs(p.along - g) < 0.06f)).Select(p => (p.high, p.depth)).ToList(), 0.06f)
                .Select(r => ((r.lo + r.hi) / 2, (r.hi - r.lo) / 2, r.depth, r.deep)).ToList();
            Debug.Log($"[rejas] barrotes: {f.bars.Count} de {points.Count} puntos: " + string.Join(" ", f.bars.Select(b => FormattableString.Invariant($"{b.along:0.000}/{b.depth:0.000}/{b.r * 2:0.000}"))));
            Debug.Log($"[rejas] huecos: " + string.Join(" ", gaps.Select(g => FormattableString.Invariant($"{g:0.000}"))));
            Debug.Log($"[rejas] travesaños: " + string.Join(" ", f.rails.Select(r => FormattableString.Invariant($"{r.high - r.half:0.000}-{r.high + r.half:0.000}/{r.depth:0.000}±{r.deep:0.000}"))));
        }

        /// <summary>The bones as capsules (Rejas): from, to, how far out the body is about them (m, a common figure's).</summary>
        static readonly (HumanBodyBones from, HumanBodyBones to, float r)[] BarsChain =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, 0.14f), (HumanBodyBones.Spine, HumanBodyBones.Chest, 0.14f), (HumanBodyBones.Chest, HumanBodyBones.UpperChest, 0.13f),
            (HumanBodyBones.UpperChest, HumanBodyBones.Neck, 0.08f), (HumanBodyBones.Neck, HumanBodyBones.Head, 0.06f), (HumanBodyBones.Head, HumanBodyBones.LeftEye, 0.09f),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.045f), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.045f),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.035f), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.035f),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, 0.035f), (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, 0.035f),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.07f), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.07f),
        };

        static readonly HashSet<HumanBodyBones> Trunk = new HashSet<HumanBodyBones> { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head };

        static readonly Mesh barsBaked = new Mesh();

        /// <summary>
        /// One frame of one of them (Rejas; `side` 1 the dead one, on the far side from the soldier, −1 the soldier): how deep
        /// into a bar its skin goes (every few of its vertices: inside a bar's round), its bones (capsules: BarsChain) and its
        /// blade (grip to tip, 1.2 cm about it), and how far past the bars' middle line its trunk and head come (their bones,
        /// with the body about them); a line [rejas] and the beat's worst.
        /// </summary>
        static void MeasureBars(RejasFrame f, string id, float side, string beat, Dictionary<string, RejasWorst> worst)
        {
            if (f.bars == null || f.bars.Count == 0) return;
            var root = units.transform.Find(id);
            var a = root != null ? root.GetComponentInChildren<Animator>() : null;
            if (a == null || !a.isHuman || !root.gameObject.activeInHierarchy) return;
            string key = beat + " " + (side > 0 ? "muerto" : "soldado");
            if (!worst.TryGetValue(key, out var w)) worst[key] = w = new RejasWorst();
            float size = a.transform.lossyScale.x / Figures.CharacterScale;
            // (Into the nearest bar of a point with `r` about it, m.)
            float Into(Vector3 p, float r)
            {
                float h = p.y - f.middle.y;
                if (h < 0.05f || h > 2.9f) return 0;
                float d = f.Depth(p), al = f.Along(p), most = 0;
                foreach (var b in f.bars)
                {
                    float gap = Mathf.Sqrt((al - b.along) * (al - b.along) + (d - b.depth) * (d - b.depth));
                    most = Mathf.Max(most, b.r + r - gap);
                }
                // (The crossbars, along the whole front: flat, their height and depth.)
                if (f.bars.Count > 0 && al > f.bars[0].along && al < f.bars[f.bars.Count - 1].along)
                    foreach (var c in f.rails)
                    {
                        // (Its distance from the crossbar's box, below 0 inside it.)
                        float dh = Mathf.Abs(h - c.high) - c.half, dd = Mathf.Abs(d - c.depth) - c.deep;
                        float outside = Mathf.Sqrt(Mathf.Max(0, dh) * Mathf.Max(0, dh) + Mathf.Max(0, dd) * Mathf.Max(0, dd)) + Mathf.Min(0, Mathf.Max(dh, dd));
                        most = Mathf.Max(most, r - outside);
                    }
                return most;
            }
            float skin = 0, bones = 0, blade = 0, past = 0;
            string skinAt = "-", bonesAt = "-", bladeAt = "-", pastAt = "-";
            foreach (var r in units.BodyRenderers(id)?.OfType<SkinnedMeshRenderer>() ?? Enumerable.Empty<SkinnedMeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.sharedMesh == null) continue;
                try
                {
                    r.BakeMesh(barsBaked, true);
                }
                catch (Exception)
                {
                    continue;
                }
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                var vv = barsBaked.vertices;
                var weights = r.sharedMesh.boneWeights;
                var bs = r.bones;
                int stride = Mathf.Max(1, vv.Length / 3000);
                for (int i = 0; i < vv.Length; i += stride)
                {
                    float into = Into(m.MultiplyPoint3x4(vv[i]), 0);
                    if (into <= skin) continue;
                    skin = into;
                    skinAt = i < weights.Length && weights[i].boneIndex0 < bs.Length && bs[weights[i].boneIndex0] != null ? bs[weights[i].boneIndex0].name : "?";
                }
            }
            foreach (var (from, to, r) in BarsChain)
            {
                Transform p = a.GetBoneTransform(from), q = a.GetBoneTransform(to);
                if (p == null || q == null) continue;
                for (int k = 0; k <= 6; k++)
                {
                    var at = Vector3.Lerp(p.position, q.position, k / 6f);
                    float into = Into(at, r * size);
                    if (into > bones) (bones, bonesAt) = (into, from.ToString());
                    // (Past the middle line, towards the other: the trunk and the head only.)
                    if (Trunk.Contains(from))
                    {
                        float over = side * f.Depth(at) + r * size;
                        if (over > past) (past, pastAt) = (over, from.ToString());
                    }
                }
            }
            if (units.BladeForLab(id) is var (grip, tip))
                for (int k = 0; k <= 10; k++)
                {
                    float into = Into(Vector3.Lerp(grip, tip, k / 10f), 0.012f);
                    if (into > blade) (blade, bladeAt) = (into, k == 10 ? "punta" : k == 0 ? "puño" : FormattableString.Invariant($"{k / 10f:0.0}"));
                }
            if (skin > w.skin) (w.skin, w.skinAt) = (skin, skinAt);
            if (bones > w.bones) (w.bones, w.bonesAt) = (bones, bonesAt);
            if (blade > w.blade) (w.blade, w.bladeAt) = (blade, bladeAt);
            if (past > w.past) (w.past, w.pastAt) = (past, pastAt);
            var (cagedNow, arms, through) = units.BarsForLab(id);
            Debug.Log(FormattableString.Invariant($"[rejas] {Time.time:0.000} {beat} {(side > 0 ? "muerto" : "soldado")} piel={skin * 100:0.0}@{skinAt} huesos={bones * 100:0.0}@{bonesAt} hoja={blade * 100:0.0}@{bladeAt} pasado={past * 100:0.0}@{pastAt} raíz={-side * f.Depth(units.Feet(id)) * 100:0} rejas={(cagedNow ? "sí" : "no")} brazos={arms:0.00}{(through ? " a través" : "")}"));
        }

        static Camera barsCamera;

        /// <summary>
        /// One frame of the lab (Rejas, BASTION_TIRAS): from the game's camera (45° down, turned each of `yaws`) 24 and 9
        /// tiles off (960×600, its 40° of view), and close (480×400): over the corridor at three quarters, over the cell, from
        /// above the bars' line.
        /// </summary>
        static void FilmBars(string folder, int frame, RejasFrame f, float[] yaws)
        {
            if (barsCamera == null)
            {
                barsCamera = new GameObject("Cámara de las rejas").AddComponent<Camera>();
                barsCamera.enabled = false;
                barsCamera.farClipPlane = 400;
                barsCamera.clearFlags = CameraClearFlags.SolidColor;
                barsCamera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
                Directory.CreateDirectory(folder);
            }
            // (Skinned again for each render: filmed in the frame's Update, the bodies showed an earlier pose than their bones.)
            foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
            void Shot(string view, Action place, int w, int h)
            {
                place();
                string dir = Path.Combine(folder, view);
                Directory.CreateDirectory(dir);
                Shots.Render(barsCamera, w, h, Path.Combine(dir, $"f{frame:000}.png"));
            }
            var at = f.middle + Vector3.up * 0.9f;
            foreach (float turn in yaws)
                foreach (float tiles in new[] { 24f, 9f })
                    Shot(FormattableString.Invariant($"juego{tiles:0}-{turn:0}"), () => MapSpace.Aim(barsCamera, at, tiles, turn), 960, 600);
            void Close(string view, Vector3 from, Vector3 to, float fov) => Shot(view, () =>
            {
                barsCamera.fieldOfView = fov;
                barsCamera.transform.position = from;
                barsCamera.transform.LookAt(to);
            }, 480, 400);
            Close("pasillo", f.middle + f.normal * 2.3f + f.along * 1.3f + Vector3.up * 1.8f, f.middle + Vector3.up * 1.15f, 42);
            Close("celda", f.middle - f.normal * 2.4f + f.along * 1.2f + Vector3.up * 2.0f, f.middle + Vector3.up * 1.15f, 42);
            Close("lado", f.middle + f.along * 3.3f + Vector3.up * 3.6f, f.middle + Vector3.up * 1.0f - f.along * 0.3f, 40);
        }
    }
}
