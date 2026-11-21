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
    }
}
