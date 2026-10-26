#nullable enable
// rules.ts: the host's rules. Sim runs every change through `emit` (applyEvent, and the event recorded for the
// clients); executeCommand checks a player's order and plays it; the enemy turn runs on from the squad's end of turn.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>Why pods come alive (podActivated), and who was involved.</summary>
    public sealed class Activation
    {
        public string Cause { get; set; } = ""; // PodCauses
        /// <summary>The soldier they saw, or who attacked them.</summary>
        public string? Unit { get; set; }
        /// <summary>The alien that saw the soldier, or that was attacked.</summary>
        public string? By { get; set; }
    }

    /// <summary>Sim.alienBudget: `{ explosives: number; psi: number }`.</summary>
    public sealed class AlienBudget
    {
        public int Explosives { get; set; } = 1;
        public int Psi { get; set; } = 1;
    }

    public sealed class CommandContext
    {
        public int Slot { get; set; } // Slot
        /// <summary>Seats that count for the end of turn; dropped players stop counting after a grace period.</summary>
        public IReadOnlyList<int> Connected { get; set; } = new int[0];
        public Rng Rng { get; set; } = new Rng(0);
    }

    /// <summary>
    /// Mutable simulation over a GameState. Every change goes through `emit`, which
    /// applies the event and records it for broadcasting.
    /// </summary>
    public sealed class Sim
    {
        /// <summary>Reinforcements drop this far (in tiles) from the nearest soldier.</summary>
        private const int DropMin = 7;
        private const int DropMax = 13;
        /// <summary>The horde comes in this close to the map's edge, and at least this far from every soldier (fallbacks after).</summary>
        private const int EdgeBand = 2;
        /// <summary>Where a whole horde has no room that close (a narrow way in through the outline, docs/MAPAS.md §5.10), this close.</summary>
        private const int EdgeDeep = 4;
        private static readonly IReadOnlyList<int> EdgeGap = new[] { 12, 8, 0 };
        /// <summary>Nor this close to the evac zone.</summary>
        private const int EdgeEvacGap = 3;

        /// <summary>What leaves the dead's ways as they were (Sim.ways): neither the ground nor where the living stand changes.</summary>
        private static readonly HashSet<string> KeepsWays = new HashSet<string>
        {
            "abilityUsed", "noise", "forgot", "hunting", "turned", "smashed", "shot", "damaged", "infected", "stunned", "disoriented", "psi", "overwatch", "trapSprung",
        };

        /// <summary>How many more times in an enemy turn those waiting behind their own get to go on (Sim.runAlienTurn).</summary>
        private const int JamPasses = 2;

        /// <summary>The Chillón's scream (docs/FASE2.md): out of 100, this many disorient and the rest panic. It never takes control.</summary>
        private const int ScreamDisorient = 60;

        /// <summary>What an explosion leaves of each kind of tile (null: unaffected).</summary>
        private static readonly IReadOnlyDictionary<string, string> BlastResult = new Dictionary<string, string>
        {
            [TileKinds.Low] = TileKinds.Floor,
            [TileKinds.High] = TileKinds.Low,
            [TileKinds.Door] = TileKinds.DoorOpen,
            [TileKinds.Window] = TileKinds.WindowBroken,
        };

        private static readonly IReadOnlyDictionary<string, string> Exposure = new Dictionary<string, string>
        {
            [PodCauses.Sighted] = ConcealReasons.Spotted,
            [PodCauses.Patrol] = ConcealReasons.Patrol,
            [PodCauses.Attacked] = ConcealReasons.Attack,
            [PodCauses.Noise] = ConcealReasons.Explosive,
        };

        public List<GameEvent> Events { get; } = new List<GameEvent>();
        /// <summary>
        /// Team-wide limits for the alien turn (XCOM 2's global cooldowns): one
        /// explosive and one Mindspin per turn, so a pod can't stack area attacks.
        /// </summary>
        public AlienBudget AlienBudget { get; set; } = new AlienBudget();
        /// <summary>Enemies left waiting behind their own this enemy turn (ai.ts walk): they go again once the others have moved.</summary>
        public HashSet<string> Jammed { get; } = new HashSet<string>();
        /// <summary>
        /// In a siege, where the dead get to from each place, their own aside (ai.ts breachPlan), while nothing changes
        /// that would change it: what stands where (a door gives way), or the living (one falls). Their own moving doesn't.
        /// </summary>
        public Dictionary<int, Reach> Ways { get; } = new Dictionary<int, Reach>();

        public GameState S { get; }
        public Rng Rng { get; }
        public IReadOnlyList<int> Connected { get; }

        public Sim(GameState s, Rng rng, IReadOnlyList<int> connected)
        {
            S = s;
            Rng = rng;
            Connected = connected;
        }

        public void Emit(GameEvent e)
        {
            Engine.Events.ApplyEvent(S, e);
            Events.Add(e);
            if (Ways.Count == 0 || KeepsWays.Contains(e.T)) return;
            var mover = e is MovedEvent moved ? Grid.GetUnit(S, moved.Unit) : null;
            if (!(mover != null && Content.Templates[mover.Template].Undead)) Ways.Clear();
        }

        public bool Over => S.Outcome != null;

        // ------------------------------------------------------------- abilities

        /// <summary>Pays for an ability: actions (all of them if it ends the turn), cooldown and charge.</summary>
        public void Pay(Unit u, string ability)
        {
            var def = Content.Abilities[ability];
            var need = Abilities.ApNeeded(u, ability);
            // In real time nothing counts actions: its time is what an ability costs (realtime.ts).
            var ap = S.Realtime != null ? 0 : ability == AbilityIds.RunAndGun ? -1 : Abilities.EndsTurn(u, ability) ? Math.Max(u.Ap, need) : need;
            Emit(new AbilityUsedEvent { Unit = u.Id, Ability = ability, Ap = ap, Cooldown = def.Cooldown ?? 0, Charge = def.Charge });
        }

        /// <summary>
        /// Runs an ability that has already been validated. Player commands and the
        /// alien AI both come through here, so every rule applies to both sides.
        /// </summary>
        public void Perform(Unit u, string ability, Unit? target = null, Pos? tile = null, MeleeOption? melee = null)
        {
            Pay(u, ability);
            bool Loaded() => Content.LoadedFor(u, AbilityIds.Shoot);
            switch (ability)
            {
                case AbilityIds.Shoot:
                case AbilityIds.Pistol:
                case AbilityIds.AimedShot:
                case AbilityIds.Rupture:
                case AbilityIds.LightningHands:
                    Attack(u, target!, ability);
                    break;
                case AbilityIds.RapidFire:
                    Attack(u, target!, ability);
                    if (u.Alive && target!.Alive && !Over && Loaded()) Attack(u, target, ability);
                    break;
                case AbilityIds.ChainShot:
                {
                    var first = Attack(u, target!, ability);
                    if (first != null && first != ShotResults.Miss && u.Alive && target!.Alive && !Over && Loaded()) Attack(u, target, ability);
                    break;
                }
                case AbilityIds.Faceoff:
                    foreach (var t in Abilities.RangedTargets(S, u, AbilityIds.Faceoff))
                    {
                        if (!u.Alive || Over) break;
                        if (t.Alive) Attack(u, t, AbilityIds.Faceoff);
                    }
                    break;
                case AbilityIds.Slash:
                case AbilityIds.StunLance:
                case AbilityIds.Claw:
                    MeleeAttack(u, melee!, ability);
                    break;
                case AbilityIds.ElbowRoom:
                    // Hacerse sitio (docs/FASE9.md): a Slash at every enemy alongside, without a step.
                    foreach (var t in Abilities.BesideEnemies(S, u))
                    {
                        if (!u.Alive || Over) break;
                        if (t.Alive) MeleeAttack(u, new MeleeOption { Target = t, Tile = u.Pos, Path = new List<Pos>() }, AbilityIds.Slash);
                    }
                    break;
                case AbilityIds.Suppression:
                    Emit(new SuppressedEvent { Unit = u.Id, Target = target!.Id });
                    if (u.Team == Teams.Xcom) BreakConcealment(ConcealReasons.Attack, u.Id, target.Id);
                    ActivatePodOf(target, u);
                    break;
                case AbilityIds.Revival:
                    Emit(new CleansedEvent { Unit = u.Id, Target = target!.Id });
                    break;
                case AbilityIds.PatchUp:
                    Emit(new PatchedEvent { Unit = u.Id, Target = target!.Id, Amount = Math.Min(Content.PatchUp, 100 - Content.ConditionOf(target)) });
                    break;
                case AbilityIds.Restoration:
                    Restoration(u);
                    break;
                case AbilityIds.Grenade:
                case AbilityIds.Launch:
                case AbilityIds.Missiles:
                    Explode(u, tile!, ability);
                    break;
                case AbilityIds.Smoke:
                    Emit(new ExplosiveEvent { Unit = u.Id, Ability = ability, Target = tile! });
                    Emit(new SmokeDeployedEvent { Unit = u.Id, Pos = tile!, Radius = Content.Abilities[AbilityIds.Smoke].Radius ?? 2, Turns = Content.SmokeTurns });
                    break;
                case AbilityIds.Flashbang:
                    Flashbang(u, tile!);
                    break;
                case AbilityIds.FirstAid:
                    // What Curar has grown and the board's cells heal more (docs/FASE10.md); Primeros auxilios and the rest also shelter.
                    // Entre los dos (board.ts): 2 more on the other player's soldiers.
                    Emit(new HealedEvent { Unit = u.Id, Target = target!.Id, Amount = Math.Min(Content.MedkitHeal + u.Mods.Heal + (Rules.Bridged(u, target) ? 2 : 0), target.MaxHp - target.Hp) });
                    if (u.Mods.Aid > target.Aided) Emit(new AidedEvent { Unit = u.Id, Target = target.Id, Amount = u.Mods.Aid });
                    break;
                case AbilityIds.Overwatch:
                case AbilityIds.KillZone:
                    Emit(new OverwatchEvent { Unit = u.Id, KillZone = ability == AbilityIds.KillZone });
                    break;
                case AbilityIds.OverwatchMelee:
                    // With Muro de acero it strikes everyone that comes alongside, once each, as Nadie pasa does with the gun.
                    Emit(new OverwatchEvent { Unit = u.Id, KillZone = Content.HasPerk(u, PerkIds.BladeWall), Melee = true });
                    break;
                case AbilityIds.Stone:
                    Stone(u, tile!);
                    break;
                case AbilityIds.Hunker:
                    Emit(new HunkerEvent { Unit = u.Id });
                    break;
                case AbilityIds.Reload:
                    Emit(new ReloadEvent { Unit = u.Id });
                    break;
                case AbilityIds.Hack:
                    Hack(u);
                    break;
                case AbilityIds.Search:
                    Search(u, tile!);
                    break;
                case AbilityIds.SearchRoom:
                    SearchRoom(u);
                    break;
                case AbilityIds.OpenDoor:
                case AbilityIds.CloseDoor:
                    Emit(new TileChangedEvent { Pos = Engine.Events.CopyPos(tile!), Kind = ability == AbilityIds.OpenDoor ? TileKinds.DoorOpen : TileKinds.Door });
                    break;
                case AbilityIds.RunAndGun:
                    break;
                case AbilityIds.Mindspin:
                    Psionic(u, target!);
                    break;
                case AbilityIds.Reanimate:
                    Reanimate(u, target!);
                    break;
                case AbilityIds.Burst:
                    Burst(u);
                    break;
                case AbilityIds.Smash:
                    Smash(u, tile!);
                    break;
            }
            // Then it is heard (docs/FASE2.md): where a tile ability lands, or where the user stands.
            // Whoever fell doing it made no more noise than what already sounded (the Hinchado sounds as it bursts).
            if (u.Alive) MakeNoise(tile ?? u.Pos, Abilities.NoiseOf(S, u, ability), u);
        }

        /// <summary>
        /// One of the dead beats on a door or a barricade (docs/FASE12.md §4): its
        /// blow's damage off what it still holds. At nothing it gives way, and the
        /// way in is open ground.
        /// </summary>
        private void Smash(Unit u, Pos tile)
        {
            var left = Abilities.HoldsAt(S, Grid.NodeOf(S, tile));
            var amount = Math.Min(left, Content.AttackWeapon(u, AbilityIds.Claw).Damage);
            Emit(new SmashedEvent { Unit = u.Id, Tile = Engine.Events.CopyPos(tile), Amount = amount, Left = left - amount });
            if (left - amount <= 0) Emit(new TileChangedEvent { Pos = Engine.Events.CopyPos(tile), Kind = TileKinds.Floor });
        }

        // ---------------------------------------------------------------- combat

        /// <summary>
        /// One shot (or blade strike): roll, damage and its side effects. Covering
        /// Fire overwatchers react before the attack goes off.
        /// </summary>
        public string? Attack(Unit attacker, Unit target, string ability, bool reaction = false)
        {
            if (!reaction) CoveringFire(attacker);
            if (!attacker.Alive || !target.Alive || Over) return null;
            var preview = Aim.PreviewShot(S, attacker, target, new ShotOptions { Ability = ability, Reaction = reaction });
            if (preview == null) return null;
            var shot = Aim.ResolveShot(preview, Rng);
            var result = shot.Result;
            Emit(new ShotEvent
            {
                Unit = attacker.Id,
                Target = target.Id,
                Ability = ability,
                From = preview.Origin,
                Result = result,
                Chance = preview.Hit,
                Roll = shot.Roll,
                Reaction = reaction,
            });
            var weapon = Content.AttackWeapon(attacker, ability);
            var primary = (Content.Abilities[ability].Weapon ?? AbilityWeapons.Primary) == AbilityWeapons.Primary && !weapon.Melee;
            if (primary && Content.HasPerk(attacker, PerkIds.HoloTargeting) && target.Alive) Emit(new MarkedEvent { Unit = target.Id, Amount = Content.HoloAim + attacker.Mods.HoloAim });
            if (result != ShotResults.Miss)
            {
                var dm = Aim.ShotDamageModsOf(attacker, ability, preview.Flanked, preview.Distance);
                var before = target.Hp;
                Damage(target, Aim.RollDamage(weapon, result, Rng, dm.Bonus, dm.Mult, dm.CritBonus), dm.Shred, attacker, dm.Pierce);
                // A zombie's bite that draws blood may infect.
                if (weapon.Melee && Content.Templates[attacker.Template].Undead && target.Hp < before) Infect(target, attacker, Content.Infection.Bite.TryGetValue(attacker.Template, out var bite) ? bite : 0);
                if (ability == AbilityIds.Rupture && target.Alive) Emit(new RupturedEvent { Unit = target.Id, Amount = Content.RuptureDamage });
                if (!target.Alive && !reaction) AfterKill(attacker, target, ability, preview.Origin);
                // Segundo aliento: a blade kill heals 1.
                if (!target.Alive && weapon.Melee && Content.HasPerk(attacker, PerkIds.SecondWind) && attacker.Alive && attacker.Hp < attacker.MaxHp)
                {
                    Emit(new HealedEvent { Unit = attacker.Id, Target = attacker.Id, Amount = 1 });
                }
            }
            if (attacker.Team == Teams.Xcom && string.IsNullOrEmpty(attacker.ControlledBy)) BreakConcealment(ConcealReasons.Attack, attacker.Id, target.Id);
            if (target.Team == Teams.Alien) ActivatePodOf(target, attacker);
            CheckOutcome();
            // Shots on someone else's move (overwatch, suppression, covering fire) are heard on their own; the rest with their ability.
            if (reaction && attacker.Alive) MakeNoise(preview.Origin, weapon.Noise, attacker);
            return result;
        }

        /// <summary>Implacable and Death From Above give an action back after a kill, once per turn.</summary>
        private void AfterKill(Unit killer, Unit victim, string ability, Pos from)
        {
            if (killer.Refunded || !killer.Alive) return;
            var melee = Content.AttackWeapon(killer, ability).Melee;
            if (melee && Content.HasPerk(killer, PerkIds.Implacable)) Emit(new RefundEvent { Unit = killer.Id, Reason = RefundReasons.Implacable });
            else if (!melee && Content.HasPerk(killer, PerkIds.DeathFromAbove) && Grid.HeightAt(S, from) > Grid.HeightAt(S, victim.Pos))
            {
                Emit(new RefundEvent { Unit = killer.Id, Reason = RefundReasons.DeathFromAbove });
            }
        }

        /// <summary>Covering Fire: overwatchers with the perk shoot at anyone who attacks.</summary>
        private void CoveringFire(Unit attacker)
        {
            var watchers = S.Units.Where((o) =>
                o.Alive &&
                Content.HostileTo(o, attacker) &&
                o.Overwatch &&
                Content.HasPerk(o, PerkIds.CoveringFire) &&
                !(o.KillZone && o.ReactedTo.Contains(attacker.Id)) &&
                Content.LoadedFor(o, AbilityIds.Shoot) &&
                Los.CanSee(S, o, attacker)).ToList();
            foreach (var o in watchers)
            {
                if (!attacker.Alive || Over) break;
                Attack(o, attacker, AbilityIds.Shoot, true);
            }
        }

        /// <summary>
        /// Armour absorbs up to its value per hit (unless the attack pierces it), then
        /// shred wears it down. Rupture adds to every hit.
        /// </summary>
        public void Damage(Unit u, int raw, int shred, Unit? source = null, int pierce = 0)
        {
            if (!u.Alive) return;
            var taken = Aim.DamageTakenBy(u, raw, pierce);
            Emit(new DamagedEvent { Unit = u.Id, Amount = taken.Total - taken.Mitigated, Mitigated = taken.Mitigated, Shred = Math.Min(shred, u.Armor) });
            if (u.Hp > 0) return;
            // Off a flight of stairs, at its nearer end (flights.ts): somewhere one can stop to pick it up.
            if (S.Mission.Item?.Carrier == u.Id) Emit(new ItemDroppedEvent { Pos = Flights.RestingPlace(S, u.Pos) });
            Emit(new DiedEvent { Unit = u.Id, By = source?.Id });
            // A patrol car's or an ambulance's siren silenced: the car stays, over its tile as over its nose's (19/10), solid. Without
            // an alarm said (a skirmish, older saves), a car but in a facility, as the client draws it (unitViews.ts ALARM_BY_MAP).
            var alarm = S.Mission.Alarm;
            var car = !string.IsNullOrEmpty(alarm) ? alarm != Alarms.Factory : S.Biome != Biomes.Facility;
            if (u.Objective && S.Mission.Kind == MissionKinds.Sabotage && car && Grid.CanStandOn(Grid.KindAt(S, u.Pos)))
            {
                // Its tile a part of the car beside it (docs/MAPAS.md, phase 1: the rest of its footprint), facing it; without one, the car's own.
                var decor = u.Pos.Z != 0 ? Grid.LevelOf(S, u.Pos.Z - 1)?.Decor : S.Decor;
                var toCar = -1;
                for (var k = 0; k < Grid.Dirs4.Count; k++)
                {
                    var d = Grid.Dirs4[k];
                    var x = u.Pos.X + d.X;
                    var y = u.Pos.Y + d.Y;
                    if (decor != null && Grid.InBounds(S, x, y) && Decor.ObjectAt(decor, S.Width, Grid.TileIndex(S, x, y))?.Kind == DecorKinds.SirenCar)
                    {
                        toCar = k;
                        break;
                    }
                }
                Emit(toCar >= 0
                    ? new TileChangedEvent { Pos = Engine.Events.CopyPos(u.Pos), Kind = TileKinds.High, Decor = DecorKinds.Part, Facing = toCar }
                    : new TileChangedEvent { Pos = Engine.Events.CopyPos(u.Pos), Kind = TileKinds.High, Decor = DecorKinds.SirenCar });
            }
            // A sectoid's death frees the minds it held.
            for (var i = 0; i < S.Units.Count; i++)
            {
                var v = S.Units[i];
                if (v.Alive && v.ControlledBy == u.Id) Emit(new ReleasedEvent { Unit = v.Id });
            }
            if (Content.Templates[u.Template].Abilities.Contains(AbilityIds.Burst)) Burst(u);
        }

        /// <summary>
        /// The Hinchado bursts where it stands (docs/FASE2.md), when it dies or of its
        /// own will beside a soldier: it is gone, and everyone beside it takes the blast.
        /// </summary>
        public void Burst(Unit u)
        {
            if (u.Alive) Emit(new DiedEvent { Unit = u.Id });
            Explode(u, u.Pos, AbilityIds.Burst);
            MakeNoise(u.Pos, Content.Weapons[WeaponIds.BloaterBurst].Noise, u);
        }

        /// <summary>
        /// A stone (docs/FASE10.md §4): heard where it lands (perform makes the
        /// noise), and whoever stands there takes its damage. Only a hit gives the
        /// squad away.
        /// </summary>
        private void Stone(Unit u, Pos at)
        {
            var hit = Grid.UnitAt(S, at);
            var struck = hit != null && hit.Alive && hit.Id != u.Id && !hit.Objective ? hit : null;
            Emit(new StoneThrownEvent { Unit = u.Id, Target = Engine.Events.CopyPos(at), Hit = struck?.Id });
            if (struck == null) return;
            Damage(struck, Content.StoneDamage + u.Mods.StoneDamage, 0, u);
            if (u.Team == Teams.Xcom) BreakConcealment(ConcealReasons.Attack, u.Id, struck.Id);
            if (struck.Team == Teams.Alien) ActivatePodOf(struck, u);
            CheckOutcome();
        }

        public void MeleeAttack(Unit u, MeleeOption option, string ability)
        {
            if (option.Path.Count > 0) MoveAlong(u, option.Path, 0);
            var target = Grid.GetUnit(S, option.Target.Id);
            if (!u.Alive || target == null || !target.Alive || Over || !Aim.InMeleeReach(S, u.Pos, target.Pos)) return;
            var result = Attack(u, target, ability);
            if (ability == AbilityIds.StunLance && result != null && result != ShotResults.Miss && target.Alive) Emit(new StunnedEvent { Unit = target.Id });
            // Golpe al oído (board.ts): a blade that strikes leaves the enemy reeling.
            if (ability == AbilityIds.Slash && u.Mods.SlashDisorient != 0 && result != null && result != ShotResults.Miss && target.Alive && !Content.Templates[target.Template].Robotic) Emit(new DisorientedEvent { Unit = target.Id });
        }

        public void Explode(Unit source, Pos center, string ability)
        {
            // The Hinchado's burst is its death: nobody gets to fire first, and it goes off without it.
            var bursting = ability == AbilityIds.Burst;
            if (!bursting) CoveringFire(source);
            if ((!source.Alive && !bursting) || Over) return;
            var weapon = Content.Weapons[Content.Abilities[ability].Explosive!];
            Emit(new ExplosiveEvent { Unit = source.Id, Ability = ability, Target = center });
            var tiles = Abilities.BlastTiles(S, ability, center, source);
            var grenade = ability == AbilityIds.Grenade || ability == AbilityIds.Launch;
            // Controlled detonation: the grenadier's own squad is left out of the blast.
            bool Spared(Unit u) => grenade && Content.HasPerk(source, PerkIds.SafeBlast) && u.Team == source.Team;
            var victims = tiles.Select((p) => Grid.UnitAt(S, p)).Where((u) => u != null && !Spared(u)).Select((u) => u!).ToList();
            var mods = Rules.BlastMods(source, ability);
            foreach (var v in victims)
            {
                var before = v.Hp;
                Damage(v, Aim.RollDamage(weapon, ShotResults.Hit, Rng, mods.Bonus), mods.Shred, source);
                if (bursting && v.Hp < before) Infect(v, source, Content.Infection.Burst);
            }
            // The Hinchado's burst is only gas: it leaves the scenery alone.
            if (!bursting)
            {
                // An object over several tiles goes as one (a car, a bed, a lorry): all of it as the tile the blast reached.
                var done = new HashSet<int>();
                foreach (var p in tiles)
                {
                    var kind = Grid.KindAt(S, p);
                    foreach (var q in kind == TileKinds.Low || kind == TileKinds.High ? Rules.ObjectPlaces(S, p) : new List<Pos> { p })
                    {
                        var node = Grid.NodeOf(S, q);
                        if (done.Contains(node)) continue;
                        done.Add(node);
                        var now = Grid.KindAt(S, q);
                        if (now != null && BlastResult.TryGetValue(now, out var next)) Emit(new TileChangedEvent { Pos = q, Kind = next });
                    }
                }
                // The frag grenades breach the walls they reach (docs/FASE7.md §4): rubble, low cover, on that floor only.
                foreach (var p in Abilities.BreachedWalls(S, ability, center, source)) Emit(new TileChangedEvent { Pos = p, Kind = TileKinds.Low, Decor = DecorKinds.Breach });
                // Upstairs, the slab it went off on gives way (docs/FASE7.md §5).
                if (Abilities.BreaksFloor(S, ability, center)) BreakFloor(center);
            }
            if (source.Team == Teams.Xcom) BreakConcealment(ConcealReasons.Explosive, source.Id);
            // Pods caught in the blast were attacked; the rest only hear it (the noise, after the action).
            var hit = new List<string>();
            foreach (var v in victims) if (!string.IsNullOrEmpty(v.PodId) && !hit.Contains(v.PodId!)) hit.Add(v.PodId!);
            ActivatePods(hit, new Activation { Cause = PodCauses.Attacked, Unit = source.Team == Teams.Xcom ? source.Id : null });
            CheckOutcome();
        }

        /// <summary>
        /// A grenade breaks the slab of storey place `p` (docs/FASE7.md §5): whoever
        /// stands on it falls to the floor below (the nearest free place to stand
        /// there), with FALL_DAMAGE a storey through the armour, and the objective
        /// lying on it drops too. If someone would have nowhere to land, the floor
        /// holds.
        /// </summary>
        private void BreakFloor(Pos p)
        {
            var taken = new HashSet<int>();
            Pos? Land()
            {
                var to = Rules.LandingBelow(S, p, taken);
                if (to != null) taken.Add(Grid.NodeOf(S, to));
                return to;
            }
            var falling = S.Units.Where((u) => u.Alive && Grid.SamePos(u.Pos, p)).ToList().Select((u) => (u, to: Land())).ToList();
            var item = S.Mission.Item;
            var onIt = item != null && string.IsNullOrEmpty(item.Carrier) && item.Pos != null && Grid.SamePos(Grid.ToPos(item.Pos), p);
            var lying = onIt ? Land() : null;
            if (falling.Any((f) => f.to == null) || (onIt && lying == null)) return;
            Emit(new FloorBrokenEvent { Pos = Engine.Events.CopyPos(p) });
            if (lying != null) Emit(new ItemDroppedEvent { Pos = lying });
            foreach (var (u, to) in falling)
            {
                Emit(new FellEvent { Unit = u.Id, From = Engine.Events.CopyPos(p), To = to! });
                Damage(u, Content.FallDamage * Math.Max(1, p.Z - Grid.HeightAt(S, to!)), 0, null, u.Armor);
            }
        }

        /// <summary>Disorients every enemy in the area; no damage, the scenery is untouched.</summary>
        private void Flashbang(Unit u, Pos center)
        {
            Emit(new ExplosiveEvent { Unit = u.Id, Ability = AbilityIds.Flashbang, Target = center });
            var hit = Abilities.BlastTiles(S, AbilityIds.Flashbang, center, u)
                .Select((p) => Grid.UnitAt(S, p))
                .Where((v) => v != null && v.Team != u.Team && !Content.Templates[v.Template].Robotic && !v.Objective)
                .Select((v) => v!)
                .ToList();
            foreach (var v in hit) Emit(new DisorientedEvent { Unit = v.Id });
            if (u.Team == Teams.Xcom) BreakConcealment(ConcealReasons.Explosive, u.Id);
            var pods = new List<string>();
            foreach (var v in hit) if (!string.IsNullOrEmpty(v.PodId) && !pods.Contains(v.PodId!)) pods.Add(v.PodId!);
            ActivatePods(pods, new Activation { Cause = PodCauses.Attacked, Unit = u.Id, By = hit.Count > 0 ? hit[0].Id : null });
        }

        private void Restoration(Unit u)
        {
            var range = Content.Abilities[AbilityIds.Restoration].Range ?? 0;
            for (var i = 0; i < S.Units.Count; i++)
            {
                var a = S.Units[i];
                if (!a.Alive || a.Team != u.Team || a.Captive || Grid.Distance(a.Pos, u.Pos) > range || !Grid.SameSpace(S, a.Pos, u.Pos)) continue;
                var heal = Math.Min(Content.RestorationHeal, a.MaxHp - a.Hp);
                if (heal > 0) Emit(new HealedEvent { Unit = u.Id, Target = a.Id, Amount = heal });
                if (Abilities.HasBadStatus(a)) Emit(new CleansedEvent { Unit = u.Id, Target = a.Id });
            }
        }

        /// <summary>Mindspin: will resists; a hit disorients, panics or (rarely) takes control.</summary>
        private void Psionic(Unit caster, Unit target)
        {
            CoveringFire(caster);
            if (!caster.Alive || !target.Alive || Over) return;
            var chance = Abilities.PsiChance(target, S);
            var success = Rng.Int(100) < chance;
            string? effect = null;
            if (success)
            {
                // The scream confuses or frightens; it never takes control (docs/FASE2.md).
                effect = Rng.Int(100) < ScreamDisorient ? PsiEffects.Disoriented : PsiEffects.Panicked;
                // «Nervios de acero» keeps panic out («Serenidad» is out of its reach: psiChance).
                if (effect == PsiEffects.Panicked && target.Mods.PsiGuard >= 1) effect = PsiEffects.Disoriented;
            }
            Emit(new PsiEvent { Unit = caster.Id, Target = target.Id, Success = success, Effect = effect, Chance = chance });
            CheckOutcome();
        }

        /// <summary>The corpse gets up where it lies, or beside it if somebody stands there (riseSpot): never on top of anyone.</summary>
        private void Reanimate(Unit caster, Unit corpse)
        {
            var at = Abilities.RiseSpot(S, corpse);
            if (at == null) return;
            var zombie = Rules.RisenUnit(ZombieId(), corpse, at, caster.PodId);
            Emit(new ReanimatedEvent { Unit = caster.Id, Corpse = corpse.Id, Zombie = zombie });
        }

        /// <summary>A fresh id for a zombie that rises (`z` and a number no unit has used).</summary>
        private string ZombieId()
        {
            return $"z{S.Units.Count((x) => x.Template == TemplateIds.Zombie) + 1}";
        }

        /// <summary>
        /// A bite (or a Hinchado's burst) that drew blood infects with `chance` out
        /// of 100, a Chaleco antibalas takes INFECTION.vest off (docs/FASE3.md). Only
        /// the squad's flesh and blood; once infected, it stays so.
        /// </summary>
        private void Infect(Unit u, Unit by, int chance)
        {
            var t = Content.Templates[u.Template];
            if (u.Infected == true || chance <= 0 || t.Team != Teams.Xcom || t.Robotic || t.Undead || u.Objective) return;
            var vest = u.Gear.Contains(ItemIds.NanoVest) ? Content.Infection.Vest : 0;
            if (Rng.Int(100) < chance - vest) Emit(new InfectedEvent { Unit = u.Id, By = by.Id });
        }

        /// <summary>
        /// The infected fallen rise as the zombies' turn begins (docs/FASE3.md), as
        /// the Chillón's corpses do but by themselves: each a group of its own, alert.
        /// One whose tile is taken rises beside it (riseSpot), or waits for a free one.
        /// </summary>
        private void RiseInfected()
        {
            foreach (var corpse in S.Units.Where((x) => !x.Alive && !x.Evacuated && x.Infected == true && !x.Reanimated).ToList())
            {
                if (Over) return;
                var at = Abilities.RiseSpot(S, corpse);
                if (at == null) continue;
                var zombie = Rules.RisenUnit(ZombieId(), corpse, at, null);
                var pod = new Pod { Id = $"p{zombie.Id}", Active = true, UnitIds = new List<string>(), Patrol = new List<Pos>(), PatrolIndex = 0 };
                zombie.PodId = pod.Id;
                Emit(new ReanimatedEvent { Corpse = corpse.Id, Zombie = zombie, Pod = pod });
            }
        }

        /// <summary>Opens the loot box on `tile` (docs/FASE8.md §B): always what it is marked for.</summary>
        private void Search(Unit u, Pos tile)
        {
            var box = Looting.SearchSpot(S, tile);
            var loot = box != null ? Looting.RollBox(box, Rng, u.Mags != null) : null;
            Emit(new SearchedEvent { Unit = u.Id, Tile = Engine.Events.CopyPos(tile), Loot = loot });
        }

        /// <summary>Searches the room `u` stands in, once for the squad (docs/FASE8.md §B).</summary>
        private void SearchRoom(Unit u)
        {
            var room = Looting.RoomAt(S, u.Pos);
            if (room == null) return;
            var loot = Looting.RollRoom(room, S.Mission.Difficulty ?? 2, Rng, S.Mission.Night == true, u.Mags != null, Looting.MarketStock(S, room));
            Emit(new RoomSearchedEvent { Unit = u.Id, Room = room.Id, Loot = loot });
        }

        private void Hack(Unit u)
        {
            var chance = Abilities.HackChance(S, u);
            var success = Rng.Int(100) < chance;
            Emit(new HackedEvent { Unit = u.Id, Success = success, Chance = chance });
            if (!success) RaiseAlarm(u);
            else CheckOutcome();
        }

        // ------------------------------------------------------------- movement

        /// <summary>
        /// Walks the path tile by tile. Enemy overwatch, pod detection and picking up
        /// the objective interrupt the walk, so a move may be emitted as several
        /// `moved` segments. Nobody walks through anyone (Arturo, 19/10): one who came
        /// into the way since the walk was planned (a pod scampering to cover) stops
        /// it short of them, never in a window's frame nor on a flight of stairs
        /// (flights.ts: nobody stops there, docs/MAPAS.md phase 2).
        /// </summary>
        public void MoveAlong(Unit u, List<Pos> path, int ap)
        {
            var segment = new List<Pos>();
            var pendingAp = ap;
            void Flush()
            {
                if (segment.Count == 0) return;
                Emit(new MovedEvent { Unit = u.Id, Path = segment, Ap = pendingAp });
                pendingAp = 0;
                segment = new List<Pos>();
            }

            var origin = Engine.Events.CopyPos(u.Pos);
            var patrolling = Paths.OnPatrol(S, u);
            // Who a patrol walking up or down a flight of stairs saw: it stops once off it.
            Unit? sighted = null;
            for (var i = 0; i < path.Count; i++)
            {
                var step = path[i];
                var blocker = Grid.UnitAt(S, step);
                if (blocker != null && blocker != u)
                {
                    Flush();
                    // Halted in a window's frame or on a flight of stairs, where nobody stands: back the way it came, to the last
                    // place it could have stopped on.
                    if (u.Alive && !Flights.CanStopAt(S, u.Pos))
                    {
                        var back = new List<Pos>();
                        for (var j = i - 2; j >= -1; j--)
                        {
                            var q = j >= 0 ? path[j] : origin;
                            back.Add(Engine.Events.CopyPos(q));
                            if (Flights.CanStopAt(S, q)) break;
                        }
                        var end = back.Count > 0 ? back[back.Count - 1] : null;
                        if (end != null && Flights.CanStopAt(S, end) && Grid.UnitAt(S, end) == null) Emit(new MovedEvent { Unit = u.Id, Path = back, Ap = 0 });
                    }
                    return;
                }

                // Doors swing open and windows shatter before the unit steps in (19/10: it walked into the shut door, then the leaf
                // swung through it): the walk so far, the door, then on through it.
                var kind = Grid.KindAt(S, step);
                if (kind == TileKinds.Door || kind == TileKinds.Window)
                {
                    Flush();
                    Emit(new TileChangedEvent { Pos = step, Kind = kind == TileKinds.Door ? TileKinds.DoorOpen : TileKinds.WindowBroken });
                    MakeNoise(step, kind == TileKinds.Door ? Content.Noise.Door : Content.Noise.Breach, u);
                }
                segment.Add(step);

                // A trap of the base under one of the dead (docs/FASE12.md §11.4): it stops there.
                if (u.Team == Teams.Alien && SpringTrap(u, step, Flush)) return;

                var item = S.Mission.Item;
                if (u.Team == Teams.Xcom && item != null && string.IsNullOrEmpty(item.Carrier) && item.Pos != null && Grid.SamePos(item.Pos, step))
                {
                    Flush();
                    Emit(new ItemPickedEvent { Unit = u.Id });
                }
                if (u.Team == Teams.Xcom && !u.Captive)
                {
                    // Beside the VIP, on its level (a floor between them keeps them apart).
                    var vip = S.Units.Find((o) => o.Alive && o.Captive && Aim.Adjacent(o.Pos, step) && o.Pos.Z == step.Z);
                    if (vip != null)
                    {
                        Flush();
                        Emit(new VipRescuedEvent { Unit = vip.Id, By = u.Id, Owner = u.Owner });
                    }
                }
                // A patrolling pod that walks into sight of the squad stops and takes cover; on a flight of stairs, once off it.
                var spotted = sighted ?? (patrolling ? SpottedFrom(u, step, Los.FacingTo(i != 0 ? path[i - 1] : u.Pos, step) ?? u.Facing) : null);
                if (spotted != null && !Flights.CanStopAt(S, step)) sighted = spotted;
                else if (spotted != null)
                {
                    Flush();
                    ActivatePods(new List<string> { u.PodId! }, new Activation { Cause = PodCauses.Patrol, Unit = spotted.Id, By = u.Id });
                    return;
                }

                // Shadowstep: nobody gets a reaction shot at this unit.
                var shadow = Content.HasPerk(u, PerkIds.Shadowstep);
                bool Loaded(Unit o) => Content.LoadedFor(o, AbilityIds.Shoot);
                // Overwatch with the blade (docs/FASE10.md §4) strikes whoever comes alongside; the gun's, whoever moves in sight.
                var reactors = shadow
                    ? new List<Unit>()
                    : S.Units.Where((o) =>
                        o.Alive &&
                        Content.HostileTo(o, u) &&
                        o.Overwatch &&
                        !(o.KillZone && o.ReactedTo.Contains(u.Id)) &&
                        (o.WatchMelee == true ? Aim.InMeleeReach(S, o.Pos, step) : Loaded(o) && Watches(o, step))).ToList();
                var suppressors = shadow ? new List<Unit>() : S.Units.Where((o) => o.Alive && Content.HostileTo(o, u) && o.Suppressing == u.Id && Loaded(o)).ToList();
                var noticing = u.Team == Teams.Xcom ? PodsNoticing(step) : new List<string>();
                if (reactors.Count == 0 && suppressors.Count == 0 && noticing.Count == 0) continue;

                Flush();
                foreach (var r in reactors)
                {
                    if (!u.Alive || Over) break;
                    if (r.Alive && r.Overwatch) Attack(r, u, r.WatchMelee == true ? AbilityIds.Slash : AbilityIds.Shoot, true);
                }
                foreach (var r in suppressors)
                {
                    if (!u.Alive || Over) break;
                    if (!r.Alive || r.Suppressing != u.Id) continue;
                    Attack(r, u, AbilityIds.Shoot, true);
                    Emit(new SuppressionEndedEvent { Unit = r.Id });
                }
                if (noticing.Count > 0 && !Over) ActivatePods(noticing, Sighting(noticing, u, step));
                if (!u.Alive || Over) return;
            }
            Flush();
            if (u.Team == Teams.Xcom && u.Alive && !Over) HuntedBy(u);
        }

        /// <summary>
        /// The dead awake that see `u` where it is go after it (Unit.hunting, ai.ts takeZombieTurn: Arturo, 22/10), even
        /// if it is out of their sight again before their turn.
        /// </summary>
        public void HuntedBy(Unit u)
        {
            for (var i = 0; i < S.Units.Count; i++)
            {
                var o = S.Units[i];
                if (!o.Alive || !Content.Templates[o.Template].Undead || !Content.HostileTo(o, u)) continue;
                if (!string.IsNullOrEmpty(o.PodId) && !S.Pods.Any((p) => p.Id == o.PodId && p.Active)) continue;
                if (o.Hunting?.Id == u.Id && o.Hunting.Missed == 0) continue;
                if (Los.CanSee(S, o, u)) Emit(new HuntingEvent { Unit = o.Id, Target = u.Id, Missed = 0 });
            }
        }

        /// <summary>
        /// One of the dead steps on a trap that is set (docs/FASE12.md §11.4): a mine
        /// blows up whoever is round it, the spikes hurt, a snare holds it (one action
        /// less next turn). It stops on the tile, if it is still standing; true when
        /// the walk ends there.
        /// </summary>
        private bool SpringTrap(Unit u, Pos step, Action flush)
        {
            var node = Grid.NodeOf(S, step);
            var trap = S.Traps?.Find((t) => t.Node == node && t.Charges > 0);
            if (trap == null) return false;
            flush();
            Emit(new TrapSprungEvent { Trap = trap.Piece, Unit = u.Id });
            if (trap.Kind == TrapKinds.Mine)
            {
                MakeNoise(step, Content.Weapons[WeaponIds.FragGrenade].Noise);
                foreach (var v in S.Units.Where((o) => o.Alive && Grid.Distance(o.Pos, step) <= Content.Traps.MineRadius && Grid.SameSpace(S, o.Pos, step)).ToList()) Damage(v, Content.Traps.Mine, 0);
            }
            else if (trap.Kind == TrapKinds.Spikes) Damage(u, Content.Traps.Spikes, 0);
            else if (u.Alive) Emit(new StunnedEvent { Unit = u.Id });
            CheckOutcome();
            return true;
        }

        /// <summary>Whether an overwatcher covers `step`: with its own sight.</summary>
        private bool Watches(Unit o, Pos step)
        {
            return Los.SightOrigin(S, o.Pos, step, Los.SightTo(S, o, step)) != null;
        }

        // ------------------------------------------------------- pods & stealth

        /// <summary>Whether a patrolling alien standing on `p` spots any soldier (detection range while the squad is concealed).</summary>
        public bool PodSpotsSquad(Unit u, Pos p)
        {
            return SpottedFrom(u, p) != null;
        }

        /// <summary>How far `u` notices a soldier on `p`: its detection while the squad is hidden, its sight after (both shorter at night, unless lit).</summary>
        private double NoticeRange(Unit u, Pos p)
        {
            return S.Concealed ? Los.DetectionTo(S, u, p) : Los.SightTo(S, u, p);
        }

        /// <summary>The first soldier an alien standing on `p` would spot (`facing`: the dead see ahead of them, the way they walk).</summary>
        public Unit? SpottedFrom(Unit u, Pos p, double? facing = null)
        {
            var face = facing ?? u.Facing;
            return Grid.Combatants(S, Teams.Xcom).Find((soldier) => Los.InViewCone(u, soldier.Pos, p, face) && Los.SightOrigin(S, p, soldier.Pos, NoticeRange(u, soldier.Pos)) != null);
        }

        /// <summary>The member of `pod` that would notice an XCOM soldier standing on `p`, if any.</summary>
        private Unit? Noticer(Pod pod, Pos p)
        {
            foreach (var id in pod.UnitIds)
            {
                var m = Grid.GetUnit(S, id);
                if (m == null || !m.Alive) continue;
                if (Los.InViewCone(m, p) && Los.SightOrigin(S, m.Pos, p, NoticeRange(m, p)) != null) return m;
            }
            return null;
        }

        /// <summary>Inactive pods that would notice an XCOM soldier standing on `p`.</summary>
        public List<string> PodsNoticing(Pos p)
        {
            return S.Pods.Where((pod) => !pod.Active && Noticer(pod, p) != null).Select((pod) => pod.Id).ToList();
        }

        /// <summary>`soldier`, standing on `p`, has been seen by these pods: by whom, for the players.</summary>
        private Activation Sighting(List<string> podIds, Unit soldier, Pos p)
        {
            var pod = S.Pods.Find((x) => podIds.Contains(x.Id));
            return new Activation { Cause = PodCauses.Sighted, Unit = soldier.Id, By = pod != null ? Noticer(pod, p)?.Id : null };
        }

        /// <summary>What gave the squad away (an Exposure: a concealmentBroken event without its tag).</summary>
        public void BreakConcealment(string reason, string? unit = null, string? by = null, double? range = null)
        {
            if (S.Concealed) Emit(new ConcealmentBrokenEvent { Reason = reason, Unit = unit, By = by, Range = range });
        }

        // ----------------------------------------------------------------- noise

        /// <summary>
        /// A noise of `radius` tiles at `tile` (docs/FASE2.md): every enemy within it
        /// that isn't already fighting a soldier in sight remembers the spot and goes
        /// to look, inactive pods too, without waking up. It never breaks concealment.
        /// The one who made it, and its pod, know what it was. What the squad does is heard SQUAD_LOUDER farther (Ana, 23/10).
        /// </summary>
        public void MakeNoise(Pos tile, double radius, Unit? by = null)
        {
            if (radius <= 0 || Over) return;
            if (by?.Team == Teams.Xcom) radius = Js.Round(radius * Rules.SquadLouder * 10) / 10;
            var heard = S.Units.Where((o) =>
                o.Alive &&
                o.Team == Teams.Alien &&
                !o.Objective &&
                string.IsNullOrEmpty(o.ControlledBy) &&
                o.Id != by?.Id &&
                !(!string.IsNullOrEmpty(by?.PodId) && o.PodId == by!.PodId) &&
                // A storey between them is one more tile to carry (decision 3 in FASE6-PLAN.md).
                Grid.Distance(o.Pos, tile) + Grid.StoreysApart(S, o.Pos, tile) <= radius &&
                !Engaged(o)).Select((o) => o.Id).ToList();
            Emit(new NoiseEvent { Tile = Engine.Events.CopyPos(tile), Radius = radius, By = by?.Id, Heard = heard });
        }

        /// <summary>An alert enemy with a soldier in sight: a noise elsewhere doesn't turn its head.</summary>
        private bool Engaged(Unit o)
        {
            if (!S.Pods.Any((p) => p.Id == o.PodId && p.Active)) return false;
            return Grid.Combatants(S, Teams.Xcom).Any((e) => Los.CanSee(S, o, e));
        }

        /// <summary>`u` was attacked (by `attacker`): its pod comes alive.</summary>
        public void ActivatePodOf(Unit u, Unit? attacker = null)
        {
            if (!string.IsNullOrEmpty(u.PodId)) ActivatePods(new List<string> { u.PodId! }, new Activation { Cause = PodCauses.Attacked, Unit = attacker?.Team == Teams.Xcom ? attacker.Id : null, By = u.Id });
        }

        /// <summary>
        /// Activates pods and lets every member "scamper" to cover, XCOM 2 style. If
        /// that gives the squad away, the reason goes out first, then the contact.
        /// </summary>
        public void ActivatePods(IReadOnlyList<string> podIds, Activation why)
        {
            ActivatePods(podIds, (pod) => why);
        }

        public void ActivatePods(IReadOnlyList<string> podIds, Func<Pod, Activation> whyOf)
        {
            var pods = S.Pods.Where((p) => podIds.Contains(p.Id) && !p.Active).ToList();
            if (pods.Count == 0) return;
            var first = whyOf(pods[0]);
            var spotter = !string.IsNullOrEmpty(first.By) ? Grid.GetUnit(S, first.By!) : null;
            var spotted = !string.IsNullOrEmpty(first.Unit) ? Grid.GetUnit(S, first.Unit!) : null;
            var seen = first.Cause == PodCauses.Sighted || first.Cause == PodCauses.Patrol;
            double? range = spotter != null && spotted != null ? Los.DetectionTo(S, spotter, spotted.Pos) : spotter != null ? Content.Templates[spotter.Template].Detection : (double?)null;
            // Woken by the dead (18/10 #47: they fight the living too), not by the squad: it stays hidden.
            if (first.Cause != PodCauses.Attacked || !string.IsNullOrEmpty(first.Unit)) BreakConcealment(Exposure[first.Cause], first.Unit, first.By, seen && spotter != null ? range : null);
            foreach (var pod in pods)
            {
                var why = whyOf(pod);
                Emit(new PodActivatedEvent { Pod = pod.Id, Cause = why.Cause, Unit = why.Unit, By = why.By });
            }
            foreach (var pod in pods) Scamper(pod.UnitIds);
            // Woken by a soldier, seen or striking: the whole pod of the dead turns to them and goes after them, not only
            // whoever saw (Ana, 23/10 #25: one of a group came, the rest stood by).
            foreach (var pod in pods)
            {
                var id = whyOf(pod).Unit;
                var prey = !string.IsNullOrEmpty(id) ? Grid.GetUnit(S, id!) : null;
                if (prey?.Team != Teams.Xcom || !prey.Alive || Over) continue;
                foreach (var m in pod.UnitIds.Select((x) => Grid.GetUnit(S, x)).ToList())
                {
                    if (m == null || !m.Alive || !Content.Templates[m.Template].Undead || !Content.HostileTo(m, prey)) continue;
                    var facing = Los.FacingTo(m.Pos, prey.Pos);
                    if (facing != null && (m.Facing == null || facing.Value != m.Facing.Value)) Emit(new TurnedEvent { Unit = m.Id, Facing = facing.Value });
                    if (m.Hunting?.Id != prey.Id || m.Hunting.Missed != 0) Emit(new HuntingEvent { Unit = m.Id, Target = prey.Id, Missed = 0 });
                }
            }
            // Woken by the sight of a soldier: whoever else of the dead sees them goes after them.
            if (seen && spotted?.Team == Teams.Xcom && spotted.Alive && !Over) HuntedBy(spotted);
        }

        /// <summary>Free move to the best nearby cover, without spending actions. The dead don't take cover.</summary>
        public void Scamper(IReadOnlyList<string> unitIds)
        {
            foreach (var id in unitIds.ToList())
            {
                var m = Grid.GetUnit(S, id);
                if (m == null || !m.Alive || Over || Content.Templates[m.Template].Undead) continue;
                var dest = Ai.ChooseScamperTile(S, m);
                if (dest == null) continue;
                var path = Paths.BuildPath(S, m, Paths.ComputeReach(S, m, Paths.CostPerAp(m)), dest);
                if (path != null) MoveAlong(m, path, 0);
            }
        }

        // -------------------------------------------------------- reinforcements

        /// <summary>
        /// Picks open tiles for a drop: not too close to the squad, not too far, on
        /// the ground. The horde (`edge`) comes in by the map's edges instead, well
        /// away from the squad and the evac zone; a siege's wave, by the middle half
        /// of its `side` (a DIRS4 index), off the corners, where it would be by two.
        /// </summary>
        public List<Pos>? ChooseDropZone(int count, bool edge = false, int? side = null)
        {
            var s = S;
            var soldiers = Grid.Combatants(s, Teams.Xcom);
            if (soldiers.Count == 0) return null;
            bool Free(int x, int y) =>
                Grid.InBounds(s, x, y) && Flights.CanStopAt(s, new Pos { X = x, Y = y, Z = 0 }) && Js.At(s.Elev, Grid.TileIndex(s, x, y), 0) == 0 && Grid.UnitAt(s, new Pos { X = x, Y = y, Z = 0 }) == null;
            bool[] Sides(int x, int y, int band) => new[] { y < band, x >= s.Width - band, y >= s.Height - band, x < band };
            bool Middle(int along, int span) => along >= span / 4.0 && along < (span * 3) / 4.0;
            // In a siege, never in a glade the pines close off: only where the way to the squad is open, or opened by breaking what was built.
            var open = edge && s.Mission.Siege != null ? OpenToSquad() : null;
            bool OnEdge(int x, int y, int band) =>
                (side == null ? Sides(x, y, band).Any((b) => b) : Sides(x, y, band)[side.Value] && (side.Value % 2 == 0 ? Middle(x, s.Width) : Middle(y, s.Height))) &&
                !s.Mission.EvacZone.Any((z) => Grid.Distance(z, new Vec2 { X = x, Y = y }) < EdgeEvacGap) &&
                (open == null || open.Contains(Grid.TileIndex(s, x, y)));
            // Further off on a bigger map (docs/MAPAS.md §6: 16 and 10 at 64).
            var scale = Math.Max(1, Math.Max(s.Width, s.Height) / 48.0);
            var bands = edge
                ? EdgeGap.Select((lo) => (lo: Js.Round(lo * scale), hi: double.PositiveInfinity)).ToList()
                : new List<(double lo, double hi)> { (DropMin, DropMax), (4, 20), (2, 60) };
            foreach (var (lo, hi) in bands)
            {
                foreach (var band in edge ? new[] { EdgeBand, EdgeDeep } : new[] { EdgeBand })
                {
                    var candidates = new List<Pos>();
                    for (var y = 0; y < s.Height; y++)
                    {
                        for (var x = 0; x < s.Width; x++)
                        {
                            if (!Free(x, y) || (edge && !OnEdge(x, y, band))) continue;
                            var at = new Vec2 { X = x, Y = y };
                            var d = soldiers.Min((o) => Grid.Distance(o.Pos, at));
                            if (d >= lo && d <= hi) candidates.Add(new Pos { X = x, Y = y, Z = 0 });
                        }
                    }
                    while (candidates.Count > 0)
                    {
                        var k = Rng.Int(candidates.Count);
                        var anchor = candidates[k];
                        candidates.RemoveAt(k);
                        var zone = new List<Pos> { anchor };
                        for (var r = 1; r <= 2 && zone.Count < count; r++)
                        {
                            for (var dy = -r; dy <= r && zone.Count < count; dy++)
                            {
                                for (var dx = -r; dx <= r && zone.Count < count; dx++)
                                {
                                    var p = new Pos { X = anchor.X + dx, Y = anchor.Y + dy, Z = 0 };
                                    // The horde stays by the edge, all of it.
                                    if (Free(p.X, p.Y) && (!edge || OnEdge(p.X, p.Y, band)) && !zone.Any((z) => Grid.SameTile(z, p))) zone.Add(p);
                                }
                            }
                        }
                        if (zone.Count >= count) return zone;
                    }
                }
            }
            return null;
        }

        /// <summary>The ground tiles joined to a soldier's by ground one can walk, or by what can be broken down (GameState.breakables).</summary>
        private HashSet<int> OpenToSquad()
        {
            var s = S;
            var seen = new HashSet<int>();
            var queue = new List<Vec2>();
            foreach (var o in Grid.Combatants(s, Teams.Xcom))
            {
                if (seen.Contains(Grid.TileIndex(s, o.Pos.X, o.Pos.Y))) continue;
                seen.Add(Grid.TileIndex(s, o.Pos.X, o.Pos.Y));
                queue.Add(o.Pos);
            }
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs4)
                {
                    var x = p.X + d.X;
                    var y = p.Y + d.Y;
                    var i = Grid.TileIndex(s, x, y);
                    if (!Grid.InBounds(s, x, y) || seen.Contains(i) || (!Grid.CanStandOn(Grid.TileAt(s, x, y)) && !(s.Breakables?.ContainsKey(i) ?? false))) continue;
                    seen.Add(i);
                    queue.Add(new Vec2 { X = x, Y = y });
                }
            }
            return seen;
        }

        /// <summary>
        /// Raises the flare for every reinforcement due at the end of alien turn
        /// `turn`: only drops from missions saved before the playtest of 2026-10-13.
        /// The horde comes in by an edge, with no flare.
        /// </summary>
        public void AnnounceReinforcements(int turn)
        {
            var list = S.Mission.Reinforcements;
            for (var i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.Landed || r.Pos != null || r.Horde == true || r.Turn != turn) continue;
                var zone = ChooseDropZone(r.Units.Count);
                if (zone != null)
                {
                    var copy = Engine.Events.CopyReinforcement(r);
                    copy.Units = r.Units;
                    copy.Pos = zone[0];
                    Emit(new ReinforcementsIncomingEvent { R = copy });
                }
            }
        }

        /// <summary>
        /// The alarm went off (`hacker` failed the hack): it gives the squad away,
        /// the terminal blares (NOISE.alarm), and a horde drawn by it comes in by an
        /// edge at the end of this enemy turn (playtest 2026-10-13: nobody drops in
        /// from nowhere).
        /// </summary>
        public void RaiseAlarm(Unit? hacker = null)
        {
            BreakConcealment(ConcealReasons.Alarm, hacker?.Id);
            var terminal = S.Mission.Terminal;
            if (terminal != null) MakeNoise(terminal, Content.Noise.Alarm, hacker);
            var id = $"r{S.Mission.Reinforcements.Count + 1}";
            var r = new Reinforcement { Id = id, Turn = S.Turn, Pos = null, Units = Setup.HordeWave(S.Mission.Difficulty ?? 2), Landed = false, Horde = true };
            Emit(new ReinforcementsIncomingEvent { R = r });
        }

        /// <summary>`facingTo(a, b)` toward a point that need not be a tile's (the middle of the map).</summary>
        private static double? FacingToward(Vec2 a, double bx, double by)
        {
            return a.X == bx && a.Y == by ? (double?)null : Js.Atan2(by - a.Y, bx - a.X);
        }

        /// <summary>
        /// Brings in every arrival that is due. The horde comes in by the map's
        /// edge, where it lands, on the prowl: an inactive pod heading for the
        /// soldier nearest to it, which only wakes up (and gives the squad away) on
        /// seeing someone. A drop with a flare (only in missions saved before the
        /// playtest of 2026-10-13) lands where it was announced, already alert. A
        /// siege's wave comes in by its side, alert, looking at the soldier nearest
        /// (docs/FASE12.md §4): it goes for the soldiers (ai.ts takeZombieTurn).
        /// </summary>
        public void LandReinforcements(int turn)
        {
            var siege = S.Mission.Siege;
            var list = S.Mission.Reinforcements;
            for (var ri = 0; ri < list.Count; ri++)
            {
                var r = list[ri];
                if (r.Landed || (r.Horde != true && r.Pos == null) || r.Turn > turn || Over) continue;
                var zone = r.Horde == true ? ChooseDropZone(r.Units.Count, true, r.Side) : ChooseDropZoneAround(r.Pos!, r.Units.Count);
                // No edge free today, or no room for all of them round a flare: they come next turn. Nobody lands on anyone.
                if (zone == null || zone.Count < r.Units.Count) continue;
                var at = zone[0];
                var pod = new Pod { Id = $"p{r.Id}", Active = r.Horde != true || siege != null, UnitIds = new List<string>(), Patrol = new List<Pos>(), PatrolIndex = 0 };
                Unit? near = null;
                if (r.Horde == true)
                {
                    var squad = new List<Unit>(Grid.Combatants(S, Teams.Xcom));
                    Js.Sort(squad, (a, b) => Math.Sign(Grid.Distance(a.Pos, at) - Grid.Distance(b.Pos, at)));
                    near = squad.Count > 0 ? squad[0] : null;
                }
                var goal = r.Horde == true ? near?.Pos : null;
                var units = new List<Unit>();
                for (var i = 0; i < r.Units.Count; i++)
                {
                    var template = r.Units[i];
                    var id = $"{r.Id}u{i + 1}";
                    pod.UnitIds.Add(id);
                    var u = Setup.MakeUnit(id, template, Content.Templates[template].Name, null, zone[i], pod.Id);
                    // They come in looking the way they come: to what they heard, or to the middle of the map (Arte, 17/10).
                    var facing = goal != null ? Los.FacingTo(u.Pos, goal) : FacingToward(u.Pos, (S.Width - 1) / 2.0, (S.Height - 1) / 2.0);
                    u.Facing = facing ?? u.Facing;
                    if (goal != null && siege == null) u.Heard = Engine.Events.CopyPos(goal);
                    units.Add(u);
                }
                Emit(new SpawnedEvent { Units = units, Pod = pod, Reinforcement = r.Id });
                if (r.Horde == true) continue;
                BreakConcealment(ConcealReasons.Reinforcements, null, units.Count > 0 ? units[0].Id : null);
                Scamper(pod.UnitIds);
            }
        }

        /// <summary>
        /// Saqueo (docs/FASE2.md): the horde's clock winds up with every turn and
        /// every noise the squad makes (HORDE in content.ts). When a wave is due, it
        /// is announced for the end of the next alien turn, by the map's edge; one a
        /// turn at most, and none while the field is already full.
        /// </summary>
        public void CallHorde(int turn)
        {
            var m = S.Mission;
            if (m.Horde == null || m.Horde.Count == 0 || Grid.Combatants(S, Teams.Alien).Count >= Content.Horde.Cap) return;
            // A few every turn, always (playtest 2026-10-15, #11); twice as many while a sabotage's alarm still sounds (#14).
            var alarm = m.Kind == MissionKinds.Sabotage && !m.ObjectiveDone ? 2 : 1;
            // A bigger map, more edge to come in by (docs/MAPAS.md §6): as many more as it is longer a side than 48.
            var few = (int)Js.Round(Content.Horde.Trickle[Math.Max(1, Math.Min(4, m.Difficulty ?? 2))] * alarm * Math.Max(1, Math.Max(S.Width, S.Height) / 48.0));
            if (few != 0)
            {
                var r = new Reinforcement { Id = $"r{m.Reinforcements.Count + 1}", Turn = turn + 1, Pos = null, Units = Enumerable.Repeat(TemplateIds.Zombie, few).ToList(), Landed = false, Horde = true, Trickle = true };
                Emit(new ReinforcementsIncomingEvent { R = r });
            }
            // And a whole wave now and then, sooner the more noise the squad makes.
            var sent = S.Mission.Reinforcements.Count((r) => r.Horde == true && r.Trickle != true);
            // At night the dead are already on their way: the clock runs a turn ahead.
            var due = (int)Math.Floor((double)((turn + (m.Night == true ? Content.Night.HordeHead : 0)) * Content.Horde.PerTurn + (m.Noise ?? 0)) / Content.Horde.Wave);
            if (due <= sent) return;
            // Heard coming: they're in by an edge at the end of the next enemy turn, and nobody knows where.
            var wave = new Reinforcement { Id = $"r{S.Mission.Reinforcements.Count + 1}", Turn = turn + 1, Pos = null, Units = new List<string>(m.Horde), Landed = false, Horde = true };
            Emit(new ReinforcementsIncomingEvent { R = wave });
        }

        /// <summary>Free tiles around a landing point (the flare tile first).</summary>
        private List<Pos> ChooseDropZoneAround(Pos center, int count)
        {
            var s = S;
            var zone = new List<Pos>();
            for (var r = 0; r <= 3 && zone.Count < count; r++)
            {
                for (var dy = -r; dy <= r && zone.Count < count; dy++)
                {
                    for (var dx = -r; dx <= r && zone.Count < count; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var p = new Pos { X = center.X + dx, Y = center.Y + dy, Z = center.Z };
                        if (Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && !zone.Any((z) => Grid.SamePos(z, p))) zone.Add(p);
                    }
                }
            }
            return zone;
        }

        // ------------------------------------------------------------ turn flow

        /// <summary>
        /// Elimination ends when one side is gone. Objective missions are won once the
        /// objective is secured and then either the hostiles are dead or the squad has
        /// left the map (with someone evacuated).
        /// </summary>
        public void CheckOutcome()
        {
            if (Over) return;
            var s = S;
            var m = s.Mission;
            var squad = Grid.Combatants(s, Teams.Xcom);
            var hostiles = Grid.Combatants(s, Teams.Alien);
            void End(string outcome, string reason) => Emit(new MissionEndedEvent { Outcome = outcome, Reason = reason });

            // A siege (docs/FASE12.md §4): the base falls with nobody left to defend it (turrets don't count); it holds once every wave is in and down.
            if (m.Siege != null)
            {
                if (!squad.Any((u) => u.Automaton == null)) End(Outcomes.Defeat, EndReasons.Wiped);
                else if (hostiles.Count == 0 && m.Reinforcements.All((r) => r.Landed)) End(Outcomes.Victory, EndReasons.Objective);
                return;
            }
            // The Limpieza is won clearing the map; leaving it by the bus is a retreat.
            if (m.Kind == MissionKinds.Elimination)
            {
                if (hostiles.Count == 0) End(Outcomes.Victory, EndReasons.Objective);
                else if (m.EvacDone == true) End(Outcomes.Defeat, EndReasons.Abandoned);
                else if (squad.Count == 0) End(Outcomes.Defeat, EndReasons.Wiped);
                return;
            }
            var target = !string.IsNullOrEmpty(m.ObjectiveUnit) ? Grid.GetUnit(s, m.ObjectiveUnit!) : null;
            if (m.Kind == MissionKinds.Rescue && target != null && !target.Alive && !target.Evacuated)
            {
                End(Outcomes.Defeat, EndReasons.ObjectiveLost);
                return;
            }
            // Every other mission is won only back on the bus (docs/PRUEBA-2026-10-14.md P1), with the
            // objective done (the item or the VIP on it); a Saqueo, by just getting somebody out.
            if (m.EvacDone == true)
            {
                var someoneOut = s.Units.Any((u) => u.Team == Teams.Xcom && u.Evacuated);
                var won = someoneOut && (m.Kind == MissionKinds.Scavenge || m.ObjectiveDone);
                End(won ? Outcomes.Victory : Outcomes.Defeat, won ? EndReasons.Objective : EndReasons.Abandoned);
                return;
            }
            if (squad.Count == 0) End(Outcomes.Defeat, EndReasons.Wiped);
        }

        /// <summary>
        /// A player asks to evacuate from `u`, in the bus (docs/PRUEBA-2026-10-14.md
        /// P1). With the partner on the field and connected it waits for their yes;
        /// alone, or answering the partner's own call, the bus leaves now.
        /// </summary>
        public void CallEvac(int slot, Unit u)
        {
            var partner = slot == 0 ? 1 : 0;
            var partnerOnField = Connected.Contains(partner) && S.Units.Any((o) => o.Alive && o.Team == Teams.Xcom && o.Owner == partner);
            if (!partnerOnField || S.EvacCall?.By == partner) DepartEvac();
            else Emit(new EvacCalledEvent { Slot = slot, Unit = u.Id });
        }

        /// <summary>
        /// The bus leaves: whoever is in it gets out (first), everyone else is lost;
        /// then the mission is decided. `timeUp`: it leaves because time ran out
        /// (playtest 18/10, #8); without the objective on board, the mission is lost
        /// to the clock.
        /// </summary>
        public void DepartEvac(bool timeUp = false)
        {
            Emit(new EvacDepartedEvent());
            var squad = S.Units.Where((o) => o.Alive && o.Team == Teams.Xcom).ToList();
            // The bitten board only with a Cuarentena at home (playtest 2026-10-15, #13).
            bool Boards(Unit o) => Abilities.InEvacZone(S, o.Pos) && !(o.Infected == true && S.Mission.Quarantine == false);
            foreach (var u in squad.Where(Boards).ToList()) Emit(new EvacuatedEvent { Unit = u.Id });
            foreach (var u in squad.Where((o) => !Boards(o)).ToList()) Emit(new LeftBehindEvent { Unit = u.Id });
            var m = S.Mission;
            var won = S.Units.Any((u) => u.Team == Teams.Xcom && u.Evacuated) && (m.Kind == MissionKinds.Scavenge || m.ObjectiveDone);
            if (timeUp && !won) Emit(new MissionEndedEvent { Outcome = Outcomes.Defeat, Reason = EndReasons.Timer });
            else CheckOutcome();
        }

        /// <summary>Ends the XCOM turn once every player with soldiers able to act is ready.</summary>
        public void MaybeEndXcomTurn()
        {
            if (Over || S.ActiveTeam != Teams.Xcom) return;
            // A Set of Slot | 'pool': null for the pool.
            var pending = new List<int?>();
            foreach (var u in Grid.Combatants(S, Teams.Xcom)) if (u.Ap > 0 && !pending.Contains(u.Owner)) pending.Add(u.Owner);
            var allConnectedReady = Connected.All((slot) => S.Ready[slot]);
            var done = pending.All((k) => k == null ? allConnectedReady : S.Ready[k.Value] || !Connected.Contains(k.Value));
            if (done) RunAlienTurn();
        }

        /// <summary>
        /// Active aliens act, inactive pods patrol, due reinforcements land, and the
        /// flares for the next drop go up so the squad gets a turn to prepare.
        /// </summary>
        public void RunAlienTurn()
        {
            var turn = S.Turn;
            AlienBudget = new AlienBudget { Explosives = 1, Psi = 1 };
            Emit(new TurnBeganEvent { Team = Teams.Alien, Turn = turn });
            // The base's turrets keep watch through the enemy's turn: each of the dead that moves in sight, once (docs/FASE12.md §11.4).
            foreach (var t in S.Units.Where((x) => x.Alive && x.Automaton != null && Content.LoadedFor(x, AbilityIds.Shoot)).ToList()) Emit(new OverwatchEvent { Unit = t.Id, KillZone = true });
            // They rise now, but only act from the next turn on (they had no turn to begin).
            RiseInfected();
            var active = S.Units.Where((u) => u.Alive && u.Team == Teams.Alien && !u.Objective && (S.Pods.Find((p) => p.Id == u.PodId)?.Active ?? false)).ToList();
            // Whoever is going for the squad acts first, one by one; then the dead with nobody to reach, all together (playtest 2026-10-16),
            // the nearest to the living first: those behind follow them, instead of finding the way taken (nobody walks through anyone, 19/10).
            var walkers = Ai.FrontFirst(S, active.Where((u) => Ai.OnlyWalks(S, u)).ToList());
            var actors = active.Where((u) => !walkers.Contains(u)).Concat(walkers).Select((u) => u.Id).ToList();
            Jammed.Clear();
            foreach (var id in actors)
            {
                var u = Grid.GetUnit(S, id)!;
                if (Over) return;
                if (u.Alive) Ai.TakeAlienTurn(this, u);
            }
            // Those who waited behind their own (nobody walks through anyone, 19/10) go on with what they have left, now that the
            // ones ahead have moved: again while somebody gets further.
            for (var pass = 0; pass < JamPasses && Jammed.Count > 0; pass++)
            {
                var waiting = actors.Where((id) => Jammed.Contains(id)).ToList();
                Jammed.Clear();
                var before = Events.Count;
                foreach (var id in waiting)
                {
                    var u = Grid.GetUnit(S, id)!;
                    if (Over) return;
                    if (u.Alive && u.Ap > 0) Ai.TakeAlienTurn(this, u);
                }
                if (Events.Count == before) break;
            }
            // What the squad did on the enemy's move (overwatch, the blade on guard) fills bars too.
            SettleBars();
            // Pods still unaware walk their round, or go to look where they heard something; with neither, they look about (ai.ts lookRound).
            foreach (var pod in S.Pods.Where((p) => !p.Active).ToList())
            {
                if (Over) return;
                Ai.PatrolPod(this, pod);
            }
            if (Over) return;
            LandReinforcements(turn);
            if (Over) return;
            CallHorde(turn);
            AnnounceReinforcements(turn + 1);
            // Mind control wears off with time. Nothing takes control any more (the scream never does): only missions saved before.
            foreach (var u in S.Units.Where((x) => x.Alive && !string.IsNullOrEmpty(x.ControlledBy)).ToList())
            {
                Emit(u.ControlTurns <= 1 ? (GameEvent)new ReleasedEvent { Unit = u.Id } : new ControlTickEvent { Unit = u.Id });
            }
            CheckOutcome();
            if (Over) return;
            // A siege ends only once every one of the dead is down (Ana, 23/10 #36: no falling back after a set turn).
            var left = S.Mission.TurnsLeft;
            if (left != null && left.Value - 1 <= 0 && !S.Mission.ObjectiveDone)
            {
                // Time's up: La Lata leaves with whoever is in it (playtest 18/10, #8); the objective on board, it is a win.
                if (S.Mission.EvacZone.Count > 0) DepartEvac(true);
                else Emit(new MissionEndedEvent { Outcome = Outcomes.Defeat, Reason = EndReasons.Timer });
                return;
            }
            // Bengalas and fires on their last turn burn out; the rest burn down as the squad's turn begins.
            foreach (var l in (S.Lights ?? new List<Light>()).ToList()) if (l.Turns != null && l.Turns <= 1) Emit(new LightOffEvent { Id = l.Id });
            Emit(new TurnBeganEvent { Team = Teams.Xcom, Turn = turn + 1, Command = OpeningCommander(turn + 1) });
            // The bitten weaken: one point of health at every turn of the squad (playtest 2026-10-15, #13), armour or not.
            foreach (var u in S.Units.Where((x) => x.Alive && x.Team == Teams.Xcom && x.Infected == true).ToList()) Damage(u, 1, 0, null, 99);
            CheckOutcome();
        }

        /// <summary>Players take turns opening the squad's turn, skipping one who isn't here or has nobody left.</summary>
        public int OpeningCommander(int turn)
        {
            var first = turn % 2 == 1 ? 0 : 1;
            var order = new[] { first, first == 0 ? 1 : 0 };
            bool Fielded(int slot) => Grid.Combatants(S, Teams.Xcom).Any((u) => u.Owner == slot);
            foreach (var slot in order) if (Connected.Contains(slot) && Fielded(slot)) return slot;
            foreach (var slot in order) if (Connected.Contains(slot)) return slot;
            return first;
        }

        /// <summary>Whether `slot` has a soldier (or a shared unit) that can still act.</summary>
        public bool HasActions(int slot)
        {
            return Grid.Combatants(S, Teams.Xcom).Any((u) => u.Ap > 0 && (u.Owner == slot || u.Owner == null));
        }

        /// <summary>Whether turns go one soldier at a time (playtest 2026-10-15, #9): the partner is here, not done, and has someone to move.</summary>
        public bool Alternates(int partner)
        {
            return CanReceiveCommand(partner) && HasActions(partner);
        }

        /// <summary>
        /// After a soldier's order (#9): while it has actions it is the one in play;
        /// once it has none (or is down, or gone), the partner plays one of theirs.
        /// </summary>
        public void AfterSoldier(Unit u, int apBefore)
        {
            if (Over || S.ActiveTeam != Teams.Xcom || u.Owner == null) return;
            var live = Grid.GetUnit(S, u.Id);
            if (live != null && live.Alive && !live.Evacuated && live.Ap > 0)
            {
                if (live.Ap < apBefore && S.ActiveUnit != live.Id) Emit(new UnitActiveEvent { Unit = live.Id });
                return;
            }
            if (!string.IsNullOrEmpty(S.ActiveUnit)) Emit(new UnitActiveEvent { Unit = null });
            var partner = u.Owner == 0 ? 1 : 0;
            if (S.Command == u.Owner && Alternates(partner)) PassCommand(partner);
        }

        /// <summary>The partner can take the command: connected and not done with the turn.</summary>
        public bool CanReceiveCommand(int slot)
        {
            return Connected.Contains(slot) && !S.Ready[slot];
        }

        public void PassCommand(int to)
        {
            if (S.Command != to) Emit(new CommandPassedEvent { To = to });
        }

        /// <summary>
        /// A holder with nothing left to do hands the command over by itself, so the
        /// partner isn't left waiting for a click.
        /// </summary>
        public void AutoPassCommand()
        {
            if (Over || S.ActiveTeam != Teams.Xcom) return;
            var holder = S.Command;
            if (holder == null) return;
            var partner = holder == 0 ? 1 : 0;
            if (!HasActions(holder.Value) && CanReceiveCommand(partner) && HasActions(partner)) PassCommand(partner);
        }

        /// <summary>A backpack order already checked (backpackRefusal).</summary>
        public void UseBackpack(ItemCommand cmd)
        {
            var u = Grid.GetUnit(S, cmd.Unit)!;
            switch (cmd.Action)
            {
                case ItemActions.Drop:
                    Emit(new LootDroppedEvent { Unit = u.Id, Index = cmd.Index!.Value });
                    break;
                case ItemActions.Take:
                    Emit(new LootTakenEvent { Unit = u.Id, Id = cmd.Id! });
                    break;
                case ItemActions.Give:
                    Emit(new LootGivenEvent { Unit = u.Id, To = cmd.To!, Index = cmd.Index!.Value });
                    break;
                case ItemActions.Use:
                {
                    var target = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(S, cmd.To!)! : u;
                    Emit(new MedicineUsedEvent { Unit = u.Id, Target = target.Id, Index = cmd.Index!.Value });
                    var amount = Math.Min(Content.MedicineHeal + u.Mods.Heal, target.MaxHp - target.Hp);
                    if (amount > 0) Emit(new HealedEvent { Unit = u.Id, Target = target.Id, Amount = amount });
                    if (target.Infected == true && Rules.MedicineStopsBite(S, u)) Emit(new CuredEvent { Unit = target.Id, By = u.Id });
                    break;
                }
                case ItemActions.Equip:
                {
                    var loot = u.Backpack != null && cmd.Index != null && cmd.Index.Value >= 0 && cmd.Index.Value < u.Backpack.Count ? u.Backpack[cmd.Index.Value] : null;
                    if (!string.IsNullOrEmpty(loot?.Weapon)) Emit(new WeaponEquippedEvent { Unit = u.Id, Index = cmd.Index!.Value });
                    else if (!string.IsNullOrEmpty(loot?.Item) && Rules.Wearable(loot!.Item!)) Emit(new VestWornEvent { Unit = u.Id, Index = cmd.Index!.Value });
                    else Emit(new BeltEquippedEvent { Unit = u.Id, Index = cmd.Index!.Value });
                    break;
                }
            }
        }

        /// <summary>
        /// Bars filled by what was just done (docs/FASE10.md §2): each one rolls its
        /// 5–10 % and its stat goes up there and then. After the whole action, so it
        /// is seen entire first; a bar the mission's last blow fills rolls at home.
        /// </summary>
        public void SettleBars()
        {
            if (Over) return;
            for (var i = 0; i < S.Units.Count; i++)
            {
                var u = S.Units[i];
                if (u.Lines == null || !u.Alive || u.Team != Teams.Xcom) continue;
                foreach (var bar in Bars.PendingBars(u.Lines, u.Deeds)) Emit(new BarFilledEvent { Unit = u.Id, Skill = bar.Skill, Filled = bar.Filled, Growth = Bars.RollGrowth(Rng) });
            }
        }

        public void AfterXcomAction()
        {
            SettleBars();
            if (Over) return;
            foreach (var u in Grid.Combatants(S, Teams.Xcom))
            {
                var noticing = PodsNoticing(u.Pos);
                if (noticing.Count > 0) ActivatePods(noticing, Sighting(noticing, u, u.Pos));
                if (Over) return;
            }
            CheckOutcome();
            MaybeEndXcomTurn();
            AutoPassCommand();
        }
    }

    public static class Rules
    {
        /// <summary>Everything the squad does makes this much more noise than its table says (Ana, 23/10: «un 15 % más de ruido»).</summary>
        public const double SquadLouder = 1.15;

        /// <summary>
        /// The places of the whole object standing on place `p` (decor.ts objectAt: a car over three tiles, a bed over two,
        /// a lorry's block), on its storey; `p` alone for anything on one tile. In the base every piece stands alone: the
        /// players build log piles side by side, and one grenade isn't a whole row of them.
        /// </summary>
        internal static List<Pos> ObjectPlaces(GameState s, Pos p)
        {
            if (s.Base != null) return new List<Pos> { p };
            var decor = p.Z != 0 ? Grid.LevelOf(s, p.Z - 1)?.Decor : s.Decor;
            var o = decor != null ? Decor.ObjectAt(decor, s.Width, Grid.TileIndex(s, p.X, p.Y)) : null;
            return o != null && o.Tiles.Count > 1 ? o.Tiles.Select((i) => new Pos { X = i % s.Width, Y = (int)Math.Floor((double)i / s.Width), Z = p.Z }).ToList() : new List<Pos> { p };
        }

        /// <summary>
        /// Where someone (or something) on storey place `p` lands when its slab gives
        /// way (docs/FASE7.md §5): on the next slab down in that column, or the ground,
        /// on `p`'s own tile if one can stand there, else the nearest free place to
        /// stand at that height (two tiles away at most). Null if there is none.
        /// </summary>
        internal static Pos? LandingBelow(GameState s, Pos p, HashSet<int> taken)
        {
            var z = p.Z - 1;
            while (z > 0 && !Grid.SlabAt(s, p.X, p.Y, z)) z--;
            for (var r = 0; r <= 2; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !Grid.InBounds(s, p.X + dx, p.Y + dy)) continue;
                        var q = new Pos { X = p.X + dx, Y = p.Y + dy, Z = z };
                        if (Flights.CanStopAt(s, q) && Grid.UnitAt(s, q) == null && !taken.Contains(Grid.NodeOf(s, q))) return q;
                    }
                }
            }
            return null;
        }

        /// <summary>What a corpse is called once it rises: a soldier keeps its name («Zombi · Clara Bosch»), anyone else is a Zombi.</summary>
        public static string RisenName(Unit corpse)
        {
            return Content.Templates[corpse.Template].Team == Teams.Xcom ? $"{Content.Templates[TemplateIds.Zombie].Name} · {corpse.Name}" : Content.Templates[TemplateIds.Zombie].Name;
        }

        /// <summary>
        /// The zombie a corpse rises as. A soldier's keeps its look (playtest
        /// 2026-10-16): `risenFrom` says whose, and the client draws that soldier dead.
        /// </summary>
        internal static Unit RisenUnit(string id, Unit corpse, Pos at, string? podId)
        {
            // It gets up looking where it fell (Arte, 17/10).
            var zombie = Setup.MakeUnit(id, TemplateIds.Zombie, RisenName(corpse), null, at, podId);
            if (corpse.Facing != null) zombie.Facing = corpse.Facing;
            if (Content.Templates[corpse.Template].Team != Teams.Xcom || corpse.Appearance == null) return zombie;
            zombie.RisenFrom = corpse.Id;
            var a = corpse.Appearance;
            zombie.Appearance = new Appearance { Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build, Sex = a.Sex, Helmet = a.Helmet, Torso = a.Torso, Arms = a.Arms, Legs = a.Legs, Character = a.Character };
            return zombie;
        }

        /// <summary>blastMods's `{ bonus: number; shred: number }`.</summary>
        internal sealed class BlastBonus
        {
            public int Bonus;
            public int Shred;
        }

        /// <summary>What the thrower adds to an explosion: damage (grenade talents) and shred.</summary>
        internal static BlastBonus BlastMods(Unit u, string ability)
        {
            var weapon = Content.Weapons[Content.Abilities[ability].Explosive!];
            var grenade = ability == AbilityIds.Grenade || ability == AbilityIds.Launch;
            var bonus = grenade ? u.Mods.GrenadeDamage : 0;
            return new BlastBonus { Bonus = bonus, Shred = weapon.Shred + (grenade ? u.Mods.GrenadeShred : 0) };
        }

        /// <summary>
        /// What a damaging ability would take off `target` (or, with none, its own
        /// numbers, for the ability cards): attacks and explosives. `shot` is the
        /// attack's preview against that target (flank and distance). Null for
        /// abilities that do no damage.
        /// </summary>
        public static DamagePreview? AbilityDamage(GameState s, Unit u, string ability, Unit? target = null, ShotPreview? shot = null)
        {
            if (Content.IsAttack(ability)) return Aim.ShotDamage(u, ability, target, shot);
            switch (ability)
            {
                case AbilityIds.Grenade:
                case AbilityIds.Launch:
                case AbilityIds.Missiles:
                {
                    var mods = BlastMods(u, ability);
                    return new DamagePreview { Hit = Aim.RangeOn(target, Aim.DamageRangeOf(Content.Weapons[Content.Abilities[ability].Explosive!], ShotResults.Hit, mods.Bonus)), Crit = null, Graze = null, Armor = target?.Armor ?? 0, Shred = mods.Shred };
                }
            }
            return null;
        }

        // ------------------------------------------------------------- commands

        /// <summary>Entre los dos (board.ts): the botiquín does more for the other player's soldiers.</summary>
        internal static bool Bridged(Unit u, Unit target)
        {
            return Content.HasPerk(u, PerkIds.Bridge) && u.Owner != null && target.Owner != null && u.Owner != target.Owner;
        }

        /// <summary>commandUnit: the unit `slot` may order, or (`error`) why not.</summary>
        private static Unit? CommandUnit(GameState s, string id, int slot, IReadOnlyList<int> connected, out string? error)
        {
            error = null;
            var u = Grid.GetUnit(s, id);
            if (u == null || !u.Alive) error = "Esa unidad no existe o está fuera de combate.";
            else if (u.Team != Teams.Xcom) error = "No puedes dar órdenes a esa unidad.";
            else if (u.Automaton != null) error = "Va sola: nadie le da órdenes.";
            else if (!Content.CommandsFor(slot, u.Owner, connected)) error = "Es de la escuadra del otro jugador.";
            else if (u.Ap <= 0) error = "Ese soldado no tiene acciones.";
            return error == null ? u : null;
        }

        /// <summary>
        /// Why a backpack order can't go through (docs/FASE8.md §C), or null. Dropping and taking are free, even with no
        /// actions left; giving, using a medicine and equipping a weapon take one.
        /// </summary>
        private static string? BackpackRefusal(GameState s, ItemCommand cmd, int slot, IReadOnlyList<int> connected)
        {
            var u = Grid.GetUnit(s, cmd.Unit);
            if (u == null || !u.Alive) return "Esa unidad no existe o está fuera de combate.";
            if (u.Team != Teams.Xcom || u.Captive || u.Objective || u.Automaton != null) return "No puedes dar órdenes a esa unidad.";
            if (!Content.CommandsFor(slot, u.Owner, connected)) return "Es de la escuadra del otro jugador.";
            if (!string.IsNullOrEmpty(u.ControlledBy) || u.Panicked) return "Ese soldado no responde.";
            var free = cmd.Action == ItemActions.Drop || cmd.Action == ItemActions.Take;
            if (!free && u.Ap <= 0) return "Ese soldado no tiene acciones.";
            if (cmd.Action == ItemActions.Take)
            {
                var left = s.Dropped?.Find((d) => d.Id == cmd.Id);
                if (left == null || !Grid.SamePos(left.Pos, u.Pos)) return "Aquí no hay nada que coger.";
                if ((left.Loot.Mags ?? 0) == 0 && (u.Backpack?.Count ?? 0) >= Looting.PackSlots(u)) return "La mochila está llena: suelta algo antes.";
                return null;
            }
            var loot = cmd.Index == null || u.Backpack == null || cmd.Index.Value < 0 || cmd.Index.Value >= u.Backpack.Count ? null : u.Backpack[cmd.Index.Value];
            if (loot == null) return "No lleva eso en la mochila.";
            switch (cmd.Action)
            {
                case ItemActions.Drop:
                    return null;
                case ItemActions.Give:
                {
                    var to = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!) : null;
                    if (to == null || !to.Alive || to.Team != Teams.Xcom || to.Captive || to.Objective || to.Id == u.Id) return "Solo a un compañero.";
                    if (!Aim.InMeleeReach(s, u.Pos, to.Pos)) return "Tiene que estar a tu lado.";
                    if ((to.Backpack?.Count ?? 0) >= Looting.PackSlots(to)) return "No le cabe: lleva la mochila llena.";
                    return null;
                }
                case ItemActions.Use:
                {
                    if ((loot.Medicines ?? 0) == 0) return "Solo se usan las medicinas.";
                    var to = !string.IsNullOrEmpty(cmd.To) ? Grid.GetUnit(s, cmd.To!) : u;
                    if (to == null || !to.Alive || to.Team != Teams.Xcom || to.Objective) return "Solo en ti o en un compañero.";
                    if (to != u && !Aim.InMeleeReach(s, u.Pos, to.Pos)) return "Tiene que estar a tu lado.";
                    if (to.Hp >= to.MaxHp && !(to.Infected == true && MedicineStopsBite(s, u))) return to == u ? "No tienes heridas." : "No tiene heridas.";
                    return null;
                }
                case ItemActions.Equip:
                    // A vest found is put on there and then (playtest 16/10 #18); one at a time.
                    if (!string.IsNullOrEmpty(loot.Item) && Wearable(loot.Item!)) return u.Worn != null && u.Worn.Count > 0 ? "Ya lleva un chaleco puesto." : null;
                    // A belt thing goes on the belt, to use there and then (playtest 18/10 #5); what only gives, once.
                    if (!string.IsNullOrEmpty(loot.Item) && Beltable(loot.Item!)) return Content.Items[loot.Item!].Charges == null && u.Gear.Contains(loot.Item!) ? "Ya lleva eso puesto." : null;
                    if (!string.IsNullOrEmpty(loot.Item)) return "La munición se monta en el arma, en la Armería.";
                    if (string.IsNullOrEmpty(loot.Weapon)) return "Solo se equipan las armas, los chalecos y lo del cinturón.";
                    if (u.Arms == null) return "Las armas encontradas son para el Bastión.";
                    return null;
            }
            return null;
        }

        /// <summary>A found accessory that goes on the belt, out there (playtest 18/10 #5): grenades, the medkit, ear plugs; not ammunition, which is the weapon's.</summary>
        public static bool Beltable(string item)
        {
            var def = Content.Items[item];
            return def.Slot == ItemSlots.Utility && !Wearable(item);
        }

        /// <summary>A found accessory worn over the clothes, out there: what only gives health (the Chaleco antibalas).</summary>
        public static bool Wearable(string item)
        {
            var def = Content.Items[item];
            return (def.Hp ?? 0) != 0 && def.Charges == null && string.IsNullOrEmpty(def.Ability);
        }

        /// <summary>Whether `u`'s medicines also stop a bite's contagion (docs/FASE8.md §C): the Enfermería for everyone, or the support talent.</summary>
        public static bool MedicineStopsBite(GameState s, Unit u)
        {
            return s.Mission?.MedicineCures == true || Content.HasPerk(u, PerkIds.LightHands);
        }

        /// <summary>Why a support ability can't go to that ally: the reason, not just "no".</summary>
        private static string AllyRefusal(Unit u, string ability, Unit? target)
        {
            if (target == null || !target.Alive) return "Aliado no válido.";
            var range = Content.Abilities[ability].Range;
            if (Grid.Distance(u.Pos, target.Pos) > (range ?? double.PositiveInfinity)) return range != null && range.Value != 0 && range.Value <= 1.5 ? "Tiene que estar a tu lado." : "Está demasiado lejos.";
            switch (ability)
            {
                case AbilityIds.FirstAid:
                    return "No tiene heridas.";
                case AbilityIds.Revival:
                    return "No sufre aturdimiento, desorientación ni pánico.";
                case AbilityIds.PatchUp:
                    return !string.IsNullOrEmpty(target.Arms?.Primary) ? "Su arma está como nueva." : "No lleva arma que arreglar.";
            }
            return "Aliado no válido.";
        }

        /// <summary>The command of `slot`, or why not (rules.ts executeCommand; the arguments of net/Rules.cs IRules).</summary>
        public static CommandResult ExecuteCommand(GameState s, Command cmd, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            return ExecuteCommand(s, cmd, new CommandContext { Slot = slot, Connected = connected, Rng = rng });
        }

        public static CommandResult ExecuteCommand(GameState s, Command cmd, CommandContext ctx)
        {
            if (s.Outcome != null) return CommandResult.Failure("La misión ha terminado.");
            // In real time the clock runs the mission, and the players give standing orders (docs/TIEMPO-REAL.md).
            // TODO(port): realtime.ts executeRealtimeCommand (after milestone 1, README): refused until then.
            if (s.Realtime != null) return CommandResult.Failure("El modo en tiempo real todavía no está en esta versión.");
            if (cmd is RtOrderCommand || cmd is RtPauseCommand || cmd is RtSlowCommand) return CommandResult.Failure("Eso es del modo en tiempo real.");
            if (s.ActiveTeam != Teams.Xcom) return CommandResult.Failure("Espera a que termine el turno enemigo.");

            var sim = new Sim(s, ctx.Rng, ctx.Connected);

            var partner = ctx.Slot == 0 ? 1 : 0;
            // Whoever holds the command acts; if it's held by nobody here, the first to act takes it.
            var holds = s.Command == ctx.Slot || s.Command == null || !ctx.Connected.Contains(s.Command.Value);

            if (cmd is EndTurnCommand end)
            {
                var turn = s.Turn;
                if (s.Ready[ctx.Slot] != end.Ready) sim.Emit(new ReadyEvent { Slot = ctx.Slot, Ready = end.Ready });
                sim.MaybeEndXcomTurn();
                // Done for this turn (and it goes on): the partner, if still playing, gets the command.
                var goesOn = s.Turn == turn && s.ActiveTeam == Teams.Xcom && s.Outcome == null;
                if (end.Ready && holds && goesOn && sim.CanReceiveCommand(partner)) sim.PassCommand(partner);
                return CommandResult.Success(sim.Events);
            }

            // Evacuating (docs/PRUEBA-2026-10-14.md P1): either player calls it from a soldier in the bus, the
            // other answers; neither needs the command, nor actions left.
            if (cmd is EvacAnswerCommand answer)
            {
                if (s.EvacCall == null || s.EvacCall.By == ctx.Slot) return CommandResult.Failure("No hay ninguna petición de evacuar que contestar.");
                if (answer.Accept) sim.DepartEvac();
                else sim.Emit(new EvacRefusedEvent { Slot = ctx.Slot });
                return CommandResult.Success(sim.Events);
            }
            if (cmd is EvacCommand evac)
            {
                var u = Grid.GetUnit(s, evac.Unit);
                if (u == null || !u.Alive || u.Team != Teams.Xcom || u.Captive) return CommandResult.Failure("Esa unidad no puede evacuar.");
                if (!Content.CommandsFor(ctx.Slot, u.Owner, ctx.Connected)) return CommandResult.Failure("Es de la escuadra del otro jugador.");
                if (!Abilities.InEvacZone(s, u.Pos)) return CommandResult.Failure("Solo desde el autobús, en la zona de evacuación.");
                if (s.EvacCall?.By == ctx.Slot) return CommandResult.Failure("Ya has pedido evacuar: falta que conteste el otro jugador.");
                if (u.Infected == true && s.Mission.Quarantine == false) return CommandResult.Failure("Con un mordisco no se sube a La Lata sin una Cuarentena en el Bastión.");
                sim.CallEvac(ctx.Slot, u);
                return CommandResult.Success(sim.Events);
            }

            if (cmd is PassCommand)
            {
                if (!holds) return CommandResult.Failure("No tienes el mando.");
                if (!ctx.Connected.Contains(partner)) return CommandResult.Failure("El otro jugador no está conectado.");
                if (s.Ready[partner]) return CommandResult.Failure("El otro jugador ya ha terminado el turno.");
                if (!sim.HasActions(partner)) return CommandResult.Failure("Al otro jugador no le quedan acciones.");
                sim.PassCommand(partner);
                return CommandResult.Success(sim.Events);
            }

            if (!holds) return CommandResult.Failure("Tiene el mando el otro jugador: espera a que te lo ceda.");
            // Turns go one soldier at a time (playtest 2026-10-15, #9): the one in play is finished before another of yours.
            string? InPlay(string id)
            {
                var busy = !string.IsNullOrEmpty(s.ActiveUnit) && s.ActiveUnit != id ? Grid.GetUnit(s, s.ActiveUnit!) : null;
                return busy != null && busy.Alive && busy.Ap > 0 && busy.Owner == ctx.Slot && sim.Alternates(partner) ? $"Primero termina con {busy.Name}, o cede el turno." : null;
            }
            if (cmd is ItemCommand item)
            {
                var refusal = BackpackRefusal(s, item, ctx.Slot, ctx.Connected);
                if (refusal != null) return CommandResult.Failure(refusal);
                var free = item.Action == ItemActions.Drop || item.Action == ItemActions.Take;
                var busyItem = free ? null : InPlay(item.Unit);
                if (busyItem != null) return CommandResult.Failure(busyItem);
                var holder = Grid.GetUnit(s, item.Unit)!;
                var apBeforeItem = holder.Ap;
                sim.PassCommand(ctx.Slot);
                sim.UseBackpack(item);
                if (!free) sim.AfterSoldier(holder, apBeforeItem);
                sim.AfterXcomAction();
                return CommandResult.Success(sim.Events);
            }
            var unitId = cmd is MoveCommand mc ? mc.Unit : cmd is AbilityCommand ac ? ac.Unit : "";
            var unit = CommandUnit(s, unitId, ctx.Slot, ctx.Connected, out var why);
            if (unit == null) return CommandResult.Failure(why!);
            var busyUnit = InPlay(unit.Id);
            if (busyUnit != null) return CommandResult.Failure(busyUnit);
            var apBefore = unit.Ap;
            // The command changes hands only once the order is sure to go through: a refused one changes nothing
            // (it used to pass first, and the host kept a holder the partner never heard of).

            if (cmd is MoveCommand move)
            {
                // A place without its level is on the ground (docs/FASE6-PLAN.md).
                var to = Grid.ToPos(move.To);
                var reach = Paths.ComputeReach(s, unit, Paths.MaxMoveCost(unit));
                var path = Paths.BuildPath(s, unit, reach, to);
                if (path == null) return CommandResult.Failure("No puede llegar ahí.");
                var cost = reach.Cost.Get(Grid.NodeOf(s, to), 0);
                var ap = Paths.ApForCost(unit, cost);
                if (ap > unit.Ap) return CommandResult.Failure("No le quedan acciones para llegar ahí.");
                sim.PassCommand(ctx.Slot);
                sim.MoveAlong(unit, path, ap);
                // Running is heard (docs/FASE2.md).
                if (ap >= 2 && unit.Alive) sim.MakeNoise(unit.Pos, Content.Noise.Dash, unit);
                sim.AfterSoldier(unit, apBefore);
                sim.AfterXcomAction();
                return CommandResult.Success(sim.Events);
            }

            var command = (AbilityCommand)cmd;
            var blocker = Abilities.AbilityBlocker(s, unit, command.Ability);
            if (blocker != null) return CommandResult.Failure(blocker);
            var def = Content.Abilities[command.Ability];
            var target = !string.IsNullOrEmpty(command.Target) ? Grid.GetUnit(s, command.Target!) : null;

            switch (def.Target)
            {
                case AbilityTargets.Enemy:
                case AbilityTargets.Ally:
                case AbilityTargets.Corpse:
                    if (target == null || !Abilities.AbilityTargets(s, unit, command.Ability).Contains(target))
                    {
                        return CommandResult.Failure(def.Target == AbilityTargets.Ally ? AllyRefusal(unit, command.Ability, target) : "Objetivo fuera de alcance o sin línea de visión.");
                    }
                    sim.PassCommand(ctx.Slot);
                    sim.Perform(unit, command.Ability, target: target);
                    break;
                case AbilityTargets.Melee:
                {
                    var option = Content.IsAttack(command.Ability) ? Abilities.MeleeTargets(s, unit, command.Ability).Find((o) => o.Target.Id == command.Target) : null;
                    if (option == null) return CommandResult.Failure("No puede alcanzar a ese enemigo este turno.");
                    sim.PassCommand(ctx.Slot);
                    sim.Perform(unit, command.Ability, melee: option);
                    break;
                }
                case AbilityTargets.Tile:
                {
                    var tile = command.Tile != null ? Grid.ToPos(command.Tile) : null;
                    if (tile == null || !Abilities.CanTargetTile(s, unit, command.Ability, tile))
                    {
                        var whyTile = command.Ability == AbilityIds.Search ? "No hay ninguna caja que abrir ahí."
                            : command.Ability == AbilityIds.OpenDoor ? "Esa casilla no es una puerta cerrada contigua."
                            : command.Ability == AbilityIds.CloseDoor ? "Esa casilla no es una puerta abierta, libre y contigua."
                            : "Fuera de alcance o fuera de la vista de la escuadra.";
                        return CommandResult.Failure(whyTile);
                    }
                    sim.PassCommand(ctx.Slot);
                    sim.Perform(unit, command.Ability, tile: tile);
                    break;
                }
                case AbilityTargets.Self:
                    sim.PassCommand(ctx.Slot);
                    sim.Perform(unit, command.Ability);
                    break;
            }

            sim.AfterSoldier(unit, apBefore);
            sim.AfterXcomAction();
            return CommandResult.Success(sim.Events);
        }

        /// <summary>
        /// `slot`'s clock ran out in the squad's turn (the host's turn clock: Arturo, 22/10): each of the soldiers they command
        /// with actions left takes cover where it has some (Cubrirse), the rest lose theirs, and they are done for the turn;
        /// the partner, if still playing, gets the command.
        /// </summary>
        public static List<GameEvent> RunOutOfTime(GameState s, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            if (s.Outcome != null || s.Realtime != null || s.ActiveTeam != Teams.Xcom || s.Ready[slot]) return new List<GameEvent>();
            var sim = new Sim(s, rng, connected);
            var turn = s.Turn;
            foreach (var u in Grid.Combatants(s, Teams.Xcom))
            {
                if (sim.Over) return sim.Events;
                if (u.Ap > 0 && Content.CommandsFor(slot, u.Owner, connected) && Abilities.AbilityBlocker(s, u, AbilityIds.Hunker) == null) sim.Perform(u, AbilityIds.Hunker);
            }
            if (!string.IsNullOrEmpty(s.ActiveUnit)) sim.Emit(new UnitActiveEvent { Unit = null });
            sim.Emit(new ReadyEvent { Slot = slot, Ready = true });
            sim.MaybeEndXcomTurn();
            var partner = slot == 0 ? 1 : 0;
            if (s.Turn == turn && s.ActiveTeam == Teams.Xcom && s.Outcome == null && sim.CanReceiveCommand(partner)) sim.PassCommand(partner);
            return sim.Events;
        }

        /// <summary>Re-evaluates the end of turn, e.g. after a player disconnects.</summary>
        public static List<GameEvent> SyncTurn(GameState s, IReadOnlyList<int> connected, Rng rng)
        {
            var sim = new Sim(s, rng, connected);
            // A call to evacuate whose answerer has gone: the bus leaves (docs/PRUEBA-2026-10-14.md P1).
            if (s.EvacCall != null && s.ActiveTeam == Teams.Xcom && s.Outcome == null && !connected.Contains(s.EvacCall.By == 0 ? 1 : 0)) sim.DepartEvac();
            sim.MaybeEndXcomTurn();
            // A holder who left hands the command to whoever is still here.
            var holder = s.Command;
            if (s.ActiveTeam == Teams.Xcom && s.Outcome == null && connected.Count > 0 && (holder == null || !connected.Contains(holder.Value))) sim.PassCommand(connected[0]);
            return sim.Events;
        }

        /// <summary>
        /// Whether the bus leaving now wins the mission (docs/PRUEBA-2026-10-14.md P1),
        /// for the prompt: a Limpieza is never won by leaving; a Saqueo, by anyone on
        /// board; the rest, with the objective done, or its item or VIP on board.
        /// </summary>
        public static bool EvacWins(GameState s)
        {
            var m = s.Mission;
            if (m.Kind == MissionKinds.Elimination) return false;
            var aboard = s.Units.Where((u) => u.Alive && u.Team == Teams.Xcom && Abilities.InEvacZone(s, u.Pos)).ToList();
            if (aboard.Count == 0) return false;
            if (m.Kind == MissionKinds.Scavenge || m.ObjectiveDone) return true;
            if (m.Kind == MissionKinds.Recovery) return !string.IsNullOrEmpty(m.Item?.Carrier) && aboard.Any((u) => u.Id == m.Item!.Carrier);
            if (m.Kind == MissionKinds.Rescue) return !string.IsNullOrEmpty(m.ObjectiveUnit) && aboard.Any((u) => u.Id == m.ObjectiveUnit);
            return false;
        }

        /// <summary>Ends the mission early (either player can abandon).</summary>
        public static List<GameEvent> AbandonMission(GameState s)
        {
            if (s.Outcome != null) return new List<GameEvent>();
            var e = new MissionEndedEvent { Outcome = Outcomes.Defeat, Reason = EndReasons.Abandoned };
            Events.ApplyEvent(s, e);
            return new List<GameEvent> { e };
        }

        /// <summary>
        /// `slot` wants to abandon the mission (playtest 16/10): with the partner
        /// here it is asked first, as the bus is; alone, or the partner having asked
        /// already, it ends now.
        /// </summary>
        public static List<GameEvent> CallAbandon(GameState s, int slot, IReadOnlyList<int> connected)
        {
            if (s.Outcome != null) return new List<GameEvent>();
            var partner = slot == 0 ? 1 : 0;
            if (!connected.Contains(partner) || s.AbandonCall?.By == partner) return AbandonMission(s);
            if (s.AbandonCall?.By == slot) return new List<GameEvent>();
            var e = new AbandonCalledEvent { Slot = slot };
            Events.ApplyEvent(s, e);
            return new List<GameEvent> { e };
        }

        /// <summary>The partner answers a call to abandon: yes ends the mission, no keeps it going.</summary>
        public static List<GameEvent> AnswerAbandon(GameState s, int slot, bool accept)
        {
            if (s.Outcome != null || s.AbandonCall == null || s.AbandonCall.By == slot) return new List<GameEvent>();
            if (accept) return AbandonMission(s);
            var e = new AbandonRefusedEvent { Slot = slot };
            Events.ApplyEvent(s, e);
            return new List<GameEvent> { e };
        }
    }
}
