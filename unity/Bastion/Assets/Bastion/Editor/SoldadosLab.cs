using System;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The soldiers of 0.12.15 (Arturo, 9/11, of Biblioteca's list): Apocalypse Survivors' rucksacks, Modern Police's gear,
    /// Modern Civilians' street clothes, and the engine's nine new heads.
    /// </summary>
    public static partial class CombatLab
    {
        /// <summary>The outfits and heads of 0.12.15, as Dress and Appearances.Heads have them.</summary>
        static readonly string[] NewOutfits = { "survivor9", "survivor10", "survivor11", "police4", "police5", "civilian5", "civilian6", "civilian7", "civilian8" };
        static readonly string[] NewHeads = { "visorHelmet", "earflapHat", "twoToneCap", "goggleHelmet", "riotHelmet", "plainCap", "bandana", "scarfHood", "bucketHat" };

        /// <summary>
        /// A row of soldiers born with a wardrobe (Appearance.Wardrobe 1), two tiles apart, facing the camera: each in one of
        /// BASTION_ROPA (NewOutfits unless said) with one of BASTION_CABEZAS (bare unless said; «nuevas», NewHeads), as many
        /// as the longer list, the shorter cycled; BASTION_SEXO f or m (else turn about), BASTION_COMPLEXION a build. Their
        /// knife in the hand, so that the rifle rides across the back (over a rucksack: Stows). Still a second, then two tiles
        /// towards the camera, filmed every frame from ahead (BASTION_TIRAS/entero), behind (/detras) and behind to the right
        /// (/espalda; BASTION_LADOS others), each with the game's camera beside (Film). Logs each one's dress, how far its
        /// rucksack stands out and the colours it is painted in. BASTION_GUARDIA=0: the knife at the belt, the rifle in the
        /// hands. BASTION_ROPA=azar: no outfit chosen, dressed as recruits are (by their name: Dress.Of's pick); with
        /// BASTION_ARMARIO=0, as those born before 0.12.15 (no Appearance.Wardrobe).
        ///   BASTION_TIRAS=&lt;folder&gt; unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Soldados -shotPlay
        ///     -shotCamera "Shot camera" -shotFrames 34 -shotFps 10 -shotOut &lt;folder&gt;
        /// </summary>
        public static void Soldados()
        {
            var outfits = Environment.GetEnvironmentVariable("BASTION_ROPA")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? NewOutfits;
            string heads = Environment.GetEnvironmentVariable("BASTION_CABEZAS");
            var hats = heads == "nuevas" ? NewHeads : heads?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new[] { HeadIds.Bare };
            string sex = Environment.GetEnvironmentVariable("BASTION_SEXO");
            string build = Environment.GetEnvironmentVariable("BASTION_COMPLEXION");
            int n = Math.Max(outfits.Length, hats.Length);
            var first = MapLab.Golden("city-7");
            var model = first.Units.First(u => u.Alive && u.Team == "xcom");
            string json = Bastion.Net.Json.Write(model);
            Plain(u => false, 2, 2 + 2 * Math.Min(12, n) - 2);
            for (int k = 0; k < n; k++)
            {
                var u = Bastion.Net.Json.Read<Unit>(json);
                u.Id = $"lab-{k}-{outfits[k % outfits.Length]}-{hats[k % hats.Length]}";
                u.Name = "Laboratorio " + k;
                u.Template = "rookie";
                u.Overwatch = false;
                u.Hunkered = false;
                u.Hidden = null;
                u.Arms = new Arms { Primary = "assaultRifle", Pistol = false };
                u.Appearance ??= new Appearance();
                u.Appearance.Character = outfits[k % outfits.Length] == "azar" ? null : outfits[k % outfits.Length];
                u.Appearance.Head = hats[k % hats.Length];
                u.Appearance.Sex = sex ?? (k % 2 == 0 ? Sexes.F : Sexes.M);
                u.Appearance.Wardrobe = Environment.GetEnvironmentVariable("BASTION_ARMARIO") == "0" ? (int?)null : Appearances.Wardrobe;
                if (build != null) u.Appearance.Build = build;
                state.Units.Add(Put(u, 2 + 2 * (k % 12), 14 - 4 * (k / 12), Math.PI / 2));
            }
            var row = state.Units.Select((u, k) => (u, (u.Appearance.Character ?? "azar" + k) + "-" + u.Appearance.Head + "-" + u.Appearance.Sex)).ToList();
            string folder = Environment.GetEnvironmentVariable("BASTION_TIRAS");
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.NextFrameAsync();
                    if (Environment.GetEnvironmentVariable("BASTION_GUARDIA") != "0")
                        foreach (var (u, _) in row) units.GuardForLab(u.Id, 0);
                    await Awaitable.NextFrameAsync();
                    foreach (var (u, name) in row)
                    {
                        var marks = Figures.FeetOf(u.Id)?.GetComponentInChildren<SoldierMarks>(true);
                        // (And, posed, how far behind the upper chest its back's surface is along the slung gun's line, model units.)
                        var animator = Figures.FeetOf(u.Id)?.GetComponentInChildren<Animator>();
                        var body = animator != null ? animator.transform : null;
                        var upper = animator != null ? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
                        float surface = 0;
                        if (body != null && upper != null)
                        {
                            var s3 = body.InverseTransformPoint(upper.position);
                            var line = new Vector2(0.45f, 0.9f).normalized;
                            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                var baked = new Mesh();
                                r.BakeMesh(baked, true);
                                foreach (var v in baked.vertices)
                                {
                                    var at = body.InverseTransformPoint(r.transform.TransformPoint(v));
                                    if (Mathf.Abs((at.x - s3.x) * line.y - (at.y - s3.y - 0.02f) * line.x) <= 0.08f) surface = Mathf.Max(surface, s3.z - at.z);
                                }
                                UnityEngine.Object.DestroyImmediate(baked);
                            }
                        }
                        Debug.Log(FormattableString.Invariant($"[soldados] {name}: pack {(marks != null ? marks.pack : -1):0.000}, its back posed {surface:0.000}, body scale {(body != null ? body.lossyScale.x : 0):0.000}"));
                        // (The clothes' cells it is painted from, as made: x,y=colour, saturation and value.)
                        if (marks == null) continue;
                        var used = Figures.FeetOf(u.Id).GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null)
                            .SelectMany(r => r.sharedMesh.uv).Select(SoldierPalette.CellOf).Where(c => !SoldierPalette.IsBody(c)).Distinct().OrderBy(c => c);
                        Debug.Log("[celdas] " + name + " " + string.Join(" ", used.Select(c =>
                        {
                            Color.RGBToHSV(marks.palette[c], out float h, out float sat, out float v);
                            return FormattableString.Invariant($"{c % 16},{c / 16}={ColorUtility.ToHtmlStringRGB(marks.palette[c])}/{h * 360:0}/{sat:0.00}/{v:0.00}");
                        })));
                    }
                    void Shoot(int frame, string what)
                    {
                        if (folder == null) return;
                        foreach (string side in (Environment.GetEnvironmentVariable("BASTION_LADOS") ?? "entero,detras,espalda").Split(','))
                        {
                            Environment.SetEnvironmentVariable("BASTION_LADO", side);
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder, side));
                            Environment.SetEnvironmentVariable("BASTION_TIRAS", System.IO.Path.Combine(folder, side));
                            Film(row, frame, what);
                        }
                    }
                    int frame = 0;
                    for (float t = 0; t < 1; t += Time.deltaTime)
                    {
                        Shoot(frame++, "quietos");
                        await Awaitable.NextFrameAsync();
                    }
                    int walking = row.Count;
                    foreach (var (u, _) in row) Walk(u);
                    async void Walk(Unit u)
                    {
                        await units.MoveAlong(u.Id, new[] { new Pos { X = u.Pos.X, Y = u.Pos.Y + 1 }, new Pos { X = u.Pos.X, Y = u.Pos.Y + 2 } }, 1);
                        walking--;
                    }
                    for (int k = 0; k < 40 && walking > 0; k++)
                    {
                        Shoot(frame++, "andan");
                        await Awaitable.NextFrameAsync();
                    }
                    Debug.Log($"[soldados] done, {frame} frames");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };
        }
    }
}
