#nullable enable
// The runs' stages in a fight (H5 and H6; new in C#): the Sigilo seen, El Toro's roar and charge, and getting up. The
// charge's line is Toro's, pure, for the AI and for UI's line while aiming.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>El Toro's numbers and his charge's line (H6, .dev-snapshots/gameplay/runs/H6-TORO.md).</summary>
    public static class Toro
    {
        /// <summary>What the charge does to whoever it crashes into.</summary>
        public const int ChargeDamage = 4;
        /// <summary>Tiles the charge runs at most.</summary>
        public const int ChargeRun = 6;
        /// <summary>Defence a soldier on the ground loses (Unit.Down).</summary>
        public const int DownDefense = 20;

        private static readonly HashSet<string> Solid = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Bars };

        /// <summary>
        /// The tiles of a charge from `u` at `t`: a straight line one of eight ways, t on it no more than ChargeRun + 1 tiles
        /// off and seen, every tile before it free to stop on at the same height, no wall's corner cut; the last beside t.
        /// Null if there is no such line (t alongside: a Mazazo, not a charge).
        /// </summary>
        public static List<Pos>? ChargePath(GameState s, Unit u, Unit t)
        {
            var dx = t.Pos.X - u.Pos.X;
            var dy = t.Pos.Y - u.Pos.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (t.Pos.Z != u.Pos.Z || steps < 2 || steps > ChargeRun + 1 || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))) return null;
            if (!Los.CanSee(s, u, t)) return null;
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            bool IsSolid(int x, int y)
            {
                var kind = Grid.KindAt(s, new Pos { X = x, Y = y, Z = u.Pos.Z });
                return kind != null && Solid.Contains(kind);
            }
            var path = new List<Pos>();
            var from = u.Pos;
            for (var k = 1; k <= steps; k++)
            {
                var p = new Pos { X = u.Pos.X + sx * k, Y = u.Pos.Y + sy * k, Z = u.Pos.Z };
                // Round a corner a diagonal step doesn't go.
                if (sx != 0 && sy != 0 && (IsSolid(from.X + sx, from.Y) || IsSolid(from.X, from.Y + sy))) return null;
                if (k == steps) break;
                if (!Grid.InBounds(s, p.X, p.Y) || !Flights.CanStopAt(s, p) || Grid.UnitAt(s, p) != null || Grid.HeightAt(s, p) != Grid.HeightAt(s, u.Pos)) return null;
                path.Add(p);
                from = p;
            }
            return path;
        }

        /// <summary>The soldiers El Toro can charge now, the nearest first.</summary>
        public static List<(Unit Target, List<Pos> Path)> ChargeTargets(GameState s, Unit u)
        {
            var out_ = new List<(Unit Target, List<Pos> Path)>();
            foreach (var t in Grid.Combatants(s, Teams.Xcom))
            {
                var path = ChargePath(s, u, t);
                if (path != null) out_.Add((t, path));
            }
            return out_.OrderBy((x) => x.Path.Count).ThenBy((x) => x.Target.Id, StringComparer.Ordinal).ToList();
        }
    }

    public sealed partial class Sim
    {
        /// <summary>
        /// A run's Sigilo seen (H5): a big wave at once by the edges (twice a horde's wave, in groups of five), landed now.
        /// </summary>
        public void StealthSpotted()
        {
            var m = S.Mission;
            var units = Setup.HordeWave(m.Difficulty ?? 2).Concat(Setup.HordeWave(m.Difficulty ?? 2)).ToList();
            var ids = new HashSet<string>();
            for (var i = 0; i < units.Count; i += 5)
            {
                var r = new Reinforcement { Id = $"r{m.Reinforcements.Count + 1}", Turn = S.Turn, Pos = null, Units = units.Skip(i).Take(5).ToList(), Landed = false, Horde = true, Side = m.HordeSide };
                Emit(new ReinforcementsIncomingEvent { R = r });
                ids.Add(r.Id);
            }
            LandReinforcements(S.Turn, ids);
        }

        /// <summary>El Toro roars (H6): every group of the map awake, and all of them come to him (a noise the whole map hears).</summary>
        public void Roar(Unit u)
        {
            Emit(new RoaredEvent { Unit = u.Id });
            var asleep = S.Pods.Where((p) => !p.Active && p.UnitIds.Any((id) => Grid.GetUnit(S, id)?.Alive == true)).Select((p) => p.Id).ToList();
            ActivatePods(asleep, new Activation { Cause = PodCauses.Attacked, By = u.Id });
            MakeNoise(u.Pos, S.Width + S.Height, u);
        }

        /// <summary>
        /// El Toro's charge at `t` (H6), its actions already paid: along the line (Toro.ChargePath), the soldier knocked down
        /// where it stands and hurt. A line cut by now: as far as it goes, and nobody hit.
        /// </summary>
        public void Charge(Unit u, Unit t)
        {
            var path = Toro.ChargePath(S, u, t);
            if (path == null)
            {
                Emit(new ChargedEvent { Unit = u.Id, Path = new List<Pos>(), Target = null });
                return;
            }
            Emit(new ChargedEvent { Unit = u.Id, Path = path.Select(Engine.Events.CopyPos).ToList(), Target = t.Id });
            Emit(new KnockedDownEvent { Unit = t.Id });
            Damage(t, Toro.ChargeDamage, 0, u);
        }

        /// <summary>Whoever is on the ground gets up as the squad's turn begins, before anything (one of its actions).</summary>
        public void GetUp()
        {
            foreach (var d in S.Units.Where((x) => x.Alive && x.Down == true).ToList()) Emit(new GotUpEvent { Unit = d.Id });
        }
    }

    public static partial class Rules
    {
        /// <summary>The cell a caged one is in (H9): its room's tiles; none if it is in no cell.</summary>
        private static List<Pos> CellOf(GameState s, Unit u)
        {
            foreach (var site in s.Interiors ?? new List<InteriorSite>())
            {
                foreach (var r in site.Rooms.Where((r) => r.Use == PoliceUses.Cells && r.Z == u.Pos.Z))
                {
                    if (u.Pos.X < r.X || u.Pos.Y < r.Y || u.Pos.X >= r.X + r.W || u.Pos.Y >= r.Y + r.H) continue;
                    return Enumerable.Range(r.Y, r.H).SelectMany((y) => Enumerable.Range(r.X, r.W).Select((x) => new Pos { X = x, Y = y, Z = r.Z })).ToList();
                }
            }
            return new List<Pos>();
        }

        /// <summary>The tiles of a caged one's cell just inside its bars, where it stands to bite (H9; Arturo, 17/11).</summary>
        public static List<Pos> CagedFront(GameState s, Unit u)
        {
            return CellOf(s, u).Where((p) => Grid.Dirs4.Any((d) => Grid.InBounds(s, p.X + d.X, p.Y + d.Y) && Grid.KindAt(s, new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z }) == TileKinds.Bars)).ToList();
        }

        /// <summary>
        /// The tiles a caged one can reach from its bars (H9; for the bots, Networking): those across them, straight, from any
        /// tile of its cell just inside them (Aim.AcrossBars). Only there is it dangerous. None if `u` is not caged.
        /// </summary>
        public static List<Pos> CagedReach(GameState s, Unit u)
        {
            var out_ = new List<Pos>();
            if (u.Caged != true) return out_;
            foreach (var p in CagedFront(s, u))
            {
                foreach (var d in Grid.Dirs4)
                {
                    var bar = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z };
                    var q = new Pos { X = p.X + 2 * d.X, Y = p.Y + 2 * d.Y, Z = p.Z };
                    if (!Grid.InBounds(s, q.X, q.Y) || Grid.KindAt(s, bar) != TileKinds.Bars || !Grid.IsWalkable(Grid.KindAt(s, q))) continue;
                    if (!out_.Any((o) => Grid.SamePos(o, q))) out_.Add(q);
                }
            }
            return out_;
        }

        /// <summary>
        /// Whether getting out on La Lata is all a mission asks (a Saqueo); not a Guarida's (its box to open), a boss's (him to
        /// kill) nor a Separados' (one of each group: SplitSafe), which are Saqueos with an objective (H5, H6, G23.4).
        /// </summary>
        public static bool FreeExit(MissionState m)
        {
            return m.Kind == MissionKinds.Scavenge && m.Lair == null && string.IsNullOrEmpty(m.Boss) && m.Split == null;
        }

        // -------------------------------------------------------------- Separados

        /// <summary>The living of `group` of a Separados (MissionState.Split): on the field, or in La Lata.</summary>
        private static IEnumerable<Unit> SplitUnits(GameState s, int group)
        {
            var ids = s.Mission.Split != null && group < s.Mission.Split.Count ? s.Mission.Split[group] : new List<string>();
            return ids.Select((id) => Grid.GetUnit(s, id)).Where((u) => u != null).Select((u) => u!);
        }

        /// <summary>
        /// G23.4 Separados won (Arturo, cuadro 94: «con alguien de cada grupo a bordo»): one of each group in La Lata, or gone
        /// in it (Evacuated). As La Lata goes (CheckOutcome, DepartEvac). A survivor who joined counts for neither.
        /// </summary>
        public static bool SplitSafe(GameState s)
        {
            return s.Mission.Split != null && Enumerable.Range(0, s.Mission.Split.Count).All((g) => SplitUnits(s, g).Any((u) => u.Evacuated));
        }

        /// <summary>
        /// Separados: a group all fallen (nobody of it alive nor in La Lata): the reward is lost whatever happens (Lore's
        /// line; no warning on leaving any more).
        /// </summary>
        public static bool SplitLost(GameState s)
        {
            return s.Mission.Split != null && Enumerable.Range(0, s.Mission.Split.Count).Any((g) => !SplitUnits(s, g).Any((u) => u.Alive || u.Evacuated));
        }

        /// <summary>
        /// Separados, for the warning on leaving (UI, Lore; SEPARADOS.md): the living of the group La Lata would leave
        /// without if it went now (nobody of it inside nor in its zone with a seat). Null with one of each going, with a group
        /// all fallen (SplitLost: no reward to lose), or out of a Separados.
        /// </summary>
        public static List<Unit>? SplitShort(GameState s)
        {
            if (s.Mission.Split == null || SplitLost(s)) return null;
            var noSeat = NoSeat(s);
            bool Goes(Unit u) => u.Evacuated || (u.Alive && Abilities.InEvacZone(s, u.Pos) && !noSeat.Contains(u));
            for (var g = 0; g < s.Mission.Split.Count; g++)
                if (!SplitUnits(s, g).Any(Goes)) return SplitUnits(s, g).Where((u) => u.Alive).ToList();
            return null;
        }

        /// <summary>
        /// Whether `o`, one of the dead, bites `u` of the squad going past it on `step` (T20, Arturo 18/11): hostile, beside it
        /// within a blade's reach, and seeing it there (its view cone, all round this close: Los.AwareRange; and its line of
        /// sight, as an overwatch's). Not `u`'s last tile: the walk's (Sim.PassBiters).
        /// </summary>
        public static bool PassBite(GameState s, Unit o, Unit u, Pos step)
        {
            return o.Alive && u.Team == Teams.Xcom && Content.HostileTo(o, u) && Content.Templates[o.Template].Undead
                && Aim.InMeleeReach(s, o.Pos, step) && Los.InViewCone(o, step)
                && Los.SightOrigin(s, o.Pos, step, Los.SightTo(s, o, step), Los.WindowReachOf(o.Team)) != null;
        }

        /// <summary>
        /// Where on `path` (the tiles `u` walks, as Paths.BuildPath gives them) an enemy the squad sees now would react (T20,
        /// for the path drawn before moving): a bite going past one of the dead (PassBite, never on the last tile), a shot or
        /// a blade of whoever keeps watch, a shot of whoever suppresses it; each once, at the first tile it would. Without
        /// dice: whether it hits is the reaction's roll. Nothing of an enemy out of the squad's sight, nor with Shadowstep.
        /// </summary>
        public static List<PathReaction> ReactionsAlong(GameState s, Unit u, IReadOnlyList<Pos>? path)
        {
            var out_ = new List<PathReaction>();
            if (path == null || path.Count == 0 || u.Team != Teams.Xcom || Content.HasPerk(u, PerkIds.Shadowstep)) return out_;
            var seen = Los.VisibleEnemies(s, Teams.Xcom);
            var done = new HashSet<string>();
            for (var i = 0; i < path.Count; i++)
            {
                var step = path[i];
                foreach (var o in seen)
                {
                    if (done.Contains(o.Id) || !o.Alive || !Content.HostileTo(o, u)) continue;
                    string? kind = null;
                    if (i < path.Count - 1 && PassBite(s, o, u, step)) kind = PathReactions.Bite;
                    else if (o.Overwatch && !(o.KillZone && o.ReactedTo.Contains(u.Id)) && (o.WatchMelee == true ? Aim.InMeleeReach(s, o.Pos, step) : Content.LoadedFor(o, AbilityIds.Shoot) && Los.SightOrigin(s, o.Pos, step, Los.SightTo(s, o, step), Los.WindowReachOf(o.Team)) != null)) kind = PathReactions.Watch;
                    else if (o.Suppressing == u.Id && Content.LoadedFor(o, AbilityIds.Shoot)) kind = PathReactions.Suppress;
                    if (kind == null) continue;
                    done.Add(o.Id);
                    out_.Add(new PathReaction { Index = i, Tile = Events.CopyPos(step), Unit = o.Id, Kind = kind });
                }
            }
            return out_;
        }

        /// <summary>
        /// A run's Rescate (N2): won as a Saqueo, getting out on La Lata, whatever became of the survivor; brought out, they
        /// join (RunFights.Rescued).
        /// </summary>
        public static bool RunsRescue(GameState s)
        {
            // Mordido's (G23.2): won only with the bitten cured and aboard (EvacWins). El herido's (G23.3), with them aboard.
            return s.Run != null && s.Mission.Kind == MissionKinds.Rescue && s.Mission.Bitten != true && s.Mission.Wounded != true;
        }

        /// <summary>G23.3 El herido: the wounded unit (MissionState.ObjectiveUnit), or null outside the stage.</summary>
        public static Unit? WoundedOf(GameState s)
        {
            return s.Mission.Wounded == true && !string.IsNullOrEmpty(s.Mission.ObjectiveUnit) ? Grid.GetUnit(s, s.Mission.ObjectiveUnit!) : null;
        }

        /// <summary>
        /// G23.3 El herido (Arturo, cuadro 73: «No se sale sin él»): the wounded alive and not in La Lata: on the floor, carried,
        /// or not found yet. La Lata doesn't go meanwhile (EvacCommand, AllAboard). Dead, it may.
        /// </summary>
        public static bool WoundedOut(GameState s)
        {
            var w = WoundedOf(s);
            return w != null && !w.Evacuated && (w.Alive || w.CarriedBy != null);
        }

        /// <summary>G23.3 El herido won: the wounded in La Lata, or gone in it (Evacuated), as it goes (CheckOutcome, DepartEvac).</summary>
        public static bool WoundedSafe(GameState s)
        {
            return WoundedOf(s) is Unit w && w.Evacuated;
        }

        /// <summary>
        /// G23.2 Mordido: whether `u` is the bitten, not cured. La Lata never takes them (WouldBoard, AllAboard, DepartEvac):
        /// they would turn in it. Left behind if it goes.
        /// </summary>
        public static bool BittenUncured(GameState s, Unit u)
        {
            return s.Mission.Bitten == true && s.Mission.Cured != true && u.Id == s.Mission.ObjectiveUnit;
        }

        /// <summary>
        /// G23.2 Mordido won: the bitten cured and in La Lata, or gone in it (Evacuated). As La Lata goes (CheckOutcome,
        /// DepartEvac), not before: MissionState.ObjectiveDone stays the Rescate's own (UI's «en La Lata» and «llena» lines).
        /// </summary>
        public static bool BittenSafe(GameState s)
        {
            return s.Mission.Bitten == true && s.Mission.Cured == true && s.Units.FirstOrDefault((u) => u.Id == s.Mission.ObjectiveUnit) is Unit b && b.Evacuated;
        }

        /// <summary>
        /// A run's survivor not yet found that `u` of the squad sees from `from` (N2; MissionState.Survivor): in its line of sight and not hidden
        /// in a building the squad hasn't found (Buildings.Hidden, as an enemy would be). Null otherwise.
        /// </summary>
        public static Unit? SurvivorSeen(GameState s, Unit u, Pos from)
        {
            if (s.Mission.SurvivorFound == true || u.Team != Teams.Xcom || u.Captive) return null;
            var vip = Abilities.Waiting(s);
            if (vip == null || Buildings.Hidden(s, vip.Pos, new[] { from })) return null;
            return Los.SightOrigin(s, from, vip.Pos, Los.SightTo(s, u, vip.Pos), Los.WindowReachOf(Teams.Xcom)) != null ? vip : null;
        }

        /// <summary>
        /// Whether a unit is shown to the players (N2): a run's survivor only once found; everyone else, as ever. For the
        /// client's figures and the HUD.
        /// </summary>
        public static bool SurvivorShown(GameState s, Unit u)
        {
            return !(s.Run != null && u.Id == s.Mission.Survivor && s.Mission.SurvivorFound != true);
        }

        // ------------------------------------------------------------- La Lata (T24)

        /// <summary>Whether La Lata is there to get into (T24): a run's, its zone drawn, not gone, and in an Asedio come.</summary>
        public static bool LaLataHere(GameState s)
        {
            var m = s.Mission;
            return s.Run != null && m.Seats != null && m.EvacZone.Count > 0 && m.EvacDone != true && !(m.HoldUntil != null && s.Turn < m.HoldUntil);
        }

        /// <summary>La Lata's seats nobody is in yet (T24): MissionState.Seats less who is aboard (Unit.Aboard). Null without seats.</summary>
        public static int? SeatsFree(GameState s)
        {
            var seats = s.Mission.Seats;
            // G23.3: the wounded takes no seat (Arturo, cuadro 92: «no ocupa plaza»).
            return seats == null ? (int?)null : Math.Max(0, seats.Value - s.Units.Count((u) => u.Aboard == true && u.Lame != true));
        }

        /// <summary>One of the squad that can get into La Lata: a soldier or one who joined, not a captive, the relay or a turret.</summary>
        private static bool Boarder(Unit u) => u.Team == Teams.Xcom && !u.Captive && !u.Objective && u.Automaton == null;

        /// <summary>
        /// Whether `u`, its walk ended where it stands, gets into La Lata (T24, Arturo 18/11): one of the squad on a tile of
        /// its zone, La Lata there (LaLataHere), a seat free. Without one it stays on the tile (Lore.BoardText.Full).
        /// </summary>
        public static bool Boards(GameState s, Unit u)
        {
            return u.Alive && Boarder(u) && LaLataHere(s) && Abilities.InEvacZone(s, u.Pos) && (SeatsFree(s) ?? 0) > 0;
        }

        /// <summary>Whether a walk of `u` ending on `tile` would get it into La Lata (T24): for the path drawn before moving.</summary>
        public static bool WouldBoard(GameState s, Unit u, Pos tile)
        {
            return u.Alive && Boarder(u) && !BittenUncured(s, u) && LaLataHere(s) && Abilities.InEvacZone(s, tile) && (SeatsFree(s) ?? 0) > 0;
        }

        /// <summary>
        /// Whether La Lata goes by itself (T24: «sale sola solo si ya están dentro todos los vivos»): someone aboard, and
        /// nobody of the squad alive out of it (a captive aside: they don't come by themselves).
        /// </summary>
        public static bool AllAboard(GameState s)
        {
            // G23.3: never without the wounded alive (WoundedOut), found or not.
            return s.Run != null && s.Units.Any((u) => u.Aboard == true) && !s.Units.Any((u) => u.Alive && Boarder(u) && !BittenUncured(s, u)) && !WoundedOut(s);
        }

        /// <summary>
        /// La Lata's tiles that count now (T21, T24; Producto, 18/11: «tantas casillas como plazas libres»): its zone's but the
        /// ones those aboard got in by (each closes the tile it got in on, its door's), as many as its seats free; full,
        /// none. Before La Lata comes (an Asedio), all of them, to wait on. Without seats (outside the runs), its zone as it
        /// is. What the client draws, and what Abilities.InEvacZone means.
        /// </summary>
        public static List<Vec2> OpenTiles(GameState s)
        {
            var zone = s.Mission.EvacZone;
            var free = SeatsFree(s);
            if (free == null || s.Run == null) return zone;
            var m = s.Mission;
            if (m.HoldUntil != null && s.Turn < m.HoldUntil) return zone;
            var shut = s.Units.Where((u) => u.Aboard == true && u.Lame != true).Select((u) => (u.Pos.X, u.Pos.Y)).ToHashSet();
            return zone.Where((z) => !shut.Contains((z.X, z.Y))).Take(free.Value).ToList();
        }

        /// <summary>The tiles one aboard may get down to (T24): La Lata's zone's, free, that can be stood on.</summary>
        public static List<Pos> AlightTiles(GameState s)
        {
            return s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).Where((p) => Grid.UnitAt(s, p) == null && Flights.CanStopAt(s, p)).ToList();
        }

        /// <summary>Why `u` can't get down out of La Lata to `tile` now, as `slot` (T24, AbilityIds.Alight), or null.</summary>
        public static string? AlightRefusal(GameState s, Unit? u, int slot, IReadOnlyList<int> connected, Pos? tile)
        {
            if (u == null || u.Aboard != true) return "No está en " + Lore.Bus.Name + ".";
            if (!Content.CommandsFor(slot, u.Owner, connected)) return "Es de la escuadra del otro jugador.";
            if (s.Outcome != null || s.ActiveTeam != Teams.Xcom || s.Mission.EvacDone == true) return "Ahora no.";
            if (u.Ap < 1) return "Sin acciones.";
            var free = AlightTiles(s);
            if (free.Count == 0) return "No hay sitio libre junto a " + Lore.Bus.Name + ".";
            if (tile == null || !free.Any((p) => Grid.SamePos(p, tile))) return "Solo a una casilla libre junto a " + Lore.Bus.Name + ".";
            return null;
        }

        /// <summary>
        /// Who is in La Lata's zone now and wouldn't fit if it left (N2): past MissionState.Seats, the squad's own first and
        /// the one who joined on the map last, each in the order they stand in the state. Empty with no seats counted
        /// (outside the runs) or room for all. The evacuation's prompt names them (Lore.EvacSituation's noSeat).
        /// </summary>
        public static List<Unit> NoSeat(GameState s)
        {
            var seats = s.Mission.Seats;
            if (seats == null) return new List<Unit>();
            // G23.3: the wounded takes no seat, on the floor in its zone either.
            var aboard = s.Units.Where((u) => u.Alive && u.Team == Teams.Xcom && !u.Objective && !u.Captive && u.Automaton == null && u.Lame != true && Abilities.InEvacZone(s, u.Pos)).ToList();
            // The seats taken inside (T24) first.
            var left = Math.Max(0, seats.Value - s.Units.Count((u) => u.Aboard == true && u.Lame != true));
            return aboard.Where((u) => u.CampaignId != null).Concat(aboard.Where((u) => u.CampaignId == null)).Skip(left).ToList();
        }
    }
}
