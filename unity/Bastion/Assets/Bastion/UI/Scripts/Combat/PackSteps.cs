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
    /// lists part on commas); «why=shoot» says in the log why a key is off; «join» and «drop» the partner (Ana) connected or dropped, «gone» dropped past the grace, «waiting=4» not in yet and waited for 4 s more; «retrato=s2» a click on a soldier's card, «reloj=0.7» the turn's clocks; «end» ends the turn, its question answered, «fin» presses it once; «sheet=s1» a soldier's sheet, «file=a49» an
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
            // «fin»: one press, as a click on the key (with soldiers that can still act, it asks first: the key armed).
            if (what == "fin")
            {
                control.OnEndTurn();
                return true;
            }
            if (what == "end")
            {
                // Twice: with soldiers that can still act it asks first (ToggleReady).
                control.OnEndTurn();
                control.OnEndTurn();
                return true;
            }
            // «banner» the stamp let go, «confirm» the aimed shot fired: the app's (CombatRun), for the lab's touches too.
            if (what == "banner")
            {
                control.Hud?.ReleaseBanner();
                return true;
            }
            // «pack»: the pockets opened or shut, as a click on the backpack's mark (Ana, 7/11 #13: only then).
            if (what == "pack")
            {
                control.Hud?.TogglePockets();
                return true;
            }
            if (what == "confirm")
            {
                control.OnConfirmTarget();
                return true;
            }
            if (what == "ability")
            {
                control.OnAbility(arg);
                return true;
            }
            if (what == "join" || what == "drop" || what == "gone")
            {
                // The partner (slot 1, Ana) connected or dropped, as the room would say (SetPlayers: the grace, the notice).
                var players = control.Players.Where(p => p.Slot != 1).Select(p => new Bastion.Net.PlayerInfo { Slot = p.Slot, Name = p.Name, Color = p.Color, Connected = p.Connected }).ToList();
                players.Add(new Bastion.Net.PlayerInfo { Slot = 1, Name = "Ana", Color = Bastion.Engine.Content.PlayerColors[1], Connected = what == "join" });
                control.SetPlayers(players);
                // «gone»: dropped, and past the server's grace already (its 30 s aren't seen in a capture).
                if (what == "gone") control.EndGraces();
                return true;
            }
            if (what == "waiting")
            {
                // «waiting=4»: the partner not in yet, the room still waiting 4 s for them (a mission opened again without
                // them: PlayerInfo.GraceMs, Red 3/11).
                float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wait);
                var players = control.Players.Where(p => p.Slot != 1).Select(p => new Bastion.Net.PlayerInfo { Slot = p.Slot, Name = p.Name, Color = p.Color, Connected = p.Connected }).ToList();
                players.Add(new Bastion.Net.PlayerInfo { Slot = 1, Name = "Ana", Color = Bastion.Engine.Content.PlayerColors[1], Connected = false, GraceMs = (long)(wait * 1000) });
                control.SetPlayers(players);
                return true;
            }
            if (what == "why")
            {
                var who = control.State.Units.FirstOrDefault(u => u.Id == control.PickedId);
                var aims = who != null ? Bastion.Engine.Abilities.RangedTargets(control.State, who, arg).Select(t => t.Id + (control.Visible(t) ? "" : " (sin ver)")) : Enumerable.Empty<string>();
                Debug.Log($"[lab] {arg}: {control.KeyReason(arg) ?? "no está en la barra"} · {control.PickedId} apunta a {string.Join(", ", aims)}");
                return true;
            }
            // «list=s3» the pointer over that soldier's card in the list, «list=» off it (#11); «mapa» the camera up over the
            // map or back, «mapa=40/12» a click on that tile from up there (#12).
            if (what == "list")
            {
                control.OnHoverSquad(arg == "" ? null : arg);
                return true;
            }
            // «retrato=s2» a click on that soldier's card under the sheet, as the pointer's (two close together: the camera to
            // it, UI-08); «reloj=0.7» the turn's clocks shown with that much left, as a game's (the lab has none).
            // «estado» the picked one's actions and movement left, and what is armed, in the log (#14 of 5/11).
            if (what == "estado")
            {
                Debug.Log($"[lab] estado: {control.LabState()}");
                return true;
            }
            // «rotulo=a3» a click on another target's odds over its flag, aiming (#11 of 5/11).
            if (what == "rotulo")
            {
                var line = control.Hud?.GetComponent<UIDocument>()?.rootVisualElement.Q("pick-" + arg);
                if (line == null) return false;
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = line;
                    line.SendEvent(click);
                }
                return true;
            }
            if (what == "retrato")
            {
                var card = control.Hud?.GetComponent<UIDocument>()?.rootVisualElement.Q("card-" + arg);
                if (card == null) return false;
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = card;
                    card.SendEvent(click);
                }
                return true;
            }
            if (what == "reloj")
            {
                if (control.Hud == null || !float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var left)) return false;
                control.Hud.ClockOf = which => new Bastion.UI.Hud.ClockView { Shown = true, Left = which == 0 ? left : Mathf.Min(1, left + 0.15f), Running = true, Color = Bastion.Engine.Content.PlayerColors[which == 0 ? control.MySlot : 1 - control.MySlot] };
                return true;
            }
            if (what == "mapa")
            {
                var xy = arg.Split('/');
                if (xy.Length == 2 && int.TryParse(xy[0], out var mx) && int.TryParse(xy[1], out var my)) control.OverheadClick(new Bastion.Engine.Pos { X = mx, Y = my, Z = 0 });
                else control.OnMiniMap();
                return true;
            }
            // «marca-mapa=40/12»: from above, a mark on that tile, as Shift and a click there (Arturo, 02/11 A3).
            if (what == "marca-mapa")
            {
                var xy = arg.Split('/');
                if (xy.Length != 2 || !int.TryParse(xy[0], out var mx) || !int.TryParse(xy[1], out var my)) return false;
                control.MarkFromAbove(new Bastion.Engine.Pos { X = mx, Y = my, Z = 0 });
                return true;
            }
            // «hover=8/6/1»: on that storey (a molotov aimed up a floor: Arturo, 01/11 #10).
            if (what == "hover" || what == "click" || what == "look")
            {
                var xy = arg.Split('/', ',');
                if (xy.Length < 2 || xy.Length > 3 || !int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y)) return false;
                var z = xy.Length == 3 && int.TryParse(xy[2], out var storey) ? storey : 0;
                var tile = new Bastion.Engine.Pos { X = x, Y = y, Z = z };
                if (what == "hover") control.Hover(tile);
                else if (what == "look") control.Rig?.Focus(Board.TileToWorld(control.State, tile));
                else control.ClickAt(tile);
                return true;
            }
            var root = control.Hud != null ? control.Hud.GetComponent<UIDocument>()?.rootVisualElement : null;
            if (root == null) return false;
            // «ficha=hide»: that key's card pinned open, as the pointer over it shows it (Esconderse's: Ana's #1 of 1/11).
            if (what == "ficha")
            {
                var place = control.KeyIndex(arg);
                var keys = root.Q("abilities")?.Query<Button>(className: "ability").ToList();
                if (keys == null || place < 0 || place >= keys.Count) return false;
                keys[place].AddToClassList("lab-pin");
                return control.Hud.PinTip("lab-pin");
            }
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
