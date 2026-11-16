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
    /// <summary>The silent takedown (0.12.26, Gameplay's SIGILO-Y-RECLUTAS.md §2): UnitViews.Takedown as the event player plays it, filmed and measured.</summary>
    public static partial class CombatLab
    {
        /// <summary>One pair of the lab: a soldier and the dead one beside it, how they stand, and what was measured.</summary>
        sealed class Silent
        {
            public string name, blade, sex, template;
            public float angle;
            public bool crouched, low, eating, diagonal, before;
            public Unit s, z, body;
            public int x, y;
            /// <summary>When it began, the blow landed, Takedown (or Lunge) was over, the dead one's head began to go down (after the blow), Die was called; seconds.</summary>
            public float start = -1, hit = -1, resolved = -1, fall = -1, dieAt = -1, hitReact = -1, dieTook = -1, over = -1;
            /// <summary>The dead one's head over its feet at the blow, the least since, and how far it came up again after Die was called (a fall begun again).</summary>
            public float headAtHit, headLeast = float.MaxValue, rise;
            /// <summary>The deepest any bone of the soldier (its hands apart), its hands, its skin went into the dead one's body (m), and when.</summary>
            public float bones, hands, skin;
            public string bonesAt = "-", handsAt = "-", skinAt = "-";
            public bool finished;
        }

        /// <summary>
        /// The silent takedown (UnitViews.Takedown), pairs on a plain floor, all at once: each a soldier (a rifle in its hands,
        /// the blade at its belt; the knife when its slot is empty) and one of the dead on the next tile, as the event player
        /// plays a ShotEvent of AbilityIds.Takedown and what follows it (EventPlayer.PlayAttack, DamagedEvent, DiedEvent):
        /// 0.2 s, the takedown (awaited till the blow lands), 0.05 s, Sync (the shot settled: the dead one still alive in the
        /// state), HitReact let go and LethalBeat (0.1 s), Sync, Die awaited, 0.2 s, the death applied, Sync.
        /// BASTION_CASOS="hoja,sexo,plantilla,ángulo,postura[,flags…];…" (each pair; unset, one from BASTION_HOJA (knife),
        /// BASTION_SEXO (m), BASTION_TEMPLATE (zombie), BASTION_ANGULO (espalda), BASTION_OCULTOS=1 (agachado)): hoja any
        /// blade of Anims.json; sexo m|f; plantilla zombie|xenoid; ángulo espalda|lado|frente or degrees (0: its back to the
        /// soldier, 90 its side, 180 facing it); postura de-pie|agachado (BASTION_OCULTOS=1: the squad unseen, all crouched as
        /// the game has them). Flags: antes (the blade's stroke instead, Lunge, its HitReact and Die as for any kill: the
        /// «before»), comiendo (it eats a fallen soldier first, Feed), bajo (crouched: Unit.Hunkered), diagonal (on the
        /// diagonal tile). Logs [silencio]: each frame of each pair (its time, the dead one's head, the left hand from where it
        /// goes, the blade from the nape, how deep the soldier's bones and skin go into the dead one's body: Profile, or
        /// capsules where its mesh gives none); at the blow, the blade's point from the neck and the head; how long after it
        /// the head began to go down; HitReact's and Die's waits and whether the fall began again; the whole takedown's time.
        /// BASTION_TIRAS=&lt;folder&gt;: each pair filmed each frame into &lt;folder&gt;/&lt;pair&gt;-der (from the soldier's right, the
        /// blade's side), -izq (its left, the hand on the head), 480×400, and -juego9, -juego24 (the game's camera 9 and 24
        /// tiles off, the middle 400×400 of its 1600×1000 frame).
        ///   BASTION_CASOS="knife,m,zombie,espalda,agachado;machete,f,xenoid,lado,de-pie" unity/tools/captura.sh -shotSetup
        ///     Bastion.EditorTools.CombatLab.Silencioso -shotPlay -shotCamera "Shot camera" -shotFrames 50 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Silencioso()
        {
            var cases = SilentCases();
            var first = MapLab.Golden("city-7");
            string soldierJson = Bastion.Net.Json.Write(first.Units.First(u => u.Alive && u.Team == "xcom"));
            string DeadJson(string template) => Bastion.Net.Json.Write(first.Units.FirstOrDefault(u => u.Alive && u.Template == template) ?? first.Units.First(u => u.Alive && u.Template == "zombie"));
            Plain(u => false, 1, 3);
            for (int k = 0; k < cases.Count; k++)
            {
                var c = cases[k];
                c.x = 2 + 5 * (k % 5);
                c.y = 7 + 8 * (k / 5);
                int dx = 1, dy = c.diagonal ? -1 : 0;
                double way = Math.Atan2(dy, dx);
                c.s = Bastion.Net.Json.Read<Unit>(soldierJson);
                c.s.Id = "lab-s" + k;
                c.s.Template = "rookie";
                c.s.Overwatch = false;
                c.s.Hidden = null;
                c.s.Hunkered = c.crouched;
                c.s.Arms = new Arms { Primary = "assaultRifle", Melee = c.blade == "knife" ? null : c.blade };
                // (The baton is no weapon of the Armería: the Cuervos'. In its hands alone, as CombatLab.Cuerpo has it.)
                if (c.blade == "baton") UnitViews.ArtsForLab[c.s.Id] = "baton";
                else UnitViews.ArtsForLab.Remove(c.s.Id);
                if (c.s.Appearance != null) c.s.Appearance.Sex = c.sex;
                Put(c.s, c.x, c.y, way);
                c.z = Bastion.Net.Json.Read<Unit>(DeadJson(c.template));
                c.z.Id = "lab-z" + k;
                c.z.Template = c.template;
                c.z.Alive = true;
                c.z.Overwatch = false;
                c.z.Hidden = null;
                c.z.Hunkered = c.low;
                Put(c.z, c.x + dx, c.y + dy, way + (c.eating ? 0 : c.angle * Math.PI / 180));
                state.Units.Add(c.s);
                state.Units.Add(c.z);
                if (c.eating)
                {
                    c.body = Bastion.Net.Json.Read<Unit>(soldierJson);
                    c.body.Id = "lab-b" + k;
                    c.body.Template = "rookie";
                    c.body.Alive = false;
                    c.body.Hp = 0;
                    Put(c.body, c.x + 2 * dx, c.y + 2 * dy, way + Math.PI / 2);
                    state.Units.Add(c.body);
                }
                Debug.Log($"[silencio] {c.name}: {c.s.Id} at ({c.x},{c.y}), {c.z.Id} {c.template} at ({c.x + dx},{c.y + dy})");
            }
            // Shots' own camera: low from the south on the first pair.
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            var mid0 = (MapSpace.World(cases[0].x, cases[0].y) + MapSpace.World(cases[0].x + 1, cases[0].y)) / 2;
            camera.transform.position = mid0 + new Vector3(0, 1.4f, -4.2f);
            camera.transform.LookAt(mid0 + Vector3.up * 1.0f);
            string folder = Environment.GetEnvironmentVariable("BASTION_TIRAS");
            bool done = false;
            var profiles = new Dictionary<string, List<Segment>>();
            async Awaitable Watch()
            {
                int frame = 0;
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    foreach (var c in cases) Measure(c, profiles);
                    if (folder != null) FilmPairs(cases, folder, frame++);
                }
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = Watch();
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    // (Its body measured standing as its clips and its lean have it; it moves with its bones from then on.)
                    foreach (var c in cases) profiles[c.z.Id] = DeadProfile(c.z.Id);
                    var runs = cases.Select(PlaySilent).ToList();
                    foreach (var r in runs) await r;
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    foreach (var c in cases)
                    {
                        string F(float f) => f < 0 ? "-" : f.ToString("0.00", CultureInfo.InvariantCulture);
                        string Cm(float m) => (m * 100).ToString("0.0", CultureInfo.InvariantCulture);
                        Debug.Log($"[silencio] {c.name} resumen: golpe a los {F(c.hit - c.start)} s, resuelto a los {F(c.resolved)} s, la caída {F(c.fall)} s tras el golpe, "
                            + $"HitReact {F(c.hitReact)} s, Die {F(c.dieTook)} s, sube tras Die {Cm(c.rise)} cm ({(c.rise > 0.15f ? "SE REPITE" : "no se repite")}), "
                            + $"acabado a los {F(c.over)} s; dentro del muerto: huesos {Cm(c.bones)} cm@{c.bonesAt}, manos {Cm(c.hands)} cm@{c.handsAt}, piel {Cm(c.skin)} cm@{c.skinAt}; "
                            + $"muerte {units.DeathForLab(c.z.Id) ?? "-"}");
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

        /// <summary>BASTION_CASOS (or the single pair from the other variables).</summary>
        static List<Silent> SilentCases()
        {
            string Env(string name, string or) => Environment.GetEnvironmentVariable(name) is string s && s.Length > 0 ? s : or;
            string spec = Environment.GetEnvironmentVariable("BASTION_CASOS");
            if (string.IsNullOrEmpty(spec))
            {
                var flags = new List<string>();
                if (Environment.GetEnvironmentVariable("BASTION_ANTES") == "1") flags.Add("antes");
                if (Environment.GetEnvironmentVariable("BASTION_COMIENDO") == "1") flags.Add("comiendo");
                if (Environment.GetEnvironmentVariable("BASTION_MUERTO_AGACHADO") == "1") flags.Add("bajo");
                if (Environment.GetEnvironmentVariable("BASTION_DIAGONAL") == "1") flags.Add("diagonal");
                spec = string.Join(",", new[] { Env("BASTION_HOJA", "knife"), Env("BASTION_SEXO", "m"), Env("BASTION_TEMPLATE", "zombie"), Env("BASTION_ANGULO", "espalda"),
                    Environment.GetEnvironmentVariable("BASTION_OCULTOS") == "1" ? "agachado" : "de-pie" }.Concat(flags));
            }
            var cases = new List<Silent>();
            foreach (string one in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var f = one.Split(',').Select(p => p.Trim()).ToList();
                string At(int k, string or) => k < f.Count && f[k].Length > 0 ? f[k] : or;
                string angle = At(3, "espalda");
                var c = new Silent
                {
                    blade = At(0, "knife"),
                    sex = At(1, "m") == "f" ? "f" : "m",
                    template = At(2, "zombie"),
                    angle = angle == "espalda" ? 0 : angle == "lado" ? 90 : angle == "frente" ? 180 : float.Parse(angle, CultureInfo.InvariantCulture),
                    crouched = At(4, "de-pie") == "agachado",
                };
                var flags = f.Skip(5).ToList();
                c.before = flags.Contains("antes");
                c.eating = flags.Contains("comiendo");
                c.low = flags.Contains("bajo");
                c.diagonal = flags.Contains("diagonal");
                c.name = string.Join("-", f.Where(p => p.Length > 0));
                cases.Add(c);
            }
            return cases;
        }

        /// <summary>One pair's takedown as the event player plays it (Silencioso), its moments kept for the summary.</summary>
        static async Awaitable PlaySilent(Silent c)
        {
            string F(float f) => f.ToString("0.000", CultureInfo.InvariantCulture);
            if (c.eating)
            {
                // (It eats the fallen first, down on its knees by the body, a while.)
                await units.Feed(c.z.Id, c.body.Id);
                await Awaitable.WaitForSecondsAsync(0.8f);
            }
            // (EventPlayer.PlayAttack: a beat before a blow.)
            await Awaitable.WaitForSecondsAsync(0.2f);
            c.start = Time.time;
            Debug.Log($"[silencio] {c.name} {F(Time.time)} empieza {AtBlow(c)}");
            void Landed()
            {
                c.hit = Time.time;
                c.headAtHit = HeadOver(c.z.Id);
                c.headLeast = c.headAtHit;
                Debug.Log($"[silencio] {c.name} {F(Time.time)} golpe a los {F(c.hit - c.start)} s {AtBlow(c)}");
            }
            if (c.before) await units.Lunge(c.s.Id, c.z.Id, false, Landed);
            else await units.Takedown(c.s.Id, c.z.Id, Landed);
            c.resolved = Time.time - c.start;
            Debug.Log($"[silencio] {c.name} {F(Time.time)} resuelto a los {F(c.resolved)} s");
            await Awaitable.WaitForSecondsAsync(0.05f);
            units.Sync(state);
            // The damage: its HitReact let go (it does nothing on one felled silently), LethalBeat, settled.
            Timed(units.HitReact(c.z.Id, 1), d => c.hitReact = d);
            await Awaitable.WaitForSecondsAsync(0.1f);
            units.Sync(state);
            // The death: Die awaited, then applied.
            c.dieAt = Time.time;
            await units.Die(c.z.Id);
            c.dieTook = Time.time - c.dieAt;
            Debug.Log($"[silencio] {c.name} {F(Time.time)} Die esperó {F(c.dieTook)} s");
            await Awaitable.WaitForSecondsAsync(0.2f);
            c.z.Alive = false;
            c.z.Hp = 0;
            units.Sync(state);
            // On till the soldier is back where it was, as it was.
            for (float w = 0; w < 3 && units.TakedownForLab(c.s.Id) != null; w += Time.deltaTime) await Awaitable.NextFrameAsync();
            c.over = Time.time - c.start;
            Debug.Log($"[silencio] {c.name} {F(Time.time)} acabado a los {F(c.over)} s");
            await Awaitable.WaitForSecondsAsync(0.6f);
            c.finished = true;
        }

        static async void Timed(Awaitable a, Action<float> took)
        {
            float from = Time.time;
            try
            {
                await a;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            took(Time.time - from);
        }

        /// <summary>`id`'s head over its feet (m).</summary>
        static float HeadOver(string id) => units.Head(id).y - units.Feet(id).y;

        /// <summary>
        /// At the blow: what of the blade goes in from where it should (the nape) and the blade's point from the dead one's
        /// neck and head bones; the left palm from where it goes on the head; the soldier's chest from the dead one's, along
        /// the ground (cm).
        /// </summary>
        static string AtBlow(Silent c)
        {
            string Cm(float m) => (m * 100).ToString("0.0", CultureInfo.InvariantCulture);
            var parts = new List<string>();
            var za = units.transform.Find(c.z.Id)?.GetComponentInChildren<Animator>();
            Transform neck = za != null && za.isHuman ? za.GetBoneTransform(HumanBodyBones.Neck) : null, head = za != null && za.isHuman ? za.GetBoneTransform(HumanBodyBones.Head) : null;
            if (units.BladeForLab(c.s.Id) is var (grip, tip))
            {
                if (neck != null) parts.Add($"punta-cuello={Cm(Vector3.Distance(tip, neck.position))}");
                if (head != null) parts.Add($"punta-cabeza={Cm(Vector3.Distance(tip, head.position))}");
                if (neck != null && head != null) parts.Add($"hoja-nuca={Cm(SegmentDistance(grip, tip, Vector3.Lerp(neck.position, head.position, 0.5f)))}");
            }
            if (units.TakedownForLab(c.s.Id) is var (grabAt, palm, nape, contact, grab, lay, offset))
                parts.Add($"filo-nuca={Cm(Vector3.Distance(contact, nape))} mano-izq={Cm(Vector3.Distance(palm, grabAt))} agarre={grab:0.00} apoyo={lay:0.00} paso={Cm(offset.magnitude)}");
            var d = units.Chest(c.s.Id) - units.Chest(c.z.Id);
            parts.Add($"pechos={Cm(new Vector2(d.x, d.z).magnitude)}");
            // (Along the line from the soldier's feet to the dead one's, and up, cm: where each part is.)
            var sa = units.transform.Find(c.s.Id)?.GetComponentInChildren<Animator>();
            var o = units.Feet(c.s.Id);
            var line = units.Feet(c.z.Id) - o;
            line.y = 0;
            line.Normalize();
            string P(Transform t) => t == null ? "-" : $"{Cm(Vector3.Dot(t.position - o, line))}/{Cm(t.position.y - o.y)}";
            if (sa != null && sa.isHuman && za != null && za.isHuman)
                parts.Add($"linea: caderas {P(sa.GetBoneTransform(HumanBodyBones.Hips))} pecho {P(sa.GetBoneTransform(HumanBodyBones.UpperChest))} hombroI {P(sa.GetBoneTransform(HumanBodyBones.LeftUpperArm))} "
                    + $"manoI {P(sa.GetBoneTransform(HumanBodyBones.LeftHand))} manoD {P(sa.GetBoneTransform(HumanBodyBones.RightHand))} | muerto caderas {P(za.GetBoneTransform(HumanBodyBones.Hips))} "
                    + $"pecho {P(za.GetBoneTransform(HumanBodyBones.UpperChest) ?? za.GetBoneTransform(HumanBodyBones.Chest))} cuello {P(neck)} cabeza {P(head)} raíz {Cm(Vector3.Dot(units.Feet(c.z.Id) - o, line))}");
            return string.Join(" ", parts);
        }

        /// <summary>How far `p` is from the segment `a`–`b`.</summary>
        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
            return Vector3.Distance(a + ab * t, p);
        }

        /// <summary>A frame of a pair (Silencioso): the fall's start, Die's fall begun again, how deep the soldier goes into the dead one, a [silencio] line.</summary>
        static void Measure(Silent c, Dictionary<string, List<Segment>> profiles)
        {
            if (c.start < 0 || c.finished) return;
            float now = Time.time, head = HeadOver(c.z.Id);
            if (c.hit >= 0)
            {
                if (c.fall < 0 && c.headAtHit - head >= 0.03f) c.fall = now - c.hit;
                if (c.dieAt >= 0 && head - c.headLeast > c.rise) c.rise = head - c.headLeast;
                c.headLeast = Mathf.Min(c.headLeast, head);
            }
            string phase = c.hit < 0 ? "antes" : now - c.hit < 0.15f ? "golpe" : "tras";
            var soldier = units.transform.Find(c.s.Id)?.GetComponentInChildren<Animator>();
            string into = "";
            if (soldier != null && soldier.isHuman && profiles.TryGetValue(c.z.Id, out var dead) && dead.Count > 0)
            {
                var (bones, bonesAt, hands, handsAt) = BonesInto(soldier, dead);
                var (skin, skinAt) = SkinInto(c.s.Id, soldier, dead);
                if (bones > c.bones) (c.bones, c.bonesAt) = (bones, $"{bonesAt}/{phase}");
                if (hands > c.hands) (c.hands, c.handsAt) = (hands, $"{handsAt}/{phase}");
                if (skin > c.skin) (c.skin, c.skinAt) = (skin, $"{skinAt}/{phase}");
                into = FormattableString.Invariant($" huesos={bones * 100:0.0}@{bonesAt} manos={hands * 100:0.0}@{handsAt} piel={skin * 100:0.0}@{skinAt}");
            }
            // (Along the line from the soldier's feet to the dead one's, and up, cm.)
            var o = MapSpace.World(c.s.Pos.X, c.s.Pos.Y);
            var line = MapSpace.World(c.z.Pos.X, c.z.Pos.Y) - o;
            line.y = 0;
            line.Normalize();
            var side = Vector3.Cross(Vector3.up, line);
            string P(Vector3 p) => FormattableString.Invariant($"{Vector3.Dot(p - o, line) * 100:0}/{(p.y - o.y) * 100:0}/{Vector3.Dot(p - o, side) * 100:0}");
            string td = units.TakedownForLab(c.s.Id) is var (grabAt, palm, nape, contact, grab, lay, offset)
                ? FormattableString.Invariant($" clip={units.TakedownClipForLab(c.s.Id).x:0.000} suelta={units.TakedownClipForLab(c.s.Id).y:0.000} agarre={grab:0.00} mano-izq={Vector3.Distance(palm, grabAt) * 100:0.0} apoyo={lay:0.00} filo-nuca={Vector3.Distance(contact, nape) * 100:0.0} paso={offset.magnitude * 100:0.0}")
                    + $" palma {P(palm)} agarre {P(grabAt)} filo {P(contact)} nuca {P(nape)} manoD {P(units.Hand(c.s.Id))}"
                    + (soldier != null && soldier.isHuman && soldier.GetBoneTransform(HumanBodyBones.RightUpperArm) is Transform rs ? $" hombroD {P(rs.position)}" : "")
                    + (units.transform.Find(c.z.Id)?.GetComponentInChildren<Animator>() is Animator za && za.isHuman && za.GetBoneTransform(HumanBodyBones.Neck) is Transform zn && za.GetBoneTransform(HumanBodyBones.Head) is Transform zh
                        ? FormattableString.Invariant($" filo-cuello={Vector3.Distance(contact, zn.position) * 100:0.0} filo-cabeza={Vector3.Distance(contact, zh.position) * 100:0.0}") : "")
                : "";
            Debug.Log(FormattableString.Invariant($"[silencio] {c.name} {now:0.000} t={now - c.start:0.000} {phase} cabeza={head:0.000}{td}{into}"));
        }

        /// <summary>
        /// The dead one's body as CombatLab.Cuerpo measures one's own (Profile: the skin's distance from each bone's axis by
        /// band and sector, moving with the bone); a stretch its mesh gives nothing for (a mesh not readable: City Zombies')
        /// as a capsule of the radius a body has there.
        /// </summary>
        static List<Segment> DeadProfile(string id)
        {
            var animator = units.transform.Find(id)?.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return new List<Segment>();
            var body = units.BodyRenderers(id)?.OfType<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null).ToList() ?? new List<SkinnedMeshRenderer>();
            List<Segment> profile;
            try
            {
                profile = Profile(animator, body);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[silencio] {id}: no profile ({e.Message}), capsules");
                profile = Profile(animator, new List<SkinnedMeshRenderer>());
            }
            var radii = new Dictionary<HumanBodyBones, float>
            {
                [HumanBodyBones.Hips] = 0.15f, [HumanBodyBones.Spine] = 0.15f, [HumanBodyBones.Chest] = 0.16f, [HumanBodyBones.UpperChest] = 0.15f, [HumanBodyBones.Neck] = 0.06f, [HumanBodyBones.Head] = 0.1f,
                [HumanBodyBones.LeftShoulder] = 0.07f, [HumanBodyBones.RightShoulder] = 0.07f, [HumanBodyBones.LeftUpperArm] = 0.055f, [HumanBodyBones.RightUpperArm] = 0.055f,
                [HumanBodyBones.LeftLowerArm] = 0.045f, [HumanBodyBones.RightLowerArm] = 0.045f, [HumanBodyBones.LeftUpperLeg] = 0.08f, [HumanBodyBones.RightUpperLeg] = 0.08f,
                [HumanBodyBones.LeftLowerLeg] = 0.055f, [HumanBodyBones.RightLowerLeg] = 0.055f,
            };
            int filled = 0;
            foreach (var seg in profile)
            {
                var which = radii.Keys.FirstOrDefault(b => animator.GetBoneTransform(b) == seg.bone);
                float cap = radii.TryGetValue(which, out float r) ? r : 0.06f;
                for (int band = 0; band < Bands; band++)
                    for (int sector = 0; sector < Sectors; sector++)
                        if (seg.radius[band, sector] <= 0)
                        {
                            seg.radius[band, sector] = cap;
                            filled++;
                        }
            }
            Debug.Log($"[silencio] {id}: profile of {profile.Count} stretches, {filled} of {profile.Count * Bands * Sectors} cells as capsules");
            return profile;
        }

        /// <summary>
        /// How deep the soldier's bones go into the dead one's body (Profile's): each bone of its chain (the trunk, the head,
        /// the arms, the legs) as five points along it, inside the skin by 1.5 cm or more; its hands (wrist to knuckles) apart.
        /// </summary>
        static (float bones, string at, float hands, string handsAt) BonesInto(Animator a, List<Segment> dead)
        {
            const float Margin = 0.015f;
            var chain = new (HumanBodyBones from, HumanBodyBones to, bool hand)[]
            {
                (HumanBodyBones.Hips, HumanBodyBones.Spine, false), (HumanBodyBones.Spine, HumanBodyBones.Chest, false), (HumanBodyBones.Chest, HumanBodyBones.UpperChest, false),
                (HumanBodyBones.UpperChest, HumanBodyBones.Neck, false), (HumanBodyBones.Neck, HumanBodyBones.Head, false), (HumanBodyBones.Head, HumanBodyBones.LeftEye, false),
                (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, false), (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, false),
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, false), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, false),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, false), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, false),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, false), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, false),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, false), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, false),
                (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, true), (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, true),
            };
            float bones = 0, hands = 0;
            string at = "-", handsAt = "-";
            foreach (var (from, to, hand) in chain)
            {
                Transform p = a.GetBoneTransform(from), q = a.GetBoneTransform(to);
                if (p == null || q == null) continue;
                for (int k = 0; k <= 4; k++)
                {
                    var point = Vector3.Lerp(p.position, q.position, k / 4f);
                    foreach (var seg in dead)
                    {
                        if (!Place(seg, point, out int band, out int sector, out float d)) continue;
                        float r = seg.radius[band, sector];
                        if (r <= 0 || d >= r - Margin) continue;
                        if (hand && r - d > hands) (hands, handsAt) = (r - d, $"{from}>{seg.bone.name}");
                        if (!hand && r - d > bones) (bones, at) = (r - d, $"{from}>{seg.bone.name}");
                    }
                }
            }
            return (bones, at, hands, handsAt);
        }

        static readonly Mesh skinBaked = new Mesh();

        /// <summary>How deep the soldier's skin goes into the dead one's body (every few of its vertices, none within 12 cm of a palm: the hands are on it), and of which of its bones' stretch.</summary>
        static (float depth, string at) SkinInto(string id, Animator a, List<Segment> dead)
        {
            const float Margin = 0.015f, Palm = 0.12f;
            var palms = new List<Vector3>();
            foreach (var (hand, knuckle) in new[] { (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal), (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal) })
                if (a.GetBoneTransform(hand) is Transform h) palms.Add(a.GetBoneTransform(knuckle) is Transform k ? Vector3.Lerp(h.position, k.position, 0.6f) : h.position);
            float deepest = 0;
            string at = "-";
            foreach (var r in units.BodyRenderers(id)?.OfType<SkinnedMeshRenderer>() ?? Enumerable.Empty<SkinnedMeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.sharedMesh == null) continue;
                r.BakeMesh(skinBaked, true);
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                var vv = skinBaked.vertices;
                // (Which of the soldier's bones each vertex goes with, where its mesh says so.)
                var weights = r.sharedMesh.boneWeights;
                var bones = r.bones;
                int stride = Mathf.Max(1, vv.Length / 1500);
                for (int i = 0; i < vv.Length; i += stride)
                {
                    var p = m.MultiplyPoint3x4(vv[i]);
                    if (palms.Any(q => (q - p).sqrMagnitude < Palm * Palm)) continue;
                    foreach (var seg in dead)
                    {
                        if (!Place(seg, p, out int band, out int sector, out float d)) continue;
                        float rr = seg.radius[band, sector];
                        if (rr <= 0 || d >= rr - Margin || rr - d <= deepest) continue;
                        deepest = rr - d;
                        at = (i < weights.Length && weights[i].boneIndex0 < bones.Length && bones[weights[i].boneIndex0] != null ? bones[weights[i].boneIndex0].name : "?") + ">" + seg.bone.name;
                    }
                }
            }
            return (deepest, at);
        }

        static Camera pairCamera;

        /// <summary>
        /// Each pair this frame (BASTION_TIRAS): from the soldier's right and its left, square to the line between them, 3.8 m
        /// off, on their middle a little over a figure's waist (480×400); the game's camera 9 and 24 tiles off on their middle, the middle
        /// 400×400 of a 1600×1000 frame.
        /// </summary>
        static void FilmPairs(List<Silent> cases, string folder, int frame)
        {
            if (pairCamera == null)
            {
                pairCamera = new GameObject("Cámara de la pareja").AddComponent<Camera>();
                pairCamera.enabled = false;
                pairCamera.farClipPlane = 400;
                pairCamera.clearFlags = CameraClearFlags.SolidColor;
                pairCamera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
                System.IO.Directory.CreateDirectory(folder);
            }
            // (Skinned again for each render: filmed in the frame's Update, the bodies showed an earlier pose than their bones.)
            foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
            foreach (var c in cases)
            {
                var from = MapSpace.World(c.s.Pos.X, c.s.Pos.Y);
                var to = MapSpace.World(c.z.Pos.X, c.z.Pos.Y);
                var mid = (from + to) / 2;
                var along = (to - from).normalized;
                var right = Vector3.Cross(Vector3.up, along);
                void Shot(string view, Action place, int w, int h)
                {
                    place();
                    string dir = System.IO.Path.Combine(folder, c.name + "-" + view);
                    System.IO.Directory.CreateDirectory(dir);
                    Shots.Render(pairCamera, w, h, System.IO.Path.Combine(dir, $"f{frame:000}.png"));
                }
                foreach (var (view, side) in new[] { ("der", 1f), ("izq", -1f) })
                    Shot(view, () =>
                    {
                        pairCamera.fieldOfView = 34;
                        pairCamera.transform.position = mid + right * (3.8f * side) + Vector3.up * 1.4f;
                        pairCamera.transform.LookAt(mid + Vector3.up * 1.12f);
                    }, 480, 400);
                foreach (float tiles in new[] { 9f, 24f })
                    Shot(FormattableString.Invariant($"juego{tiles:0}"), () =>
                    {
                        pairCamera.fieldOfView = 40;
                        MapSpace.Aim(pairCamera, mid + Vector3.up * 0.9f, tiles);
                        pairCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(20 * Mathf.Deg2Rad) * 400 / 1000f) * Mathf.Rad2Deg;
                    }, 400, 400);
            }
        }
    }
}
