#nullable enable
// events.ts: the reducer every copy of the state replays (applyEvent), and its deep copy (cloneState). The GameEvent
// union is Types/GameEvent.cs.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static class Events
    {
        private static Unit UnitOf(GameState s, string id)
        {
            var u = Grid.GetUnit(s, id);
            if (u == null) throw new InvalidOperationException($"Unknown unit {id}");
            return u;
        }

        /// <summary>Lets go of whoever `u` was suppressing.</summary>
        private static void EndSuppression(GameState s, Unit u)
        {
            if (string.IsNullOrEmpty(u.Suppressing)) return;
            var target = Grid.GetUnit(s, u.Suppressing!);
            if (target?.SuppressedBy == u.Id) target.SuppressedBy = null;
            u.Suppressing = null;
        }

        /// <summary>Counts something a soldier of the squad did that teaches (Fase 9, skills.ts).</summary>
        private static void Deed(GameState s, string id, string what)
        {
            var u = Grid.GetUnit(s, id);
            if (u == null || u.Team != Teams.Xcom || u.Owner == null || u.Captive) return;
            var deeds = u.Deeds ??= new Deeds();
            deeds[what] = (deeds.TryGetValue(what, out var n) ? n : 0) + 1;
        }

        /// <summary>What was thrown last reached an enemy `u`: it teaches once (docs/FASE10.md §3).</summary>
        private static void ThrowLanded(GameState s, Unit u)
        {
            var blow = s.Blow;
            if (blow?.Kind != BlowKinds.Blast || blow.Landed == true || Grid.GetUnit(s, blow.Unit)?.Team == u.Team) return;
            blow.Landed = true;
            Deed(s, blow.Unit, DeedIds.ThrowHit);
        }

        private static void ClearStatuses(Unit u)
        {
            u.Stunned = false;
            u.Disoriented = 0;
            u.Panicked = false;
        }

        /// <summary>The cooldowns of `u` go down a turn; those at nothing are gone.</summary>
        private static void TickCooldowns(Unit u)
        {
            foreach (var id in u.Cooldowns.Keys.ToList())
            {
                var left = (u.Cooldowns.TryGetValue(id, out var n) ? n : 0) - 1;
                if (left > 0) u.Cooldowns[id] = left;
                else u.Cooldowns.Remove(id);
            }
        }

        private static RtUnit RtUnitOf(Realtime rt, string id)
        {
            if (!rt.Units.TryGetValue(id, out var me))
            {
                me = new RtUnit { Order = new IdleRtOrder(), Doing = RtDoings.Idle, Until = rt.Clock };
                rt.Units[id] = me;
            }
            return me;
        }

        public static void ApplyEvent(GameState s, GameEvent e)
        {
            s.Seq++;
            switch (e)
            {
                case RtClockEvent ev:
                    if (s.Realtime != null) s.Realtime.Clock = ev.Clock;
                    break;
                case RtPausedEvent ev:
                    if (s.Realtime != null) s.Realtime.Paused = ev.Paused;
                    break;
                case RtOrderEvent ev:
                {
                    if (s.Realtime == null) break;
                    var me = RtUnitOf(s.Realtime, ev.Unit);
                    me.Order = ev.Order;
                    me.Path = null;
                    me.Waited = null;
                    break;
                }
                case RtDoingEvent ev:
                {
                    if (s.Realtime == null) break;
                    var me = RtUnitOf(s.Realtime, ev.Unit);
                    me.Doing = ev.Doing;
                    me.Until = ev.Until;
                    me.Target = !string.IsNullOrEmpty(ev.Target) ? ev.Target : null;
                    me.Ability = !string.IsNullOrEmpty(ev.Ability) ? ev.Ability : null;
                    break;
                }
                case RtSpeedEvent ev:
                    if (s.Realtime != null) s.Realtime.Speed = ev.Speed;
                    break;
                case RtRoundEvent ev:
                {
                    // What turnBegan wears off, a unit's on its own round and the map's on the clock's; no actions to give back, no mission turn gone.
                    if (string.IsNullOrEmpty(ev.Unit))
                    {
                        foreach (var cloud in s.Smoke) cloud.Turns--;
                        s.Smoke = s.Smoke.Where((cloud) => cloud.Turns > 0).ToList();
                        foreach (var light in s.Lights ?? new List<Light>()) if (light.Turns != null) light.Turns--;
                        break;
                    }
                    foreach (var u in s.Units)
                    {
                        if (!u.Alive || u.Id != ev.Unit) continue;
                        u.Marked = 0;
                        if (u.Disoriented > 0) u.Disoriented--;
                        u.Stunned = false;
                        u.Hunkered = false;
                        u.Aided = 0;
                        u.ReactedTo = new List<string>();
                        EndSuppression(s, u);
                        TickCooldowns(u);
                    }
                    break;
                }
                case TurnBeganEvent ev:
                {
                    s.ActiveTeam = ev.Team;
                    s.Turn = ev.Turn;
                    // A call to evacuate nobody answered lapses with the squad's turn.
                    s.EvacCall = null;
                    if (ev.Team == Teams.Xcom)
                    {
                        s.Ready = new[] { false, false };
                        s.ActiveUnit = null;
                        if (ev.Command != null) s.Command = ev.Command;
                        if (s.Mission.TurnsLeft != null && ev.Turn > 1) s.Mission.TurnsLeft--;
                        // Holo marks last for the rest of the squad's turn and the alien turn after it.
                        foreach (var u in s.Units) u.Marked = 0;
                        foreach (var cloud in s.Smoke) cloud.Turns--;
                        s.Smoke = s.Smoke.Where((cloud) => cloud.Turns > 0).ToList();
                        // Bengalas and fires burn down (the last turn goes out with a lightOff before this).
                        foreach (var light in s.Lights ?? new List<Light>()) if (light.Turns != null) light.Turns--;
                    }
                    foreach (var u in s.Units)
                    {
                        if (!u.Alive) continue;
                        if (u.Team != ev.Team)
                        {
                            // Their turn just ended: disorientation wears off.
                            if (u.Disoriented > 0) u.Disoriented--;
                            continue;
                        }
                        // A turret has no turn of its own: it keeps watch in the enemy's (docs/FASE12.md §11.4).
                        u.Ap = u.Captive || u.Objective || u.Automaton != null ? 0 : u.Stunned ? 1 : Content.ApPerTurnOf(u);
                        u.Stunned = false;
                        u.Overwatch = false;
                        u.KillZone = false;
                        u.ReactedTo = new List<string>();
                        u.Hunkered = false;
                        u.Aided = 0;
                        u.Refunded = false;
                        EndSuppression(s, u);
                        TickCooldowns(u);
                        if (u.Panicked)
                        {
                            // Cowers for the whole turn.
                            u.Ap = 0;
                            u.Hunkered = true;
                            u.Panicked = false;
                        }
                    }
                    break;
                }
                case MovedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    var last = ev.Path.Count > 0 ? ev.Path[ev.Path.Count - 1] : null;
                    // It looks the way it walked (los.ts inViewCone: the dead see ahead of them).
                    var before = ev.Path.Count > 1 ? ev.Path[ev.Path.Count - 2] : u.Pos;
                    if (last != null) u.Facing = Los.FacingTo(before, last) ?? u.Facing;
                    if (last != null) u.Pos = CopyPos(last);
                    u.Ap = Math.Max(0, u.Ap - ev.Ap);
                    EndSuppression(s, u);
                    // Running, with both actions, teaches Forma física (docs/FASE10.md §3).
                    if (ev.Ap >= 2) Deed(s, u.Id, DeedIds.Run);
                    break;
                }
                case AbilityUsedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - ev.Ap);
                    if (ev.Cooldown > 0) u.Cooldowns[ev.Ability] = ev.Cooldown;
                    if (!string.IsNullOrEmpty(ev.Charge)) u.Charges[ev.Charge!] = Math.Max(0, (u.Charges.TryGetValue(ev.Charge!, out var n) ? n : 0) - 1);
                    if (ev.Ability != AbilityIds.Overwatch && ev.Ability != AbilityIds.KillZone && ev.Ap > 0) EndSuppression(s, u);
                    break;
                }
                case ShotEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    var aim = Grid.GetUnit(s, ev.Target);
                    if (aim != null) u.Facing = Los.FacingTo(u.Pos, aim.Pos) ?? u.Facing;
                    if (Content.FiresSidearm(u, ev.Ability)) u.PistolAmmo = Math.Max(0, (u.PistolAmmo ?? 0) - 1);
                    else if (Content.ClipOf(u, ev.Ability) > 0) u.Ammo = Math.Max(0, u.Ammo - 1);
                    if (ev.Reaction && u.Overwatch)
                    {
                        if (u.KillZone) u.ReactedTo.Add(ev.Target);
                        else u.Overwatch = false;
                    }
                    // Bad luck: a player's misses in a row add up (aim.ts badLuckAim), a hit clears them.
                    if (u.Team == Teams.Xcom && u.Owner != null && string.IsNullOrEmpty(u.ControlledBy))
                    {
                        var streak = s.MissStreak ??= new[] { 0, 0 };
                        streak[u.Owner.Value] = ev.Result == ShotResults.Miss ? Math.Min(Aim.BadLuckMisses, streak[u.Owner.Value] + 1) : 0;
                    }
                    // Every shot wears the weapon in the hand (docs/FASE9.md §5); half with Mantenimiento.
                    if (Content.FiresPrimary(u, ev.Ability)) u.Arms!.Condition = Math.Max(0, Content.ConditionOf(u) - Content.WearOf(Content.AttackWeapon(u, ev.Ability).Id) / (u.Mods.Care != 0 ? 2.0 : 1.0));
                    var blade = Content.Abilities[ev.Ability].Weapon == AbilityWeapons.Melee || Content.AttackWeapon(u, ev.Ability).Melee;
                    s.Blow = new Blow { Unit = u.Id, Kind = blade ? BlowKinds.Blade : BlowKinds.Gun };
                    // Each one at an enemy teaches, and more if it lands (docs/FASE10.md §3).
                    if (Grid.GetUnit(s, ev.Target)?.Team != u.Team)
                    {
                        Deed(s, u.Id, blade ? DeedIds.Swing : DeedIds.Shot);
                        if (ev.Result != ShotResults.Miss) Deed(s, u.Id, blade ? DeedIds.Blow : DeedIds.Hit);
                    }
                    break;
                }
                case DamagedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Hp = Math.Max(0, u.Hp - ev.Amount);
                    u.Armor = Math.Max(0, u.Armor - ev.Shred);
                    ThrowLanded(s, u);
                    break;
                }
                case DiedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Alive = false;
                    u.Hp = 0;
                    u.Ap = 0;
                    u.Overwatch = false;
                    u.Hunkered = false;
                    u.Aided = 0;
                    EndSuppression(s, u);
                    if (!string.IsNullOrEmpty(u.SuppressedBy))
                    {
                        var by = Grid.GetUnit(s, u.SuppressedBy!);
                        if (by != null) by.Suppressing = null;
                        u.SuppressedBy = null;
                    }
                    if (!string.IsNullOrEmpty(ev.By))
                    {
                        var killer = Grid.GetUnit(s, ev.By!);
                        if (killer != null && killer.Team != u.Team)
                        {
                            killer.Kills++;
                            // Silencing the alarm teaches Maña, however it went; the rest, the weapon that killed.
                            if (u.Objective) Deed(s, killer.Id, DeedIds.Fix);
                            else if (s.Blow?.Unit == killer.Id && s.Blow.Kind != BlowKinds.Blast) Deed(s, killer.Id, s.Blow.Kind == BlowKinds.Blade ? DeedIds.BladeKill : DeedIds.GunKill);
                        }
                    }
                    if (s.Mission.Kind == MissionKinds.Sabotage && s.Mission.ObjectiveUnit == u.Id) CompleteObjective(s);
                    // A soldier leaves what it carried where it fell, for another to take (Arturo, 18/10 #49); off a flight of stairs
                    // at its nearer end, where one can stop (flights.ts).
                    if (u.Team == Teams.Xcom && u.Automaton == null && !u.Objective && !u.Captive)
                    {
                        var at = Flights.RestingPlace(s, u.Pos);
                        foreach (var loot in Looting.FallenGear(u)) LeaveOnFloor(s, at, loot, u.Id);
                        u.Backpack = new List<Loot>();
                    }
                    break;
                }
                case StunnedEvent ev:
                    UnitOf(s, ev.Unit).Stunned = true;
                    break;
                case OverwatchEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Overwatch = true;
                    u.KillZone = ev.KillZone;
                    u.WatchMelee = ev.Melee == true;
                    u.ReactedTo = new List<string>();
                    break;
                }
                case HunkerEvent ev:
                    UnitOf(s, ev.Unit).Hunkered = true;
                    Deed(s, ev.Unit, DeedIds.Hunker);
                    break;
                case StoneThrownEvent ev:
                    s.Blow = new Blow { Unit = ev.Unit, Kind = BlowKinds.Blast };
                    Deed(s, ev.Unit, DeedIds.Throw);
                    break;
                case BarFilledEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    if (u.Lines == null) break;
                    var before = u.Lines.Growth.TryGetValue(ev.Skill, out var g) ? g : 0;
                    u.Lines.Growth[ev.Skill] = before + ev.Growth;
                    if (!u.Lines.Rolls.TryGetValue(ev.Skill, out var rolls))
                    {
                        rolls = new List<int>();
                        u.Lines.Rolls[ev.Skill] = rolls;
                    }
                    rolls.Add(ev.Growth);
                    // Its stat, at once: health too, the new points full.
                    var delta = Bars.GrowthDeltaOf(ev.Skill, before, before + ev.Growth);
                    u.Mods = Content.AddMods(u.Mods, delta.Mods);
                    u.MaxHp += delta.Hp;
                    u.Hp += delta.Hp;
                    break;
                }
                case ReloadEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    Content.ReloadWeapon(u);
                    if (u.Mags != null) u.Mags = Math.Max(0, u.Mags.Value - 1);
                    break;
                }
                case ExplosiveEvent ev:
                    s.Blow = new Blow { Unit = ev.Unit, Kind = BlowKinds.Blast };
                    if (Content.Thrown.Contains(ev.Ability)) Deed(s, ev.Unit, DeedIds.Throw);
                    break;
                case HealedEvent ev:
                {
                    var target = UnitOf(s, ev.Target);
                    target.Hp = Math.Min(target.MaxHp, target.Hp + ev.Amount);
                    if (ev.Amount > 0) Deed(s, ev.Unit, ev.Unit == ev.Target ? DeedIds.HealSelf : DeedIds.HealOther);
                    break;
                }
                case AidedEvent ev:
                    UnitOf(s, ev.Target).Aided = ev.Amount;
                    break;
                case CleansedEvent ev:
                    ClearStatuses(UnitOf(s, ev.Target));
                    break;
                case RefundEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap += 1;
                    u.Refunded = true;
                    break;
                }
                case SuppressedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    var target = UnitOf(s, ev.Target);
                    u.Suppressing = target.Id;
                    target.SuppressedBy = u.Id;
                    u.Ammo = Math.Max(0, u.Ammo - 2);
                    break;
                }
                case SuppressionEndedEvent ev:
                    EndSuppression(s, UnitOf(s, ev.Unit));
                    break;
                case MarkedEvent ev:
                    UnitOf(s, ev.Unit).Marked = ev.Amount;
                    break;
                case RupturedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ruptured = Math.Max(u.Ruptured, ev.Amount);
                    break;
                }
                case PsiEvent ev:
                {
                    if (!ev.Success) break;
                    var target = UnitOf(s, ev.Target);
                    if (ev.Effect == PsiEffects.Disoriented) target.Disoriented = 1;
                    else if (ev.Effect == PsiEffects.Panicked) target.Panicked = true;
                    // Only in missions saved before the zombies: the scream never takes control.
                    else if (ev.Effect == PsiEffects.Controlled)
                    {
                        var caster = UnitOf(s, ev.Unit);
                        target.Team = Teams.Alien;
                        target.ControlledBy = caster.Id;
                        target.ControlTurns = Content.ControlTurns;
                        target.PodId = caster.PodId;
                        target.Ap = 0;
                        target.Overwatch = false;
                        target.Hunkered = false;
                        EndSuppression(s, target);
                    }
                    break;
                }
                case ControlTickEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.ControlTurns = Math.Max(0, u.ControlTurns - 1);
                    break;
                }
                case ReleasedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Team = Teams.Xcom;
                    u.ControlledBy = null;
                    u.ControlTurns = 0;
                    u.PodId = null;
                    u.Ap = 0;
                    u.Overwatch = false;
                    break;
                }
                case ReanimatedEvent ev:
                {
                    UnitOf(s, ev.Corpse).Reanimated = true;
                    var z = CloneUnit(ev.Zombie);
                    s.Units.Add(z);
                    if (ev.Pod != null) s.Pods.Add(ClonePod(ev.Pod));
                    var pod = s.Pods.Find((p) => p.Id == z.PodId);
                    if (pod != null) pod.UnitIds.Add(z.Id);
                    break;
                }
                case InfectedEvent ev:
                    UnitOf(s, ev.Unit).Infected = true;
                    break;
                case SmokeDeployedEvent ev:
                    s.Smoke.Add(new SmokeCloud { Pos = CopyPos(ev.Pos), Radius = ev.Radius, Turns = ev.Turns });
                    break;
                case DisorientedEvent ev:
                    UnitOf(s, ev.Unit).Disoriented = 1;
                    ThrowLanded(s, UnitOf(s, ev.Unit));
                    break;
                case EvacuatedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Alive = false;
                    u.Evacuated = true;
                    u.Ap = 0;
                    u.Overwatch = false;
                    EndSuppression(s, u);
                    var item = s.Mission.Item;
                    if (item?.Carrier == u.Id)
                    {
                        item.Carrier = null;
                        item.Evacuated = true;
                        CompleteObjective(s);
                    }
                    if (s.Mission.Kind == MissionKinds.Rescue && s.Mission.ObjectiveUnit == u.Id) CompleteObjective(s);
                    break;
                }
                case EvacCalledEvent ev:
                    s.EvacCall = new EvacCall { By = ev.Slot, Unit = ev.Unit };
                    break;
                case EvacRefusedEvent _:
                    s.EvacCall = null;
                    break;
                case AbandonCalledEvent ev:
                    s.AbandonCall = new AbandonCall { By = ev.Slot };
                    break;
                case AbandonRefusedEvent _:
                    s.AbandonCall = null;
                    break;
                case EvacDepartedEvent _:
                    s.EvacCall = null;
                    s.Mission.EvacDone = true;
                    break;
                case LeftBehindEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Alive = false;
                    u.Lost = true;
                    u.Ap = 0;
                    u.Overwatch = false;
                    EndSuppression(s, u);
                    // What it carried stays with it.
                    if (s.Mission.Item?.Carrier == u.Id) s.Mission.Item.Carrier = null;
                    break;
                }
                case ItemPickedEvent ev:
                {
                    var item = s.Mission.Item;
                    if (item != null)
                    {
                        item.Carrier = ev.Unit;
                        item.Pos = null;
                    }
                    break;
                }
                case ItemDroppedEvent ev:
                {
                    var item = s.Mission.Item;
                    if (item != null)
                    {
                        item.Carrier = null;
                        item.Pos = CopyPos(ev.Pos);
                    }
                    break;
                }
                case FloorBrokenEvent ev:
                {
                    var level = ev.Pos.Z > 0 ? Grid.LevelOf(s, ev.Pos.Z - 1) : null;
                    if (level != null) level.Tiles[Grid.TileIndex(s, ev.Pos.X, ev.Pos.Y)] = null;
                    break;
                }
                case FellEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Pos = CopyPos(ev.To);
                    u.Overwatch = false;
                    u.Hunkered = false;
                    break;
                }
                case TileChangedEvent ev:
                {
                    // On the ground, or on a storey's slab (docs/FASE6-PLAN.md).
                    var i = Grid.TileIndex(s, ev.Pos.X, ev.Pos.Y);
                    if (ev.Pos.Z == 0) s.Tiles[i] = ev.Kind;
                    else
                    {
                        var storey = Grid.LevelOf(s, ev.Pos.Z - 1);
                        if (storey != null) storey.Tiles[i] = ev.Kind;
                    }
                    // A door or barricade of a siege that is gone (a blast took it): a breach.
                    var holds = s.Breakables;
                    var node = Grid.NodeOf(s, ev.Pos);
                    if (holds != null && holds.ContainsKey(node) && ev.Kind != TileKinds.Door && ev.Kind != TileKinds.DoorOpen && ev.Kind != TileKinds.Low) holds[node] = 0;
                    if (!string.IsNullOrEmpty(ev.Decor))
                    {
                        // A breached wall keeps its look through a reload (docs/FASE7.md §4).
                        if (ev.Pos.Z == 0)
                        {
                            s.Decor ??= Enumerable.Repeat(0, s.Width * s.Height).ToList();
                            s.Decor[i] = Decor.DecorCode(ev.Decor!, ev.Facing ?? 0);
                        }
                        else
                        {
                            var storey = Grid.LevelOf(s, ev.Pos.Z - 1);
                            if (storey != null)
                            {
                                storey.Decor ??= Enumerable.Repeat(0, s.Width * s.Height).ToList();
                                storey.Decor[i] = Decor.DecorCode(ev.Decor!, ev.Facing ?? 0);
                            }
                        }
                    }
                    break;
                }
                case TrapSprungEvent ev:
                {
                    var trap = s.Traps?.Find((t) => t.Piece == ev.Trap);
                    if (trap != null) trap.Charges = Math.Max(0, trap.Charges - 1);
                    break;
                }
                case SmashedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Facing = Los.FacingTo(u.Pos, ev.Tile) ?? u.Facing;
                    if (s.Breakables != null) s.Breakables[Grid.NodeOf(s, ev.Tile)] = Math.Max(0, ev.Left);
                    break;
                }
                case PodActivatedEvent ev:
                {
                    var pod = s.Pods.Find((p) => p.Id == ev.Pod);
                    if (pod != null) pod.Active = true;
                    break;
                }
                case ConcealmentBrokenEvent _:
                    s.Concealed = false;
                    break;
                case CommandPassedEvent ev:
                    s.Command = ev.To;
                    s.ActiveUnit = null;
                    break;
                case UnitActiveEvent ev:
                    s.ActiveUnit = !string.IsNullOrEmpty(ev.Unit) ? ev.Unit : null;
                    break;
                case ReadyEvent ev:
                    s.Ready[ev.Slot] = ev.Ready;
                    break;
                case MissionEndedEvent ev:
                    s.Outcome = ev.Outcome;
                    s.OutcomeReason = ev.Reason;
                    break;
                case HackedEvent ev:
                    if (ev.Success)
                    {
                        CompleteObjective(s);
                        Deed(s, ev.Unit, DeedIds.Fix);
                    }
                    break;
                case VipRescuedEvent ev:
                {
                    var vip = UnitOf(s, ev.Unit);
                    vip.Captive = false;
                    vip.Owner = ev.Owner;
                    break;
                }
                case ReinforcementsIncomingEvent ev:
                {
                    var list = s.Mission.Reinforcements;
                    var i = list.FindIndex((r) => r.Id == ev.R.Id);
                    var copy = CopyReinforcement(ev.R);
                    if (i >= 0) list[i] = copy;
                    else list.Add(copy);
                    break;
                }
                case SpawnedEvent ev:
                {
                    foreach (var u in ev.Units) s.Units.Add(CloneUnit(u));
                    s.Pods.Add(ClonePod(ev.Pod));
                    var r = s.Mission.Reinforcements.Find((x) => x.Id == ev.Reinforcement);
                    if (r != null) r.Landed = true;
                    break;
                }
                case PatrolEvent ev:
                {
                    var pod = s.Pods.Find((p) => p.Id == ev.Pod);
                    if (pod != null) pod.PatrolIndex = ev.Index;
                    break;
                }
                case NoiseEvent ev:
                {
                    // Whoever hears it turns to look.
                    foreach (var id in ev.Heard)
                    {
                        var u = UnitOf(s, id);
                        u.Heard = CopyPos(ev.Tile);
                        u.Facing = Los.FacingTo(u.Pos, ev.Tile) ?? u.Facing;
                    }
                    var by = !string.IsNullOrEmpty(ev.By) ? Grid.GetUnit(s, ev.By!) : null;
                    if (by?.Team == Teams.Xcom) s.Mission.Noise = (s.Mission.Noise ?? 0) + Content.HordePoints(ev.Radius);
                    break;
                }
                case ForgotEvent ev:
                    UnitOf(s, ev.Unit).Heard = null;
                    break;
                case HuntingEvent ev:
                    UnitOf(s, ev.Unit).Hunting = !string.IsNullOrEmpty(ev.Target) ? new Hunting { Id = ev.Target!, Missed = ev.Missed } : null;
                    break;
                case TurnedEvent ev:
                    UnitOf(s, ev.Unit).Facing = ev.Facing;
                    break;
                case SearchedEvent ev:
                {
                    (s.Searched ??= new List<int>()).Add(Grid.NodeOf(s, ev.Tile));
                    if (ev.Loot != null) Stow(s, UnitOf(s, ev.Unit), ev.Loot);
                    Deed(s, ev.Unit, DeedIds.Search);
                    break;
                }
                case LootDroppedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    var loot = u.Backpack![ev.Index];
                    u.Backpack.RemoveAt(ev.Index);
                    LeaveOnFloor(s, u.Pos, loot);
                    break;
                }
                case LootTakenEvent ev:
                {
                    var at = s.Dropped!.FindIndex((d) => d.Id == ev.Id);
                    var left = s.Dropped[at];
                    s.Dropped.RemoveAt(at);
                    Stow(s, UnitOf(s, ev.Unit), left.Loot);
                    break;
                }
                case LootGivenEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - 1);
                    var loot = u.Backpack![ev.Index];
                    u.Backpack.RemoveAt(ev.Index);
                    Stow(s, UnitOf(s, ev.To), loot);
                    break;
                }
                case MedicineUsedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - 1);
                    var loot = u.Backpack![ev.Index];
                    loot.Medicines = (loot.Medicines ?? 1) - 1;
                    if (loot.Medicines <= 0)
                    {
                        loot.Medicines = null;
                        if (IsEmpty(loot)) u.Backpack.RemoveAt(ev.Index);
                    }
                    break;
                }
                case PatchedEvent ev:
                {
                    var target = UnitOf(s, ev.Target);
                    if (!string.IsNullOrEmpty(target.Arms?.Primary)) target.Arms!.Condition = Math.Min(100, Content.ConditionOf(target) + ev.Amount);
                    break;
                }
                case CuredEvent ev:
                    UnitOf(s, ev.Unit).Infected = false;
                    Deed(s, ev.By, ev.By == ev.Unit ? DeedIds.HealSelf : DeedIds.HealOther);
                    break;
                case WeaponEquippedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - 1);
                    var loot = u.Backpack![ev.Index];
                    u.Backpack.RemoveAt(ev.Index);
                    EquipWeapon(s, u, loot);
                    break;
                }
                case VestWornEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - 1);
                    var loot = u.Backpack![ev.Index];
                    u.Backpack.RemoveAt(ev.Index);
                    var hp = Content.Items[loot.Item!].Hp ?? 0;
                    u.MaxHp += hp;
                    u.Hp += hp;
                    (u.Worn ??= new List<string>()).Add(loot.Item!);
                    break;
                }
                case BeltEquippedEvent ev:
                {
                    var u = UnitOf(s, ev.Unit);
                    u.Ap = Math.Max(0, u.Ap - 1);
                    var loot = u.Backpack![ev.Index];
                    u.Backpack.RemoveAt(ev.Index);
                    var def = Content.Items[loot.Item!];
                    u.Gear.Add(def.Id);
                    if (def.Charges != null) foreach (var c in def.Charges) u.Charges[c.Key] = (u.Charges.TryGetValue(c.Key, out var n) ? n : 0) + c.Value;
                    if (def.Mods != null) u.Mods = Content.AddMods(u.Mods, def.Mods);
                    (u.Belted ??= new List<string>()).Add(def.Id);
                    break;
                }
                case RoomSearchedEvent ev:
                {
                    (s.RoomsSearched ??= new List<int>()).Add(ev.Room);
                    if (ev.Loot != null) Stow(s, UnitOf(s, ev.Unit), ev.Loot);
                    Deed(s, ev.Unit, DeedIds.Search);
                    break;
                }
                case LightOnEvent ev:
                    (s.Lights ??= new List<Light>()).Add(CopyLight(ev.Light));
                    break;
                case LightOffEvent ev:
                    if (s.Lights != null) s.Lights = s.Lights.Where((l) => l.Id != ev.Id).ToList();
                    break;
            }
        }

        /// <summary>`!Object.keys(loot).length`: nothing set.</summary>
        private static bool IsEmpty(Loot l)
        {
            return l.Supplies == null && l.Item == null && l.Medicines == null && l.Food == null && l.Parts == null && l.Weapon == null && l.WeaponId == null && l.Condition == null && l.Mods == null && l.Mod == null && l.Mags == null;
        }

        /// <summary>
        /// A weapon in hand (docs/FASE8.md §C), loaded: the one it replaces goes into the backpack (or to the floor), with
        /// its Armería id if it was the soldier's own. A new main gun leaves the pistol beside it with the rounds it had.
        /// </summary>
        private static void EquipWeapon(GameState s, Unit u, Loot loot)
        {
            var kind = loot.Weapon!;
            var id = loot.WeaponId;
            var arms = u.Arms!;
            var melee = Content.FoundWeapons[kind].Slot == FoundWeaponSlots.Melee;
            var old = melee ? arms.Melee : arms.Primary;
            var oldId = melee ? arms.MeleeId : arms.PrimaryId;
            var kept = new Loot();
            if (!melee)
            {
                if (arms.Condition != null) kept.Condition = arms.Condition;
                if (arms.Mods != null && arms.Mods.Count > 0) kept.Mods = new List<string>(arms.Mods);
            }
            if (melee)
            {
                arms.Melee = kind;
                arms.MeleeId = !string.IsNullOrEmpty(id) ? id : null;
            }
            else
            {
                if (string.IsNullOrEmpty(arms.Primary)) u.PistolAmmo = u.Ammo;
                arms.Primary = kind;
                arms.PrimaryId = !string.IsNullOrEmpty(id) ? id : null;
                arms.Condition = loot.Condition;
                arms.Mods = loot.Mods != null && loot.Mods.Count > 0 ? new List<string>(loot.Mods) : null;
                u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
                u.WeaponName = Content.FoundWeapons[kind].Name;
            }
            if (!string.IsNullOrEmpty(old))
            {
                kept.Weapon = old;
                if (!string.IsNullOrEmpty(oldId)) kept.WeaponId = oldId;
                Stow(s, u, kept);
            }
        }

        /// <summary>
        /// Where a find goes (docs/FASE7.md, FASE8.md §C): magazines straight to the
        /// spares; anything else into the backpack, or onto the floor at the finder's
        /// feet when it is full, to be swapped for something there.
        /// </summary>
        private static void Stow(GameState s, Unit u, Loot loot)
        {
            if (loot.Mags != null && loot.Mags != 0)
            {
                u.Mags = (u.Mags ?? 0) + loot.Mags.Value;
                return;
            }
            if ((u.Backpack?.Count ?? 0) < Looting.PackSlots(u)) (u.Backpack ??= new List<Loot>()).Add(CopyLoot(loot));
            else LeaveOnFloor(s, u.Pos, loot);
        }

        /// <summary>Something left on the floor of `pos`, with the next id.</summary>
        private static void LeaveOnFloor(GameState s, Pos pos, Loot loot, string? from = null)
        {
            s.DroppedCount = (s.DroppedCount ?? 0) + 1;
            (s.Dropped ??= new List<DroppedLoot>()).Add(new DroppedLoot { Id = $"l{s.DroppedCount}", Pos = CopyPos(pos), Loot = CopyLoot(loot), From = !string.IsNullOrEmpty(from) ? from : null });
        }

        /// <summary>The mission's objective is secured: the timer stops.</summary>
        private static void CompleteObjective(GameState s)
        {
            s.Mission.ObjectiveDone = true;
            s.Mission.TurnsLeft = null;
        }

        // ------------------------------------------------------------ copies
        // The state is plain JSON by design, so a JSON round-trip is a faithful deep copy (TypeScript's cloneState); here,
        // field by field. `{ ...x }` (a shallow copy) is CopyPos, CopyLoot and the like.

        /// <summary>`{ ...p }`.</summary>
        public static Pos CopyPos(Pos p)
        {
            return new Pos { X = p.X, Y = p.Y, Z = p.Z };
        }

        private static Vec2 CopyVec(Vec2 p)
        {
            return new Vec2 { X = p.X, Y = p.Y };
        }

        private static Pos? CopyPosOrNull(Pos? p)
        {
            return p != null ? CopyPos(p) : null;
        }

        private static List<T>? CopyList<T>(List<T>? xs, Func<T, T> copy)
        {
            return xs?.Select(copy).ToList();
        }

        private static List<string>? CopyStrings(List<string>? xs)
        {
            return xs != null ? new List<string>(xs) : null;
        }

        private static List<int>? CopyInts(List<int>? xs)
        {
            return xs != null ? new List<int>(xs) : null;
        }

        /// <summary>`{ ...loot }`: a shallow copy (its `mods` the same list).</summary>
        public static Loot CopyLoot(Loot l)
        {
            return new Loot { Supplies = l.Supplies, Item = l.Item, Medicines = l.Medicines, Food = l.Food, Parts = l.Parts, Weapon = l.Weapon, WeaponId = l.WeaponId, Condition = l.Condition, Mods = l.Mods, Mod = l.Mod, Mags = l.Mags };
        }

        private static Loot CloneLoot(Loot l)
        {
            var c = CopyLoot(l);
            c.Mods = CopyStrings(l.Mods);
            return c;
        }

        /// <summary>The copy reinforcementsIncoming keeps: `{ ...r, pos: r.pos ? { ...r.pos } : null, units: [...r.units] }`.</summary>
        public static Reinforcement CopyReinforcement(Reinforcement r)
        {
            return new Reinforcement { Id = r.Id, Turn = r.Turn, Pos = CopyPosOrNull(r.Pos), Units = new List<string>(r.Units), Landed = r.Landed, Horde = r.Horde, Trickle = r.Trickle, Side = r.Side };
        }

        /// <summary>The copy lightOn keeps: `{ ...light, tile: { ...light.tile }, tiles: [...light.tiles] }`.</summary>
        public static Light CopyLight(Light l)
        {
            return new Light { Id = l.Id, Tile = CopyPos(l.Tile), Radius = l.Radius, Turns = l.Turns, Tiles = CopyInts(l.Tiles) };
        }

        private static Skills CopySkills(Skills x)
        {
            var c = new Skills();
            foreach (var e in x) c[e.Key] = e.Value;
            return c;
        }

        private static Growth CopyGrowth(Growth x)
        {
            var c = new Growth();
            foreach (var e in x) c[e.Key] = e.Value;
            return c;
        }

        /// <summary>A unit, deep (JSON.parse(JSON.stringify(u))).</summary>
        public static Unit CloneUnit(Unit u)
        {
            return new Unit
            {
                Id = u.Id,
                Team = u.Team,
                Template = u.Template,
                Name = u.Name,
                Owner = u.Owner,
                Pos = CopyPos(u.Pos),
                Hp = u.Hp,
                MaxHp = u.MaxHp,
                Armor = u.Armor,
                Ap = u.Ap,
                Ammo = u.Ammo,
                Mags = u.Mags,
                Arms = u.Arms != null ? new Arms { Primary = u.Arms.Primary, PrimaryId = u.Arms.PrimaryId, Condition = u.Arms.Condition, Mods = CopyStrings(u.Arms.Mods), Melee = u.Arms.Melee, MeleeId = u.Arms.MeleeId } : null,
                PistolAmmo = u.PistolAmmo,
                Charges = new Dictionary<string, int>(u.Charges),
                Overwatch = u.Overwatch,
                Hunkered = u.Hunkered,
                Aided = u.Aided,
                Stunned = u.Stunned,
                Alive = u.Alive,
                Evacuated = u.Evacuated,
                PodId = u.PodId,
                Lost = u.Lost,
                Rank = u.Rank,
                Perks = new List<string>(u.Perks),
                Mods = Content.AddMods(u.Mods, new UnitMods()),
                Kills = u.Kills,
                CampaignId = u.CampaignId,
                Appearance = u.Appearance != null ? new Appearance { Primary = u.Appearance.Primary, Secondary = u.Appearance.Secondary, Visor = u.Appearance.Visor, Head = u.Appearance.Head, Build = u.Appearance.Build, Sex = u.Appearance.Sex, Helmet = u.Appearance.Helmet, Torso = u.Appearance.Torso, Arms = u.Appearance.Arms, Legs = u.Appearance.Legs, Character = u.Appearance.Character } : null,
                Nickname = u.Nickname,
                WeaponTier = u.WeaponTier,
                ArmorTier = u.ArmorTier,
                WeaponName = u.WeaponName,
                Objective = u.Objective,
                Captive = u.Captive,
                Will = u.Will,
                Cooldowns = new Dictionary<string, int>(u.Cooldowns),
                KillZone = u.KillZone,
                ReactedTo = new List<string>(u.ReactedTo),
                WatchMelee = u.WatchMelee,
                Lines = u.Lines != null ? new MissionLines { Start = CopySkills(u.Lines.Start), Bonus = u.Lines.Bonus, Growth = CopyGrowth(u.Lines.Growth), Rolls = u.Lines.Rolls.ToDictionary((e) => e.Key, (e) => new List<int>(e.Value)) } : null,
                Worn = CopyStrings(u.Worn),
                Automaton = u.Automaton != null ? new Automaton { Piece = u.Automaton.Piece } : null,
                Belted = CopyStrings(u.Belted),
                Facing = u.Facing,
                Suppressing = u.Suppressing,
                SuppressedBy = u.SuppressedBy,
                Marked = u.Marked,
                Ruptured = u.Ruptured,
                Disoriented = u.Disoriented,
                Panicked = u.Panicked,
                ControlledBy = u.ControlledBy,
                ControlTurns = u.ControlTurns,
                Reanimated = u.Reanimated,
                Refunded = u.Refunded,
                Gear = new List<string>(u.Gear),
                Heard = CopyPosOrNull(u.Heard),
                Hunting = u.Hunting != null ? new Hunting { Id = u.Hunting.Id, Missed = u.Hunting.Missed } : null,
                Backpack = CopyList(u.Backpack, CloneLoot),
                PackSlots = u.PackSlots,
                Deeds = u.Deeds != null ? CopyDeeds(u.Deeds) : null,
                RisenFrom = u.RisenFrom,
                Infected = u.Infected,
            };
        }

        private static Deeds CopyDeeds(Deeds d)
        {
            var c = new Deeds();
            foreach (var e in d) c[e.Key] = e.Value;
            return c;
        }

        /// <summary>A pod, deep.</summary>
        public static Pod ClonePod(Pod p)
        {
            return new Pod { Id = p.Id, Active = p.Active, UnitIds = new List<string>(p.UnitIds), Patrol = p.Patrol.Select(CopyPos).ToList(), PatrolIndex = p.PatrolIndex };
        }

        private static RtOrder CloneOrder(RtOrder o)
        {
            switch (o)
            {
                case MoveRtOrder m:
                    return new MoveRtOrder { To = CopyPos(m.To) };
                case AttackRtOrder a:
                    return new AttackRtOrder { Target = a.Target, Ability = a.Ability };
                case WatchRtOrder w:
                    return new WatchRtOrder { Melee = w.Melee };
                case ActRtOrder a:
                    return new ActRtOrder { Ability = a.Ability, Target = a.Target, Tile = CopyPosOrNull(a.Tile) };
                default:
                    return new IdleRtOrder();
            }
        }

        private static MedicalDetail CloneDetail(MedicalDetail d)
        {
            return new MedicalDetail { Kind = d.Kind, X = d.X, Y = d.Y, Z = d.Z, Facing = d.Facing, Height = d.Height, Variant = d.Variant };
        }

        /// <summary>The state is plain JSON by design, so a JSON round-trip is a faithful deep copy.</summary>
        public static GameState CloneState(GameState s)
        {
            var m = s.Mission;
            return new GameState
            {
                Seq = s.Seq,
                Realtime = s.Realtime != null ? new Realtime { Clock = s.Realtime.Clock, Paused = s.Realtime.Paused, Speed = s.Realtime.Speed, Units = s.Realtime.Units.ToDictionary((e) => e.Key, (e) => new RtUnit { Order = CloneOrder(e.Value.Order), Doing = e.Value.Doing, Until = e.Value.Until, Target = e.Value.Target, Ability = e.Value.Ability, Path = CopyList(e.Value.Path, CopyPos), Waited = e.Value.Waited, Round = e.Value.Round }) } : null,
                MapId = s.MapId,
                MapName = s.MapName,
                Biome = s.Biome,
                Site = s.Site,
                Width = s.Width,
                Height = s.Height,
                Tiles = new List<string>(s.Tiles),
                Elev = new List<int>(s.Elev),
                Ground = CopyInts(s.Ground),
                Medical = CopyList(s.Medical, (x) => new MedicalSite { X = x.X, Y = x.Y, W = x.W, H = x.H, Wear = x.Wear, Kind = x.Kind, Rooms = CopyList(x.Rooms, (r) => new MedicalRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = r.Z, Use = r.Use }), Dressing = CopyList(x.Dressing, CloneDetail) }),
                Homes = CopyList(s.Homes, (x) => new HomeSite { X = x.X, Y = x.Y, W = x.W, H = x.H, Wear = x.Wear, Rooms = x.Rooms.Select((r) => new HomeRoom { X = r.X, Y = r.Y, W = r.W, H = r.H, Z = r.Z, Use = r.Use }).ToList(), Dressing = CopyList(x.Dressing, CloneDetail) }),
                Markets = CopyList(s.Markets, (x) => new MarketSite { X = x.X, Y = x.Y, W = x.W, H = x.H, Front = x.Front, Stock = x.Stock }),
                Decor = CopyInts(s.Decor),
                Storeys = CopyInts(s.Storeys),
                Levels = CopyList(s.Levels, (l) => new Level { Tiles = new List<string?>(l.Tiles), Decor = CopyInts(l.Decor) }),
                Lights = CopyList(s.Lights, CopyLight),
                Frame = CopyList(s.Frame, (f) => new FramePiece { Kind = f.Kind, X = f.X, Y = f.Y, W = f.W, H = f.H, Side = f.Side, Storeys = f.Storeys }),
                Units = s.Units.Select(CloneUnit).ToList(),
                Pods = s.Pods.Select(ClonePod).ToList(),
                Mission = new MissionState
                {
                    Kind = m.Kind,
                    TurnsLeft = m.TurnsLeft,
                    Item = m.Item != null ? new MissionItem { Pos = CopyPosOrNull(m.Item.Pos), Carrier = m.Item.Carrier, Evacuated = m.Item.Evacuated } : null,
                    EvacZone = m.EvacZone.Select(CopyVec).ToList(),
                    EvacBus = m.EvacBus != null ? new EvacBus { Tiles = m.EvacBus.Tiles.Select(CopyVec).ToList(), Heading = m.EvacBus.Heading } : null,
                    Terminal = CopyPosOrNull(m.Terminal),
                    ObjectiveUnit = m.ObjectiveUnit,
                    ObjectiveDone = m.ObjectiveDone,
                    EvacDone = m.EvacDone,
                    HackBonus = m.HackBonus,
                    Alarm = m.Alarm,
                    MedicineCures = m.MedicineCures,
                    Quarantine = m.Quarantine,
                    Reinforcements = m.Reinforcements.Select(CopyReinforcement).ToList(),
                    Difficulty = m.Difficulty,
                    Noise = m.Noise,
                    Horde = CopyStrings(m.Horde),
                    Night = m.Night,
                    Siege = m.Siege != null ? new Siege { Hold = m.Siege.Hold } : null,
                },
                Turn = s.Turn,
                ActiveTeam = s.ActiveTeam,
                Concealed = s.Concealed,
                MissStreak = s.MissStreak != null ? (int[])s.MissStreak.Clone() : null,
                Blow = s.Blow != null ? new Blow { Unit = s.Blow.Unit, Kind = s.Blow.Kind, Landed = s.Blow.Landed } : null,
                Ready = (bool[])s.Ready.Clone(),
                Command = s.Command,
                ActiveUnit = s.ActiveUnit,
                EvacCall = s.EvacCall != null ? new EvacCall { By = s.EvacCall.By, Unit = s.EvacCall.Unit } : null,
                AbandonCall = s.AbandonCall != null ? new AbandonCall { By = s.AbandonCall.By } : null,
                Outcome = s.Outcome,
                OutcomeReason = s.OutcomeReason,
                CampaignMission = s.CampaignMission != null ? new CampaignMission { Id = s.CampaignMission.Id, Name = s.CampaignMission.Name, Story = s.CampaignMission.Story, Kicker = s.CampaignMission.Kicker, Item = s.CampaignMission.Item, Briefing = s.CampaignMission.Briefing } : null,
                Smoke = s.Smoke.Select((c) => new SmokeCloud { Pos = CopyPos(c.Pos), Radius = c.Radius, Turns = c.Turns }).ToList(),
                Vehicle = s.Vehicle,
                Snow = s.Snow,
                Searched = CopyInts(s.Searched),
                RoomsSearched = CopyInts(s.RoomsSearched),
                Dropped = CopyList(s.Dropped, (d) => new DroppedLoot { Id = d.Id, Pos = CopyPos(d.Pos), Loot = CloneLoot(d.Loot), From = d.From }),
                DroppedCount = s.DroppedCount,
                Breakables = s.Breakables != null ? new Dictionary<int, int>(s.Breakables) : null,
                Base = s.Base != null ? new SiegeBase { Pieces = s.Base.Pieces.Select((p) => new BasePiece { Id = p.Id, Piece = p.Piece, X = p.X, Y = p.Y, Rot = p.Rot, Size = p.Size != null ? (int[])p.Size.Clone() : null, Hp = p.Hp, Level = p.Level, Charges = p.Charges }).ToList(), Felled = CopyInts(s.Base.Felled), Level = s.Base.Level } : null,
                Traps = CopyList(s.Traps, (t) => new Trap { Piece = t.Piece, Kind = t.Kind, Node = t.Node, Charges = t.Charges }),
                Slow = CopyInts(s.Slow),
            };
        }
    }
}
