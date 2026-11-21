using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Combat
{
    public sealed partial class CombatController
    {
        /// <summary>
        /// Time left on the partner's call, as the clock shows it («0:45»), by its kind (evac, abandon): the server's
        /// deadline, which the game keeps; null, none.
        /// </summary>
        public Func<string, string> DeadlineLeft = kind => null;

        /// <summary>
        /// The bar (game.ts abilities): the picked soldier's keys with their menus folded in, then the evacuation's and a
        /// call to abandon's. Menus are a key with Menu empty and its choices after it, each with Menu set to its id.
        /// </summary>
        List<AbilityView> BarKeys()
        {
            var u = Unit();
            // One inside La Lata (T24): Bajar, and the calls (Evacuar from inside).
            var inside = Inside();
            var calls = AbandonActions().Concat(EvacActions(u ?? inside)).ToList();
            if (inside != null)
            {
                var down = new List<AbilityView> { AlightKey(inside) };
                // Nothing else of this player's left (T24, two players): «Ceder el mando» too, as the game's menu says it;
                // first and lit, what will be pressed most (Producto, 18/11), with its key.
                if (OnlyInside())
                {
                    var give = CallKey("passCommand", HudText.Command.Pass, HudText.Command.PassTip);
                    give.Hotkey = HudText.Command.PassKey;
                    give.Active = true;
                    down.Insert(0, give);
                }
                return down.Concat(calls).ToList();
            }
            if (u == null || !Commands(u)) return calls;
            var keys = WithMenus(SoldierAbilities(u), u);
            // (A soldier's turn ends by «Terminar turno» while it is in play: one key for both, Arturo 7/11, Ana's #5.)
            return keys.Concat(calls).ToList();
        }

        /// <summary>Who the command passes to when a soldier of mine is done (the rules' Alternates): the partner here, not done, with something left; null, nobody.</summary>
        string CommandGoesTo()
        {
            var partner = PartnerHere();
            return partner != null && !State.Ready[partner.Slot] && AbilityTexts.BusySlots(State).Contains(partner.Slot) ? partner.Name : null;
        }

        /// <summary>
        /// The question when another of one's own is picked with one in play (Lore, TEXTOS-0124.md): what it still has,
        /// what ending its turn costs, «Terminar su turno» and «Seguir con …». Only while it is still in play and mine.
        /// </summary>
        CallView LeaveCall()
        {
            if (leaveAsked == null) return null;
            var busy = UnitById(State, leaveAsked);
            if (busy == null || !busy.Alive || State.ActiveUnit != busy.Id || !Paths.HasTurnLeft(busy) || !HasCommand() || !CanAct())
            {
                leaveAsked = null;
                return null;
            }
            var to = CommandGoesTo();
            return new CallView
            {
                Kind = "leave",
                Kicker = "",
                Title = busy.Name,
                Text = HudText.Done.State(busy.Name, busy.Ap, Paths.MoveBudget(State, busy)) + " " + HudText.Done.Stakes(to),
                Accept = HudText.Done.Yes,
                Refuse = HudText.Done.No(busy.Name),
            };
        }

        /// <summary>A view of the bar by its id, inside a menu too.</summary>
        AbilityView BarView(string id) => BarKeys().FirstOrDefault(v => v.Id == id);

        /// <summary>Where `id`'s key stands along the bar (a menu's choice: its menu's key); -1, not on it (the labs: PackSteps «ficha=hide»).</summary>
        public int KeyIndex(string id)
        {
            var keys = BarKeys();
            var view = keys.FirstOrDefault(v => v.Id == id);
            if (view == null) return -1;
            return keys.Where(v => string.IsNullOrEmpty(v.Menu)).ToList().FindIndex(v => v.Id == (string.IsNullOrEmpty(view.Menu) ? id : view.Menu));
        }

        /// <summary>Why a key of the bar is off, «sí» if it is on, none if it is not there (tests: PackSteps «why=shoot»).</summary>
        public string KeyReason(string id) => BarView(id) is AbilityView v ? (v.Enabled ? "sí" : v.Reason) : null;

        List<string> AbilityList(Unit u)
        {
            var m = State.Mission;
            // A door's key only while there is one to open or close beside the soldier (they come last, so no hotkey moves).
            bool Door(string id) => (id != AbilityIds.OpenDoor && id != AbilityIds.CloseDoor) || Abilities.DoorTiles(State, u, id == AbilityIds.OpenDoor ? "door" : "doorOpen").Count > 0;
            // Hablar only while a run's survivor waits and has been seen (N2, 17/11): the engine lists it for everyone.
            var waiting = Abilities.Waiting(State);
            // El herido (U25): the wounded is carried, never talked to (the engine refuses it): no Hablar in that stage.
            bool Talk(string id) => id != AbilityIds.Talk || (waiting != null && !Unfound(waiting) && m.Wounded != true);
            // Leaving is the bar's own action (EvacActions), for any of ours in the bus: never the soldier's.
            var list = Abilities.AbilitiesOf(u)
                .Where(id => id != AbilityIds.Evac && (id != AbilityIds.Hack || (m.Terminal != null && !m.ObjectiveDone)) && (id != AbilityIds.Search || HasSpots) && Door(id) && Talk(id))
                .ToList();
            // El herido's (U25, G23.3; the engine's, this fight only): Cargar, or Dejar and Pasar while carrying; before the
            // doors, which come and go, so their numbers don't move.
            var carry = Abilities.CarryAbilities(State, u);
            if (carry.Count > 0)
            {
                var at = list.FindIndex(id => id == AbilityIds.OpenDoor || id == AbilityIds.CloseDoor);
                list.InsertRange(at < 0 ? list.Count : at, carry);
            }
            return list;
        }

        /// <summary>Whether this map has furniture to search (Registrar): the game's, from the map (game.ts hasSpots).</summary>
        public bool HasSpots = true;

        /// <summary>
        /// A choice a menu always has though the soldier lacks it: Lanzar's grenade (Arturo, 28/10; Ana: «no te da a elegir
        /// entre granada y piedra»), off, none left. The engine lists it only while a grenade is carried; 0.10.1 did too.
        /// </summary>
        AbilityView Missing(Unit u, string menu, string id)
        {
            if (menu != "menu:throw" || id != AbilityIds.Grenade || u.Team != Teams.Xcom) return null;
            var def = Content.Abilities[id];
            return new AbilityView
            {
                Id = id,
                Name = def.Name,
                Enabled = false,
                Reason = AbilityTexts.NoneLeft(ChargeIds.Grenade),
                Cost = AbilityTexts.CostText(u, id),
                Description = def.Description,
                Count = 0,
                Free = def.Ap == 0,
                Depleted = true,
            };
        }

        /// <summary>
        /// Disparar's picture: the gun in hand's (Content.AttackWeapon); the bolt-action rifle its own, though it fires as the
        /// assault rifle (Arturo, 18/11 T14: «aparece como un rifle de asalto en el botón»).
        /// </summary>
        static string ShotPicture(Unit u) => u.Arms?.Primary == FoundWeaponIds.HuntingRifle ? FoundWeaponIds.HuntingRifle : Content.AttackWeapon(u, AbilityIds.Shoot).Id;

        /// <summary>A weapon found in the field that has a picture of its own on the Tajo's key (the knife and the machete keep the blade).</summary>
        static bool Found(string weapon) => weapon == WeaponIds.Pan || weapon == WeaponIds.Bat || weapon == WeaponIds.Crowbar || weapon == WeaponIds.Chainsaw;

        // Where the bar's time goes (the stalls: Red and Arte, 7/11; RendimientoLab.Parones).
        static readonly Unity.Profiling.ProfilerMarker MBlocker = new Unity.Profiling.ProfilerMarker("Bastion.UI.Bar.Blocker");
        static readonly Unity.Profiling.ProfilerMarker MTargets = new Unity.Profiling.ProfilerMarker("Bastion.UI.Bar.Targets");
        static readonly Unity.Profiling.ProfilerMarker MTexts = new Unity.Profiling.ProfilerMarker("Bastion.UI.Bar.Texts");

        List<AbilityView> SoldierAbilities(Unit u)
        {
            var list = AbilityList(u);
            var views = new List<AbilityView>();
            for (var i = 0; i < list.Count; i++)
            {
                var id = list[i];
                var def = Content.Abilities[id];
                // Golpe silencioso only with one to take down (Gameplay, 16/11): no key otherwise.
                if (id == AbilityIds.Takedown && !HasTargets(id)) continue;
                // The bow has nothing to reload (U29: no clip, each shot takes an arrow from the pack): no key, not one always off.
                if (id == AbilityIds.Reload && Content.FiresArrows(u) && Content.ClipOf(u, AbilityIds.Shoot) == 0) continue;
                // The chainsaw's blows are its fuel's (Content.RunsChainsaw): shown as the grenades' are.
                int? charge = def.Charge != null ? AbilityTexts.Charge(u, def.Charge) : Content.RunsChainsaw(u, id) ? AbilityTexts.Charge(u, ChargeIds.Fuel) : (int?)null;
                string reason;
                using (MBlocker.Auto()) reason = !CanAct() ? "No es vuestro turno." : WaitingFor(u.Id) ?? Abilities.AbilityBlocker(State, u, id);
                bool none;
                using (MTargets.Auto()) none = reason == null && (def.Target == AbilityTargets.Enemy || def.Target == AbilityTargets.Melee || def.Target == AbilityTargets.Ally) && !HasTargets(id);
                if (none) reason = def.Target == AbilityTargets.Ally ? "No hay aliados válidos." : def.Target == AbilityTargets.Melee ? "Ningún enemigo a su alcance." : "Sin enemigos a la vista.";
                using var textsMark = MTexts.Auto();
                u.Cooldowns.TryGetValue(id, out var cooldown);
                var attack = Content.IsAttack(id);
                views.Add(new AbilityView
                {
                    Id = id,
                    // Disparar by the gun in hand (Arturo, 23/10 #7): its picture on the key, not always the rifle's; the Tajo by a
                    // found blunt weapon or the chainsaw (Ana's notes, Arturo 29/10).
                    Weapon = id == AbilityIds.Shoot ? ShotPicture(u) : id == AbilityIds.Slash && Found(Content.AttackWeapon(u, id).Id) ? Content.AttackWeapon(u, id).Id : null,
                    Enabled = reason == null,
                    Reason = reason,
                    // Out of rounds in the magazine (Ana, 20/10 #24): said on the key.
                    Note = reason != null && reason.EndsWith(": recarga.", StringComparison.Ordinal) ? AbilityTexts.ReloadFirst : null,
                    // An attack's card names the weapon it fires: its cost and its noise are that weapon's.
                    // Registrar by what is beside (a body, a boot: Arturo, 02/11 A9, A13), as its title says it (Lore.BoxTitle).
                    Name = attack ? $"{def.Name} · {AbilityTexts.WeaponLabel(u, id)}" : id == AbilityIds.Hack ? $"{def.Name} · {Abilities.HackChance(State, u)}%" : id == AbilityIds.Search && SearchBeside(u) is string spot ? Lore.BoxTitle(spot) : id == AbilityIds.OpenDoor && UnlocksBeside(u) ? Lore.KeyText.OpenWithKey : def.Name,
                    Cost = AbilityTexts.CostText(u, id),
                    Limits = AbilityTexts.LimitsText(u, id),
                    Damage = AbilityTexts.DamageText(State, u, id),
                    Noise = AbilityTexts.NoiseText(State, u, id),
                    Stats = AbilityTexts.AbilityStats(State, u, id),
                    // Recargar with the crossbow loads a bolt, not a magazine (U29; Lore's).
                    Description = id == AbilityIds.Search && SearchBeside(u) is string beside ? AbilityTexts.SearchWords(beside).effect
                        : id == AbilityIds.Reload && Content.FiresArrows(u) ? Lore.ArrowsText.Reload : def.Description,
                    // Registrar by what is beside: a car, a body, a box (Arturo, 18/11 T12: it was the backpack every time).
                    Icon = id == AbilityIds.Search ? SearchIcon(SearchBeside(u)) : null,
                    Active = mode != Mode.Move && modeAbility == id,
                    Count = charge ?? -1,
                    Cooldown = cooldown,
                    Free = def.Ap == 0,
                    Depleted = charge == 0 || (attack && Content.AttackWeapon(u, id).Clip > 0 && u.Ammo <= 0),
                });
            }
            return views;
        }

        /// <summary>
        /// The actions with a menu (docs/FASE10.md §4) folded into one key each, where the first of them was: Lanzar,
        /// Vigilancia and Investigar (the box too when there is one beside). The keys are numbered again along the bar,
        /// Lanzar always right after Vigilancia; a choice keeps its own card, its name as the menu says it.
        /// </summary>
        List<AbilityView> WithMenus(List<AbilityView> views, Unit u)
        {
            var keys = new List<(AbilityView key, List<AbilityView> options)>();
            var done = new HashSet<string>();
            var crate = CrateBeside(u);
            foreach (var v in views)
            {
                var menu = AbilityTexts.Menus.FirstOrDefault(m => m.Members.Contains(v.Id));
                if (menu == null)
                {
                    keys.Add((v, null));
                    continue;
                }
                if (!done.Add(menu.Id)) continue;
                var options = menu.Members.Select(id => views.FirstOrDefault(x => x.Id == id) ?? Missing(u, menu.Id, id)).Where(x => x != null && menu.Shows(x, crate)).ToList();
                if (options.Count == 0) continue;
                // Lanzar's choices with none left say so (Lore, 28/10), not the engine's «No quedan usos.».
                if (menu.Id == "menu:throw")
                    foreach (var o in options)
                        if (o.Count == 0 && Content.Abilities[o.Id].Charge is string charge && AbilityTexts.NoneLeft(charge) is string none)
                        {
                            o.Enabled = false;
                            o.Reason = none;
                        }
                var usable = options.Where(o => o.Enabled).ToList();
                var first = options[0];
                var several = options.Count > 1;
                var key = new AbilityView
                {
                    Id = menu.Id,
                    Name = menu.Name,
                    // Lanzar is the stone's, and the grenade's for one without it (since 29/10 a skill of the board's).
                    // Investigar with something beside to search, by what it is (T12).
                    Icon = menu.Id == "menu:throw" && options.All(o => o.Id != AbilityIds.Stone) ? AbilityIds.Grenade
                        : menu.Id == "menu:investigate" && options.FirstOrDefault(o => o.Id == AbilityIds.Search)?.Icon is string looks ? looks : menu.Icon,
                    Enabled = usable.Count > 0,
                    Reason = usable.Count > 0 ? null : first.Reason,
                    Note = usable.Count > 0 ? null : options.FirstOrDefault(o => o.Note != null)?.Note,
                    // Its choices, not its name again (the card's title says it; Lore, 18/11): «Registrar la habitación o abrir el
                    // maletero del coche de policía. Pulsa para elegir.»
                    Description = several ? $"{ChoiceList(options.Select(o => o.Name.ToLowerInvariant()).ToList())}. Pulsa para elegir." : first.Description,
                    // With a choice to make, each choice's card has its own figures (#18): none on the menu's.
                    Cost = several ? "" : first.Cost,
                    Limits = several ? null : first.Limits,
                    Damage = several ? null : first.Damage,
                    Noise = several ? null : first.Noise,
                    Stats = several ? new List<StatPair>() : first.Stats,
                    Active = options.Any(o => o.Active),
                    Free = first.Free,
                    Depleted = options.All(o => o.Depleted),
                };
                foreach (var o in options)
                {
                    o.Menu = menu.Id;
                    if (AbilityTexts.MenuLabels.TryGetValue(o.Id, out var label) && several) o.Name = o.Id == AbilityIds.Search && SearchBeside(u) is string spot ? AbilityTexts.SearchWords(spot).label : o.Id == AbilityIds.OpenDoor && UnlocksBeside(u) ? Lore.KeyText.OpenWithKey : label;
                }
                keys.Add((key, options));
            }
            // Lanzar always right after Vigilancia, whatever the soldier carries: the same key for everyone.
            var throwAt = keys.FindIndex(k => k.key.Id == "menu:throw");
            var watchAt = keys.FindIndex(k => k.key.Id == "menu:watch");
            if (throwAt >= 0 && watchAt >= 0 && throwAt != watchAt + 1)
            {
                var t = keys[throwAt];
                keys.RemoveAt(throwAt);
                keys.Insert(keys.FindIndex(k => k.key.Id == "menu:watch") + 1, t);
            }
            // The usual keys in their order (Numbered), the rest after them (the board's, a door, the crowbar).
            keys = keys.OrderBy(k => Slot(k.key.Id) < 0 ? Numbered.Length : Slot(k.key.Id)).ToList();
            var bar = new List<AbilityView>();
            var place = 0;
            foreach (var (key, options) in keys)
            {
                // Each key's number is its place along the bar, 1 to 9 and 0 (Arturo, 18/11 T15: «que los números del teclado
                // equivalgan a los números de cada botón», not one number for each action whatever is before it); past the
                // tenth, none.
                var number = place < 10 ? (place == 9 ? "0" : (place + 1).ToString()) : "";
                place++;
                // With the finger, no keys (Lore, 7/11).
                key.Hotkey = Bastion.Client.GameInput.Touch || number == "" ? "" : key.Id == AbilityIds.Shoot ? $"{number} · Espacio" : number;
                bar.Add(key);
                if (options != null) bar.AddRange(options);
            }
            return bar;
        }

        /// <summary>A soldier's usual keys, in the order they go along the bar (their number is their place: T15).</summary>
        static readonly string[] Numbered = { AbilityIds.Shoot, AbilityIds.Slash, AbilityIds.Pistol, "menu:watch", "menu:throw", AbilityIds.Reload, AbilityIds.Hunker, AbilityIds.Hide, AbilityIds.FirstAid, "menu:investigate" };

        /// <summary>`id`'s place among Numbered; -1, none.</summary>
        static int Slot(string id) => Array.IndexOf(Numbered, id);

        /// <summary>A menu's choices as one sentence (Lore, 18/11): the first capitalised, the last joined by «o».</summary>
        static string ChoiceList(List<string> names)
        {
            var said = names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " o " + names[names.Count - 1];
            return said.Length > 0 ? char.ToUpperInvariant(said[0]) + said.Substring(1) : said;
        }

        /// <summary>Registrar's picture by what is searched (Looting.SearchSpot): a car's boot, a body, a box; none, the backpack.</summary>
        static string SearchIcon(string spot) => spot == null ? null : spot == Looting.CarBoot || spot == Looting.PoliceBoot ? "search:car" : BodyLike(spot) ? "search:body" : "search:box";

        /// <summary>A box beside the soldier, opened or not, that its hands reach: a body at its feet too (SearchBeside).</summary>
        bool CrateBeside(Unit u) => SearchBeside(u) != null;

        /// <summary>
        /// Whether «Abrir la puerta» is La llave's door only, opened with the key `u` carries (U22): its key named so (Lore's
        /// KeyText.OpenWithKey). Another door beside as well, the name of always.
        /// </summary>
        bool UnlocksBeside(Unit u)
        {
            if (u == null || State.Mission.Locked == null || Looting.KeyOf(u) == null) return false;
            var doors = Abilities.DoorTiles(State, u, TileKinds.Door);
            return doors.Count > 0 && doors.All(p => Looting.LockedAt(State, p));
        }

        /// <summary>
        /// What Registrar would work on beside `u`, by kind (Looting.SearchSpot: a box, a car's boot, a body, a piece, the one
        /// it stands on too: Looting.ReachesToOpen): one still to search first, two tiles off too with Gancho (Looting.
        /// SearchableTiles, as the click; U15: the key had none there), else any searched its hands reach; null, none.
        /// </summary>
        string SearchBeside(Unit u)
        {
            if (u == null) return null;
            if (Looting.SearchableTiles(State, u).FirstOrDefault() is Pos open) return Looting.SearchSpot(State, open);
            string opened = null;
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    var p = new Pos { X = u.Pos.X + dx, Y = u.Pos.Y + dy, Z = u.Pos.Z };
                    var kind = Looting.SearchSpot(State, p);
                    if (kind == null || !Looting.ReachesToOpen(State, u.Pos, p)) continue;
                    if (!Looting.WasSearched(State, p)) return kind;
                    opened ??= kind;
                }
            return opened;
        }

        /// <summary>
        /// Bajar for one inside La Lata (T24): an action, to a free tile of its zone; off with the engine's why
        /// (Rules.AlightRefusal), lit while armed on more than one tile.
        /// </summary>
        AbilityView AlightKey(Unit inside)
        {
            var def = Content.Abilities[AbilityIds.Alight];
            var no = Rules.AlightRefusal(State, inside, MySlot, ActiveSlots(), Rules.AlightTiles(State).FirstOrDefault());
            return new AbilityView
            {
                Id = AbilityIds.Alight,
                Name = def.Name,
                Hotkey = "1",
                Enabled = no == null && CanAct(),
                Reason = no,
                Cost = AbilityTexts.CostText(inside, AbilityIds.Alight),
                Description = def.Description,
                Active = AlightArmed(),
            };
        }

        static AbilityView CallKey(string id, string name, string description, string limits = null)
        {
            return new AbilityView { Id = id, Name = name, Hotkey = "", Enabled = true, Cost = "Sin acción", Description = description, Free = true, Limits = limits };
        }

        /// <summary>
        /// A call to abandon on the bar (playtest 16/10), like the bus's: the partner's «Abandonar» and «Seguir», on whatever
        /// turn it is; this player's own call, off and waiting, with the time left before it goes through.
        /// </summary>
        List<AbilityView> AbandonActions()
        {
            var call = State.AbandonCall;
            if (call == null || State.Outcome != null) return new List<AbilityView>();
            var left = DeadlineLeft("abandon");
            if (call.By != MySlot)
            {
                var ask = Lore.AbandonAsk(PlayerName(call.By));
                var limits = left != null ? Lore.ProposalClock(left, false) : null;
                return new List<AbilityView>
                {
                    CallKey("abandonAccept", Lore.AbandonText.Accept, $"{ask}. {Lore.AbandonText.Stakes}", limits),
                    CallKey("abandonRefuse", Lore.AbandonText.Refuse, $"{ask}. Seguís en la misión.", limits),
                };
            }
            var waiting = Lore.EvacWaiting(PartnerHere()?.Name ?? "el otro jugador");
            var key = CallKey("abandon", Lore.AbandonText.Accept, waiting);
            key.Enabled = false;
            key.Reason = left != null ? Lore.ProposalClock(left, true) : waiting;
            return new List<AbilityView> { key };
        }

        /// <summary>
        /// The evacuation on the bar (playtest 15/10 #10), with no window over the field: «Evacuar» for a soldier of ours in
        /// the bus (pressed twice: the first says what leaving costs), the wait while ours waits, and the partner's
        /// «Aceptar» and «Esperar» while theirs does.
        /// </summary>
        List<AbilityView> EvacActions(Unit u)
        {
            var s = State;
            var none = new List<AbilityView>();
            if (!CanAct() || s.Mission.EvacZone.Count == 0) return none;
            var call = s.EvacCall;
            var o = EvacSituation(s);
            var left = DeadlineLeft("evac");
            var leave = s.Mission.Kind == MissionKinds.Elimination ? "Retirarse" : "Evacuar";
            if (call != null && call.By != MySlot)
            {
                var ask = Lore.EvacAsk(PlayerName(call.By), o);
                var limits = left != null ? Lore.EvacClock(left, false) : null;
                return new List<AbilityView> { CallKey("evacAccept", Lore.EvacBar.Accept, ask.Text, limits), CallKey("evacRefuse", Lore.EvacBar.Wait, Lore.EvacBar.Stay, limits) };
            }
            // Asking costs no action: whichever of ours is in the bus asks, the one picked if it is, any other if not.
            var aboard = MineAboard().OrderBy(x => x.Id != u?.Id).FirstOrDefault();
            if (call != null && call.By == MySlot)
            {
                var waiting = Lore.EvacWaiting(EvacPartner()?.Name ?? "el otro jugador");
                var wait = CallKey("evac", leave, waiting);
                wait.Enabled = false;
                wait.Reason = left != null ? Lore.EvacClock(left, true) : waiting;
                return new List<AbilityView> { wait };
            }
            if (aboard == null) return none;
            var p = Lore.EvacPrompt(o, EvacPartner()?.Name);
            var armed = evacArmed == aboard.Id && Time.time < evacArmedUntil;
            var go = CallKey("evac", armed ? $"Confirmar: {p.Go}" : p.Go, $"{p.Title}. {p.Text}");
            go.Active = armed;
            // In an expedition it is the stage that ends (Lore, 20/11).
            go.Cost = EvacPartner() != null ? $"Lo acepta {PartnerHere()?.Name ?? "el otro jugador"}" : s.Run != null ? "Termina la etapa" : "Termina la misión";
            // El herido out and alive (U25; the engine refuses it): off, why in Lore's words, named once they've been seen.
            // Its card says only that (Producto, 19/11): La Lata's own words, «si salís ahora…», over a key that doesn't let
            // anyone out, contradicted it.
            if (Rules.WoundedOut(s) && Rules.WoundedOf(s) is Unit hurt)
            {
                go.Enabled = false;
                go.Active = false;
                go.Cost = null;
                go.Description = null;
                go.Reason = Lore.WoundedText.NotWithout(Unfound(hurt) ? null : hurt.Name);
            }
            return new List<AbilityView> { go };
        }
    }
}
