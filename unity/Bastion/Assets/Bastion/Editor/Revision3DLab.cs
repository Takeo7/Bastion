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
    /// The review of everything 3D (Gameplay, 10/11, Arturo's «revisa toda la parte de los modelos 3D, las animaciones, cómo
    /// cogen las armas»): sweeps that put every outfit through what a soldier does, measured and photographed by state.
    /// </summary>
    public static partial class CombatLab
    {
        /// <summary>
        /// Every outfit of one sex (BASTION_SEXO f or m; BASTION_ROPA some of them) in two lines of twelve on a plain floor,
        /// armed as the game arms them (BASTION_PRIMARIA an engine weapon, assaultRifle unless said, «none» for none;
        /// BASTION_FILO a blade, the knife unless said; BASTION_PISTOLA=0 without one; BASTION_NIVEL the weapon tier), with
        /// BASTION_CABEZAS on their heads in turn (bare unless said), BASTION_COMPLEXION a build (normal unless said), BASTION_ARMARIO=0 as those
        /// born before 0.12.15. All do at once what a soldier does (BASTION_PASOS some of the steps: espera, corre, agachado,
        /// apunta, recarga, vigilancia, pistola, filo, escondido, golpe, muerte). Every frame it logs each one's grip and how
        /// deep what it carries goes into its body ([traje]); at each state it photographs each one (BASTION_TIRAS/&lt;state&gt;/
        /// &lt;view&gt;/&lt;nn-outfit&gt;.png) from BASTION_VISTAS: frente, izq, espalda, der (custom close views), juego and juegoN
        /// (the game's camera 9 tiles off from the two sides, the middle 400×400 of its frame).
        ///   BASTION_SEXO=m BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Trajes -shotPlay
        ///     -shotCamera "Shot camera" -shotFrames 520 -shotFps 10 -shotSize 160x90 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Trajes()
        {
            // (A figure the lab's camera does not see is not animated: all of them, as these photos go round each.)
            Environment.SetEnvironmentVariable("BASTION_ANIMAR_TODAS", "1");
            string Env(string name) => Environment.GetEnvironmentVariable(name);
            string sex = Env("BASTION_SEXO") == "f" ? Sexes.F : Sexes.M;
            var outfits = Env("BASTION_ROPA")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Dress.OutfitsFor(sex).ToArray();
            var hats = Env("BASTION_CABEZAS")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new[] { HeadIds.Bare };
            string build = Env("BASTION_COMPLEXION");
            // (Several primaries or blades, comma apart: every outfit with each loadout in turn, 36 soldiers at most.)
            var primaries = (Env("BASTION_PRIMARIA") ?? "assaultRifle").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var blades = Env("BASTION_FILO")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[] { null };
            int loadouts = Math.Max(primaries.Length, blades.Length);
            bool pistol = Env("BASTION_PISTOLA") != "0";
            int? tier = int.TryParse(Env("BASTION_NIVEL"), out int t) ? t : (int?)null;
            var steps = Env("BASTION_PASOS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var views = (Env("BASTION_VISTAS") ?? "frente,izq,espalda,juego,juegoN").Split(',', StringSplitOptions.RemoveEmptyEntries);
            string folder = Env("BASTION_TIRAS");
            int per = Math.Max(outfits.Length, hats.Length);
            int n = Math.Min(36, per * loadouts);
            var first = MapLab.Golden("city-7");
            string json = Bastion.Net.Json.Write(first.Units.First(u => u.Alive && u.Team == "xcom"));
            Plain(u => false, 2, 2 + 2 * Math.Min(12, n) - 2);
            var row = new List<(Unit u, string label)>();
            for (int k = 0; k < n; k++)
            {
                var u = Bastion.Net.Json.Read<Unit>(json);
                string outfit = outfits[k % per % outfits.Length], hat = hats[k % per % hats.Length];
                string primary = primaries[k / per % primaries.Length], blade = blades[k / per % blades.Length];
                string label = $"{k:00}-{outfit}" + (hats.Length > 1 ? "-" + hat : "") + (primaries.Length > 1 ? "-" + primary : "") + (blades.Length > 1 ? "-" + blade : "");
                u.Id = "lab-" + label;
                u.Name = "Laboratorio " + k;
                u.CampaignId = null;
                u.Template = "rookie";
                u.Overwatch = false;
                u.Hunkered = false;
                u.Hidden = null;
                u.WeaponTier = tier;
                u.Arms = new Arms { Primary = primary == "none" ? null : primary, Melee = blade == "none" ? null : blade, Pistol = pistol };
                u.Appearance ??= new Appearance();
                u.Appearance.Character = outfit;
                u.Appearance.Head = hat;
                u.Appearance.Sex = sex;
                u.Appearance.Wardrobe = Env("BASTION_ARMARIO") == "0" ? (int?)null : Appearances.Wardrobe;
                u.Appearance.Build = build ?? "normal";
                state.Units.Add(Put(u, 2 + 2 * (k % 12), n > 24 ? 20 - 7 * (k / 12) : 18 - 8 * (k / 12), -Math.PI / 2));
                row.Add((u, label));
            }
            string now = "inicio";
            bool done = false;
            var aims = new Dictionary<string, int>();
            async Awaitable Watch()
            {
                while (!done)
                {
                    // (After the frame's Update: the pose this frame drew. End of frame never comes in batchmode.)
                    await Awaitable.NextFrameAsync();
                    foreach (var (u, label) in row)
                        Debug.Log(FormattableString.Invariant($"[traje] {Time.time:0.000} {now} {label} {units.GripReport(u.Id)} {IntoReport(u.Id)}"));
                }
            }
            async Awaitable All(Func<Unit, Awaitable> act)
            {
                var going = row.Select(r => act(r.u)).ToList();
                foreach (var g in going) await g;
            }
            // (Those with a long gun: the others have the pistol or the blade in the hand already.)
            async Awaitable Long(Func<Unit, Awaitable> act)
            {
                var going = row.Where(r => r.u.Arms.Primary != null).Select(r => act(r.u)).ToList();
                foreach (var g in going) await g;
            }
            async Awaitable Photo(string what, float after = 0)
            {
                if (after > 0) await Awaitable.WaitForSecondsAsync(after);
                await Awaitable.NextFrameAsync();
                now = what;
                Snap(row, folder, what, views);
            }
            async Awaitable Step(string name, Func<Awaitable> act)
            {
                if (steps != null && !steps.Contains(name)) return;
                try
                {
                    await act();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[traje] step {name}: " + e);
                }
            }
            Vector3 Ahead(Unit u, float tiles = 5) => MapSpace.World(u.Pos.X, u.Pos.Y - tiles) + Vector3.up * 1.4f;
            Pos[] Path(Unit u, int by, int count) => Enumerable.Range(1, count).Select(i => new Pos { X = u.Pos.X, Y = u.Pos.Y + by * i }).ToArray();
            void Shoot()
            {
                foreach (var (u, _) in row)
                {
                    fx.MuzzleFlash(units.Muzzle(u.Id), Bastion.Client.Effects.Fx.Hex(0xffd27a));
                    units.Recoil(u.Id);
                }
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    Measures(row);
                    _ = Watch();
                    await Step("espera", async () =>
                    {
                        foreach (string rest in new[] { "rifleIdle", "rifleLowered", "rifleIdleLook" })
                        {
                            now = rest;
                            foreach (var (u, _) in row) units.RestForLab(u.Id, rest);
                            await Photo(rest, 1.6f);
                        }
                    });
                    await Step("corre", async () =>
                    {
                        now = "corre";
                        var going = All(u => units.MoveAlong(u.Id, Path(u, -1, 4), 1));
                        await Photo("corre-1", 0.7f);
                        await Photo("corre-2", 0.2f);
                        await Photo("corre-3", 0.2f);
                        await going;
                        foreach (var (u, _) in row) u.Pos = new Pos { X = u.Pos.X, Y = u.Pos.Y - 4 };
                        await Awaitable.WaitForSecondsAsync(0.5f);
                        now = "esprinta";
                        going = All(u => units.MoveAlong(u.Id, Path(u, 1, 4), 2));
                        await Photo("esprinta-1", 0.6f);
                        await Photo("esprinta-2", 0.2f);
                        await going;
                        foreach (var (u, _) in row) u.Pos = new Pos { X = u.Pos.X, Y = u.Pos.Y + 4 };
                        await Awaitable.WaitForSecondsAsync(0.5f);
                    });
                    await Step("agachado", async () =>
                    {
                        now = "agachandose";
                        foreach (var (u, _) in row) u.Hunkered = true;
                        units.Sync(state);
                        await Photo("agachado", 1.2f);
                        // ([traje-suelo]: the lowest of what each carries, not in its hands, over its feet, m; the 3D review's F-07, 10/11.)
                        foreach (var (u, label) in row)
                        {
                            float low = float.MaxValue;
                            foreach (var (model, drawn) in units.CarriedForLab(u.Id))
                                if (!drawn) foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) low = Mathf.Min(low, r.bounds.min.y);
                            if (low < float.MaxValue) Debug.Log(FormattableString.Invariant($"[traje-suelo] {label} {low - units.Feet(u.Id).y:0.000}"));
                        }
                        now = "agachado-anda";
                        var going = All(u => units.MoveAlong(u.Id, Path(u, -1, 2), 1));
                        await Photo("agachado-anda-1", 0.6f);
                        await Photo("agachado-anda-2", 0.3f);
                        await going;
                        foreach (var (u, _) in row) u.Pos = new Pos { X = u.Pos.X, Y = u.Pos.Y - 2 };
                        await Awaitable.WaitForSecondsAsync(0.6f);
                        now = "agachado-apuntando";
                        await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u)));
                        await Photo("agachado-apunta", 0.6f);
                        Shoot();
                        await Photo("agachado-disparo");
                        await Awaitable.WaitForSecondsAsync(0.7f);
                        now = "agachado-bajando";
                        await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                        now = "agachado-recarga";
                        var loading = All(u => units.Reload(u.Id));
                        await Photo("agachado-recarga-1", 0.4f);
                        await Photo("agachado-recarga-2", 0.4f);
                        await loading;
                        now = "levantandose";
                        foreach (var (u, _) in row) u.Hunkered = false;
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(1.0f);
                    });
                    await Step("apunta", async () =>
                    {
                        now = "apuntando";
                        await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u)));
                        await Photo("apunta", 0.6f);
                        Shoot();
                        await Photo("disparo");
                        await Awaitable.WaitForSecondsAsync(0.7f);
                        now = "bajando";
                        await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                        await Awaitable.WaitForSecondsAsync(0.5f);
                    });
                    await Step("recarga", async () =>
                    {
                        now = "recarga";
                        var loading = All(u => units.Reload(u.Id));
                        await Photo("recarga-1", 0.4f);
                        await Photo("recarga-2", 0.4f);
                        await loading;
                        await Awaitable.WaitForSecondsAsync(0.5f);
                    });
                    await Step("vigilancia", async () =>
                    {
                        now = "a-vigilancia";
                        foreach (var (u, _) in row) u.Overwatch = true;
                        units.Sync(state);
                        await Photo("vigilancia", 1.4f);
                        now = "vigilancia-apuntando";
                        await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u, 4)));
                        await Photo("vigilancia-apunta", 0.5f);
                        now = "vigilancia-bajando";
                        await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                        foreach (var (u, _) in row) u.Overwatch = false;
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(0.8f);
                    });
                    await Step("pistola", async () =>
                    {
                        if (!pistol || row.All(r => r.u.Arms.Primary == null)) return;
                        now = "pistola-apuntando";
                        await Long(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u), true));
                        await Photo("pistola-apunta", 0.6f);
                        now = "pistola-bajando";
                        await Long(u => units.LowerWeapon(u.Id, aims[u.Id]));
                        await Awaitable.WaitForSecondsAsync(0.8f);
                    });
                    await Step("escondido", async () =>
                    {
                        now = "escondiendose";
                        await All(u => units.Hide(u.Id));
                        foreach (var (u, _) in row) u.Hidden = true;
                        units.Sync(state);
                        await Photo("escondido", 1.2f);
                        foreach (var (u, _) in row) u.Hidden = null;
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(1.2f);
                    });
                    await Step("filo", async () =>
                    {
                        if (row.All(r => r.u.Arms.Primary == null)) return;
                        now = "filo";
                        foreach (var (u, _) in row) if (u.Arms.Primary != null) units.GuardForLab(u.Id, 0);
                        await Photo("filo", 0.5f);
                        // ([traje-espalda]: the top of what it carries on its back over its head's bone, m; the 3D review's F-18, 10/11.)
                        foreach (var (u, label) in row)
                            foreach (var (model, drawn) in units.CarriedForLab(u.Id))
                                if (!drawn && model.parent != null && model.parent.name == "lleva weapon")
                                {
                                    float top = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.bounds.max.y).DefaultIfEmpty(float.MinValue).Max();
                                    Debug.Log(FormattableString.Invariant($"[traje-espalda] {label} {top - units.Head(u.Id).y:0.000}"));
                                }
                    });
                    await Step("golpe", async () =>
                    {
                        now = "golpe";
                        var hit = All(u => units.HitReact(u.Id, 1));
                        await Photo("golpe", 0.15f);
                        await hit;
                        await Awaitable.WaitForSecondsAsync(0.5f);
                    });
                    await Step("muerte", async () =>
                    {
                        now = "muriendo";
                        var dying = All(u => units.Die(u.Id));
                        await Photo("muriendo", 0.5f);
                        await dying;
                        await Photo("muerto", 1.0f);
                        // ([traje-armas]: where each of its weapons is and whether it shows, dead; the 3D review's F-02 and F-11, 10/11.)
                        foreach (var (u, label) in row) Debug.Log($"[traje-armas] {label} {units.ArmsReport(u.Id)}");
                    });
                    Debug.Log($"[traje] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[traje] " + e);
                }
                finally
                {
                    done = true;
                }
            };
        }

        static Camera eye3d;

        /// <summary>
        /// Each of the row photographed now from `views` into folder/what/view/label.png. The close views are custom ones
        /// (not the game's), 2.6 m off round a point between its feet and its head, so that a crouched or a fallen body
        /// stays in the frame: frente, from ahead and to its right; izq, ahead and to its left; espalda, behind and to its
        /// right; der, square from its right; arriba, from above (the fallen). juego and juegoN are the game's camera 9
        /// tiles off (BASTION_DIST) from the south-east and the north-east, the middle 400×400 of its 1600×1000 frame.
        /// </summary>
        static void Snap(List<(Unit u, string label)> row, string folder, string what, string[] views)
        {
            if (folder == null) return;
            if (eye3d == null)
            {
                eye3d = new GameObject("Cámara de la revisión").AddComponent<Camera>();
                eye3d.enabled = false;
                eye3d.farClipPlane = 400;
                eye3d.clearFlags = CameraClearFlags.SolidColor;
                eye3d.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            }
            foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) ? d : 9;
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "estados.txt"), FormattableString.Invariant($"{Time.time:0.00} {what}\n"));
            foreach (string view in views)
            {
                string into = System.IO.Path.Combine(folder, what, view);
                System.IO.Directory.CreateDirectory(into);
                foreach (var (u, label) in row)
                {
                    var feet = units.Feet(u.Id);
                    var turn = Quaternion.Euler(0, units.FacingNow(u.Id) ?? 0, 0);
                    // (The body's middle from its bones: the root stays where a fallen body began.)
                    var chest = units.Chest(u.Id);
                    var head = units.Head(u.Id);
                    var middle = new Vector3((chest.x + feet.x) / 2, Mathf.Lerp(0, head.y, 0.62f), (chest.z + feet.z) / 2);
                    string path = System.IO.Path.Combine(into, label + ".png");
                    if (view == "juego" || view == "juegoN")
                    {
                        MapSpace.Aim(eye3d, new Vector3(middle.x, 0.9f, middle.z), off, view == "juego" ? 45 : 135);
                        eye3d.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(20 * Mathf.Deg2Rad) * 400 / 1000f) * Mathf.Rad2Deg;
                        Shots.Render(eye3d, 400, 400, path);
                        continue;
                    }
                    eye3d.fieldOfView = 34;
                    if (view == "arriba")
                    {
                        var over = new Vector3(chest.x, 0, chest.z);
                        eye3d.transform.position = over + new Vector3(0.9f, 2.9f, -0.9f);
                        eye3d.transform.LookAt(over + Vector3.up * 0.2f);
                        Shots.Render(eye3d, 600, 600, path);
                        continue;
                    }
                    var from = view == "izq" ? new Vector3(-1.5f, 0.3f, 2.1f) : view == "espalda" ? new Vector3(1.9f, 0.2f, -1.5f) : view == "der" ? new Vector3(2.6f, 0.1f, 0.3f)
                        : view == "detras" ? new Vector3(0.3f, 0.3f, -2.6f) : new Vector3(1.5f, 0.3f, 2.1f);
                    var on = view == "espalda" ? turn * new Vector3(0, 0, -0.2f) : view == "der" ? turn * new Vector3(0, 0, 0.3f) : Vector3.zero;
                    // (From 3.8 m: the whole figure with its hair and its feet; the far plane takes the others of the row out.)
                    eye3d.transform.position = middle + turn * (from * 1.45f);
                    eye3d.transform.LookAt(middle + on);
                    eye3d.farClipPlane = 6.5f;
                    Shots.Render(eye3d, 480, 600, path);
                    eye3d.farClipPlane = 400;
                }
            }
        }

        /// <summary>
        /// What each of the row is made of, once: how it was dressed (Dress.Key), its head's height, its shoulders' and
        /// hips' width at the bones, its skin's box, its scale, what it carries, and any renderer without a material or
        /// with the error shader (pink) or a mesh missing ([traje-medidas], [traje-falta]).
        /// </summary>
        static void Measures(List<(Unit u, string label)> row)
        {
            foreach (var (u, label) in row)
            {
                var root = Figures.FeetOf(u.Id);
                if (root == null) { Debug.Log($"[traje-falta] {label}: no figure"); continue; }
                var animator = root.GetComponentInChildren<Animator>();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.sharedMesh != null).ToList();
                var box = skins.Count > 0 ? skins.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds();
                float Between(HumanBodyBones a, HumanBodyBones b) => animator != null && animator.isHuman && animator.GetBoneTransform(a) is Transform x && animator.GetBoneTransform(b) is Transform y ? Vector3.Distance(x.position, y.position) : 0;
                float headAt = animator != null && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Head) is Transform h ? h.position.y - root.position.y : 0;
                var dress = Dress.Of(u.Appearance, u.CampaignId ?? u.Name);
                var marks = root.GetComponentInChildren<SoldierMarks>(true);
                string carried = string.Join(",", units.CarriedForLab(u.Id).Select(c => c.model.name + (c.drawn ? "*" : "")));
                Debug.Log(FormattableString.Invariant($"[traje-medidas] {label}: dress {dress?.Key}, head {headAt * 100:0.0} cm, shoulders {Between(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm) * 100:0.0}, hips {Between(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg) * 100:0.0}, box {box.size.x * 100:0}×{box.size.y * 100:0}×{box.size.z * 100:0}, top {(box.max.y - root.position.y) * 100:0.0}, scale {(animator != null ? animator.transform.lossyScale.x : 0):0.000}, pack {(marks != null ? marks.pack : -1):0.000}, skins {skins.Count}, verts {skins.Sum(r => r.sharedMesh.vertexCount)}, carries {carried}; {units.ArmsReport(u.Id)}"));
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer s && s.sharedMesh == null) Debug.Log($"[traje-falta] {label}: {r.name} without a mesh");
                    if (r is MeshRenderer && r.GetComponent<MeshFilter>() is MeshFilter f && f.sharedMesh == null) Debug.Log($"[traje-falta] {label}: {r.name} without a mesh");
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader") Debug.Log($"[traje-falta] {label}: {r.name} material {(m == null ? "none" : m.name + " with the error shader")}");
                }
            }
        }

        static readonly string[] HairsF = { "hairBun", "hairBangs", "hairSidePony", "hairCurls", "hairPony", "hairLong", "hairTopBun", "hairPonyBangs", "hairStraight", "hairHighBun", "hairSwept" };
        static readonly string[] HairsM = { "hairFade", "hairMessy", "hairMedium", "hairBack", "hairShaggy", "hairLongM" };
        static readonly string[] BeardsM = { null, "beard1", "beard2", "beard3", "beard4", "beard5", "beard6", "beard7", "beard8" };
        static readonly string[] FacesAll = { "face1", "face2", "face3", "face4", "face5", "face6", "face7", "face8" };

        /// <summary>
        /// Heads: soldiers of one sex (BASTION_SEXO f or m) one tile apart in lines of 24, each a cell of BASTION_MODO:
        ///   barbas (men): every headwear with every beard and none; the bare and the masked with each hair in turn;
        ///   caras: every headwear with every face;
        ///   gorros: every headwear (BASTION_CABEZAS some: 168 cells at most) with every outfit: hoods and helmets against collars and rucksacks;
        ///   pelos: every hair (BASTION_PELOS some) bare (BASTION_CABEZA another headwear: the mask keeps the hair) with every
        ///   outfit (BASTION_ROPA some): the hair against collars, hoods and rucksacks.
        /// What isn't the cell's is left to the name's pick (Dress.Of), a name being looked for that gives the cell. In
        /// BASTION_ROPA's first outfit (survivor2) but in pelos; unarmed but for the knife, standing. Each photographed once
        /// (BASTION_TIRAS/&lt;view&gt;/&lt;label&gt;.png): retrato (the portraits' framing), perfil (its right side), nuca (from
        /// behind), tres (from ahead to its left and above). Logs how each was dressed ([cabeza]).
        ///   BASTION_SEXO=m BASTION_MODO=barbas BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Cabezas
        ///     -shotPlay -shotCamera "Shot camera" -shotFrames 30 -shotFps 10 -shotSize 160x90 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Cabezas()
        {
            // (A figure the lab's camera does not see is not animated: all of them, as these photos go round each.)
            Environment.SetEnvironmentVariable("BASTION_ANIMAR_TODAS", "1");
            string Env(string name) => Environment.GetEnvironmentVariable(name);
            string sex = Env("BASTION_SEXO") == "f" ? Sexes.F : Sexes.M;
            string mode = Env("BASTION_MODO") ?? "caras";
            var outfits = Env("BASTION_ROPA")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? (mode == "pelos" || mode == "gorros" ? Dress.OutfitsFor(sex).ToArray() : new[] { "survivor2" });
            var hairs = Env("BASTION_PELOS")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? (sex == Sexes.F ? HairsF : HairsM);
            string folder = Env("BASTION_TIRAS");
            // The cells: outfit, headwear, and what the name has to give (hair, beard, face; null, any).
            var cells = new List<(string outfit, string head, string hair, string beard, bool anyBeard, string face, string label)>();
            if (mode == "barbas")
                foreach (string head in HeadIds.All)
                    for (int b = 0; b < BeardsM.Length; b++)
                        // (Any hair: the name gives the hair and the beard from one hash, and not every pair comes out.)
                        cells.Add((outfits[0], head, null, BeardsM[b], false, null, $"{head}-{BeardsM[b] ?? "sin"}"));
            else if (mode == "pelos")
                foreach (string hair in hairs)
                    foreach (string outfit in outfits)
                        cells.Add((outfit, Env("BASTION_CABEZA") ?? HeadIds.Bare, hair, null, true, null, $"{hair}-{outfit}"));
            else if (mode == "gorros")
                foreach (string head in Env("BASTION_CABEZAS")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? HeadIds.All.ToArray())
                    foreach (string outfit in outfits)
                        cells.Add((outfit, head, null, null, true, null, $"{head}-{outfit}"));
            else
                foreach (string head in HeadIds.All)
                    foreach (string face in FacesAll)
                        cells.Add((outfits[0], head, null, null, true, face, $"{head}-{face}"));
            var first = MapLab.Golden("city-7");
            string json = Bastion.Net.Json.Write(first.Units.First(u => u.Alive && u.Team == "xcom"));
            Plain(u => false, 1, 24);
            var row = new List<(Unit u, string label)>();
            int name = 0;
            for (int k = 0; k < cells.Count && k < 24 * 7; k++)
            {
                var c = cells[k];
                var u = Bastion.Net.Json.Read<Unit>(json);
                u.Id = "lab-" + k;
                u.CampaignId = null;
                u.Template = "rookie";
                u.Overwatch = false;
                u.Hunkered = false;
                u.Hidden = null;
                u.Arms = new Arms { Primary = null, Pistol = false };
                u.Appearance ??= new Appearance();
                u.Appearance.Character = c.outfit;
                u.Appearance.Head = c.head;
                u.Appearance.Sex = sex;
                u.Appearance.Wardrobe = Appearances.Wardrobe;
                u.Appearance.Build = Env("BASTION_COMPLEXION") ?? "normal";
                Dress dress = null;
                for (int tries = 0; tries < 400000; tries++)
                {
                    u.Name = "Laboratorio " + name++;
                    dress = Dress.Of(u.Appearance, u.Name);
                    if ((c.hair == null || dress.hair == c.hair) && (c.anyBeard || dress.beard == c.beard) && (c.face == null || dress.face == c.face)) break;
                    dress = null;
                }
                if (dress == null) { Debug.Log($"[cabeza] {c.label}: no name gives it"); continue; }
                Debug.Log($"[cabeza] {k:000} {c.label}: {dress.Key}");
                state.Units.Add(Put(u, 1 + k % 24, 22 - 3 * (k / 24), -Math.PI / 2));
                row.Add((u, $"{k:000}-{c.label}" + (mode == "barbas" && dress.hair != null ? "-" + dress.hair : "")));
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(1.2f);
                    await Awaitable.NextFrameAsync();
                    Measures(row);
                    if (folder != null) Busts(row, folder);
                    Debug.Log($"[cabeza] {Time.time:0.00} s: done, {row.Count}");
                }
                catch (Exception e)
                {
                    Debug.LogError("[cabeza] " + e);
                }
            };
        }

        /// <summary>
        /// Each of the row's head photographed into folder/view/label.png, nothing further than 2.6 m drawn (the lines behind):
        /// retrato, as the portraits (Portraits: 1.45 tiles ahead of the head, 20° of view; here 320×320, theirs is
        /// 160×160); perfil, square from its right; nuca, from behind; tres, from ahead to its left and above (custom views).
        /// </summary>
        static void Busts(List<(Unit u, string label)> row, string folder)
        {
            var eye = new GameObject("Cámara de las cabezas").AddComponent<Camera>();
            eye.enabled = false;
            eye.nearClipPlane = 0.05f;
            eye.farClipPlane = 2.6f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
            foreach (string view in new[] { "retrato", "perfil", "nuca", "tres" })
            {
                string into = System.IO.Path.Combine(folder, view);
                System.IO.Directory.CreateDirectory(into);
                foreach (var (u, label) in row)
                {
                    var turn = Quaternion.Euler(0, units.FacingNow(u.Id) ?? 0, 0);
                    var at = units.Head(u.Id) + Vector3.up * 0.02f * MapSpace.Tile;
                    var from = view == "retrato" ? new Vector3(0, 0.03f * MapSpace.Tile, 1.45f * MapSpace.Tile) : view == "perfil" ? new Vector3(2.1f, 0, 0)
                        : view == "nuca" ? new Vector3(0.25f, 0.1f, -2.1f) : new Vector3(-1.3f, 0.55f, 1.6f);
                    eye.fieldOfView = view == "retrato" ? 20 : 22;
                    eye.transform.position = at + turn * from;
                    eye.transform.LookAt(view == "retrato" ? at : at - Vector3.up * 0.08f);
                    Shots.Render(eye, 320, 320, System.IO.Path.Combine(into, label + ".png"));
                }
            }
            UnityEngine.Object.DestroyImmediate(eye.gameObject);
        }
    }
}
