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
    /// The review of the weapons and the poses (Gameplay, 9/11, Arturo's «revisa cómo cogen las armas… si apuntan bien»):
    /// a soldier for each weapon in a row on a plain floor, all doing the same at once, measured as they do it.
    /// </summary>
    public static partial class CombatLab
    {
        /// <summary>Every firearm of Anims.json's weapons, as the review takes them (the pistol last).</summary>
        static readonly string[] Firearms = { "rifle", "rifle2", "rifle3", "shotgun", "sniper", "sniper2", "machineGun", "machineGun2", "huntingRifle", "boltRifle", "smg", "pistol" };

        /// <summary>The row: one soldier for each of BASTION_ARMAS (Firearms unless said) two tiles apart along row 12, facing north.</summary>
        static List<(Unit u, string art)> Row(int y = 12)
        {
            var first = MapLab.Golden("city-7");
            // (BASTION_SOLDADO=n: the n-th of its soldiers instead of the first.)
            var squad = first.Units.Where(u => u.Alive && u.Team == "xcom").ToList();
            var model = squad[int.TryParse(Environment.GetEnvironmentVariable("BASTION_SOLDADO"), out int pick) ? Math.Clamp(pick, 0, squad.Count - 1) : 0];
            var arts = (Environment.GetEnvironmentVariable("BASTION_ARMAS") ?? string.Join(",", Firearms)).Split(',', StringSplitOptions.RemoveEmptyEntries);
            Plain(u => false, 1, 2 * arts.Length - 1);
            // (The same body for all, so that only the weapon changes; BASTION_COMPLEXION=heavy, slim…: another build.)
            string json = Bastion.Net.Json.Write(model);
            var row = new List<(Unit, string)>();
            // (BASTION_PALMAS=x,y,z;…: each soldier's palm point on its gun, «-» its weapon's own; the 3D review's F-12, 10/11.)
            var palms = Environment.GetEnvironmentVariable("BASTION_PALMAS")?.Split(';');
            for (int k = 0; k < arts.Length; k++)
            {
                var u = Bastion.Net.Json.Read<Unit>(json);
                u.Id = "lab-" + arts[k] + (palms != null ? "-" + k : "");
                u.Template = "rookie";
                u.Overwatch = false;
                u.Hunkered = false;
                u.Hidden = null;
                if (Environment.GetEnvironmentVariable("BASTION_COMPLEXION") is string build && u.Appearance != null) u.Appearance.Build = build;
                if (Environment.GetEnvironmentVariable("BASTION_SEXO") is string sex && u.Appearance != null) u.Appearance.Sex = sex;
                Put(u, 1 + 2 * k, y, -Math.PI / 2);
                state.Units.Add(u);
                UnitViews.ArtsForLab[u.Id] = arts[k];
                string label = arts[k];
                if (palms != null && k < palms.Length && palms[k] != "-")
                {
                    var p = palms[k].Split(',').Select(c => float.Parse(c, CultureInfo.InvariantCulture)).ToArray();
                    UnitViews.PalmForLab[u.Id] = new Vector3(p[0], p[1], p[2]);
                    label += "@" + palms[k];
                }
                else if (palms != null) label += "@-";
                row.Add((u, label));
            }
            return row;
        }

        /// <summary>
        /// The close camera on the soldier BASTION_ENFOQUE of the row (0 the first): in front of it and to its right, its
        /// height (a custom view, not the game's); BASTION_VIEW=game the game's, BASTION_DIST tiles off, on it.
        /// </summary>
        static void Focus(List<(Unit u, string art)> row)
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BASTION_ENFOQUE"), out int k) || k < 0 || k >= row.Count || Camera.main is not Camera eye) return;
            var at = MapSpace.World(row[k].u.Pos.X, row[k].u.Pos.Y);
            string view = Environment.GetEnvironmentVariable("BASTION_VIEW");
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) ? d : 24;
            if (view == "game")
            {
                MapSpace.Aim(eye, at, off);
                return;
            }
            // (BASTION_LADO=izq: from its left; =detras, from behind; =arriba, from above its right shoulder.)
            string side = Environment.GetEnvironmentVariable("BASTION_LADO");
            var from = side == "izq" ? new Vector3(-2.2f, 1.5f, 1.2f) : side == "detras" ? new Vector3(0.9f, 1.7f, -2.4f) : side == "arriba" ? new Vector3(0.7f, 2.6f, -0.9f) : new Vector3(2.2f, 1.5f, 1.4f);
            eye.fieldOfView = 34;
            eye.transform.position = at + from;
            eye.transform.LookAt(at + Vector3.up * (side == "arriba" ? 1.1f : 1.15f));
        }

        // ------------------------------------------------------------ grips

        /// <summary>
        /// Grips (the review of 9/11, its 1): the row (Row) does at once what a soldier does with a gun: waits in each of
        /// RIFLE_RESTS, runs four tiles and sprints back, crouches, walks crouched, aims and fires crouched, stands, aims and
        /// fires twice, reloads, goes on watch and fires from it, hides. Each frame (BASTION_CADA=n: every n-th; penetration
        /// at most every 0.1 s, every frame in a shot or a reload) it logs for each soldier how its hands hold the gun
        /// (UnitViews.GripReport) and how deep any weapon it shows goes into its body ([agarre]: Into). BASTION_MARCAS=1:
        /// the handguard's point (red) and the muzzle (yellow) marked on each gun.
        ///   BASTION_ARMAS=rifle,smg unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Agarres -shotPlay
        ///     -shotCamera "Shot camera" -shotFrames 700 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Agarres()
        {
            var row = Row();
            Focus(row);
            bool marks = Environment.GetEnvironmentVariable("BASTION_MARCAS") == "1";
            int every = int.TryParse(Environment.GetEnvironmentVariable("BASTION_CADA"), out int n) && n > 0 ? n : 1;
            string now = "inicio";
            bool dense = false;
            float lastDepth = -1;
            int frame = 0;
            bool done = false;
            async Awaitable Watch()
            {
                while (!done)
                {
                    // (After the frame's Update, UnitViews' step among them: the pose this frame drew. End of frame never comes in batchmode.)
                    await Awaitable.NextFrameAsync();
                    if (++frame % every != 0) continue;
                    bool depth = dense || Time.time - lastDepth >= 0.099f;
                    if (depth) lastDepth = Time.time;
                    foreach (var (u, art) in row)
                    {
                        if (marks) Mark(u.Id, art);
                        string into = depth ? " " + IntoReport(u.Id) : "";
                        Debug.Log(FormattableString.Invariant($"[agarre] {Time.time:0.000} {now} {art} {units.GripReport(u.Id)}{into}"));
                    }
                    Film(row, frame / every, now);
                }
            }
            async Awaitable All(Func<Unit, Awaitable> act)
            {
                var going = row.Select(r => act(r.u)).ToList();
                foreach (var g in going) await g;
            }
            Vector3 Ahead(Unit u, float tiles = 5) => MapSpace.World(u.Pos.X, u.Pos.Y - tiles) + Vector3.up * 1.4f;
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = Watch();
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    // (BASTION_PRUEBA_PEN=1: the first one's gun put through its chest for a frame, to see the measure find it.)
                    if (Environment.GetEnvironmentVariable("BASTION_PRUEBA_PEN") == "1")
                    {
                        var (gun, _) = units.CarriedForLab(row[0].u.Id).First();
                        var chest = units.Chest(row[0].u.Id);
                        var keep = (gun.position, gun.rotation);
                        gun.SetPositionAndRotation(chest - Vector3.forward * 0.3f, Quaternion.identity);
                        Debugging = true;
                        Debug.Log($"[agarre] prueba: the gun through the chest: {IntoReport(row[0].u.Id)}");
                        Debugging = false;
                        gun.SetPositionAndRotation(chest + Vector3.forward * 0.6f, Quaternion.identity);
                        Debug.Log($"[agarre] prueba: the gun ahead of the chest: {IntoReport(row[0].u.Id)}");
                        gun.SetPositionAndRotation(keep.position, keep.rotation);
                    }
                    // (BASTION_REPOSO=s: each rest this long, a loop of each at least, for its seam: the 3D review's A-12, 10/11.)
                    float restFor = float.TryParse(Environment.GetEnvironmentVariable("BASTION_REPOSO"), NumberStyles.Float, CultureInfo.InvariantCulture, out float rs) ? rs : 2.2f;
                    foreach (string rest in new[] { "rifleIdle", "rifleLowered", "rifleIdleLook" })
                    {
                        now = "reposo-" + rest;
                        foreach (var (u, _) in row) units.RestForLab(u.Id, rest);
                        await Awaitable.WaitForSecondsAsync(restFor);
                    }
                    if (Environment.GetEnvironmentVariable("BASTION_SOLO_REPOSO") == "1")
                    {
                        Debug.Log($"[combat] {Time.time:0.00} s: done");
                        return;
                    }
                    // (BASTION_SOLO_RECARGA=1: from the rests straight to a reload crouched and one standing, at a quicker frame
                    // rate: the 3D review's P-1, 10/11, the left hand to the pistol.)
                    if (Environment.GetEnvironmentVariable("BASTION_SOLO_RECARGA") == "1")
                    {
                        now = "agachado";
                        foreach (var (u, _) in row) u.Hunkered = true;
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(1.2f);
                        now = "agachado-recarga";
                        dense = true;
                        await All(u => units.Reload(u.Id));
                        dense = false;
                        now = "agachado";
                        await Awaitable.WaitForSecondsAsync(0.4f);
                        now = "levantandose";
                        foreach (var (u, _) in row) u.Hunkered = false;
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(1.0f);
                        now = "recarga";
                        dense = true;
                        await All(u => units.Reload(u.Id));
                        dense = false;
                        now = "quieto";
                        await Awaitable.WaitForSecondsAsync(0.6f);
                        Debug.Log($"[combat] {Time.time:0.00} s: done");
                        return;
                    }
                    now = "carrera";
                    await All(u => units.MoveAlong(u.Id, Enumerable.Range(1, 4).Select(i => new Pos { X = u.Pos.X, Y = u.Pos.Y - i }).ToList(), 1));
                    now = "esprint";
                    await All(u => units.MoveAlong(u.Id, Enumerable.Range(1, 4).Select(i => new Pos { X = u.Pos.X, Y = u.Pos.Y - 4 + i }).ToList(), 2));
                    now = "quieto";
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    now = "agachado";
                    foreach (var (u, _) in row) u.Hunkered = true;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.6f);
                    now = "agachado-andando";
                    await All(u => units.MoveAlong(u.Id, Enumerable.Range(1, 3).Select(i => new Pos { X = u.Pos.X, Y = u.Pos.Y - i }).ToList(), 1));
                    await All(u => units.MoveAlong(u.Id, Enumerable.Range(1, 3).Select(i => new Pos { X = u.Pos.X, Y = u.Pos.Y - 3 + i }).ToList(), 1));
                    now = "agachado-apuntando";
                    var aims = new Dictionary<string, int>();
                    foreach (var (u, _) in row) aims[u.Id] = 0;
                    await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u)));
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    await Shots("agachado-disparo", 1, s => now = s, d => dense = d);
                    now = "agachado-apuntando";
                    await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                    await Awaitable.WaitForSecondsAsync(0.4f);
                    now = "agachado-recarga";
                    dense = true;
                    await All(u => units.Reload(u.Id));
                    dense = false;
                    now = "agachado";
                    await Awaitable.WaitForSecondsAsync(0.4f);
                    now = "levantandose";
                    foreach (var (u, _) in row) u.Hunkered = false;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    now = "apuntando";
                    await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u)));
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    await Shots("disparo", 2, s => now = s, d => dense = d);
                    now = "bajando";
                    await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    now = "recarga";
                    dense = true;
                    await All(u => units.Reload(u.Id));
                    dense = false;
                    now = "quieto";
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    now = "vigilancia";
                    foreach (var (u, _) in row) u.Overwatch = true;
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.6f);
                    now = "vigilancia-apuntando";
                    await All(async u => aims[u.Id] = await units.RaiseWeapon(u.Id, Ahead(u, 4)));
                    await Shots("vigilancia-disparo", 1, s => now = s, d => dense = d);
                    now = "vigilancia";
                    await All(u => units.LowerWeapon(u.Id, aims[u.Id]));
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    foreach (var (u, _) in row) u.Overwatch = false;
                    now = "escondiendose";
                    await All(u => units.Hide(u.Id));
                    foreach (var (u, _) in row) u.Hidden = true;
                    units.Sync(state);
                    now = "escondido";
                    await Awaitable.WaitForSecondsAsync(1.6f);
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

            async Awaitable Shots(string name, int count, Action<string> say, Action<bool> densely)
            {
                for (int shot = 0; shot < count; shot++)
                {
                    say(name);
                    densely(true);
                    foreach (var (u, _) in row)
                    {
                        fx.MuzzleFlash(units.Muzzle(u.Id), Bastion.Client.Effects.Fx.Hex(0xffd27a));
                        units.Recoil(u.Id);
                    }
                    await Awaitable.WaitForSecondsAsync(0.45f);
                    densely(false);
                    say(name + "-tras");
                    await Awaitable.WaitForSecondsAsync(0.35f);
                }
            }
        }

        static Camera near, far;

        /// <summary>
        /// BASTION_TIRAS=folder: each soldier of the row filmed this frame by two cameras of its own, into folder/&lt;art&gt;-cerca
        /// and folder/&lt;art&gt;-juego (f000.png on): close, 2.6 m off in front of it and to its right (BASTION_LADO=izq, to its
        /// left; =detras, behind), 480×600, after it as it moves (a custom view, not the game's); and the game's camera
        /// BASTION_DIST tiles off (9, its nearest, unless said) on it, the middle 400×400 of its 1600×1000 frame as the
        /// game draws it. The state's name goes into folder/estados.txt by frame.
        /// </summary>
        static void Film(List<(Unit u, string art)> row, int frame, string what) => Film(row, frame, what, null);

        /// <summary>
        /// Film with, for each soldier aiming at a point (`aimOf`), a third camera into folder/&lt;art&gt;-hombro: UI's over the
        /// shoulder of a shot that kills (ShotCam.OverShoulder, as EventPlayer: from its chest at the point, 40° of view,
        /// 800×500), as it falls on an open floor: its first turn, 0.34 rad to the right, nothing in the way.
        /// </summary>
        static void Film(List<(Unit u, string art)> row, int frame, string what, Func<string, Vector3?> aimOf)
        {
            string folder = Environment.GetEnvironmentVariable("BASTION_TIRAS");
            if (folder == null) return;
            if (near == null)
            {
                near = new GameObject("Cámara de cerca").AddComponent<Camera>();
                far = new GameObject("Cámara del juego").AddComponent<Camera>();
                foreach (var c in new[] { near, far })
                {
                    c.enabled = false;
                    c.farClipPlane = 400;
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
                }
                near.fieldOfView = 34;
                System.IO.Directory.CreateDirectory(folder);
            }
            System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "estados.txt"), FormattableString.Invariant($"{frame:000} {Time.time:0.00} {what}\n"));
            // (Filmed in the frame's Update, the bodies skinned once a frame showed an earlier pose than their bones and what
            // they hold: through a stroke the hand open and a hand's length off the blade. Skinned again for each render.)
            foreach (var skin in units.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.forceMatrixRecalculationPerRender = true;
            string side = Environment.GetEnvironmentVariable("BASTION_LADO");
            float off = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) ? d : 9;
            foreach (var (u, art) in row)
            {
                var feet = units.Feet(u.Id);
                var turn = Quaternion.Euler(0, units.FacingNow(u.Id) ?? 0, 0);
                // (BASTION_LADO=der: square from its right, on a point 0.7 m ahead of it; =entero, the whole of it from ahead and
                // to its right, 3.4 m off, on its middle: the dead, who lean; =espalda, from behind and to its right, its back's
                // depth: a rucksack and the gun over it.)
                var from = side == "izq" ? new Vector3(-1.5f, 1.45f, 2.1f) : side == "detras" ? new Vector3(0.6f, 1.6f, -2.5f) : side == "der" ? new Vector3(2.8f, 1.3f, 0.7f)
                    : side == "entero" ? new Vector3(1.3f, 1.1f, 3.1f) : side == "espalda" ? new Vector3(1.9f, 1.35f, -1.5f) : new Vector3(1.5f, 1.45f, 2.1f);
                near.transform.position = feet + turn * from;
                near.transform.LookAt(feet + Vector3.up * (side == "entero" ? 0.9f : 1.15f) + (side == "der" ? turn * new Vector3(0, 0, 0.7f) : side == "espalda" ? turn * new Vector3(0, 0, -0.2f) : Vector3.zero));
                // (BASTION_LADO=mano: 0.7 m off the right hand, from its right and a little in front, on it.)
                if (side == "mano")
                {
                    var hand = units.Hand(u.Id);
                    near.transform.position = hand + turn * new Vector3(0.55f, 0.15f, 0.4f);
                    near.transform.LookAt(hand);
                }
                string a = System.IO.Path.Combine(folder, art + "-cerca"), b = System.IO.Path.Combine(folder, art + "-juego");
                System.IO.Directory.CreateDirectory(a);
                System.IO.Directory.CreateDirectory(b);
                Shots.Render(near, 480, 600, System.IO.Path.Combine(a, $"f{frame:000}.png"));
                // (BASTION_HOMBROS=1: the shoulder and the face close, into folder/<art>-perfil, square from its right, and
                // folder/<art>-frente, from ahead and to its right, folder/<art>-izq, from its left and ahead, and
                // folder/<art>-detras, from behind its right shoulder; on a point between its head and chest, 0.25 m ahead;
                // and folder/<art>-cuerpo, the whole of it square from its right, on its middle.)
                if (Environment.GetEnvironmentVariable("BASTION_HOMBROS") == "1")
                {
                    var on = Vector3.Lerp(units.Chest(u.Id), units.Head(u.Id), 0.5f) + turn * new Vector3(0, 0, 0.25f);
                    var middle = feet + Vector3.up * 0.85f + turn * new Vector3(0, 0, 0.3f);
                    foreach (var (name, at, centre) in new[] { ("perfil", new Vector3(1.5f, 0.05f, 0.25f), on), ("frente", new Vector3(0.75f, 0.2f, 1.4f), on), ("izq", new Vector3(-1.2f, 0.1f, 0.6f), on),
                        ("detras", new Vector3(0.75f, 0.25f, -1.1f), on), ("cuerpo", new Vector3(3.1f, 0.15f, 0), middle) })
                    {
                        string c = System.IO.Path.Combine(folder, art + "-" + name);
                        System.IO.Directory.CreateDirectory(c);
                        near.transform.position = centre + turn * at;
                        near.transform.LookAt(centre);
                        Shots.Render(near, 480, 480, System.IO.Path.Combine(c, $"f{frame:000}.png"));
                    }
                }
                // (The game's frame cropped to its middle: the same camera with the field of view of that crop.)
                far.fieldOfView = 40;
                MapSpace.Aim(far, feet + Vector3.up * 0.9f, off);
                far.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(20 * Mathf.Deg2Rad) * 400 / 1000f) * Mathf.Rad2Deg;
                Shots.Render(far, 400, 400, System.IO.Path.Combine(b, $"f{frame:000}.png"));
                if (aimOf?.Invoke(u.Id) is Vector3 to && OverShoulder(units.Chest(u.Id), to) is var (pos, look))
                {
                    string c = System.IO.Path.Combine(folder, art + "-hombro");
                    System.IO.Directory.CreateDirectory(c);
                    far.fieldOfView = 40;
                    far.transform.position = pos;
                    far.transform.LookAt(look);
                    Shots.Render(far, 800, 500, System.IO.Path.Combine(c, $"f{frame:000}.png"));
                }
            }
        }

        /// <summary>ShotCam.OverShoulder on an open floor (UI's, 9/11): its first turn and rise, all of its reach free.</summary>
        static (Vector3 pos, Vector3 look)? OverShoulder(Vector3 from, Vector3 to)
        {
            const float T = MapSpace.Tile, Reach = 1.8f * T, Head = 0.22f * T, Turn = 0.34f, Keep = 22 * Mathf.Deg2Rad, Steep = 40 * Mathf.Deg2Rad;
            var flat = new Vector3(to.x - from.x, 0, to.z - from.z);
            float run = flat.magnitude;
            if (run < 0.015f) return null;
            flat /= run;
            var right = new Vector3(flat.z, 0, -flat.x);
            var head = from + Vector3.up * Head;
            float pitch = Mathf.Clamp(Mathf.Atan2(to.y - head.y, run), -Steep, Steep);
            float back = Mathf.Cos(Turn) * Reach;
            var at = head - flat * (Mathf.Cos(pitch) * back) + right * (Mathf.Sin(Turn) * Reach);
            at.y -= Mathf.Sin(pitch) * back;
            float ahead = Mathf.Atan2(to.x - at.x, to.z - at.z);
            float off = Mathf.Repeat(Mathf.Atan2(head.x - at.x, head.z - at.z) - ahead + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            float swing = Mathf.Sign(off) * Mathf.Max(0, Mathf.Abs(off) - Keep);
            return (at, at + Quaternion.Euler(0, swing * Mathf.Rad2Deg, 0) * (to - at));
        }

        /// <summary>The handguard's point (red) and the muzzle (yellow) on `id`'s gun in the hands, once (BASTION_MARCAS).</summary>
        static void Mark(string id, string art)
        {
            var w = Anims.Weapon(art);
            foreach (var (model, drawn) in units.CarriedForLab(id))
            {
                if (!drawn || model.Find("marca fore") != null) continue;
                void Dot(string name, Vector3 local, Color colour)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    dot.name = name;
                    UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
                    dot.SetParent(model, false);
                    dot.localPosition = local;
                    dot.localScale = Vector3.one * 0.025f / Mathf.Max(model.lossyScale.x, 1e-4f);
                    var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
                    dot.GetComponent<Renderer>().sharedMaterial = m;
                }
                if (w?.fore != null && w.fore.Length >= 2) Dot("marca fore", new Vector3(0, w.fore[0], w.fore[1]), Color.red);
                else Dot("marca fore", Vector3.zero, Color.red);
                Dot("marca boca", units.MuzzleLocalForLab(id), Color.yellow);
            }
        }

        // ------------------------------------------------------------ into the body

        static readonly Mesh baked = new Mesh();
        static bool Debugging;

        /// <summary>
        /// How deep each weapon `id` shows goes into its body (centimetres; «mano» the one in its hands, «lleva» one where it
        /// rides), with the bone: the deepest of the weapon's vertices (300 at most, none within 10 cm of a palm: a grip
        /// closes round it) inside the body as Profile has it, by 1.5 cm or more.
        /// </summary>
        static string IntoReport(string id)
        {
            var animator = units.transform.Find(id)?.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return "pen=-";
            if (!profiles.TryGetValue(id, out var profile))
            {
                var body = units.BodyRenderers(id)?.OfType<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null).ToList();
                if (body == null || body.Count == 0) return "pen=-";
                profiles[id] = profile = Profile(animator, body);
                if (Debugging) Debug.Log($"[agarre] profile of {id}: " + string.Join("; ", profile.Select(p => FormattableString.Invariant($"{p.bone.name} {p.length:0.00} m, radii {p.radius.Cast<float>().Where(r => r > 0).DefaultIfEmpty(0).Min() * 100:0}–{p.radius.Cast<float>().Max() * 100:0} cm"))));
            }
            var palms = new List<Vector3>();
            foreach (var (hand, knuckle) in new[] { (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal), (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal) })
                if (animator.GetBoneTransform(hand) is Transform h) palms.Add(animator.GetBoneTransform(knuckle) is Transform k ? Vector3.Lerp(h.position, k.position, 0.6f) : h.position);
            var parts = new List<string>();
            foreach (var (model, drawn) in units.CarriedForLab(id))
            {
                var (depth, bone) = Into(model, profile, palms);
                parts.Add(FormattableString.Invariant($"{(drawn ? "mano" : "lleva")}={depth * 100:0.0}@{bone}"));
            }
            return string.Join(" ", parts);
        }

        static readonly Dictionary<string, List<Segment>> profiles = new Dictionary<string, List<Segment>>();

        /// <summary>A stretch of the body along a bone: its axis in the bone's frame (from it), and the skin's distance from that axis by band along it and sector round it.</summary>
        sealed class Segment
        {
            public Transform bone;
            public Vector3 axis, across, side;
            public float length;
            public float[,] radius = new float[Bands, Sectors];
        }

        const int Bands = 5, Sectors = 16;

        /// <summary>
        /// The body as stretches along its bones (the trunk from the hips up, the head, the arms to the wrists, the legs),
        /// each the skin's distance from its axis in bands and sectors (the 90th of each one's vertices, those skinned to
        /// that bone or a bone under it short of the next), measured in the bone's own frame on the body as it stands now:
        /// it moves with the bone. The hands are left out.
        /// </summary>
        static List<Segment> Profile(Animator a, List<SkinnedMeshRenderer> renderers)
        {
            var chain = new (HumanBodyBones from, HumanBodyBones to)[]
            {
                (HumanBodyBones.Hips, HumanBodyBones.Spine), (HumanBodyBones.Spine, HumanBodyBones.Chest), (HumanBodyBones.Chest, HumanBodyBones.UpperChest),
                (HumanBodyBones.UpperChest, HumanBodyBones.Neck), (HumanBodyBones.Neck, HumanBodyBones.Head), (HumanBodyBones.Head, HumanBodyBones.LastBone),
                (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm), (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            };
            var segments = new List<Segment>();
            var of = new Dictionary<Transform, Segment>();
            foreach (var (from, to) in chain)
            {
                var b = a.GetBoneTransform(from);
                if (b == null) continue;
                Vector3 end;
                if (to == HumanBodyBones.LastBone)
                {
                    var neck = a.GetBoneTransform(HumanBodyBones.Neck);
                    end = b.position + (neck != null ? (b.position - neck.position).normalized : Vector3.up) * 0.24f;
                }
                else if (a.GetBoneTransform(to) is Transform t) end = t.position;
                else continue;
                var axisWorld = end - b.position;
                if (axisWorld.sqrMagnitude < 1e-6f) continue;
                var seg = new Segment { bone = b, length = axisWorld.magnitude, axis = Quaternion.Inverse(b.rotation) * axisWorld.normalized };
                seg.across = Vector3.Cross(seg.axis, Mathf.Abs(seg.axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                seg.side = Vector3.Cross(seg.axis, seg.across);
                segments.Add(seg);
                of[b] = seg;
            }
            // Each skin bone's stretch: itself, else the nearest above it that has one (a twist bone, a pad on its parent's).
            var hands = new HashSet<Transform>();
            for (var hb = HumanBodyBones.LeftHand; hb <= HumanBodyBones.RightHand; hb++) if (a.GetBoneTransform(hb) is Transform t) hands.Add(t);
            Segment Owner(Transform t)
            {
                for (var x = t; x != null; x = x.parent)
                {
                    if (hands.Contains(x)) return null;
                    if (of.TryGetValue(x, out var seg)) return seg;
                }
                return null;
            }
            var lists = segments.ToDictionary(sg => sg, sg => new List<float>[Bands, Sectors]);
            foreach (var r in renderers)
            {
                r.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                var vv = baked.vertices;
                var weights = r.sharedMesh.boneWeights;
                var rb = r.bones;
                var owners = rb.Select(Owner).ToArray();
                for (int i = 0; i < vv.Length && i < weights.Length; i++)
                {
                    int bi = weights[i].boneIndex0;
                    var seg = bi < owners.Length ? owners[bi] : null;
                    if (seg == null) continue;
                    var p = m.MultiplyPoint3x4(vv[i]);
                    if (!Place(seg, p, out int band, out int sector, out float d)) continue;
                    var l = lists[seg];
                    (l[band, sector] ??= new List<float>()).Add(d);
                }
            }
            foreach (var seg in segments)
            {
                var l = lists[seg];
                for (int band = 0; band < Bands; band++)
                    for (int sector = 0; sector < Sectors; sector++)
                    {
                        var ds = l[band, sector];
                        if (ds == null || ds.Count < 3) continue;
                        ds.Sort();
                        seg.radius[band, sector] = ds[(int)(ds.Count * 0.9f)];
                    }
            }
            return segments;
        }

        /// <summary>Where `p` (world) is about `seg`: its band along (false past either end by more than a band), its sector round, its distance from the axis.</summary>
        static bool Place(Segment seg, Vector3 p, out int band, out int sector, out float d)
        {
            var q = Quaternion.Inverse(seg.bone.rotation) * (p - seg.bone.position);
            float along = Vector3.Dot(q, seg.axis) / seg.length;
            var radial = q - seg.axis * (along * seg.length);
            d = radial.magnitude;
            band = Mathf.FloorToInt(along * Bands);
            sector = (int)((Mathf.Atan2(Vector3.Dot(radial, seg.side), Vector3.Dot(radial, seg.across)) / (2 * Mathf.PI) + 1) * Sectors) % Sectors;
            if (band < -1 || band > Bands) return false;
            band = Mathf.Clamp(band, 0, Bands - 1);
            return true;
        }

        /// <summary>How deep `model`'s vertices go into the body (metres, 1.5 cm in at least), and the bone of that stretch.</summary>
        static (float depth, string bone) Into(Transform model, List<Segment> body, List<Vector3> palms)
        {
            const float Fist = 0.1f, Margin = 0.015f;
            float deepest = 0;
            string at = "-";
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy || mf.name.StartsWith("marca")) continue;
                var vv = Readable(mf.sharedMesh);
                int stride = Mathf.Max(1, vv.Length / 300);
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < vv.Length; i += stride)
                {
                    var p = m.MultiplyPoint3x4(vv[i]);
                    if (palms.Any(q => (q - p).sqrMagnitude < Fist * Fist)) continue;
                    foreach (var seg in body)
                    {
                        if (!Place(seg, p, out int band, out int sector, out float d)) continue;
                        float r = seg.radius[band, sector];
                        if (r <= 0) continue;
                        if (d < r - Margin && r - d > deepest)
                        {
                            deepest = r - d;
                            at = seg.bone.name;
                        }
                    }
                }
            }
            return (deepest, at);
        }

        static readonly Dictionary<Mesh, Vector3[]> readable = new Dictionary<Mesh, Vector3[]>();

        /// <summary>A mesh's vertices even when it isn't imported Read/Write (in play mode those come back empty): through a copy CombineMeshes makes.</summary>
        static Vector3[] Readable(Mesh mesh)
        {
            if (readable.TryGetValue(mesh, out var vv)) return vv;
            vv = mesh.isReadable ? mesh.vertices : Array.Empty<Vector3>();
            if (vv.Length == 0 && mesh.vertexCount > 0)
            {
                // (From the graphics card: the position stream's floats, read back.)
                int stream = mesh.GetVertexAttributeStream(UnityEngine.Rendering.VertexAttribute.Position);
                int offset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Position);
                var format = mesh.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Position);
                using (var buffer = mesh.GetVertexBuffer(stream))
                {
                    int stride = mesh.GetVertexBufferStride(stream);
                    var bytes = new byte[buffer.count * buffer.stride];
                    buffer.GetData(bytes);
                    vv = new Vector3[mesh.vertexCount];
                    if (format == UnityEngine.Rendering.VertexAttributeFormat.Float32)
                        for (int i = 0; i < vv.Length; i++)
                            vv[i] = new Vector3(BitConverter.ToSingle(bytes, i * stride + offset), BitConverter.ToSingle(bytes, i * stride + offset + 4), BitConverter.ToSingle(bytes, i * stride + offset + 8));
                    if (Debugging) Debug.Log($"[agarre] read back {mesh.name}: {buffer.count}×{buffer.stride} ({stride}), position at {offset} as {format}; first {(vv.Length > 0 ? vv[0] : Vector3.zero):F3}, bounds {mesh.bounds}");
                }
            }
            return readable[mesh] = vv;
        }

        // ------------------------------------------------------------ aim

        /// <summary>
        /// Aim (the review of 9/11, its 2): the row (Row) aims at a point four tiles off in each of BASTION_ANGULOS (degrees
        /// clockwise from north: every 45 unless said), at each of BASTION_ALTURAS (storeys over the ground the point is at,
        /// a chest's height over it: 0, 1, 2 and −1 unless said), in each of BASTION_POSTURAS (de-pie, agachado, vigilancia),
        /// and fires once; each aim logs where the barrel and the eyes point against the point ([punteria]: UnitViews.AimReport),
        /// once up and each frame of the shot (at 60 fps, the game's, -shotFps 60).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Punteria -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 6000 -shotFps 60 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Punteria()
        {
            var row = Row();
            Focus(row);
            var angles = (Environment.GetEnvironmentVariable("BASTION_ANGULOS") ?? "0,45,90,135,180,225,270,315").Split(',').Select(a => float.Parse(a, CultureInfo.InvariantCulture)).ToList();
            var heights = (Environment.GetEnvironmentVariable("BASTION_ALTURAS") ?? "0,1,2,-1").Split(',').Select(a => float.Parse(a, CultureInfo.InvariantCulture)).ToList();
            var postures = (Environment.GetEnvironmentVariable("BASTION_POSTURAS") ?? "de-pie,agachado,vigilancia").Split(',');
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BASTION_FOTOGRAMAS"), out int f) ? f : 16;
            // (BASTION_TIRAS: filmed as Agarres films, every BASTION_CADA-th frame; BASTION_LINEA=1: each barrel's line drawn
            // 8 m on from its muzzle, and a ball where it aims.)
            int every = int.TryParse(Environment.GetEnvironmentVariable("BASTION_CADA"), out int n) && n > 0 ? n : 1;
            bool lines = Environment.GetEnvironmentVariable("BASTION_LINEA") == "1";
            string now = "inicio";
            var points = new Dictionary<string, Vector3>();
            bool done = false;
            async Awaitable Filming()
            {
                int frame = 0;
                var drawn = new Dictionary<string, (Transform line, Transform ball)>();
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    if (lines)
                        foreach (var (u, _) in row)
                        {
                            if (!drawn.TryGetValue(u.Id, out var d))
                                drawn[u.Id] = d = (Stick(new Color(1, 0.15f, 0.1f)), Ball(new Color(1, 0.9f, 0.1f)));
                            var gun = units.CarriedForLab(u.Id).FirstOrDefault(c => c.drawn).model;
                            bool up = points.TryGetValue(u.Id, out var at) && gun != null;
                            d.line.gameObject.SetActive(up);
                            d.ball.gameObject.SetActive(up);
                            if (!up) continue;
                            var muzzle = gun.TransformPoint(units.MuzzleLocalForLab(u.Id));
                            d.line.SetPositionAndRotation(muzzle + gun.forward * 4, Quaternion.LookRotation(gun.forward) * Quaternion.Euler(90, 0, 0));
                            d.ball.position = at;
                        }
                    if (++frame % every == 0) Film(row, frame / every, now, id => points.TryGetValue(id, out var p) ? p : (Vector3?)null);
                }
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = Filming();
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    // (At rest, a point far ahead at the eyes' height: what the eyes' measure gives looking straight on.)
                    foreach (var (u, art) in row)
                    {
                        Debug.Log($"[punteria] gaze {art}: {units.CalibrateGazeForLab(u.Id)}");
                        var ahead = units.Head(u.Id) + Quaternion.Euler(0, units.FacingNow(u.Id) ?? 0, 0) * Vector3.forward * 20;
                        Debug.Log($"[punteria] {Time.time:0.000} reposo 0 0 up {art} {units.AimReport(u.Id, ahead)}");
                    }
                    foreach (string posture in postures)
                    {
                        foreach (var (u, _) in row)
                        {
                            u.Hunkered = posture == "agachado";
                            u.Overwatch = posture == "vigilancia";
                        }
                        units.Sync(state);
                        await Awaitable.WaitForSecondsAsync(1.0f);
                        foreach (float height in heights)
                            foreach (float angle in angles)
                            {
                                float r = angle * Mathf.Deg2Rad;
                                Vector3 Point(Unit u) => MapSpace.World(u.Pos.X + 4 * Mathf.Sin(r), u.Pos.Y - 4 * Mathf.Cos(r)) + Vector3.up * (1.25f + height * MapSpace.Storey);
                                string what = FormattableString.Invariant($"{posture} {angle:0} {height:0}");
                                now = FormattableString.Invariant($"{posture}-{angle:0}-{height:0}");
                                foreach (var (u, _) in row) points[u.Id] = Point(u);
                                var aims = new Dictionary<string, int>();
                                foreach (var (u, _) in row) _ = Raise(u, Point(u));
                                async Awaitable Raise(Unit u, Vector3 p) => aims[u.Id] = await units.RaiseWeapon(u.Id, p);
                                await Awaitable.WaitForSecondsAsync(1.1f);
                                foreach (var (u, art) in row) Debug.Log($"[punteria] {Time.time:0.000} {what} up {art} {units.AimReport(u.Id, Point(u))}");
                                foreach (var (u, art) in row) Debug.Log($"[hombro] {what} {art} {units.ShoulderReport(u.Id)} {units.GripReport(u.Id)} {IntoReport(u.Id)}");
                                if (Environment.GetEnvironmentVariable("BASTION_STANCE") == "1")
                                    foreach (var (u, art) in row) Debug.Log($"[punteria] stance {art}: {units.Stance(u.Id)}");
                                foreach (var (u, _) in row)
                                {
                                    fx.MuzzleFlash(units.Muzzle(u.Id), Bastion.Client.Effects.Fx.Hex(0xffd27a));
                                    units.Recoil(u.Id);
                                }
                                for (int k = 1; k <= frames; k++)
                                {
                                    await Awaitable.NextFrameAsync();
                                    foreach (var (u, art) in row) Debug.Log($"[punteria] {Time.time:0.000} {what} +{k} {art} {units.AimReport(u.Id, Point(u))}");
                                }
                                await Awaitable.WaitForSecondsAsync(0.3f);
                                points.Clear();
                                now = "bajando";
                                foreach (var (u, _) in row) if (aims.TryGetValue(u.Id, out int aim)) _ = units.LowerWeapon(u.Id, aim);
                                await Awaitable.WaitForSecondsAsync(0.5f);
                            }
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

        // ------------------------------------------------------------ blades and throws

        /// <summary>Every blade of Anims.json's weapons, as the review takes them.</summary>
        static readonly string[] Blades = { "knife", "machete", "pan", "bat", "crowbar", "chainsaw", "katana", "longsword", "rake", "stick", "baton" };

        /// <summary>One of the dead for each of `row`, kept off the floor till placed (its id «lab-z-» and the soldier's art).</summary>
        static List<Unit> Targets(List<(Unit u, string art)> row)
        {
            var first = MapLab.Golden("city-7");
            string json = Bastion.Net.Json.Write(first.Units.First(u => u.Alive && u.Template == "zombie"));
            var dead = new List<Unit>();
            foreach (var (u, art) in row)
            {
                var z = Bastion.Net.Json.Read<Unit>(json);
                z.Id = "lab-z-" + art;
                Put(z, u.Pos.X, u.Pos.Y - 1, Math.PI / 2);
                state.Units.Add(z);
                dead.Add(z);
            }
            return dead;
        }

        /// <summary>
        /// Blades (the review of 9/11, its 1 and 2): a soldier for each of BASTION_HOJAS (Blades unless said) in the row, a
        /// rifle in its hands and the blade at its belt or across its back, as the Armería arms it (the knife: no blade of
        /// its own; the baton, the Cuervos', and BASTION_EN_MANO=1 all of them, the blade in its hands, nothing else), and
        /// one of the dead a tile off each: ahead, to its right, behind, in turn. Each time it waits, is on guard at it (the
        /// target picked: AimAt melee), strikes it (Lunge, as EventPlayer), and back; then on watch with the blade. Each frame
        /// [cuerpo]: the grip (GripReport) and how deep (Into); through the stroke [golpe]: where its root and the blade's
        /// middle are against the target's chest (degrees round the upright, from its own chest) and how far the blade's
        /// middle is from it.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Cuerpo -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 400 -shotFps 30 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Cuerpo()
        {
            Environment.SetEnvironmentVariable("BASTION_ARMAS", Environment.GetEnvironmentVariable("BASTION_HOJAS") ?? string.Join(",", Blades));
            var row = Row();
            bool inHand = Environment.GetEnvironmentVariable("BASTION_EN_MANO") == "1";
            foreach (var (u, art) in row)
            {
                if (inHand || art == "baton") continue;
                UnitViews.ArtsForLab.Remove(u.Id);
                u.Arms = new Arms { Primary = "assaultRifle", Melee = art == "knife" ? null : art };
            }
            var dead = Targets(row);
            Focus(row);
            int every = int.TryParse(Environment.GetEnvironmentVariable("BASTION_CADA"), out int n) && n > 0 ? n : 1;
            string now = "inicio";
            bool done = false, stroke = false;
            async Awaitable Watch()
            {
                int frame = 0;
                float lastDepth = -1;
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    frame++;
                    bool depth = stroke || Time.time - lastDepth >= 0.099f;
                    if (depth) lastDepth = Time.time;
                    for (int k = 0; k < row.Count; k++)
                    {
                        var (u, art) = row[k];
                        string into = depth ? " " + IntoReport(u.Id) : "";
                        Debug.Log(FormattableString.Invariant($"[cuerpo] {Time.time:0.000} {now} {art} {units.GripReport(u.Id)}{into}"));
                        if (stroke) Debug.Log(FormattableString.Invariant($"[golpe] {Time.time:0.000} {now} {art} {Blow(u.Id, dead[k].Id)}"));
                        if (stroke && Environment.GetEnvironmentVariable("BASTION_STANCE") == "1") Debug.Log(FormattableString.Invariant($"[capas] {Time.time:0.000} {art} {units.Stance(u.Id)}"));
                    }
                    if (frame % every == 0) Film(row, frame / every, now);
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
                    foreach (var (where, dx, dy) in new[] { ("delante", 0, -1), ("derecha", 1, 0), ("detras", 0, 1) })
                    {
                        for (int k = 0; k < row.Count; k++)
                        {
                            Put(dead[k], row[k].u.Pos.X + dx, row[k].u.Pos.Y + dy, Math.Atan2(-dy, -dx));
                            row[k].u.Facing = -Math.PI / 2;
                        }
                        units.Sync(state);
                        Reveal();
                        now = "reposo-" + where;
                        await Awaitable.WaitForSecondsAsync(1.0f);
                        now = "guardia-" + where;
                        for (int k = 0; k < row.Count; k++) units.AimAt(row[k].u.Id, units.Chest(dead[k].Id), true);
                        await Awaitable.WaitForSecondsAsync(1.2f);
                        for (int k = 0; k < row.Count; k++) units.AimAt(row[k].u.Id, null);
                        await Awaitable.WaitForSecondsAsync(0.2f);
                        now = "golpe-" + where;
                        stroke = true;
                        var blows = new List<Awaitable>();
                        for (int k = 0; k < row.Count; k++)
                        {
                            var (u, art) = row[k];
                            string target = dead[k].Id;
                            blows.Add(units.Lunge(u.Id, target, false, () => Debug.Log(FormattableString.Invariant($"[golpe] {Time.time:0.000} {now} {art} lands {Blow(u.Id, target)}"))));
                        }
                        foreach (var b in blows) await b;
                        await Awaitable.WaitForSecondsAsync(0.6f);
                        stroke = false;
                        now = "tras-" + where;
                        await Awaitable.WaitForSecondsAsync(0.6f);
                    }
                    now = "vigilancia-hoja";
                    foreach (var (u, _) in row)
                    {
                        u.Overwatch = true;
                        u.WatchMelee = true;
                    }
                    units.Sync(state);
                    await Awaitable.WaitForSecondsAsync(1.6f);
                    foreach (var (u, _) in row) u.Overwatch = false;
                    units.Sync(state);
                    now = "fin";
                    await Awaitable.WaitForSecondsAsync(0.8f);
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

        /// <summary>
        /// Medicine (the review of 9/11, Gameplay: «agacharse con las manos al herido»): a soldier for each of BASTION_ARMAS
        /// (the rifle and the pistol unless said) in the row; the first treats a wounded one a tile ahead of it (under half
        /// its health, slumped), the others themselves, as EventPlayer plays a medicine (UnitViews.Treat), twice.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Curar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 200 -shotFps 30 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Curar()
        {
            if (Environment.GetEnvironmentVariable("BASTION_ARMAS") == null) Environment.SetEnvironmentVariable("BASTION_ARMAS", "rifle,pistol");
            if (Resources.Load<Bastion.Client.Effects.FxArt>("FxArt") == null) FxArtMake.Run();
            var row = Row();
            var (first, _) = row[0];
            var wounded = Bastion.Net.Json.Read<Unit>(Bastion.Net.Json.Write(first));
            wounded.Id = "lab-herido";
            wounded.Hp = Math.Max(1, wounded.MaxHp / 3);
            UnitViews.ArtsForLab.Remove(wounded.Id);
            Put(wounded, first.Pos.X, first.Pos.Y - 1, Math.PI / 2);
            state.Units.Add(wounded);
            Focus(row);
            int every = int.TryParse(Environment.GetEnvironmentVariable("BASTION_CADA"), out int n) && n > 0 ? n : 1;
            string now = "inicio";
            bool done = false;
            Vector3? home = null;
            async Awaitable Watch()
            {
                int frame = 0;
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    frame++;
                    // (The one treating another, each frame: how far its root has gone from its tile, and its muzzle over its
                    // right shoulder, cm; the 3D review's A-19, 10/11. And how the gun sits in its hand, G-1.)
                    var id = first.Id;
                    var rig = units.transform.Find(id)?.GetComponentInChildren<Animator>();
                    if (home is Vector3 h && rig != null && rig.isHuman && rig.GetBoneTransform(HumanBodyBones.RightUpperArm) is Transform shoulder)
                    {
                        var feet = units.Feet(id);
                        Debug.Log(FormattableString.Invariant($"[curar] {Time.time:0.000} {now} root={new Vector2(feet.x - h.x, feet.z - h.z).magnitude * 100:0.0} muzzleUp={(units.Muzzle(id).y - shoulder.position.y) * 100:0.0}"));
                        Debug.Log(FormattableString.Invariant($"[mano-fusil] {id} {Time.time:0.000} {now} {units.GripReport(id)}"));
                    }
                    if (frame % every == 0) Film(row, frame / every, now);
                }
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    home = units.Feet(first.Id);
                    _ = Watch();
                    await Awaitable.WaitForSecondsAsync(1.0f);
                    for (int time = 1; time <= 2; time++)
                    {
                        now = "cura-" + time;
                        var cures = new List<Awaitable>();
                        for (int k = 0; k < row.Count; k++) cures.Add(units.Treat(row[k].u.Id, k == 0 ? wounded.Id : row[k].u.Id));
                        foreach (var c in cures) await c;
                        now = "tras-" + time;
                        await Awaitable.WaitForSecondsAsync(0.8f);
                    }
                    now = "fin";
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

        /// <summary>The turn of what `id` holds in its right hand's frame (the 3D review's G-1: how far the gun turns in the hand); null without.</summary>
        static Quaternion? InHand(string id)
        {
            var rig = units.transform.Find(id)?.GetComponentInChildren<Animator>();
            var model = units.CarriedForLab(id).FirstOrDefault(c => c.drawn).model;
            return rig != null && rig.isHuman && model != null && rig.GetBoneTransform(HumanBodyBones.RightHand) is Transform hand ? Quaternion.Inverse(hand.rotation) * model.rotation : (Quaternion?)null;
        }

        /// <summary>Where `id` and what it holds are against `target`'s chest: its root's heading and the held model's middle (degrees round the upright, from its chest), and that middle's distance from the target's chest (cm).</summary>
        static string Blow(string id, string target)
        {
            var chest = units.Chest(id);
            var aim = units.Chest(target) - chest;
            var model = units.CarriedForLab(id).FirstOrDefault(c => c.drawn).model;
            float Yaw(Vector3 d) => Vector3.SignedAngle(new Vector3(aim.x, 0, aim.z), new Vector3(d.x, 0, d.z), Vector3.up);
            var facing = Quaternion.Euler(0, units.FacingNow(id) ?? 0, 0) * Vector3.forward;
            string blade = "-";
            if (model != null)
            {
                var box = new Bounds(model.position, Vector3.zero);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) if (!r.name.StartsWith("marca")) box.Encapsulate(r.bounds);
                blade = FormattableString.Invariant($"bladeYaw={Yaw(box.center - chest):0.0} gap={(box.center - units.Chest(target)).magnitude * 100:0}");
            }
            // (How far out each upper arm is from the trunk's line down: a cross is both over 70°.)
            string arms = "";
            var animator = units.transform.Find(id)?.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Hips) is Transform hips && animator.GetBoneTransform(HumanBodyBones.Neck) is Transform neck)
            {
                var down = (hips.position - neck.position).normalized;
                float Out(HumanBodyBones upper, HumanBodyBones lower) => animator.GetBoneTransform(upper) is Transform u && animator.GetBoneTransform(lower) is Transform l ? Vector3.Angle(l.position - u.position, down) : 0;
                arms = FormattableString.Invariant($" armL={Out(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm):0} armR={Out(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm):0}");
            }
            // (The right palm from where the held model's grip is, and what is held.)
            string grip = "";
            if (model != null && animator != null && animator.GetBoneTransform(HumanBodyBones.RightHand) is Transform rh && animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal) is Transform rk)
            {
                var palm = Vector3.Lerp(rh.position, rk.position, 0.6f);
                // (How far the middle finger is curled at its first two joints, degrees: straight is 0.)
                string curl = "-";
                if (animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate) is Transform mi && animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal) is Transform md)
                    curl = FormattableString.Invariant($"{Vector3.Angle(rk.position - rh.position, mi.position - rk.position):0}/{Vector3.Angle(mi.position - rk.position, md.position - mi.position):0}");
                grip = FormattableString.Invariant($" palmToModel={(palm - model.position).magnitude * 100:0} curl={curl} hand={rh.position.x:0.000},{rh.position.y:0.000},{rh.position.z:0.000}");
            }
            return FormattableString.Invariant($"rootYaw={Yaw(facing):0.0} {blade}{arms}{grip}");
        }

        /// <summary>
        /// Throws (the review of 9/11, its 2): a soldier for each of the grenade, the molotov and the stone in a row (a rifle
        /// in its hands), each throwing at a point five tiles off ahead, to its right and behind in turn, as EventPlayer
        /// throws (turned to it, 0.25 s, ThrowMotion; TossStone). As the hand lets go [lanzar]: where the throwing hand was
        /// going over the last tenth of a second against the line from it to the point, round the upright and up (degrees),
        /// and the root's heading against it; then the arc.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Lanzar -shotPlay -shotCamera "Shot camera"
        ///     -shotFrames 300 -shotFps 30 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Lanzar()
        {
            Environment.SetEnvironmentVariable("BASTION_ARMAS", "rifle,rifle2,rifle3,shotgun,smg");
            var row = Row();
            string[] kinds = { "granada", "molotov", "piedra", "humo", "aturdidora" };
            foreach (var (u, _) in row)
            {
                UnitViews.ArtsForLab.Remove(u.Id);
                u.Arms = new Arms { Primary = "assaultRifle" };
            }
            Focus(row);
            string now = "inicio";
            bool done = false;
            var trail = new Dictionary<string, Queue<(float t, Vector3 p)>>();
            var aims = new Dictionary<string, Vector3>();
            var inHandAt = new Dictionary<string, Quaternion>();
            async Awaitable Watch()
            {
                int frame = 0;
                while (!done)
                {
                    await Awaitable.NextFrameAsync();
                    frame++;
                    foreach (var (u, _) in row)
                    {
                        if (!trail.TryGetValue(u.Id, out var q)) trail[u.Id] = q = new Queue<(float, Vector3)>();
                        q.Enqueue((Time.time, units.ThrowHand(u.Id)));
                        while (q.Count > 0 && q.Peek().t < Time.time - 0.1f) q.Dequeue();
                        if (aims.TryGetValue(u.Id, out var to)) Debug.Log($"[mano] {Time.time:0.000} {u.Id} {Release(u.Id, to)}");
                        // (The rifle in the right hand as the left throws: the 3D review's G-1, 10/11, its «rigid», and how far
                        // it has turned in that hand since the throw began, «turn», degrees.)
                        string turned = inHandAt.TryGetValue(u.Id, out var was) && InHand(u.Id) is Quaternion rel ? FormattableString.Invariant($" turn={Quaternion.Angle(rel, was):0.0}") : "";
                        Debug.Log(FormattableString.Invariant($"[mano-fusil] {u.Id} {Time.time:0.000} {now} {units.GripReport(u.Id)}{turned}"));
                    }
                    Film(row, frame, now);
                }
            }
            string Release(string id, Vector3 to)
            {
                var q = trail.TryGetValue(id, out var x) ? x.ToArray() : Array.Empty<(float t, Vector3 p)>();
                var hand = units.ThrowHand(id);
                var going = q.Length > 0 ? hand - q[0].p : Vector3.zero;
                var want = to - hand;
                float Yaw(Vector3 d) => Vector3.SignedAngle(new Vector3(want.x, 0, want.z), new Vector3(d.x, 0, d.z), Vector3.up);
                float Pitch(Vector3 d) => Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                var facing = Quaternion.Euler(0, units.FacingNow(id) ?? 0, 0) * Vector3.forward;
                return FormattableString.Invariant($"handYaw={Yaw(going):0.0} handPitch={Pitch(going):0.0} speed={going.magnitude / 0.1f:0.0} rootYaw={Yaw(facing):0.0} handUp={hand.y - units.Feet(id).y:0.00}");
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    _ = Watch();
                    await Awaitable.WaitForSecondsAsync(0.8f);
                    foreach (var (where, dx, dy) in new[] { ("delante", 0, -5), ("derecha", 5, 0), ("detras", 0, 5) })
                    {
                        now = "lanzar-" + where;
                        var throws = new List<Awaitable>();
                        for (int k = 0; k < row.Count; k++)
                        {
                            var (u, _) = row[k];
                            string kind = kinds[k % kinds.Length];
                            var to = MapSpace.World(u.Pos.X + dx, u.Pos.Y + dy);
                            throws.Add(Throw(u.Id, kind, to, where));
                        }
                        foreach (var t in throws) await t;
                        await Awaitable.WaitForSecondsAsync(1.2f);
                        foreach (var (u, _) in row) units.Face(u.Id, units.Feet(u.Id) + Vector3.forward);
                        await Awaitable.WaitForSecondsAsync(0.8f);
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
            async Awaitable Throw(string id, string kind, Vector3 to, string where)
            {
                aims[id] = to;
                if (InHand(id) is Quaternion rel) inHandAt[id] = rel;
                units.Face(id, to);
                await Awaitable.WaitForSecondsAsync(0.25f);
                if (kind == "piedra")
                {
                    var hand = await units.TossStone(id, to);
                    Debug.Log($"[lanzar] {Time.time:0.000} {where} {kind} {Release(id, to)}");
                    await fx.GrenadeArc(hand, to);
                    return;
                }
                Awaitable arc = null;
                // (As EventPlayer: what is held, and the same in the air.)
                string held = kind == "molotov" ? "molotov" : kind == "humo" ? "smoke" : kind == "aturdidora" ? "flash" : "grenade";
                await units.ThrowMotion(id, () =>
                {
                    Debug.Log($"[lanzar] {Time.time:0.000} {where} {kind} {Release(id, to)}");
                    arc = kind == "molotov" ? fx.MolotovArc(units.ThrowHand(id), to) : fx.GrenadeArc(units.ThrowHand(id), to, default, held);
                }, default, held);
                if (arc != null) await arc;
            }
        }

        /// <summary>
        /// Where each blade's model runs along the way it is held (WeaponArt along, from its grip, in its own units at its
        /// size): its far end, its back end (the pommel), and how wide it is across there; for the grips (the review of 9/11).
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CombatLab.Mangos
        /// </summary>
        public static void Mangos()
        {
            var catalog = FigureCatalog.Load();
            foreach (var id in Blades)
            {
                var art = Anims.Weapon(id);
                var prefab = art != null ? catalog.Weapon(art.fbx) : null;
                if (prefab == null)
                {
                    Debug.Log($"[mangos] {id}: no model");
                    continue;
                }
                var model = UnityEngine.Object.Instantiate(prefab);
                var along = art.along != null && art.along.Length >= 3 ? new Vector3(art.along[0], art.along[1], art.along[2]).normalized : Vector3.forward;
                var grip = art.grip != null && art.grip.Length >= 3 ? new Vector3(art.grip[0], art.grip[1], art.grip[2]) : Vector3.zero;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var p = model.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                        float t = Vector3.Dot(p - grip, along);
                        lo = Mathf.Min(lo, t);
                        hi = Mathf.Max(hi, t);
                    }
                }
                float size = art.size > 0 ? art.size : 1;
                Debug.Log(FormattableString.Invariant($"[mangos] {id} ({art.fbx}): along {along}, grip {grip}; from the grip, back {lo:0.000} to front {hi:0.000} (model units; ×{size} × {Figures.CharacterScale})"));
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        /// <summary>A thin stick 8 m long along its own up (a barrel's line), unlit, in `colour`.</summary>
        static Transform Stick(Color colour)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
            t.localScale = new Vector3(0.012f, 4, 0.012f);
            t.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
            return t;
        }

        /// <summary>A small ball (where it aims), unlit, in `colour`.</summary>
        static Transform Ball(Color colour)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
            t.localScale = Vector3.one * 0.12f;
            t.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
            return t;
        }
    }
}
