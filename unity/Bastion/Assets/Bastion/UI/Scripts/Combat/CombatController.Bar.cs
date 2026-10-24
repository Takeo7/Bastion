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
            return WithMenus(SoldierAbilities(u), u).Concat(calls).ToList();
        }

        /// <summary>A view of the bar by its id, inside a menu too.</summary>
        AbilityView BarView(string id) => BarKeys().FirstOrDefault(v => v.Id == id);

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

        List<AbilityView> SoldierAbilities(Unit u)
        {
            var list = AbilityList(u);
            var views = new List<AbilityView>();
            for (var i = 0; i < list.Count; i++)
            {
                var id = list[i];
                var def = Content.Abilities[id];
                int? charge = def.Charge != null ? AbilityTexts.Charge(u, def.Charge) : (int?)null;
                var reason = !CanAct() ? "No es vuestro turno." : WaitingFor(u.Id) ?? Abilities.AbilityBlocker(State, u, id);
                if (reason == null && (def.Target == AbilityTargets.Enemy || def.Target == AbilityTargets.Melee || def.Target == AbilityTargets.Ally) && TargetOptions(id).Count == 0)
                    reason = def.Target == AbilityTargets.Ally ? "No hay aliados válidos." : def.Target == AbilityTargets.Melee ? "Ningún enemigo a su alcance." : "Sin enemigos a la vista.";
                u.Cooldowns.TryGetValue(id, out var cooldown);
                var attack = Content.IsAttack(id);
                views.Add(new AbilityView
                {
                    Id = id,
                    // Disparar by the gun in hand (Arturo, 23/10 #7): its picture on the key, not always the rifle's.
                    Weapon = id == AbilityIds.Shoot ? Content.AttackWeapon(u, AbilityIds.Shoot).Id : null,
                    Enabled = reason == null,
                    Reason = reason,
                    // Out of rounds in the magazine (Ana, 20/10 #24): said on the key.
                    Note = reason != null && reason.EndsWith(": recarga.", StringComparison.Ordinal) ? AbilityTexts.ReloadFirst : null,
                    // An attack's card names the weapon it fires: its cost and its noise are that weapon's.
                    Name = attack ? $"{def.Name} · {AbilityTexts.WeaponLabel(u, id)}" : id == AbilityIds.Hack ? $"{def.Name} · {Abilities.HackChance(State, u)}%" : def.Name,
                    Cost = AbilityTexts.CostText(u, id),
                    Limits = AbilityTexts.LimitsText(u, id),
                    Damage = AbilityTexts.DamageText(State, u, id),
                    Noise = AbilityTexts.NoiseText(State, u, id),
                    Stats = AbilityTexts.AbilityStats(State, u, id),
                    Description = def.Description,
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
                var options = menu.Members.Select(id => views.FirstOrDefault(x => x.Id == id)).Where(x => x != null && menu.Shows(x, crate)).ToList();
                if (options.Count == 0) continue;
                var usable = options.Where(o => o.Enabled).ToList();
                var first = options[0];
                var several = options.Count > 1;
                var key = new AbilityView
                {
                    Id = menu.Id,
                    Name = menu.Name,
                    Icon = menu.Icon,
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
                    if (AbilityTexts.MenuLabels.TryGetValue(o.Id, out var label) && several) o.Name = label;
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

        /// <summary>A box beside the soldier, opened or not, that its hands reach (loot.ts searchSpot, reachesToSearch).</summary>
        bool CrateBeside(Unit u)
        {
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var p = new Pos { X = u.Pos.X + dx, Y = u.Pos.Y + dy, Z = u.Pos.Z };
                    if (Looting.SearchSpot(State, p) != null && Looting.ReachesToSearch(State, u.Pos, p)) return true;
                }
            return false;
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
            // Asking costs no action: whichever of ours is in the bus asks, the one picked if it is, any other if not;
            // one who can board before a bitten one who can't.
            var inside = MineAboard().OrderBy(x => x.Id != u?.Id).ToList();
            bool Boards(Unit x) => !(x.Infected == true && s.Mission.Quarantine == false);
            var aboard = inside.FirstOrDefault(Boards) ?? inside.FirstOrDefault();
            if (call != null && call.By == MySlot)
            {
                var waiting = Lore.EvacWaiting(EvacPartner()?.Name ?? "el otro jugador");
                var wait = CallKey("evac", leave, waiting);
                wait.Enabled = false;
                wait.Reason = left != null ? Lore.EvacClock(left, true) : waiting;
                return new List<AbilityView> { wait };
            }
            if (aboard == null) return none;
            if (!Boards(aboard))
            {
                var bitten = CallKey("evac", leave, Lore.BiteNoBoard);
                bitten.Enabled = false;
                bitten.Reason = Lore.BiteNoBoard;
                return new List<AbilityView> { bitten };
            }
            var p = Lore.EvacPrompt(o, EvacPartner()?.Name);
            var armed = evacArmed == aboard.Id && Time.time < evacArmedUntil;
            var go = CallKey("evac", armed ? $"Confirmar: {p.Go}" : p.Go, $"{p.Title}. {p.Text}");
            go.Active = armed;
            go.Cost = EvacPartner() != null ? $"Lo acepta {PartnerHere()?.Name ?? "el otro jugador"}" : "Termina la misión";
            return new List<AbilityView> { go };
        }
    }
}
