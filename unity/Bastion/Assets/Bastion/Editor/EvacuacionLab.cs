using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// Evacuación's four civilians (Producto, 20/11: .dev-snapshots/arte/unity/runs/evacuacion/), a look to choose from:
    /// on a generated map, a patch cleared by hand, a soldier, the four and a dead one in a row, from the game's camera.
    /// The civilians are the VIP's template in Modern Civilians' street clothes, bare-headed, nothing in their hands.
    ///   BASTION_CIVILES=tal (as the outfits are) | claro (their tops pale) | azul (their tops a light blue) | #rrggbb
    ///   BASTION_ROPA=civilian1,civilian6,civilian7,civilian8 BASTION_DIST=24 BASTION_GIRO=45
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.EvacuacionLab.Civiles -shotCamera "Shot camera" …
    /// </summary>
    public static class EvacuacionLab
    {
        static string Env(string name, string or) => Environment.GetEnvironmentVariable(name) ?? or;

        public static void Civiles()
        {
            var made = Env("BASTION_GENERAR", "wilds,3").Split(',');
            var state = MapLab.Generated(made[0], int.Parse(made[1]), made.Length > 2 ? made[2] : "elimination", made.Length > 3 ? made[3] : null);
            var at = Env("BASTION_CAMPO", $"{state.Width / 2 - 6},{state.Height / 2 - 5}").Split(',').Select(int.Parse).ToArray();
            int ox = at[0], oy = at[1];
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 13; x++)
                {
                    int i = (oy + y) * state.Width + ox + x;
                    state.Tiles[i] = TileKinds.Floor;
                    if (state.Decor != null && i < state.Decor.Count) state.Decor[i] = 0;
                }
            state.Lights?.RemoveAll(l => l.Tile.X >= ox && l.Tile.X < ox + 13 && l.Tile.Y >= oy && l.Tile.Y < oy + 9);
            var outfits = Env("BASTION_ROPA", "civilian1,civilian6,civilian7,civilian8").Split(',');
            var squad = state.Units.Where(u => u.Team == Teams.Xcom).Take(1).ToList();
            state.Units.RemoveAll(u => !squad.Contains(u));
            squad[0].Pos = new Pos { X = ox + 2, Y = oy + 4 };
            var civilians = new List<Unit>();
            for (int k = 0; k < 4; k++)
            {
                var u = Setup.MakeUnit("civil" + k, TemplateIds.Vip, "Civil " + k, null, new Pos { X = ox + 4 + k, Y = oy + 4 }, null);
                u.Appearance = new Appearance { Character = outfits[k % outfits.Length], Head = HeadIds.Bare, Sex = k % 2 == 0 ? Sexes.F : Sexes.M, Wardrobe = Appearances.Wardrobe };
                state.Units.Add(u);
                civilians.Add(u);
            }
            state.Units.Add(Setup.MakeUnit("z1", TemplateIds.Zombie, "z1", null, new Pos { X = ox + 9, Y = oy + 4 }, null));
            state.Units.Add(Setup.MakeUnit("z2", TemplateIds.Zombie, "z2", null, new Pos { X = ox + 10, Y = oy + 4 }, null));
            // (Nothing of the map's own burning there; all of them facing south, the camera.)
            state.Fires?.Clear();
            foreach (var u in state.Units) u.Facing = Math.PI / 2;
            foreach (var u in state.Units) Debug.Log($"[civiles] {u.Id}: {u.Template}, team {u.Team}, {u.Appearance?.Character}, arms {u.Arms?.Primary}");

            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
            MapLab.Map(state, "evacuacion");
            var units = new GameObject("Unidades").AddComponent<UnitViews>();
            units.Sync(state);
            foreach (var u in state.Units) units.Reveal(u.Id);
            units.MasksNow();

            // Their tops (the torso's cells and the arms'): towards one colour, keeping the cloth's shading.
            string how = Env("BASTION_CIVILES", "tal");
            string dye = how == "claro" ? "#e9e2cf" : how == "azul" ? "#8fc3e6" : how.StartsWith("#") ? how : null;
            if (dye != null && ColorUtility.TryParseHtmlString(dye, out var d))
                foreach (var u in civilians)
                {
                    var figure = Figures.FeetOf(u.Id);
                    var marks = figure != null ? figure.GetComponentInChildren<SoldierMarks>(true) : null;
                    if (marks == null)
                    {
                        Debug.LogWarning($"[civiles] {u.Id}: no marks, not painted");
                        continue;
                    }
                    var colours = (Color32[])marks.palette.Clone();
                    int n = 0;
                    for (int c = 0; c < 256; c++)
                    {
                        int bits = marks.cells[c];
                        if ((bits & (1 << 2 | 1 << 3)) == 0 || SoldierPalette.IsBody(c)) continue;
                        Color srgb = colours[c];
                        var lin = srgb.linear;
                        float lum = 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
                        var mixed = Color.Lerp(lin, d.linear * Mathf.Clamp(lum / 0.2f, 0.55f, 1.15f), 0.85f).gamma;
                        colours[c] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.r) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.g) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(mixed.b) * 255), 255);
                        n++;
                    }
                    var map = SoldierPalette.Texture(colours);
                    var block = new MaterialPropertyBlock();
                    foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (r.sharedMaterial == null || !r.sharedMaterial.HasTexture("_ColorMap")) continue;
                        r.GetPropertyBlock(block);
                        block.SetTexture("_ColorMap", map);
                        r.SetPropertyBlock(block);
                    }
                    Debug.Log($"[civiles] {u.Id}: {n} cells towards {dye}");
                }

            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.farClipPlane = 800;
            float F(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            MapSpace.Aim(camera, MapSpace.TileToWorld(state, new Pos { X = ox + 6, Y = oy + 4 }), F("BASTION_DIST", 24), F("BASTION_GIRO", 45));
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
        }
    
        /// <summary>
        /// The civilians' fear (Arturo's Mixamo clips of 21/11), each candidate on a civilian of its own, a row apart, one of
        /// the dead beside those that cower: BASTION_MIEDO=andar (scaredWalk, scaredWalkQuick and scaredBack, walked five
        /// tiles at BASTION_PASO m/s each, «0.9,1.6,0.6») or quieto (terrified, cowerCrouch). BASTION_VISTA=lado|juego,
        /// BASTION_DIST. -shotPlay.
        /// </summary>
        public static void Miedo()
        {
            new GameObject("Shot camera").AddComponent<Camera>().farClipPlane = 800;
            void Started(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Started;
                BuildFear();
            }
            UnityEditor.EditorApplication.playModeStateChanged += Started;
        }

        static void BuildFear()
        {
            var state = MapLab.Generated("wilds", 3, "elimination");
            int ox = state.Width / 2 - 7, oy = state.Height / 2 - 5;
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 15; x++)
                {
                    int i = (oy + y) * state.Width + ox + x;
                    state.Tiles[i] = TileKinds.Floor;
                    if (state.Decor != null && i < state.Decor.Count) state.Decor[i] = 0;
                }
            state.Lights?.RemoveAll(l => l.Tile.X >= ox && l.Tile.X < ox + 15 && l.Tile.Y >= oy && l.Tile.Y < oy + 10);
            state.Fires?.Clear();
            state.Concealed = false;
            bool walk = Env("BASTION_MIEDO", "andar") == "andar";
            var clips = walk ? new[] { "scaredWalk", "scaredBack" } : new[] { "terrified" };
            // BASTION_SOLO=clip: that one alone, the camera on it (and with it as it walks).
            string solo = Environment.GetEnvironmentVariable("BASTION_SOLO");
            if (solo != null)
            {
                walk = solo.StartsWith("scared");
                clips = new[] { solo };
            }
            var paces = Env("BASTION_PASO", "1.6,0.6").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            state.Units.Clear();
            for (int k = 0; k < clips.Length; k++)
            {
                var u = Setup.MakeUnit("c" + k, TemplateIds.Civilian, "Civil " + k, null, walk ? new Pos { X = ox + 4, Y = oy + 2 + k * 2 } : new Pos { X = ox + 4 + k * 4, Y = oy + 4 }, null);
                // (East, or south to the camera; the one backing away walks east facing west.)
                u.Facing = walk ? (clips[k] == "scaredBack" ? Math.PI : 0) : Math.PI / 2;
                state.Units.Add(u);
                if (!walk) state.Units.Add(Setup.MakeUnit("z" + k, TemplateIds.Zombie, "z" + k, null, new Pos { X = ox + 6 + k * 4, Y = oy + 4 }, null));
            }
            Environment.SetEnvironmentVariable("BASTION_POSE", string.Join(";", clips.Select((c, k) => $"c{k}={c}")));
            Bastion.Client.NightLights.StateForLab = state;
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
            MapLab.Map(state, "miedo");
            var units = new GameObject("Unidades").AddComponent<UnitViews>();
            units.Sync(state);
            foreach (var u in state.Units) units.Reveal(u.Id);
            units.MasksNow();
            var mover = units.gameObject.AddComponent<MiedoMover>();
            mover.units = units;
            mover.ids = clips.Select((c, k) => "c" + k).ToArray();
            mover.paces = walk ? paces : new float[0];
            foreach (var c in clips) Debug.Log(FormattableString.Invariant($"[miedo] {c}: {(FigureCatalog.Load()?.Clip(c) is AnimationClip clip ? clip.length.ToString("0.00") + " s" : "none")}"));

            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            float F(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : or;
            var at = MapSpace.TileToWorld(state, new Pos { X = ox + 6, Y = oy + 4 }) + new Vector3(MapSpace.Tile * 0.5f, 0.9f, 0);
            if (Env("BASTION_VISTA", "lado") == "juego") MapSpace.Aim(camera, at, F("BASTION_DIST", 24), 0);
            else
            {
                var from = at + new Vector3(0, 2.2f, -F("BASTION_DIST", 13));
                camera.fieldOfView = 30;
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            }
            if (solo != null && Env("BASTION_VISTA", "lado") != "juego")
            {
                var feet = Figures.FeetOf("c0");
                var off = new Vector3(walk ? 0 : MapSpace.Tile, 1.6f, -F("BASTION_DIST", 7));
                camera.fieldOfView = 30;
                mover.camera = camera;
                mover.offset = off;
                mover.aim = new Vector3(walk ? 0 : MapSpace.Tile, 0.9f, 0);
            }
            else if (solo != null) MapSpace.Aim(camera, MapSpace.TileToWorld(state, new Pos { X = ox + 6, Y = oy + 2 }), F("BASTION_DIST", 24), 0);
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Of(state, false));
        }
    }

    /// <summary>EvacuacionLab.Miedo's: each civilian's row of clips stepped, and those that walk moved east at their pace, five tiles.</summary>
    public sealed class MiedoMover : MonoBehaviour
    {
        public UnitViews units;
        public string[] ids;
        public float[] paces;
        public Camera camera;
        public Vector3 offset, aim;
        float[] gone;

        void LateUpdate()
        {
            if (camera == null || Figures.FeetOf(ids[0]) is not Transform feet) return;
            camera.transform.SetPositionAndRotation(feet.position + offset, Quaternion.LookRotation(aim - offset));
        }

        void Update()
        {
            gone ??= new float[ids.Length];
            for (int k = 0; k < ids.Length; k++)
            {
                units.LabStep(ids[k]);
                if (k >= paces.Length || Time.timeSinceLevelLoad < 1) continue;
                var feet = Figures.FeetOf(ids[k]);
                if (feet == null || gone[k] >= 5 * MapSpace.Tile) continue;
                float step = paces[k] * Time.deltaTime;
                gone[k] += step;
                feet.position += Vector3.right * step;
            }
        }
    }
}
