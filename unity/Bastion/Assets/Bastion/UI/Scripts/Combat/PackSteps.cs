using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// For tests of the backpack without clicking (the combat lab's BASTION_TOQUES, CombatRun's BASTION_PASOS_COMBATE): a
    /// step as a player's press on the HUD, through the same buttons (a submit on them, as Red's UiPress). «pick=s1» picks a
    /// soldier; «slot=0» opens (or closes) a slot's menu; «act=Curar» presses the menu's line that starts so; «floor=0» a
    /// thing on the floor; «fallen» a fallen soldier's «Coger…»; «offer» the notice's «Cambiar»; «x» the menu's ✕. Also the
    /// map's: «ability=grenade» a key of the bar, «hover=47/57» the pointer on a tile, «click=44/55» a click there, «look=39/38» the camera to it (a slash: the steps'
    /// lists part on commas); «why=shoot» says in the log why a key is off; «end» ends the turn, its question answered; «sheet=s1» a soldier's sheet, «file=a49» an
    /// enemy's file, «close» the window over them. Whether
    /// it found what to press (it says so in the log).
    /// </summary>
    public static class PackSteps
    {
        public static bool Do(CombatController control, string step)
        {
            var at = step.IndexOf('=');
            var what = at < 0 ? step : step.Substring(0, at);
            var arg = at < 0 ? "" : step.Substring(at + 1);
            if (what == "pick")
            {
                control.Pick(arg);
                return true;
            }
            if (what == "sheet")
            {
                control.OnSheet(arg);
                return true;
            }
            if (what == "file")
            {
                control.OnEnemyFile(arg);
                return true;
            }
            if (what == "close")
            {
                control.Hud?.CloseWindow();
                return true;
            }
            if (what == "end")
            {
                // Twice: with soldiers that can still act it asks first (ToggleReady).
                control.OnEndTurn();
                control.OnEndTurn();
                return true;
            }
            if (what == "ability")
            {
                control.OnAbility(arg);
                return true;
            }
            if (what == "why")
            {
                var who = control.State.Units.FirstOrDefault(u => u.Id == control.PickedId);
                var aims = who != null ? Bastion.Engine.Abilities.RangedTargets(control.State, who, arg).Select(t => t.Id + (control.Visible(t) ? "" : " (sin ver)")) : Enumerable.Empty<string>();
                Debug.Log($"[lab] {arg}: {control.KeyReason(arg) ?? "no está en la barra"} · {control.PickedId} apunta a {string.Join(", ", aims)}");
                return true;
            }
            if (what == "hover" || what == "click" || what == "look")
            {
                var xy = arg.Split('/', ',');
                if (xy.Length != 2 || !int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y)) return false;
                var tile = new Bastion.Engine.Pos { X = x, Y = y, Z = 0 };
                if (what == "hover") control.Hover(tile);
                else if (what == "look") control.Rig?.Focus(Board.TileToWorld(control.State, tile));
                else control.ClickAt(tile);
                return true;
            }
            var root = control.Hud != null ? control.Hud.GetComponent<UIDocument>()?.rootVisualElement : null;
            if (root == null) return false;
            int.TryParse(arg, out var n);
            Button button = null;
            switch (what)
            {
                case "slot":
                    button = root.Query<Button>(className: "pack-slot-btn").ToList().ElementAtOrDefault(n);
                    break;
                case "act":
                    button = root.Query<Button>(className: "pack-act").ToList().FirstOrDefault(b => b.Q<Label>(className: "pack-act__label")?.text.StartsWith(arg, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "floor":
                    button = root.Query<Button>(className: "pack-floor-item").ToList().ElementAtOrDefault(n);
                    break;
                case "fallen":
                    button = root.Query<Button>(className: "pack-fallen__take").ToList().FirstOrDefault();
                    break;
                case "offer":
                    button = root.Q(className: "note--offer")?.Q<Button>();
                    break;
                case "x":
                    button = root.Q<Button>(className: "pack-menu__x");
                    break;
            }
            if (button == null || !button.enabledInHierarchy || button.resolvedStyle.display == DisplayStyle.None)
            {
                Debug.Log($"[mochila] {step}: nada que pulsar");
                return false;
            }
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
            Debug.Log($"[mochila] {step}: pulsado «{Text(button)}»");
            return true;
        }

        static string Text(Button b) => string.Join(" ", b.Query<Label>().ToList().Select(l => l.text).Prepend(b.text).Where(t => !string.IsNullOrEmpty(t)));
    }
}
