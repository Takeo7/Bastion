using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The HUD's proposals for Arturo, after how Synty shows its INTERFACE packs in their demo scenes: a sheet laid over
    /// the HUD's own (Assets/Bastion/UI/Propuestas/&lt;name&gt;.uss) that moves and dresses its pieces, and the packs' parts
    /// placed round them (the bar's canisters and counter, the wires, the screws…), found by name in Assets/Synty. In the
    /// editor's captures only; the real HUD is untouched until one is chosen.
    /// </summary>
    public sealed class Temas : MonoBehaviour
    {
        /// <summary>The proposal by name (Taller, Mando, Losetas); empty, none: today's HUD.</summary>
        public string Tema = "";

        /// <summary>Each proposal's name and its line, for the sheet.</summary>
        static readonly Dictionary<string, (string name, string line)> Captions = new Dictionary<string, (string, string)>
        {
            ["Taller"] = ("1 · Taller", "Apocalypse HUD como en sus demos: la barra es un aparato con cilindros y contador, y el texto va suelto sobre la escena."),
            ["Mando"] = ("2 · Mando", "Military Combat HUD como en sus demos: placas azul acero con tornillos, la escuadra numerada y avisos con canto rojo."),
            ["Losetas"] = ("3 · Losetas", "Modern Menus: losetas inclinadas y gruesas, colores vivos y teclas con su número en un hexágono."),
        };

        float started;
        bool dressed;
        VisualElement hud;
        VisualElement layer;
        /// <summary>What follows the HUD each frame: the parts' places and the figures they show.</summary>
        readonly List<Action> refresh = new List<Action>();

        void Start() => started = Time.realtimeSinceStartup;

        void Update()
        {
            if (!dressed)
            {
                if (Time.realtimeSinceStartup - started < 0.2f) return;
                dressed = true;
                Dress();
            }
            foreach (var r in refresh) r();
        }

        void Dress()
        {
            var top = GetComponent<UIDocument>().rootVisualElement;
            hud = top.Q("hud");
            // The HUD's root, or the room menu's (its proposal too).
            var root = hud ?? top.Q(className: "start") ?? top;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(Tema))
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>($"Assets/Bastion/UI/Propuestas/{Tema}.uss");
                if (sheet == null) Debug.LogError($"[tema] Assets/Bastion/UI/Propuestas/{Tema}.uss missing");
                else root.styleSheets.Add(sheet);
            }
