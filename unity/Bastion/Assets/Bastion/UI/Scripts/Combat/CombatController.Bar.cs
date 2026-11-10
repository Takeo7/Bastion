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
            var calls = AbandonActions().Concat(EvacActions(u)).ToList();
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
                Text = HudText.Done.State(busy.Name, busy.Ap, Paths.MoveBudget(busy)) + " " + HudText.Done.Stakes(to),
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
            // Leaving is the bar's own action (EvacActions), for any of ours in the bus: never the soldier's.
            return Abilities.AbilitiesOf(u)
                .Where(id => id != AbilityIds.Evac && (id != AbilityIds.Hack || (m.Terminal != null && !m.ObjectiveDone)) && (id != AbilityIds.Search || HasSpots) && Door(id))
                .ToList();
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
                    Weapon = id == AbilityIds.Shoot ? Content.AttackWeapon(u, AbilityIds.Shoot).Id : id == AbilityIds.Slash && Found(Content.AttackWeapon(u, id).Id) ? Content.AttackWeapon(u, id).Id : null,
                    Enabled = reason == null,
                    Reason = reason,
                    // Out of rounds in the magazine (Ana, 20/10 #24): said on the key.
                    Note = reason != null && reason.EndsWith(": recarga.", StringComparison.Ordinal) ? AbilityTexts.ReloadFirst : null,
                    // An attack's card names the weapon it fires: its cost and its noise are that weapon's.
                    // Registrar by what is beside (a body, a boot: Arturo, 02/11 A9, A13), as its title says it (Lore.BoxTitle).
                    Name = attack ? $"{def.Name} · {AbilityTexts.WeaponLabel(u, id)}" : id == AbilityIds.Hack ? $"{def.Name} · {Abilities.HackChance(State, u)}%" : id == AbilityIds.Search && SearchBeside(u) is string spot ? Lore.BoxTitle(spot) : def.Name,
                    Cost = AbilityTexts.CostText(u, id),
                    Limits = AbilityTexts.LimitsText(u, id),
                    Damage = AbilityTexts.DamageText(State, u, id),
                    Noise = AbilityTexts.NoiseText(State, u, id),
                    Stats = AbilityTexts.AbilityStats(State, u, id),
                    Description = id == AbilityIds.Search && SearchBeside(u) is string beside ? AbilityTexts.SearchWords(beside).effect : def.Description,
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
                    Icon = menu.Id == "menu:throw" && options.All(o => o.Id != AbilityIds.Stone) ? AbilityIds.Grenade : menu.Icon,
                    Enabled = usable.Count > 0,
                    Reason = usable.Count > 0 ? null : first.Reason,
                    Note = usable.Count > 0 ? null : options.FirstOrDefault(o => o.Note != null)?.Note,
                    Description = several ? $"{menu.Name}: {string.Join(", ", options.Select(o => o.Name.ToLowerInvariant()))}. Pulsa para elegir." : first.Description,
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
                    if (AbilityTexts.MenuLabels.TryGetValue(o.Id, out var label) && several) o.Name = o.Id == AbilityIds.Search && SearchBeside(u) is string spot ? AbilityTexts.SearchWords(spot).label : label;
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
            var bar = new List<AbilityView>();
            for (var i = 0; i < keys.Count; i++)
            {
                var (key, options) = keys[i];
                key.Hotkey = i < 9 ? (key.Id == AbilityIds.Shoot ? $"{i + 1} · Espacio" : (i + 1).ToString()) : i == 9 ? "0" : "";
                bar.Add(key);
                if (options != null) bar.AddRange(options);
            }
            return bar;
        }

        /// <summary>A box beside the soldier, opened or not, that its hands reach: a body at its feet too (SearchBeside).</summary>
        bool CrateBeside(Unit u) => SearchBeside(u) != null;

        /// <summary>
        /// What Registrar would work on beside `u`, by kind (Looting.SearchSpot: a box, a car's boot, a body, the one it
        /// stands on too: Looting.ReachesToOpen): one still to search first, else any its hands reach; null, none.
        /// </summary>
        string SearchBeside(Unit u)
        {
            if (u == null) return null;
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
            go.Cost = EvacPartner() != null ? $"Lo acepta {PartnerHere()?.Name ?? "el otro jugador"}" : "Termina la misión";
            return new List<AbilityView> { go };
        }
    }
}
