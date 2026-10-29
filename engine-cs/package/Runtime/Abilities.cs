#nullable enable
// abilities.ts: Single source of truth for "can this unit use this ability, on what?".
// The server validates commands with it, the client greys out buttons with it
// and the AI picks targets with it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public sealed class MeleeOption
    {
        public Unit Target { get; set; } = new Unit();
        /// <summary>Tile to strike from (the attacker's own tile when already adjacent).</summary>
        public Pos Tile { get; set; } = new Pos();
        public List<Pos> Path { get; set; } = new List<Pos>();
    }

    public static class Abilities
    {
        public static List<string> AbilitiesOf(Unit u)
        {
            var list = new List<string>(Content.Templates[u.Template].Abilities);
            // Arms of its own (docs/FASE8.md §A): everyone has a blade, and the pistol is a second gun only beside a primary.
            if (u.Arms != null)
            {
                if (!list.Contains(AbilityIds.Slash)) list.Insert(list.IndexOf(AbilityIds.Shoot) + 1, AbilityIds.Slash);
                var pistol = list.IndexOf(AbilityIds.Pistol);
                if (!string.IsNullOrEmpty(u.Arms.Primary) && pistol < 0) list.Insert(list.IndexOf(AbilityIds.Slash) + 1, AbilityIds.Pistol);
                if (string.IsNullOrEmpty(u.Arms.Primary) && pistol >= 0) list.RemoveAt(pistol);
            }
            // Nobody throws grenades without having carried some (docs/FASE9.md §4): the squad's come only from the belt.
            if (u.Team == Teams.Xcom && !u.Charges.ContainsKey(ChargeIds.Grenade) && list.Contains(AbilityIds.Grenade)) list.RemoveAt(list.IndexOf(AbilityIds.Grenade));
            var granted = u.Perks.Select((id) => Board.PerkAbility.TryGetValue(id, out var a) ? a : null)
                .Concat(u.Gear.Select((id) => Content.Items.TryGetValue(id, out var item) ? item.Ability : null));
            foreach (var g in granted)
            {
                if (!string.IsNullOrEmpty(g) && !list.Contains(g!)) list.Add(g!);
            }
            if (u.Team == Teams.Xcom && !u.Captive && Content.Templates[u.Template].Team == Teams.Xcom)
            {
                // Evacuating is the squad's, by the bus (the `evac` command, docs/PRUEBA-2026-10-14.md P1), not one soldier's ability.
                if (u.Template != TemplateIds.Vip) list.AddRange(new[] { AbilityIds.Hack, AbilityIds.Search, AbilityIds.SearchRoom, AbilityIds.OpenDoor, AbilityIds.CloseDoor });
                // Fase 10 (docs/FASE10.md §4): the blade can keep watch too. The stone is the board's since 29/10 (Piedras).
                if (u.Arms != null && list.Contains(AbilityIds.Overwatch)) list.Insert(list.IndexOf(AbilityIds.Overwatch) + 1, AbilityIds.OverwatchMelee);
            }
            return list;
        }

        /// <summary>On the evac zone: it lies on the ground.</summary>
        public static bool InEvacZone(GameState s, Pos p)
        {
            return p.Z == 0 && s.Mission.EvacZone.Any((z) => Grid.SameTile(z, p));
        }

        /// <summary>Hack from a tile beside the terminal, on its floor.</summary>
        public static bool CanHackFrom(GameState s, Unit u, Pos? from = null)
        {
            from ??= u.Pos;
            var t = s.Mission.Terminal;
            return t != null && Aim.Adjacent(from, t) && from.Z == t.Z;
        }

        public static int HackChance(GameState s, Unit u)
        {
            var bonus = u.Mods.Hack + s.Mission.HackBonus;
            return Math.Max(5, Math.Min(95, Content.HackBase + bonus));
        }

        /// <summary>
        /// Chance that the Chillón's scream (or a psionic power) takes hold of
        /// `target`; 0 = immune: machines, the dead, «Serenidad» (psiGuard 2). Voz
        /// tranquila and the like (calmAura) add +10 will per level to allies within 4
        /// tiles (`s` needed).
        /// </summary>
        public static int PsiChance(Unit target, GameState? s = null)
        {
            if (Content.Templates[target.Template].Robotic || target.Will >= 100 || !string.IsNullOrEmpty(target.ControlledBy) || target.Mods.PsiGuard >= 2) return 0;
            var calm = 0;
            if (s != null)
            {
                foreach (var a in s.Units)
                {
                    if (a.Alive && a.Team == target.Team && a.Mods.CalmAura > 0 && Grid.Distance(a.Pos, target.Pos) <= CalmRange && Grid.SameSpace(s, a.Pos, target.Pos)) calm = Math.Max(calm, a.Mods.CalmAura);
                }
            }
            var will = target.Will + CalmWill * calm;
            return Math.Max(10, Math.Min(90, Content.PsiBase - (will - 40)));
        }

        /// <summary>Voz tranquila and the like (calmAura): reach and will per level.</summary>
        private const int CalmRange = 4;
        private const int CalmWill = 10;

        /// <summary>Action points the ability needs right now (sniper shots need both; free actions need none).</summary>
        public static int ApNeeded(Unit u, string ability)
        {
            var def = Content.Abilities[ability];
            if (def.Ap == 0) return 0;
            return Content.IsAttack(ability) ? Math.Max(def.Ap, Content.AttackWeapon(u, ability).ApCost) : def.Ap;
        }

        /// <summary>Whether using the ability spends every action left (perks can lift that).</summary>
        public static bool EndsTurn(Unit u, string ability)
        {
            if ((ability == AbilityIds.Grenade || ability == AbilityIds.Launch) && Content.HasPerk(u, PerkIds.Salvo)) return false;
            return Content.Abilities[ability].EndsTurn;
        }

        /// <summary>Rounds the ability needs in the clip (attacks with a magazine need at least one).</summary>
        public static int AmmoNeeded(Unit u, string ability)
        {
            var def = Content.Abilities[ability];
            if (def.Ammo.HasValue && def.Ammo.Value != 0) return def.Ammo.Value;
            if (ability == AbilityIds.Overwatch || ability == AbilityIds.KillZone) return Content.ClipOf(u, AbilityIds.Shoot) > 0 ? 1 : 0;
            return Content.IsAttack(ability) && Content.ClipOf(u, ability) > 0 ? 1 : 0;
        }

        /// <summary>Tile abilities done with the hands beside the soldier, not thrown: a disoriented soldier can still do them.</summary>
        private static readonly HashSet<string> ByHand = new HashSet<string> { AbilityIds.Search, AbilityIds.SearchRoom, AbilityIds.OpenDoor, AbilityIds.CloseDoor, AbilityIds.Smash };

        /// <summary>
        /// Doors beside `u` it can open (`door`) or close (`doorOpen`, with nobody in
        /// the doorway), reaching them with the hands as when searching (loot.ts
        /// reachesToSearch): beside it, on its level, not round a wall's corner.
        /// </summary>
        public static List<Pos> DoorTiles(GameState s, Unit u, string kind)
        {
            return Grid.Dirs8.Select((d) => new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = u.Pos.Z })
                .Where((p) => Grid.KindAt(s, p) == kind && Looting.ReachesToSearch(s, u.Pos, p) && !(kind == TileKinds.DoorOpen && Grid.UnitAt(s, p) != null))
                .ToList();
        }

        /// <summary>In a siege (docs/FASE12.md §4), a door still shut, or a barricade, a wall or a building still up, that the dead can break down.</summary>
        public static bool Breakable(GameState s, Pos p)
        {
            var kind = Grid.KindAt(s, p);
            return (kind == TileKinds.Door || kind == TileKinds.Low || kind == TileKinds.Wall || kind == TileKinds.High) && HoldsAt(s, Grid.NodeOf(s, p)) > 0;
        }

        /// <summary>`s.breakables?.[node] ?? 0`.</summary>
        internal static int HoldsAt(GameState s, int node)
        {
            return s.Breakables != null && s.Breakables.TryGetValue(node, out var n) ? n : 0;
        }

        /// <summary>What one of the dead on `from` can beat on: beside it, reached as with the hands (doorTiles).</summary>
        public static List<Pos> BreakablesBeside(GameState s, Pos from)
        {
            if (s.Breakables == null) return new List<Pos>();
            return Grid.Dirs8.Select((d) => new Pos { X = from.X + d.X, Y = from.Y + d.Y, Z = from.Z })
                .Where((p) => Grid.InBounds(s, p.X, p.Y) && Breakable(s, p) && Looting.ReachesToSearch(s, from, p))
                .ToList();
        }

        /// <summary>Why the ability cannot be used right now (ignoring targets), or null if it can.</summary>
        public static string? AbilityBlocker(GameState s, Unit u, string ability)
        {
            if (!AbilitiesOf(u).Contains(ability)) return "No tiene esa habilidad.";
            var def = Content.Abilities[ability];
            var need = ApNeeded(u, ability);
            if (u.Ap <= 0) return "Sin acciones.";
            if (u.Ap < need) return need == 2 ? "Necesita 2 acciones: no puede moverse y disparar en el mismo turno." : "Sin acciones.";
            var cooldown = u.Cooldowns.TryGetValue(ability, out var cd) ? cd : 0;
            if (cooldown > 0) return $"Disponible en {cooldown} {(cooldown == 1 ? "turno" : "turnos")}.";
            if (!string.IsNullOrEmpty(def.Charge) && !((u.Charges.TryGetValue(def.Charge!, out var left) ? left : 0) > 0)) return "No quedan usos.";
            if (Content.RunsChainsaw(u, ability) && !((u.Charges.TryGetValue(ChargeIds.Fuel, out var fuel) ? fuel : 0) > 0)) return "La motosierra no tiene gasolina hasta la próxima misión.";
            var ammo = AmmoNeeded(u, ability);
            if (ammo > 0 && (Content.IsAttack(ability) ? Content.RoundsOf(u, ability) : u.Ammo) < ammo)
            {
                if (u.Mags == 0) return "Sin munición y sin cargadores.";
                return ammo > 1 ? $"Necesita {ammo} balas en el cargador: recarga." : "Sin munición: recarga.";
            }
            if (u.Disoriented != 0 && def.Target == Engine.AbilityTargets.Tile && !ByHand.Contains(ability) && ability != AbilityIds.Stone) return "Con desorientación no se pueden usar explosivos.";
            if (Content.FiresPrimary(u, ability) && Content.ConditionOf(u) <= 0) return "Rota: no dispara. Se arregla en la Armería.";
            switch (ability)
            {
                case AbilityIds.Reload:
                    if (Content.FullyLoaded(u)) return !string.IsNullOrEmpty(u.Arms?.Primary) ? "Los cargadores están llenos." : "El cargador está lleno.";
                    if (u.Mags == 0) return "No quedan cargadores.";
                    break;
                case AbilityIds.Hunker:
                    if (!Cover.HasAnyCover(s, u.Pos)) return "Necesita estar a cubierto.";
                    break;
                case AbilityIds.Evac:
                    if (!InEvacZone(s, u.Pos)) return "Solo desde la zona de evacuación.";
                    break;
                case AbilityIds.ElbowRoom:
                    if (BesideEnemies(s, u).Count == 0) return "No tienes ningún enemigo al lado.";
                    break;
                case AbilityIds.Hack:
                    if (s.Mission.Terminal == null) return "No hay ningún equipo que arreglar en esta misión.";
                    if (s.Mission.ObjectiveDone) return "El equipo ya está en marcha.";
                    if (!CanHackFrom(s, u)) return "Ponte junto al equipo.";
                    break;
                case AbilityIds.Search:
                    // A loot box beside the soldier (docs/FASE8.md §B). A full backpack doesn't stop it: what doesn't fit goes on the floor.
                    if (Looting.SearchableTiles(s, u).Count == 0)
                    {
                        var done = Grid.Dirs8.Any((d) =>
                        {
                            var p = new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = u.Pos.Z };
                            return Looting.SearchSpot(s, p) != null && Looting.ReachesToSearch(s, u.Pos, p);
                        });
                        return done ? "Ya está abierta." : "No hay ninguna caja al lado.";
                    }
                    break;
                case AbilityIds.SearchRoom:
                {
                    var room = Looting.RoomAt(s, u.Pos);
                    if (room == null) return "Solo dentro de una habitación.";
                    if (Looting.RoomSearched(s, room)) return "Esta habitación ya está registrada.";
                    break;
                }
                case AbilityIds.OpenDoor:
                    if (DoorTiles(s, u, TileKinds.Door).Count == 0) return "No hay ninguna puerta cerrada a tu lado.";
                    break;
                case AbilityIds.CloseDoor:
                    if (DoorTiles(s, u, TileKinds.DoorOpen).Count == 0) return "No hay ninguna puerta abierta a tu lado.";
                    break;
                case AbilityIds.Smash:
                    if (BreakablesBeside(s, u.Pos).Count == 0) return "No hay ninguna puerta ni barricada a su lado.";
                    break;
            }
            return null;
        }

        private static bool Hostile(Unit u, Unit t)
        {
            return t.Alive && Content.HostileTo(u, t) && !t.Captive;
        }

        /// <summary>Enemies within reach of a blade without a step (Hacerse sitio, docs/FASE9.md).</summary>
        public static List<Unit> BesideEnemies(GameState s, Unit u)
        {
            return s.Units.Where((t) => Hostile(u, t) && Aim.InMeleeReach(s, u.Pos, t.Pos)).ToList();
        }

        /// <summary>Enemies a ranged attack can hit right now.</summary>
        public static List<Unit> RangedTargets(GameState s, Unit u, string ability)
        {
            return s.Units.Where((t) => Hostile(u, t) && Aim.PreviewShot(s, u, t, new ShotOptions { Ability = ability }) != null).ToList();
        }

        /// <summary>
        /// Enemies a melee ability can reach: only whoever is alongside, seen by the
        /// team. Striking is striking and running is running (Arturo, 16/10): getting
        /// there is a move of its own. `ability` stays for the callers.
        /// </summary>
        public static List<MeleeOption> MeleeTargets(GameState s, Unit u, string ability)
        {
            return s.Units.Where((t) => Hostile(u, t) && Aim.TeamSees(s, u.Team, t) && Aim.InMeleeReach(s, u.Pos, t.Pos))
                .Select((t) => new MeleeOption { Target = t, Tile = u.Pos, Path = new List<Pos>() })
                .ToList();
        }

        /// <summary>Statuses Revival and Restoration remove.</summary>
        public static bool HasBadStatus(Unit u)
        {
            return u.Stunned || u.Disoriented > 0 || u.Panicked;
        }

        /// <summary>Allies a support ability can target (`'revival' | 'firstAid' | 'patchUp'`).</summary>
        public static List<Unit> AllyTargets(GameState s, Unit u, string ability)
        {
            var range = Content.Abilities[ability].Range ?? double.PositiveInfinity;
            return s.Units.Where((t) =>
            {
                if (!t.Alive || t.Team != u.Team || t.Captive || Grid.Distance(u.Pos, t.Pos) > range) return false;
                // By hand, beside the ally: never through a floor.
                if (range <= 1.5 && !Grid.SameSpace(s, u.Pos, t.Pos)) return false;
                if (ability == AbilityIds.FirstAid) return t.Hp < t.MaxHp;
                if (ability == AbilityIds.Revival) return HasBadStatus(t);
                // Apaños (docs/FASE9.md): a weapon in the hand that isn't as good as new, its own or a partner's alongside.
                return !string.IsNullOrEmpty(t.Arms?.Primary) && Content.ConditionOf(t) < 100;
            }).ToList();
        }

        /// <summary>
        /// Where a corpse gets up (the Chillón's, or one of the bitten): on its own
        /// tile if nobody stands there, else on the first free tile a step away on its
        /// floor (not a shut door); null with none, and it can't rise yet. Nobody
        /// rises on top of anyone (Arturo, 19/10). One that fell on a flight of stairs
        /// gets up off it, at its nearer end (flights.ts: nobody stops there).
        /// </summary>
        public static Pos? RiseSpot(GameState s, Unit corpse)
        {
            var from = corpse.Pos;
            bool Free(Pos p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null;
            if (Free(from)) return new Pos { X = from.X, Y = from.Y, Z = from.Z };
            var on = Grid.KindAt(s, from) == TileKinds.Ladder ? Flights.LongFlightAt(s, from) : null;
            if (on != null)
            {
                var ends = new List<Pos> { Flights.FlightFoot(on.Flight), Flights.FlightLanding(on.Flight) };
                if (on.Step * 2 >= on.Flight.Tiles.Count - 1) ends.Reverse();
                return ends.FirstOrDefault(Free);
            }
            foreach (var d in Grid.Dirs8)
            {
                var p = new Pos { X = from.X + d.X, Y = from.Y + d.Y, Z = from.Z };
                if (Free(p) && Grid.KindAt(s, p) != TileKinds.Door && Grid.HeightAt(s, p) == Grid.HeightAt(s, from) && Paths.StepBetween(s, from, p) != null) return p;
            }
            return null;
        }

        /// <summary>Corpses a sectoid can raise: in range and in sight, flesh and blood, with room to get up (riseSpot).</summary>
        public static List<Unit> CorpseTargets(GameState s, Unit u)
        {
            var range = Content.Abilities[AbilityIds.Reanimate].Range ?? 0;
            return s.Units.Where((t) =>
                !t.Alive &&
                !t.Evacuated &&
                !t.Reanimated &&
                !t.Objective &&
                !Content.Templates[t.Template].Robotic &&
                t.Template != TemplateIds.Zombie &&
                Grid.Distance(u.Pos, t.Pos) <= range &&
                Los.CanSeeTile(s, u.Pos, t.Pos, Los.SightTo(s, u, t.Pos)) &&
                RiseSpot(s, t) != null).ToList();
        }

        /// <summary>Every valid unit target of a targeted ability (attacks, support, psionics, corpses).</summary>
        public static List<Unit> AbilityTargets(GameState s, Unit u, string ability)
        {
            var def = Content.Abilities[ability];
            switch (ability)
            {
                case AbilityIds.Suppression:
                    return RangedTargets(s, u, AbilityIds.Shoot);
                case AbilityIds.Mindspin:
                    return s.Units.Where((t) => Hostile(u, t) && Grid.Distance(u.Pos, t.Pos) <= (def.Range ?? 0) && Los.CanSee(s, u, t) && PsiChance(t, s) > 0).ToList();
                case AbilityIds.Reanimate:
                    return CorpseTargets(s, u);
                case AbilityIds.Revival:
                case AbilityIds.FirstAid:
                case AbilityIds.PatchUp:
                    return AllyTargets(s, u, ability);
            }
            if (def.Target == Engine.AbilityTargets.Melee && Content.IsAttack(ability)) return MeleeTargets(s, u, ability).Select((o) => o.Target).ToList();
            if (def.Target == Engine.AbilityTargets.Enemy && Content.IsAttack(ability)) return RangedTargets(s, u, ability);
            return new List<Unit>();
        }

        /// <summary>Explosives: target tile within range and seen by the squad (not necessarily by the thrower).</summary>
        public static bool CanTargetTile(GameState s, Unit u, string ability, Pos at)
        {
            var tile = Grid.ToPos(at);
            var def = Content.Abilities[ability];
            if (def.Target != Engine.AbilityTargets.Tile || !Grid.InBounds(s, tile.X, tile.Y)) return false;
            if (Grid.Distance(u.Pos, tile) > AbilityRange(u, ability)) return false;
            if (ability == AbilityIds.Search) return Looting.IsSearchable(s, tile) && Looting.ReachesToSearch(s, u.Pos, tile);
            if (ability == AbilityIds.OpenDoor || ability == AbilityIds.CloseDoor) return DoorTiles(s, u, ability == AbilityIds.OpenDoor ? TileKinds.Door : TileKinds.DoorOpen).Any((p) => Grid.SamePos(p, tile));
            if (ability == AbilityIds.Smash) return BreakablesBeside(s, u.Pos).Any((p) => Grid.SamePos(p, tile));
            return s.Units.Any((o) => Los.IsViewer(o, u.Team) && Los.CanSeeTile(s, o.Pos, tile, Los.SightTo(s, o, tile)));
        }

        /// <summary>Reach of a targeted ability for this unit (talents can lengthen grenade throws).</summary>
        public static double AbilityRange(Unit? u, string ability)
        {
            var @base = Content.Abilities[ability].Range ?? 0;
            return Content.Thrown.Contains(ability) ? @base + (u?.Mods.GrenadeRange ?? 0) : @base;
        }

        /// <summary>Blast radius of an area ability for this unit (perks can widen grenades).</summary>
        public static double BlastRadius(Unit? u, string ability)
        {
            var @base = Content.Abilities[ability].Radius ?? 0;
            return ability == AbilityIds.Grenade || ability == AbilityIds.Launch ? @base + (u?.Mods.GrenadeRadius ?? 0) : @base;
        }

        /// <summary>
        /// The places a blast on `at` reaches: within its radius, and on a map with
        /// storeys only those in its space (grid.ts sameSpace: a floor stops it).
        /// </summary>
        public static List<Pos> BlastTiles(GameState s, string ability, Pos at, Unit? thrower = null)
        {
            var center = Grid.ToPos(at);
            var radius = BlastRadius(thrower, ability);
            var out_ = new List<Pos>();
            var r = (int)Math.Ceiling(radius);
            for (var y = center.Y - r; y <= center.Y + r; y++)
            {
                for (var x = center.X - r; x <= center.X + r; x++)
                {
                    if (!Grid.InBounds(s, x, y) || Grid.Distance(center, new Vec2 { X = x, Y = y }) > radius) continue;
                    if (s.Levels == null) out_.Add(new Pos { X = x, Y = y, Z = 0 });
                    else foreach (var p in Grid.PlacesOn(s, x, y)) if (Grid.SameSpace(s, center, p)) out_.Add(p);
                }
            }
            return out_;
        }

        /// <summary>Explosives that breach walls (docs/FASE7.md §4): the frag grenades, thrown, launched or going off on a vest.</summary>
        private static readonly HashSet<string> Breaching = new HashSet<string> { AbilityIds.Grenade, AbilityIds.Launch, AbilityIds.Missiles };

        /// <summary>
        /// Walls a blast breaks (docs/FASE7.md §4), left as rubble: within its radius,
        /// on the floor it went off on, and beside one of the tiles it reaches. None
        /// for anything but the frag grenades.
        /// </summary>
        public static List<Pos> BreachedWalls(GameState s, string ability, Pos at, Unit? thrower = null)
        {
            if (!Breaching.Contains(ability)) return new List<Pos>();
            var center = Grid.ToPos(at);
            var radius = BlastRadius(thrower, ability);
            var reached = new HashSet<int>(BlastTiles(s, ability, at, thrower).Where((p) => Grid.KindAt(s, p) != TileKinds.Wall).Select((p) => Grid.NodeOf(s, p)));
            var out_ = new List<Pos>();
            var r = (int)Math.Ceiling(radius);
            for (var y = center.Y - r; y <= center.Y + r; y++)
            {
                for (var x = center.X - r; x <= center.X + r; x++)
                {
                    var p = new Pos { X = x, Y = y, Z = center.Z };
                    if (!Grid.InBounds(s, x, y) || Grid.Distance(center, p) > radius || Grid.KindAt(s, p) != TileKinds.Wall) continue;
                    if (Grid.Dirs4.Any((d) => Grid.InBounds(s, x + d.X, y + d.Y) && reached.Contains(Grid.NodeOf(s, new Pos { X = x + d.X, Y = y + d.Y, Z = center.Z })))) out_.Add(p);
                }
            }
            return out_;
        }

        /// <summary>
        /// Whether a blast on `at` breaks the slab it went off on (docs/FASE7.md §5): a frag grenade on a storey's floor or a
        /// roof; never where it bears a flight of stairs (flights.ts bearsFlight: its tiles, its foot, its landing).
        /// </summary>
        public static bool BreaksFloor(GameState s, string ability, Pos at)
        {
            var p = Grid.ToPos(at);
            return Breaching.Contains(ability) && p.Z >= 1 && Grid.SlabAt(s, p.X, p.Y, p.Z) && !Flights.BearsFlight(s, p);
        }

        /// <summary>
        /// How far, in tiles, using the ability is heard (docs/FASE2.md, `NOISE` in
        /// content.ts): an attack sounds like its weapon, a blast like its explosive,
        /// unless the ability says otherwise. Heard where a tile ability lands, or
        /// where the user stands.
        /// </summary>
        public static int NoiseOf(GameState s, Unit u, string ability)
        {
            var def = Content.Abilities[ability];
            int noise;
            if (def.Noise.HasValue) noise = def.Noise.Value;
            else if (Content.IsAttack(ability)) noise = Content.AttackWeapon(u, ability).Noise;
            else if (ability == AbilityIds.Suppression) noise = Content.AttackWeapon(u, AbilityIds.Shoot).Noise;
            else noise = !string.IsNullOrEmpty(def.Explosive) ? Content.Weapons[def.Explosive!].Noise : 0;
            // Sin hacer ruido (docs/FASE9.md): searching and opening boxes in silence. A silencer halves a shot's noise.
            if (u.Mods.Quiet != 0 && (ability == AbilityIds.Search || ability == AbilityIds.SearchRoom)) return 0;
            if (ability == AbilityIds.Stone) noise += u.Mods.StoneNoise;
            if (Content.FiresPrimary(u, ability) && Content.HasMod(u, WeaponModIds.Silencer)) noise = (int)Math.Ceiling(noise / 2.0);
            return noise;
        }
    }
}