#endif
            if (hud != null)
            {
                // The parts under the panels, over the map's marks.
                layer = Ui.El("tema-layer");
                layer.pickingMode = PickingMode.Ignore;
                layer.style.position = Position.Absolute;
                layer.style.left = 0;
                layer.style.top = 0;
                layer.style.right = 0;
                layer.style.bottom = 0;
                var marks = hud.Q("edge-marks");
                hud.Insert(marks != null ? hud.IndexOf(marks) + 1 : 0, layer);
                if (Tema == "Taller") Taller();
                if (Tema == "Mando") Mando();
                if (Tema == "Losetas") Losetas();
            }
            // BASTION_ROTULO=1: its name and its line over the bar, for the sheet with all three.
            if (Environment.GetEnvironmentVariable("BASTION_ROTULO") == "1" && Captions.TryGetValue(Tema ?? "", out var caption))
            {
                var plate = Ui.El("panel tema-caption", hud ?? top);
                plate.pickingMode = PickingMode.Ignore;
                plate.style.position = Position.Absolute;
                plate.style.top = 70;
                plate.style.left = Length.Percent(50);
                plate.style.translate = new Translate(Length.Percent(-50), 0);
                plate.style.width = 640;
                plate.style.alignItems = Align.Center;
                // Its own plate: the proposals make the HUD's panels see-through.
                plate.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.92f);
                plate.style.paddingTop = 8;
                plate.style.paddingBottom = 10;
                plate.style.paddingLeft = 16;
                plate.style.paddingRight = 16;
                var title = Ui.Text(caption.name.ToUpperInvariant(), "font-title", plate);
                title.style.fontSize = 26;
                var line = Ui.Text(caption.line, null, plate);
                line.style.fontSize = 14;
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            var hudRoot = GetComponent<HudRoot>();
            if (hudRoot == null || !hudRoot.Ready) return;
            hudRoot.Log("Samuel Adeyemi corre hasta el coche.", "#a678ff");
            hudRoot.Log("Un zombi aparece en la calle.", "#ff6b5a");
            hudRoot.Log("Dmitri Volkov vigila la esquina.", "#a678ff");
        }

        // =========================================================================================== 1 · Taller

        /// <summary>
        /// Apocalypse HUD as its ARPG demo (top-down, as ours): the bar a bolted machine at the bottom, its keys numbered
        /// underneath, a red canister for the picked soldier's health on its left and a blue one for his ammunition on its
        /// right, a counter with his actions on its corner, wires hanging off it; the stats as rows wired from the screen's
        /// edge; the rest loose text over the scene (in the sheet).
        /// </summary>
        void Taller()
        {
            var bar = hud.Q("abilities");
            var rig = Follow(bar);
            // Behind the left end: a rack, its wires.
            Put(rig, "SPR_Apocalypse_Greeble_ElectronicsRack_01", -150, -40, 110, 137);
            Put(rig, "SPR_Apocalypse_Greeble_Wires_Clump_03", -128, 30, 50, 134);
            Put(rig, "SPR_Apocalypse_Greeble_ElectronicsRack_02", null, -28, 92, 115).style.right = -150;
            // Health and ammunition: canisters on both ends.
            var health = Canister(rig, new Color(0.92f, 0.25f, 0.27f), true);
            var ammo = Canister(rig, new Color(0.25f, 0.78f, 0.92f), false);
            // Over the keys: the picked soldier on a metal tube, his health in it.
            var tube = Ui.El("tema-tube", rig);
            At(tube, left: 34, top: -40, right: 34, height: 34);
            var tubeWell = Ui.El("tema-tube__well", tube);
            var tubeFill = Ui.El("tema-tube__fill", tubeWell);
            var tubeText = Ui.Text("", "tema-tube__text font-title", tube);
            Put(tube, "SPR_Apocalypse_Bar_Metal_01", 0, 0, null, null).style.right = 0;
            tube.Q(className: "tema-piece").style.bottom = 0;
            // The counter on the tube's left end: actions left.
            Put(rig, "SPR_Apocalypse_Greeble_Vent_01", -12, -66, 30, 37);
            var counter = Put(rig, "SPR_Apocalypse_Box_RustyMed_01", -2, -64, 52, 52);
            counter.AddToClassList("tema-counter");
            var counterText = Ui.Text("", "tema-counter__text font-title", counter);
            Put(rig, "SPR_Apocalypse_Greeble_Capacitor_01", null, -52, 54, 22).style.right = 60;
            Put(rig, "SPR_Apocalypse_Greeble_ScrewsWires_01", null, null, 106, 39).style.right = -176;
            rig.Children().Last().style.bottom = -4;

            // The objective: a yellow triangle by its name, a box to tick by its line.
            var goal = Follow(hud.Q("goal"));
            Put(goal, "SPR_HUD_Apocalypse_Map_Objective_01_Clean", 0, 6, 30, 30, new Color(0.89f, 0.84f, 0.29f));
            Put(goal, "SPR_HUD_Apocalypse_MissionObjective_01_Inactive", 8, 40, 17, 17, new Color(0.9f, 0.9f, 0.9f));

            // The stats on the bottom left: wired rows.
            var unit = hud.Q("unit-panel");
            var wires = Follow(unit);
            refresh.Add(() =>
            {
                var card = hud.Q(className: "squad-card--selected");
                var (hp, hpAll) = Pips(card?.Q(className: "pips--hp"));
                var (ap, apAll) = Pips(card?.Q(className: "pips--ap"));
                var (rounds, roundsAll) = Pips(unit.Q(className: "stat--ammo")?.Q(className: "pips"));
                health(hpAll > 0 ? (float)hp / hpAll : 0);
                ammo(roundsAll > 0 ? (float)rounds / roundsAll : 0);
                tubeFill.style.width = Length.Percent(hpAll > 0 ? 100f * hp / hpAll : 0);
                var name = unit.Q<Label>("name")?.text ?? "";
                tubeText.text = $"{name.ToUpperInvariant()}   {hp} / {hpAll}";
                counterText.text = ap.ToString();
                // Each key's number under it.
                Number(bar.Query(className: "ability").ToList(), "tema-key");
                // A wire into each row of the stats, from the screen's edge.
                var rows = unit.Query(className: "stat").ToList();
                while (wires.childCount < rows.Count)
                {
                    Put(wires, "SPR_Apocalypse_Greeble_Wires_Single_03", 0, 0, 52, 13, new Color(0.48f, 0.6f, 0.32f));
                }
                for (var i = 0; i < wires.childCount; i++)
                {
                    var w = wires[i];
                    Ui.Show(w, i < rows.Count);
                    if (i >= rows.Count) continue;
                    var r = rows[i].layout;
                    w.style.left = -50;
                    w.style.top = rows[i].parent.layout.y + r.y + r.height / 2 - 7;
                }
            });
        }

        // =========================================================================================== 2 · Mando

        /// <summary>
        /// Military Combat HUD as its TeamShooter and Survival demos: the log moved up to the top left as their feed; the
        /// squad numbered as their fireteam; over the keys a rail with the picked soldier and his health, between two
        /// hexagons (health in red, actions in blue); his weapon in a plate over «Terminar turno», the rounds in big
        /// figures; an orange gem by the objective.
        /// </summary>
        void Mando()
        {
            var log = hud.Q("log");
            if (log != null)
            {
                hud.Add(log);
                log.AddToClassList("tema-feed");
            }
            var bar = hud.Q("abilities");
            var rig = Follow(bar);
            var rail = Ui.El("tema-rail", rig);
            rail.pickingMode = PickingMode.Ignore;
            At(rail, left: 40, right: 40, top: -36, height: 30);
            var well = Ui.El("tema-rail__well", rail);
            var railFill = Ui.El("tema-rail__fill", well);
            var railText = Ui.Text("", "tema-rail__text font-title", rail);
            var hpText = Hexagon(rig, new Color(0.75f, 0.18f, 0.18f), true);
            var apText = Hexagon(rig, new Color(0.14f, 0.6f, 0.8f), false);

            var turn = hud.Q("turn-actions");
            var plate = Follow(turn);
            var weapon = Put(plate, "SPR_MilitaryCombat_Frame_Medium_15", -76, null, null, 78);
            weapon.style.right = 0;
            weapon.style.bottom = Length.Percent(100);
            weapon.style.marginBottom = 8;
            weapon.AddToClassList("tema-weapon");
            var gun = Ui.El("tema-weapon__icon", weapon);
            var gunSprite = Sprite("ICON_SM_Wep_Preset_A_Rifle_01_Military_Clean");
            if (gunSprite != null) gun.style.backgroundImage = new StyleBackground(gunSprite);
            var figures = Ui.El("tema-weapon__figures", weapon);
            var big = Ui.Text("", "tema-weapon__big font-title", figures);
            var small = Ui.Text("", "tema-weapon__small font-title", figures);
            var gunName = Ui.Text("", "tema-weapon__name font-title", weapon);
            Ui.Text("1", "tema-weapon__key font-title", weapon);

            var goal = Follow(hud.Q("goal"));
            Put(goal, "SPR_MilitaryCombat_Socket_Background_01", 6, 8, 30, 30);
            Put(goal, "SPR_MilitaryCombat_Socket_Gem_01", 9, 11, 24, 24, new Color(0.97f, 0.64f, 0.06f));
            Put(goal, "SPR_MilitaryCombat_Socket_Frame_01", 4, 6, 34, 34);

            var unit = hud.Q("unit-panel");
            refresh.Add(() =>
            {
                var card = hud.Q(className: "squad-card--selected");
                var (hp, hpAll) = Pips(card?.Q(className: "pips--hp"));
                var (ap, _) = Pips(card?.Q(className: "pips--ap"));
                var ammo = unit.Q(className: "stat--ammo");
                var (rounds, roundsAll) = Pips(ammo?.Q(className: "pips"));
                var spare = ammo?.Q<Label>(className: "stat__value")?.text ?? "";
                railFill.style.width = Length.Percent(hpAll > 0 ? 100f * hp / hpAll : 0);
                railText.text = $"{(unit.Q<Label>("name")?.text ?? "").ToUpperInvariant()}   {hp} / {hpAll}";
                hpText.text = hp.ToString();
                apText.text = ap.ToString();
                big.text = rounds.ToString("00");
                small.text = "/" + roundsAll.ToString("00") + (spare.Length > 0 ? "  " + spare : "");
                gunName.text = (unit.Q<Label>("weapon-name")?.text ?? "").ToUpperInvariant();
                // The fireteam's numbers, the bar's keys' numbers.
                Number(hud.Q("squad").Query(className: "squad-card__accent").ToList(), "tema-number");
                Number(bar.Query(className: "ability").ToList(), "tema-key");
            });
        }

        // =========================================================================================== 3 · Losetas

        /// <summary>
        /// Modern Menus as its main menus: each key and each soldier with its number in a hexagon, the soldier's in his
        /// player's colour (SquadList paints the accent's background; here it tints the hexagon instead).
        /// </summary>
        void Losetas()
        {
            var bar = hud.Q("abilities");
            refresh.Add(() =>
            {
                foreach (var accent in hud.Q("squad").Query(className: "squad-card__accent").ToList())
                {
                    var c = accent.style.backgroundColor;
                    if (c.keyword != StyleKeyword.Undefined || c.value.a == 0) continue;
                    accent.style.unityBackgroundImageTintColor = c.value;
                    accent.style.backgroundColor = new Color(0, 0, 0, 0);
                }
                Number(hud.Q("squad").Query(className: "squad-card__accent").ToList(), "tema-number");
                Number(bar.Query(className: "ability").ToList(), "tema-key");
            });
        }

        /// <summary>A hexagon on one end of the bar, its colour and a figure in it (set through the label returned).</summary>
        Label Hexagon(VisualElement rig, Color fill, bool left)
        {
            var h = Ui.El("tema-hex", rig);
            h.pickingMode = PickingMode.Ignore;
            At(h, top: -14, width: 70, height: 70);
            if (left) h.style.left = -64;
            else h.style.right = -64;
            Put(h, "SPR_MilitaryCombat_Badge_Shield_20_Background", 4, 4, 62, 62, fill);
            Put(h, "SPR_MilitaryCombat_Badge_Shield_20_Silver", 0, 0, 70, 70);
            return Ui.Text("", "tema-hex__text font-title", h);
        }

        /// <summary>A number from 1 on each of `items`, in a label of class `cls` (made once).</summary>
        static void Number(List<VisualElement> items, string cls)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var n = items[i].Q<Label>(className: cls);
                if (n == null)
                {
                    n = Ui.Text("", cls + " font-title", items[i]);
                    n.pickingMode = PickingMode.Ignore;
                }
                n.text = (i + 1).ToString();
            }
        }

        /// <summary>
        /// One of the bar's canisters, on its left end or its right: the lid, the glass and its liquid, the base. Returns
        /// how full it is (0–1) to set.
        /// </summary>
        Action<float> Canister(VisualElement rig, Color liquid, bool left)
        {
            var c = Ui.El("tema-canister", rig);
            c.pickingMode = PickingMode.Ignore;
            At(c, top: -34, width: 70, height: 116);
            if (left) c.style.left = -84;
            else c.style.right = -84;
            Put(c, "SPR_Apocalypse_Container_01_Base_Back", 0, 92, 70, 7);
            var glass = Put(c, "SPR_Apocalypse_Container_01_Body", 3, 24, 64, 74, new Color(1, 1, 1, 0.18f));
            var fill = Put(c, "SPR_Apocalypse_Container_01_Body", 3, null, 64, 0, liquid);
            fill.style.bottom = 18;
            Put(c, "SPR_Apocalypse_Container_01_Lid", 0, 0, 70, 30);
            Put(c, "SPR_Apocalypse_Container_01_Base", 0, 96, 70, 19);
            return f =>
            {
                f = Mathf.Clamp01(f);
                fill.style.height = 74 * f;
                fill.style.unityBackgroundImageTintColor = new Color(liquid.r, liquid.g, liquid.b, f > 0 ? 0.9f : 0);
            };
        }

        // =========================================================================================== parts

        /// <summary>A layer over `anchor`'s place, kept on it each frame: its parts are placed from its corners.</summary>
        VisualElement Follow(VisualElement anchor)
        {
            var rig = Ui.El("tema-rig", layer);
            rig.pickingMode = PickingMode.Ignore;
            rig.style.position = Position.Absolute;
            refresh.Add(() =>
            {
                if (anchor == null || anchor.panel == null) return;
                var b = anchor.worldBound;
                var at = layer.WorldToLocal(b.position);
                rig.style.left = at.x;
                rig.style.top = at.y;
                rig.style.width = b.width;
                rig.style.height = b.height;
                Ui.Show(rig, anchor.resolvedStyle.display != DisplayStyle.None && b.width > 0);
            });
            return rig;
        }

        /// <summary>A Synty sprite placed by hand under `parent` (left and top from its corner; null leaves them free).</summary>
        static VisualElement Put(VisualElement parent, string sprite, float? left, float? top, float? width, float? height, Color? tint = null)
        {
            var e = Ui.El("tema-piece", parent);
            e.pickingMode = PickingMode.Ignore;
            e.style.position = Position.Absolute;
            At(e, left: left, top: top, width: width, height: height);
            var s = Sprite(sprite);
            if (s != null) e.style.backgroundImage = new StyleBackground(s);
            if (tint != null) e.style.unityBackgroundImageTintColor = tint.Value;
            return e;
        }

        static void At(VisualElement e, float? left = null, float? top = null, float? right = null, float? bottom = null, float? width = null, float? height = null)
        {
            e.style.position = Position.Absolute;
            if (left != null) e.style.left = left.Value;
            if (top != null) e.style.top = top.Value;
            if (right != null) e.style.right = right.Value;
            if (bottom != null) e.style.bottom = bottom.Value;
            if (width != null) e.style.width = width.Value;
            if (height != null) e.style.height = height.Value;
        }

        /// <summary>How many of a row's pips are on, and how many it has.</summary>
        static (int on, int all) Pips(VisualElement row) => row == null ? (0, 0) : (row.Query(className: "pip--on").ToList().Count, row.childCount);

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>A sprite of the packs by its file's name (in the editor; the lab only).</summary>
        static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var found)) return found;
            Sprite s = null;
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/Synty" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                break;
            }
#endif
            if (s == null) Debug.LogError($"[tema] sprite {name} missing");
            return sprites[name] = s;
        }
    }
}
