using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The runs' first boss, the Brute (UnitViews.Brute; Gameplay's rules of 16/11), on the plain floor with a soldier:
    /// drawn with Boss Zombies' Brute and moving and acting as one (UnitViews.LabBody, LabKind) over a template the engine
    /// has (the Chillón's: no eating, no weapon), as UI's event player would play it (EventPlayer's waits: PlayAttack, then
    /// its DamagedEvent, DiedEvent). BASTION_ACCION:
    ///   andar: it walks three tiles up to the soldier; rugido: it roars at it; golpe: its blow from beside it, the
    ///   soldier's hit; embestida: it charges five tiles in a straight line and knocks it down, which gets up 2 s on;
    ///   tumbado: the soldier knocked down from where it stands, up 2 s on, down again, hit and killed lying there;
    ///   muerte: the soldier shoots it dead; todo (unless said): walks, roars, charges (the soldier down and up), strikes,
    ///   dies.
    /// Each frame BASTION_TIRAS (tira.sh sets it) gets four pictures of its own, in folder/&lt;view&gt;/f/fNNN.png: «sur» and
    /// «norte», close and low from either side, whole figures; «juego24» and «juego9», the game's camera that many tiles off,
    /// the middle 480×300 of its 1600×1000 frame; all on the middle of the two; folder/estados.txt «frame time step». The
    /// strip's own camera is «sur». Logged: [bruto] each step and its seconds, [alto] the heights, [cuerpo] how deep each
    /// goes into the other each frame they are near (cm, the bone's), [pies] the Brute's feet each frame, [golpe] and
    /// [choque] where its fists and its chest are as the blow lands and as the charge meets.
    ///   zsh tira.sh brute CombatLab.Brute 300 960x540 BASTION_ACCION=todo
    /// </summary>
    public static partial class CombatLab
    {
        public static void Brute()
        {
            string action = Environment.GetEnvironmentVariable("BASTION_ACCION") ?? "todo";
            var first = MapLab.Golden("city-7");
            string soldierId = first.Units.First(u => u.Alive && u.Team == "xcom").Id;
            string deadId = first.Units.First(u => u.Alive && u.Template == "zombie").Id;
            // The Brute's body, gait and size over the Chillón's rules (the engine has no «brute» yet).
            UnitViews.LabBody = "brute";
            UnitViews.LabKind = "brute";
            const int sx = 11;
            int bx = action switch { "andar" => 16, "rugido" => 14, "golpe" => 12, "embestida" => 17, "tumbado" => 13, "muerte" => 14, _ => 20 };
            Plain(u => u.Id == soldierId || u.Id == deadId, sx, bx);
            soldier = Put(state.Units.First(u => u.Id == soldierId), sx, 10, 0);
            var copy = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(state.Units.First(u => u.Id == deadId)));
            copy.Id = "bruto";
            copy.Template = TemplateIds.Sectoid;
            target = Put(copy, bx, 10, Math.PI);
            state.Units = new List<Unit> { soldier, target };
            string brute = target.Id, man = soldier.Id;
            Pos P(int x) => new Pos { X = x, Y = 10 };
            Pos[] Line(int from, int to) => Enumerable.Range(0, Math.Abs(to - from)).Select(k => P(from + Math.Sign(to - from) * (k + 1))).ToArray();
            string now = "inicio";
            bool done = false;
            float stepAt = 0;
            void Say(string what)
            {
                now = what;
                stepAt = Time.time;
                Debug.Log(FormattableString.Invariant($"[bruto] {Time.time:0.00} s: {what}"));
            }
            void Took(string what) => Debug.Log(FormattableString.Invariant($"[bruto] {what}: {Time.time - stepAt:0.00} s"));

            async Awaitable Walk(int to)
            {
                Say("andar");
                await units.MoveAlong(brute, Line(target.Pos.X, to), 1);
                target.Pos = P(to);
                Took("andar 3 casillas");
                await Awaitable.WaitForSecondsAsync(0.6f);
            }
            async Awaitable RoarAt()
            {
                // (UI turns it to the squad as it sees it, then the roar.)
                units.Face(brute, units.Feet(man));
                await Awaitable.WaitForSecondsAsync(0.3f);
                Say("rugido");
                Peaks();
                await units.Roar(brute);
                Took("rugido (hasta resolverse)");
                await Awaitable.WaitForSecondsAsync(0.8f);
            }
            async Awaitable SmashIt()
            {
                Say("golpe");
                await units.Smash(brute, man, () =>
                {
                    Took("golpe hasta onHit");
                    // (A frame on: as it lands the bones are the frame before's.)
                    Later(() => Debug.Log("[golpe] " + Fists()));
                });
                // (EventPlayer: a blow that lands, its DamagedEvent straight after; 6 of 10 or so: a hit, not a critical.)
                await Awaitable.WaitForSecondsAsync(0.05f);
                await units.HitReact(man, 1);
                now = "golpe-tras";
                await Awaitable.WaitForSecondsAsync(1.4f);
            }
            async Awaitable ChargeIt(int to)
            {
                Say("embestida");
                var path = Line(target.Pos.X, to);
                await units.Charge(brute, path, man, () =>
                {
                    Took($"embestida de {path.Length} casillas hasta onImpact");
                    Later(() => Debug.Log("[choque] " + Met()));
                });
                target.Pos = P(to);
                Say("tumbado");
                await units.KnockDown(man, units.Feet(brute));
                Took("caída (hasta quieto)");
                now = "en-el-suelo";
                // (The soldier stays down till the squad's next turn: here, 2 s.)
                await Awaitable.WaitForSecondsAsync(2);
                Say("levantarse");
                await units.GetUp(man);
                Took("levantarse");
                now = "de-pie";
                await Awaitable.WaitForSecondsAsync(1);
            }
            async Awaitable Down()
            {
                Say("tumbado");
                await units.KnockDown(man, units.Feet(brute));
                Took("caída (hasta quieto)");
                now = "en-el-suelo";
                await Awaitable.WaitForSecondsAsync(2);
                Say("levantarse");
                await units.GetUp(man);
                Took("levantarse");
                now = "de-pie";
                await Awaitable.WaitForSecondsAsync(1);
                Say("tumbado-2");
                await units.KnockDown(man, units.Feet(brute));
                now = "en-el-suelo-2";
                await Awaitable.WaitForSecondsAsync(0.6f);
                // (Hit lying there, then killed there: its flash, no second fall.)
                Say("golpeado-en-el-suelo");
                await units.HitReact(man, 1);
                Say("muere-en-el-suelo");
                await units.Die(man);
                Took("muerte en el suelo");
                now = "muerto";
                await Awaitable.WaitForSecondsAsync(1.5f);
            }
            async Awaitable Shot()
            {
                Say("disparo");
                int aim = await units.RaiseWeapon(man, units.Chest(brute));
                var from = units.Muzzle(man);
                var to = units.Chest(brute);
                fx.MuzzleFlash(from, Bastion.Client.Effects.Fx.Hex(0xffd27a));
                units.Recoil(man);
                await fx.Bullet(from, to, Bastion.Client.Effects.Fx.Hex(0xffd27a));
                fx.Sparks(to, Bastion.Client.Effects.Fx.Hex(0xffc46b));
                // (EventPlayer: a damage that kills, its hit loose and LethalBeat, then the death waited for.)
                _ = units.HitReact(brute, 1.5f);
                await Awaitable.WaitForSecondsAsync(0.1f);
                Say("muerte");
                await units.Die(brute);
                Took("muerte del bruto (hasta quieto)");
                now = "muerto";
                _ = units.LowerWeapon(man, aim);
                await Awaitable.WaitForSecondsAsync(1.5f);
            }

            // Where its fists are as the blow lands, against the soldier's head and chest (m: along the line from the Brute,
            // up, across), and how far apart.
            string Fists()
            {
                var a = Figures.FeetOf(brute)?.GetComponentInChildren<Animator>();
                if (a == null || !a.isHuman) return "-";
                Vector3 l = a.GetBoneTransform(HumanBodyBones.LeftHand).position, r = a.GetBoneTransform(HumanBodyBones.RightHand).position;
                var along = units.Feet(man) - units.Feet(brute);
                along.y = 0;
                along.Normalize();
                var across = Vector3.Cross(Vector3.up, along);
                string Off(Vector3 p, Vector3 from) => FormattableString.Invariant($"({Vector3.Dot(p - from, along):0.00},{p.y - from.y:0.00},{Vector3.Dot(p - from, across):0.00})");
                var mid = (l + r) / 2;
                return $"fists apart {Vector3.Distance(l, r):0.00} m; from the soldier's head {Off(mid, units.Head(man))}, from its chest {Off(mid, units.Chest(man))}";
            }
            // Where the Brute's chest and head are as the charge meets the soldier, against the soldier's chest (m along the line, up).
            string Met()
            {
                var along = units.Feet(man) - units.Feet(brute);
                along.y = 0;
                along.Normalize();
                string Off(Vector3 p) => FormattableString.Invariant($"({Vector3.Dot(p - units.Chest(man), along):0.00},{p.y - units.Chest(man).y:0.00})");
                var body = Figures.FeetOf(brute)?.GetComponentInChildren<Animator>();
                var shoulder = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.LeftUpperArm).position : units.Chest(brute);
                return $"its chest {Off(units.Chest(brute))}, head {Off(units.Head(brute))}, left shoulder {Off(shoulder)} from the soldier's chest";
            }
            // The roar's height: the head and the hands over the feet as it is up (m), each frame of it.
            async void Peaks()
            {
                var a = Figures.FeetOf(brute)?.GetComponentInChildren<Animator>();
                if (a == null || !a.isHuman) return;
                float head = 0, hands = 0;
                for (float t0 = Time.time; Time.time - t0 < 1.4f;)
                {
                    await Awaitable.NextFrameAsync();
                    if (a == null) return;
                    float y = units.Feet(brute).y;
                    head = Mathf.Max(head, a.GetBoneTransform(HumanBodyBones.Head).position.y - y);
                    hands = Mathf.Max(hands, Mathf.Min(a.GetBoneTransform(HumanBodyBones.LeftHand).position.y, a.GetBoneTransform(HumanBodyBones.RightHand).position.y) - y);
                }
                Debug.Log(FormattableString.Invariant($"[alto] roaring: head up to {head:0.00} m, both hands up to {hands:0.00} m"));
            }

            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = BruteWatch(() => now, () => done, brute, man, Fists);
                    await Awaitable.WaitForSecondsAsync(1);
                    Heights(brute, man);
                    switch (action)
                    {
                        case "andar":
                            await Walk(13);
                            break;
                        case "rugido":
                            await RoarAt();
                            break;
                        case "golpe":
                            await SmashIt();
                            break;
                        case "embestida":
                            await ChargeIt(12);
                            break;
                        case "tumbado":
                            await Down();
                            break;
                        case "muerte":
                            await Shot();
                            break;
                        default:
                            await Walk(17);
                            await RoarAt();
                            await ChargeIt(12);
                            await SmashIt();
                            await Shot();
                            break;
                    }
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
        }

        /// <summary>`what` a frame on (Awaitable.NextFrameAsync).</summary>
        static async void Later(Action what)
        {
            await Awaitable.NextFrameAsync();
            what();
        }

        /// <summary>[alto]: each one's head over its feet and the top of its body (m), standing at ease.</summary>
        static void Heights(params string[] ids)
        {
            foreach (string id in ids)
            {
                var feet = Figures.FeetOf(id);
                var a = feet?.GetComponentInChildren<Animator>();
                if (a == null || !a.isHuman) continue;
                var body = units.BodyOnlyForLab(id);
                float top = body != null && body.Count > 0 ? body.Max(r => r.bounds.max.y) - feet.position.y : 0;
                float head = a.GetBoneTransform(HumanBodyBones.Head).position.y - feet.position.y;
                float neck = a.GetBoneTransform(HumanBodyBones.Neck).position.y - feet.position.y;
                float shoulders = Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, a.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                Debug.Log(FormattableString.Invariant($"[alto] {id}: top {top:0.00} m, head {head:0.00}, neck {neck:0.00}, shoulders {shoulders:0.00} apart, size {feet.GetChild(0).lossyScale.x:0.000}"));
            }
        }

        /// <summary>
        /// Each frame till `done`: [pies] the Brute's feet, root and body (m), [cuerpo] how deep each goes into the other (cm)
        /// while they are within two tiles, and BASTION_TIRAS's four pictures (Brute's class notes).
        /// </summary>
        static async Awaitable BruteWatch(Func<string> now, Func<bool> done, string brute, string man, Func<string> fists)
        {
            string folder = Environment.GetEnvironmentVariable("BASTION_TIRAS");
            var views = new List<(string name, Camera camera, int w, int h)>();
            if (folder != null)
            {
                foreach (var (name, w, h) in new[] { ("sur", 640, 400), ("norte", 640, 400), ("juego24", 480, 300), ("juego9", 480, 300) })
                {
                    var c = new GameObject("Cámara " + name).AddComponent<Camera>();
                    c.enabled = false;
                    c.farClipPlane = 400;
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
                    views.Add((name, c, w, h));
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder, name, "f"));
                }
            }
            var shot = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Shot camera");
            Dictionary<string, List<Segment>> profile = null;
            var baked = new Mesh();
            int frame = 0;
            while (!done())
            {
                await Awaitable.NextFrameAsync();
                if (units == null) return;
                var b = Figures.FeetOf(brute);
                var s = Figures.FeetOf(man);
                if (b == null || s == null) continue;
                // (On the middle of the two, or on the Brute while it is far: eased between, 4.5 to 2.5 tiles apart.)
                var apart = units.Feet(brute) - units.Feet(man);
                apart.y = 0;
                var mid = Vector3.Lerp(units.Feet(brute), (units.Feet(brute) + units.Feet(man)) / 2, Mathf.Clamp01((4.5f - apart.magnitude / MapSpace.Tile) / 2));
                // The strip's own camera, close from the south on the middle of the two.
                if (shot != null) BruteClose(shot, mid, -1);
                var ba = b.GetComponentInChildren<Animator>();
                if (ba != null && ba.isHuman)
                {
                    Vector3 l = ba.GetBoneTransform(HumanBodyBones.LeftFoot).position, r = ba.GetBoneTransform(HumanBodyBones.RightFoot).position, o = units.Feet(brute);
                    var body = b.childCount > 0 ? b.GetChild(0).position : o;
                    Debug.Log(FormattableString.Invariant($"[pies] {Time.time:0.000} {now()} {l.x:0.0000} {l.y:0.0000} {l.z:0.0000} {r.x:0.0000} {r.y:0.0000} {r.z:0.0000} {body.x:0.0000} {body.z:0.0000}"));
                }
                // How deep: each one's skin against the other's stretches (RevisionLab's Profile, made at ease, first time near).
                var gap = units.Feet(brute) - units.Feet(man);
                gap.y = 0;
                if (gap.magnitude < 2.2f * MapSpace.Tile && ba != null)
                {
                    profile ??= new Dictionary<string, List<Segment>>
                    {
                        [brute] = ProfileOf(brute),
                        [man] = ProfileOf(man),
                    };
                    var (intoMan, manBone) = Deepest(brute, profile[man], baked);
                    var (intoBrute, bruteBone) = Deepest(man, profile[brute], baked);
                    // (And what the soldier holds into the Brute, its hands left out.)
                    string held = "";
                    var sa = s.GetComponentInChildren<Animator>();
                    var palms = new List<Vector3>();
                    if (sa != null && sa.isHuman)
                        foreach (var hb in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand }) palms.Add(sa.GetBoneTransform(hb).position);
                    foreach (var (model, drawn) in units.CarriedForLab(man))
                    {
                        if (!drawn || model == null || !model.gameObject.activeInHierarchy) continue;
                        var (depth, bone) = Into(model, profile[brute], palms);
                        held = FormattableString.Invariant($" weapon={depth * 100:0.0}@{bone}");
                    }
                    Debug.Log(FormattableString.Invariant($"[cuerpo] {Time.time:0.000} {now()} gap={gap.magnitude / MapSpace.Tile:0.00} bruteIntoSoldier={intoMan * 100:0.0}@{manBone} soldierIntoBrute={intoBrute * 100:0.0}@{bruteBone}{held}"));
                }
                if (now() == "golpe") Debug.Log(FormattableString.Invariant($"[golpe] {Time.time:0.000} ") + fists());
                if (folder == null) continue;
                foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
                System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "estados.txt"), FormattableString.Invariant($"{frame:000} {Time.time:0.00} {now()}\n"));
                foreach (var (name, camera, w, h) in views)
                {
                    if (name == "sur" || name == "norte") BruteClose(camera, mid, name == "sur" ? -1 : 1);
                    else
                    {
                        MapSpace.Aim(camera, mid + Vector3.up * 0.9f, name == "juego24" ? 24 : 9);
                        // (The game's 1600×1000 frame's middle w×h: the same camera with that crop's field of view.)
                        camera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(20 * Mathf.Deg2Rad) * h / 1000f) * Mathf.Rad2Deg;
                    }
                    Shots.Render(camera, w, h, System.IO.Path.Combine(folder, name, "f", $"f{frame:000}.png"));
                }
                frame++;
            }
        }

        /// <summary>Close and low from the south (`side` −1) or the north (+1), whole figures, on `mid`.</summary>
        static void BruteClose(Camera camera, Vector3 mid, int side)
        {
            camera.fieldOfView = 40;
            camera.transform.position = mid + new Vector3(0, 1.4f, side * 6.5f);
            camera.transform.LookAt(mid + Vector3.up * 1.0f);
        }

        /// <summary>`id`'s body as stretches along its bones (RevisionLab's Profile), its skin as it stands now.</summary>
        static List<Segment> ProfileOf(string id)
        {
            var a = Figures.FeetOf(id)?.GetComponentInChildren<Animator>();
            var skins = units.BodyOnlyForLab(id)?.OfType<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null).ToList();
            if (a == null || !a.isHuman || skins == null || skins.Count == 0) return new List<Segment>();
            var profile = Profile(a, skins);
            Debug.Log($"[cuerpo] profile of {id}: {profile.Count} stretches, {skins.Count} skins ({string.Join(", ", skins.Select(r => $"{r.name} {r.sharedMesh.vertexCount} v, readable {r.sharedMesh.isReadable}"))})");
            return profile;
        }

        /// <summary>How deep `id`'s skin (its body's meshes baked as they are now, some 600 of their vertices) goes into `body` (m, past 1.5 cm), and the bone of that stretch.</summary>
        static (float depth, string bone) Deepest(string id, List<Segment> body, Mesh baked)
        {
            const float Margin = 0.015f;
            float deepest = 0;
            string at = "-";
            if (body.Count == 0) return (0, "-");
            foreach (var r in units.BodyOnlyForLab(id)?.OfType<SkinnedMeshRenderer>() ?? Enumerable.Empty<SkinnedMeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.sharedMesh == null) continue;
                r.BakeMesh(baked, true);
                var vv = baked.vertices;
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                int stride = Mathf.Max(1, vv.Length / 600);
                for (int i = 0; i < vv.Length; i += stride)
                {
                    var p = m.MultiplyPoint3x4(vv[i]);
                    foreach (var seg in body)
                    {
                        if (!Place(seg, p, out int band, out int sector, out float d)) continue;
                        float radius = seg.radius[band, sector];
                        if (radius <= 0 || d >= radius - Margin || radius - d <= deepest) continue;
                        deepest = radius - d;
                        at = seg.bone.name;
                    }
                }
            }
            return (deepest, at);
        }
    }
}
