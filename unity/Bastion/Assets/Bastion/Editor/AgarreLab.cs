using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bastion.Client;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// T22 (Arturo, 18/11: «no coge bien el arma… revisar cada postura y animación»): the base's figures with every weapon
    /// found, as the base holds them, in play mode on the runs' clearing (ShowFixed). BASTION_VISTA=barracks (the default:
    /// Soldados' and Personalizar's figure, its weapon changed every BASTION_CADA seconds, 1.4, from BASTION_DESDE, 2, each
    /// change in the log, «[agarre] k weapon from frame N»), or squad (the row of Escuadra, a soldier a weapon, eight at
    /// most). BASTION_ARMAS=found weapon ids by commas (FoundWeaponIds.All unless said); BASTION_GIRO=degrees: the figure
    /// turned (90 its right side to the camera); BASTION_PERSONALIZAR=1, Personalizar's shot with its panels.
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AgarreLab.Base -shotPlay -shotWarm 0 -shotFrames 140 -shotFps 5
    /// </summary>
    public static class AgarreLab
    {
        public static void Base()
        {
            Settlement.SwayOff = true;
            void Go(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= Go;
                Make();
            }
            UnityEditor.EditorApplication.playModeStateChanged += Go;
        }

        static void Make()
        {
            var weapons = (Environment.GetEnvironmentVariable("BASTION_ARMAS") ?? string.Join(",", FoundWeaponIds.All)).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool squad = Environment.GetEnvironmentVariable("BASTION_VISTA") == Settlement.Views.Squad;
            float every = Number("BASTION_CADA", 1.4f), from = Number("BASTION_DESDE", 2f);
            var campaign = new CampaignState { FoundWeapons = new List<FoundWeapon>() };
            int n = squad ? Math.Min(8, weapons.Count) : 1;
            for (int i = 0; i < n; i++)
            {
                string colour = i % 2 == 0 ? "#e8a33a" : "#4fb0e8";
                campaign.Soldiers.Add(new CampaignSoldier { Id = $"lab{i}", Name = $"Lab {i}", Owner = i % 2, Alive = true, Appearance = Appearances.DefaultAppearance(i % 2, colour, i % 2 == 1 ? "m" : "f") });
                if (squad && weapons[i] != FoundWeaponIds.Pistol) campaign.FoundWeapons.Add(new FoundWeapon { Id = $"w{i}", Kind = weapons[i], Holder = $"lab{i}" });
                if (squad && weapons[i] == FoundWeaponIds.Pistol) campaign.FoundWeapons.Add(new FoundWeapon { Id = $"w{i}", Kind = FoundWeaponIds.Pistol, Holder = $"lab{i}" });
            }
            Bastion.Client.Units.Dress.Roster(campaign.Soldiers);
            Bastion.Client.Units.SquadTint.ColourOf = slot => slot == 0 ? "#e8a33a" : "#4fb0e8";
            var clearing = new GameObject("Asentamiento").AddComponent<Settlement>();
            clearing.Camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Shot camera") ?? new GameObject("Shot camera").AddComponent<Camera>();
            clearing.FitShots = false;
            clearing.ShowFixed();
            Mood.Show(Mood.Day);
            if (squad)
            {
                clearing.SetLineup(campaign, campaign.Soldiers.Select(s => new Settlement.LineupEntry { SoldierId = s.Id, Colour = s.Owner == 0 ? "#e8a33a" : "#4fb0e8", Status = "ok" }).ToList(), false, true);
                clearing.SetView(Settlement.Views.Squad, true);
                Debug.Log($"[agarre] the row: {string.Join(", ", weapons.Take(n))}");
                return;
            }
            // BASTION_PERSONALIZAR=1: Personalizar open (Settlement.Customizing, T1), with its panels where UI's 1280×800
            // capture of 18/11 has them (the top bar, the left sheet, the right one, the name, the buttons, the tabs).
            if (Environment.GetEnvironmentVariable("BASTION_PERSONALIZAR") == "1")
            {
                Rect R(float x0, float y0, float x1, float y1) => new Rect(x0 / 1280, y0 / 800, (x1 - x0) / 1280, (y1 - y0) / 800);
                clearing.FitShots = true;
                // (The shots fitted to the capture's size: when play mode starts, the camera has the batch screen's.)
                clearing.ScreenSize = new Vector2(1280, 800);
                clearing.SetInsets(new List<Rect> { R(0, 0, 1280, 45), R(10, 55, 307, 343), R(972, 58, 1264, 755), R(538, 95, 742, 160), R(368, 670, 912, 712), R(415, 765, 865, 800) });
                clearing.Customizing = true;
            }
            clearing.SetView(Settlement.Views.Barracks, true);
            var look = campaign.Soldiers[0].Appearance;
            bool turned = float.TryParse(Environment.GetEnvironmentVariable("BASTION_GIRO"), NumberStyles.Float, CultureInfo.InvariantCulture, out float turn);
            int shown = -1;
            void Step()
            {
                if (!UnityEditor.EditorApplication.isPlaying || clearing == null)
                {
                    UnityEditor.EditorApplication.update -= Step;
                    return;
                }
                int k = Mathf.Clamp(Mathf.FloorToInt((Time.time - from) / every), 0, weapons.Count - 1);
                if (k == shown) return;
                shown = k;
                string w = weapons[k];
                // (The pistol alone: none in the hands, the pistol by the thigh.)
                clearing.Showcase("lab0", look, w == FoundWeaponIds.Pistol ? null : w, false, "Lab 0", "#e8a33a", true);
                // (Once: TurnShowcase adds to the turn, as a drag does, and it is kept while it is the same soldier.)
                if (turned && k == 0) clearing.TurnShowcase(turn);
                Debug.Log(FormattableString.Invariant($"[agarre] {k} {w} from frame {Time.frameCount}, {Time.time:0.00} s; the last tucked {Bastion.Client.Units.UnitViews.LastTuckDown:0.0}° under the level"));
            }
            UnityEditor.EditorApplication.update += Step;
        }

        /// <summary>
        /// T22's sawn-off (Producto, 18/11: «sawnOff se ve tan larga como pumpShotgun»; it is the same model, Apocalypse has no
        /// sawn-off): the shotguns of the packs we have, side by side and level at the figures' scale, from their right, one
        /// above the other in BASTION_MODELOS' order (paths from Assets/; Apocalypse's pump and the others unless said), the
        /// first at the top, on a plain grey. Edit mode.
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AgarreLab.Escopetas -shotSize 1600x1000 -shotOut escopetas.png
        /// </summary>
        public static void Escopetas()
        {
            const string P = "Assets/Synty/";
            var paths = (Environment.GetEnvironmentVariable("BASTION_MODELOS") ?? string.Join(",",
                P + "PolygonApocalypse/Prefabs/Weapons/Guns/SM_Wep_Shotgun_01.prefab", P + "PolygonGangWarfare/Models/SM_Wep_Shotgun_01.fbx",
                P + "PolygonPoliceStation/Models/SM_Wep_Shotgun_01.fbx", P + "PolygonFarm/Models/SM_Wep_Shotgun_01.fbx",
                P + "PolygonShops/Models/SM_Wep_Shotgun_UnderOver_01.fbx")).Split(',', StringSplitOptions.RemoveEmptyEntries);
            float y = 0;
            foreach (string path in paths)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.Log($"[escopetas] {path}: not found");
                    continue;
                }
                var gun = (GameObject)UnityEngine.Object.Instantiate(prefab);
                gun.transform.localScale = Vector3.one * Bastion.Client.Units.Figures.CharacterScale;
                // (Its barrel along +z: seen from its right, the muzzle to the right of the picture.)
                gun.transform.SetPositionAndRotation(new Vector3(0, y, 0), Quaternion.identity);
                foreach (var t in gun.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Silencer")).ToList()) t.gameObject.SetActive(false);
                Debug.Log($"[escopetas] {path} at {y:0.00}");
                y -= 0.32f;
            }
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(0.9f, -y / 2 + 0.2f);
            camera.transform.SetPositionAndRotation(new Vector3(5, y / 2 + 0.16f, 0.45f), Quaternion.LookRotation(Vector3.left));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.64f, 0.66f);
            var sun = new GameObject("Sol").AddComponent<UnityEngine.Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-1, -0.6f, -0.3f));
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
        }

        /// <summary>
        /// T22's grips (Producto, 18/11: the right hand on the scope, the left a fist under the handguard): each long gun of the
        /// base, by its art (BASTION_ARTES: Anims.json's weapons, the base's unless said), its mesh's profile from its right
        /// in its own frame, unscaled: for each 1.5 cm along z, the least and most y of its vertices and what parts they are
        /// of ([perfil]), and its parts' boxes, to put the hands (WeaponArt.palm on the stock's wrist behind the trigger
        /// guard, fore on the handguard). Edit mode.
        ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.AgarreLab.Perfil -quit
        /// </summary>
        public static void Perfil()
        {
            var arts = (Environment.GetEnvironmentVariable("BASTION_ARTES") ?? "rifle,rifle2,rifle3,shotgun,shotgunPump,sniper,sniper2,machineGun,machineGun2,boltRifle").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var catalog = Bastion.Client.Units.FigureCatalog.Load();
            foreach (var id in arts)
            {
                var art = Bastion.Client.Units.Anims.Weapon(id);
                var prefab = art != null ? catalog.Weapon(art.fbx) : null;
                if (prefab == null)
                {
                    Debug.Log($"[perfil] {id}: no model");
                    continue;
                }
                var gun = (GameObject)UnityEngine.Object.Instantiate(prefab);
                gun.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                gun.transform.localScale = Vector3.one;
                var slices = new SortedDictionary<int, (float lo, float hi, HashSet<string> parts)>();
                foreach (var f in gun.GetComponentsInChildren<MeshFilter>())
                {
                    if (f.sharedMesh == null || !f.gameObject.activeInHierarchy || f.name.Contains("Silencer")) continue;
                    var m = f.transform.localToWorldMatrix;
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        var p = m.MultiplyPoint3x4(v);
                        int k = Mathf.FloorToInt(p.z / 0.015f);
                        var (lo, hi, parts) = slices.TryGetValue(k, out var was) ? was : (float.PositiveInfinity, float.NegativeInfinity, new HashSet<string>());
                        parts.Add(f.name.Replace("SM_Wep_", ""));
                        slices[k] = (Mathf.Min(lo, p.y), Mathf.Max(hi, p.y), parts);
                    }
                }
                Debug.Log($"[perfil] {id} ({art.fbx}): palm {Show(art.palm)}, fore {Show(art.fore)}");
                foreach (var (k, (lo, hi, parts)) in slices)
                    Debug.Log(FormattableString.Invariant($"[perfil] {id} z {k * 0.015f:0.000}: y {lo:0.000} .. {hi:0.000} ({string.Join(" ", parts)})"));
                UnityEngine.Object.DestroyImmediate(gun);
            }
            string Show(float[] v) => v == null ? "-" : string.Join(",", v.Select(x => x.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        static float Number(string name, float or) => float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : or;
    }
}
