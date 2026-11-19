using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// A player that plays a mission by itself, with the engine's own functions, for the tests between two apps
    /// (BASTION_CAMPANA, the app's CampaignRun): never in a game of the players. Each seat plays its own soldiers while it
    /// holds the command, one at a time: it reloads, shoots or strikes what it is likely to hit or what is close, and
    /// otherwise goes for the objective (a Botín's item, the radio to fix, the one to free, the alarm to silence; with the
    /// job done, La Lata, everyone) or the nearest enemy, in stretches (Arturo, 5/11 #14: moving apart from the actions),
    /// never at a run (all the movement at once, heard: Arturo, 6/11); with its actions spent, while the partner plays, it
    /// cedes the command and keeps its movement for later; in La Lata with the job done and everyone aboard who can get
    /// there, it calls the bus; the partner's call it answers yes.
    /// Without a gun (the campaign starts with two pistols for four: Arturo, 2/11) it goes for one first, in a police car's
    /// boot or on the floor, and puts it on; and with the blade it walks up to the dead and strikes. Walled off from where it
    /// goes (a boarded door: Arturo, 2/11), it takes the boards off with the crowbar or goes round. With nothing to do, it
    /// keeps watch or takes cover, and with nothing left for any soldier it ends its turn, movement left or not (the turn
    /// doesn't end by itself while some is left: Arturo, 5/11 #14). Deterministic: no dice of its own.
    /// In a run's fight (H3; Gameplay, 16/11) it uses the pack: a medicine for one at half health or less, what lies at its
    /// feet picked up, a gun in the pack put on, and the reload spends a magazine; with the gun empty and none left, the
    /// blade. A run's Saqueo (the normal node) has no objective: the squad searches the rooms nearest by the walk, never
    /// hunting the dead, and goes for La Lata once RunLeaveTurn comes, once one of it is at half health or less, or once no
    /// room is left (RunLeaving). Hidden, it walks the squad's extra too (Paths with the state).
    /// La llave (G23.1; Gameplay, 19/11), a Saqueo with the police station's armoury locked: the squad kills and searches
    /// the dead in uniform (never knowing which holds the key: Mission.KeyHolder isn't looked at), picks the key up where
    /// it fell, takes it to the door and opens it, searches the armoury's furniture, and goes (KeyLeaving).
    /// </summary>
    public static class MissionBot
    {
        /// <summary>Shots at least this likely are taken whatever else there is to do.</summary>
        public const int GoodShot = 50;

        /// <summary>An enemy this close (tiles) is shot at worse odds.</summary>
        public const double Close = 7;

        /// <summary>A run's Saqueo: the turn the squad goes for La Lata, whatever is left to search (RunLeaving).</summary>
        public const int RunLeaveTurn = 6;

        /// <summary>La llave (G23.1): the turn the squad goes for La Lata with the armoury still locked (KeyLeaving).</summary>
        public const int KeyLeaveTurn = 14;

        /// <summary>La llave: with the armoury open, the turn the squad goes, its furniture searched or not (KeyLeaving).</summary>
        public const int KeyArmouryTurn = 20;

        /// <summary>A run's Saqueo: rooms farther than this many turns' walk aren't gone for (RoomGoal); with none nearer, the squad goes.</summary>
        public const int RoomTurns = 2;

        /// <summary>A run's Saqueo: the rooms the squad searches before it goes (Gameplay, 16/11: «las 3–4 más cercanas»).</summary>
        public const int RoomsWanted = 4;

        /// <summary>A run's Saqueo: the dead the squad sees this near (tiles) to one of it, and this many, and it goes (RunLeaving).</summary>
        public const double ThreatNear = 5;
        public const int ThreatMany = 3;

        /// <summary>The next order of `slot`, or null: nothing for it now (the partner holds the command, or it is done).</summary>
        public static Command? Next(GameState s, int slot, IReadOnlyList<int> connected)
        {
            if (s.Outcome != null || s.ActiveTeam != Teams.Xcom) return null;
            // The partner calls La Lata: yes. One's own call waits for the partner.
            if (s.EvacCall != null) return s.EvacCall.By != slot ? new EvacAnswerCommand { Accept = true } : null;
            var holds = s.Command == slot || s.Command == null || !connected.Contains(s.Command.Value);
            if (!holds || s.Ready[slot]) return null;
            // Inside La Lata (T24, Gameplay 18/11), with nobody left outside that can get to it: one of them asks it to go
            // (it goes by itself only with all the living in).
            var inside = s.Run != null ? s.Units.FirstOrDefault((u) => u.Aboard == true && Content.CommandsFor(slot, u.Owner, connected)) : null;
            if (inside != null && s.Mission.EvacDone != true && AllAboard(s)) return new EvacCommand { Unit = inside.Id };
            // Movement left counts too: the turn doesn't end by itself while there is some (Arturo, 5/11 #14).
            var able = Grid.Combatants(s, Teams.Xcom).Where((u) => !u.Evacuated && Paths.HasTurnLeft(u) && Content.CommandsFor(slot, u.Owner, connected)).ToList();
            if (able.Count == 0) return new EndTurnCommand { Ready = true };
            // Each soldier's walks, past the dead too (they don't block it here): the way to its goal, the nearest of them by
            // the walk, and in a Saqueo whether any is left to walk to.
            var walks = new Dictionary<string, Reach>();
            Reach WalksOf(Unit x) => walks.TryGetValue(x.Id, out var r) ? r : walks[x.Id] = Paths.ComputeReach(s, x, double.MaxValue, null, (o) => o.Team != x.Team);
            // A run's Saqueo: each soldier's nearest room left to search, the rooms found on the way kept for the next one.
            var known = new Dictionary<int, LootRoom?>();
            var rooms = new Dictionary<string, Pos?>();
            Pos? RoomOf(Unit x) => rooms.TryGetValue(x.Id, out var r) ? r : rooms[x.Id] = RoomGoal(s, x, WalksOf(x), known);
            var leaving = Leaving(s, WalksOf, RoomOf);
            var goals = able.ToDictionary((x) => x.Id, (x) => Goal(s, x, leaving, WalksOf, RoomOf));
            // The soldier in play first: it is to be finished before another (Rules InPlay). Then the others, the one
            // nearest its goal first; one with nothing to do lets the next one play, and only with none left is the turn over,
            // movement left or not.
            var order = able.OrderBy((x) => x.Id == s.ActiveUnit ? 0 : 1)
                .ThenBy((x) => goals[x.Id] is Pos g ? Grid.Distance(x.Pos, g) : 0)
                .ThenBy((x) => x.Id, System.StringComparer.Ordinal);
            foreach (var u in order)
            {
                // In play with its actions spent (Arturo, 7/11: in play till it has nothing, or is left), while the partner
                // plays: the partner's go, its movement kept for later in the turn, as the command went on by itself before.
                // Left (DoneCommand), that movement was lost: of seeds 1 to 120 of the test apps' campaign, the first
                // mission was won as often either way (23), and both 18 times so against 20, but 364 lost its siege.
                if (u.Id == s.ActiveUnit && u.Ap <= 0 && PartnerPlays(s, slot, connected)) return new PassCommand();
                var cmd = Choose(s, u, leaving, goals[u.Id], WalksOf(u), slot, connected, WalksOf);
                if (cmd != null) return cmd;
                // In play with nothing to do, its actions kept, it holds the others back while the partner plays (Rules
                // InPlay): the partner's go.
                if (u.Id == s.ActiveUnit && PartnerPlays(s, slot, connected)) return new PassCommand();
            }
            return new EndTurnCommand { Ready = true };
        }

        /// <summary>
        /// Whether the partner takes turns with `slot`: the rules' own (Sim.Alternates: here, not done, and with someone to
        /// move, or one aboard La Lata with its actions, T24), so the bot hands over the command whenever the rules would
        /// refuse another of its soldiers (Rules InPlay). Counting only those on the field, it kept the command with Pepe's
        /// one in La Lata, gave Ana's other one its order, refused for ever (R9, 18/11: the bench's chapter of seed 28).
        /// </summary>
        private static bool PartnerPlays(GameState s, int slot, IReadOnlyList<int> connected)
        {
            return new Sim(s, new Rng(0), connected).Alternates(slot == 0 ? 1 : 0);
        }

        /// <summary>
        /// The job is done and the squad is to go: the item carried, the VIP freed (it comes along), the radio fixed, the
        /// alarm silenced; in a Saqueo, none of the dead left that a soldier can walk to (it searches nothing, and with no
        /// clock it would stay for ever: 5/11).
        /// </summary>
        private static bool Leaving(GameState s, System.Func<Unit, Reach> walksOf, System.Func<Unit, Pos?> roomOf)
        {
            var m = s.Mission;
            if (m.Kind == MissionKinds.Recovery) return !string.IsNullOrEmpty(m.Item?.Carrier);
            // Mordido (G23.2): not with the bitten till they are cured.
            if (BittenFight(s)) return BittenLeaving(s);
            if (m.Kind == MissionKinds.Rescue && Vip(s) is Unit vip && !vip.Captive) return true;
            // A run's Rescate with the survivor fallen: a Saqueo to get out of (N2, Gameplay 17/11).
            if (m.Kind == MissionKinds.Rescue && s.Run != null && Vip(s) == null) return true;
            if (s.Run != null)
            {
                // A run's Asedio and Travesía: La Lata from the start, where the Asedio holds till it comes; a Guarida, once
                // its box is open; El Toro, once he is dead; either of these given up when the squad is broken (H5, H6).
                if (m.HoldUntil != null || s.Run.NodeKind == NodeKinds.Crossing) return true;
                if (m.Lair != null || !string.IsNullOrEmpty(m.Boss)) return m.ObjectiveDone || Routed(s);
                // La llave (G23.1): once its armoury is searched, or with nothing left to go for.
                if (KeyFight(s)) return KeyLeaving(s, walksOf);
            }
            if (RunSaqueo(s)) return RunLeaving(s, roomOf);
            if (m.Kind == MissionKinds.Scavenge && m.EvacZone.Count > 0)
            {
                var enemies = Enemies(s).Select((e) => Grid.NodeOf(s, e.Pos)).ToList();
                return !Grid.Combatants(s, Teams.Xcom).Any((o) => !o.Evacuated && enemies.Any((n) => walksOf(o).Cost.TryGetValue(n, out _)));
            }
            return m.Kind != MissionKinds.Elimination && m.ObjectiveDone;
        }

        /// <summary>The one to free in a rescue, alive; null in other missions.</summary>
        private static Unit? Vip(GameState s)
        {
            var m = s.Mission;
            return m.Kind == MissionKinds.Rescue && m.ObjectiveUnit != null && Grid.GetUnit(s, m.ObjectiveUnit) is Unit u && u.Alive && !u.Evacuated ? u : null;
        }

        /// <summary>The sabotage's alarm, still ringing; null in other missions.</summary>
        private static Unit? Alarm(GameState s)
        {
            var m = s.Mission;
            return m.Kind == MissionKinds.Sabotage && !m.ObjectiveDone && m.ObjectiveUnit != null && Grid.GetUnit(s, m.ObjectiveUnit) is Unit u && u.Alive ? u : null;
        }

        private static Command? Choose(GameState s, Unit u, bool leaving, Pos? target, Reach walks, int slot, IReadOnlyList<int> connected, System.Func<Unit, Reach> walksOf)
        {
            // In La Lata with the job done: the bus, once everyone is aboard who can get there. No clock hurries it: only the
            // sabotage's alarm has one now (Arturo, 5/11 #1), and it stops once the alarm is silenced (Events.CompleteObjective).
            // Not in a run: there it is asked from inside La Lata only (T24; the first lines of Next), and one standing in its
            // zone gets in on its next walk.
            if (s.Run == null && leaving && Abilities.InEvacZone(s, u.Pos) && BusHere(s))
            {
                var all = AllAboard(s);
                // The one with the item aboard and the dead on it: now, with whoever is in.
                var cornered = s.Mission.Item?.Carrier is string c && Grid.GetUnit(s, c) is Unit holder && Abilities.InEvacZone(s, holder.Pos)
                    && Enemies(s).Any((e) => Grid.Distance(holder.Pos, e.Pos) <= 2);
                if (all || cornered) return new EvacCommand { Unit = u.Id };
            }
            if (leaving && Abilities.InEvacZone(s, u.Pos))
            {
                var room = MakeRoom(s, u);
                if (room != null) return room;
            }
            var armed = Armed(s, u);
            var carrier = s.Mission.Item?.Carrier == u.Id;
            if (s.Run != null)
            {
                var pack = PackOrder(s, u, armed, slot, connected);
                if (pack != null) return pack;
                // La llave (G23.1): the key picked up where it lies (beside it, if it fell with one of the squad), and the
                // locked door beside it opened with it.
                if (KeyFight(s) && RunPack.FloorInReach(s, u).FirstOrDefault((d) => d.Gear.Kind == GearKinds.Key) is DroppedGear key)
                {
                    var take = new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Gear = key.Gear.Id };
                    if (RunPack.Refusal(s, take, slot, connected) == null) return take;
                }
                if (KeyFight(s) && Looting.KeyOf(u) != null && Usable(s, u, AbilityIds.OpenDoor)
                    && Abilities.DoorTiles(s, u, TileKinds.Door).FirstOrDefault((p) => Looting.LockedAt(s, p)) is Pos door)
                    return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.OpenDoor, Tile = door };
                // Mordido (G23.2): the antiviral picked up where it lies, and used on the bitten from beside them (or by
                // them on themselves), before anything else.
                if (BittenFight(s))
                {
                    if (RunPack.FloorInReach(s, u).FirstOrDefault((d) => d.Gear.Kind == GearKinds.Antiviral) is DroppedGear drug)
                    {
                        var take = new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Gear = drug.Gear.Id };
                        if (RunPack.Refusal(s, take, slot, connected) == null) return take;
                    }
                    if (Looting.AntiviralOf(u) is Gear antiviral && Vip(s) is Unit bitten)
                    {
                        var use = new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = antiviral.Id, To = bitten.Id == u.Id ? null : bitten.Id };
                        if (RunPack.Refusal(s, use, slot, connected) == null) return use;
                    }
                }
            }
            if (!armed)
            {
                var gun = PackedGun(u);
                if (gun != null && u.Ap > 0) return new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Index = gun };
                // One at its feet is picked up (free); a police car's boot beside it, searched.
                var dropped = s.Dropped?.FirstOrDefault((d) => IsGun(d.Loot.Weapon) && Grid.SamePos(d.Pos, u.Pos));
                if (dropped != null && Looting.Fits(u, dropped.Loot)) return new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = dropped.Id };
                var boot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Looting.SearchSpot(s, p) == Looting.PoliceBoot) : null;
                if (boot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = boot };
            }
            if (u.Ammo <= 0 && Usable(s, u, AbilityIds.Reload)) return Ability(u, AbilityIds.Reload);
            // The job, where it is a thing to do: the radio fixed from beside it, the alarm hit at any fair odds.
            if (Usable(s, u, AbilityIds.Hack)) return Ability(u, AbilityIds.Hack);
            // A run's survivor beside it: talked to, and they join (N2, Gameplay 17/11).
            if (Usable(s, u, AbilityIds.Talk)) return Ability(u, AbilityIds.Talk);
            var alarm = Alarm(s);
            var silence = alarm != null ? BestShot(s, u, double.MaxValue, alarm.Id) : null;
            if (silence != null && silence.Value.hit >= 15) return silence.Value.cmd;
            // The silent takedown (Arturo, 16/11): one of the dead beside it that hasn't seen it, killed for sure and unheard.
            var unaware = Usable(s, u, AbilityIds.Takedown) ? Abilities.TakedownTargets(s, u).OrderBy((t) => t.Id, System.StringComparer.Ordinal).FirstOrDefault() : null;
            if (unaware != null) return Ability(u, AbilityIds.Takedown, unaware.Id);
            // A Guarida's box beside it: opened, the job done (H5).
            var lair = s.Run != null && s.Mission.Lair is Pos box && !s.Mission.ObjectiveDone && Usable(s, u, AbilityIds.Search)
                ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Grid.SameTile(p, box)) : null;
            if (lair != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = lair };
            // El Toro first, at any fair odds and farther than the rest: his group comes on, he doesn't stop (H6).
            var bull = Bull(s);
            var atBull = bull != null && !carrier ? BestShot(s, u, Close * 2, bull.Id) : null;
            if (atBull != null && atBull.Value.hit >= 25) return atBull.Value.cmd;
            // Only what is close is shot at: a shot is heard, and draws more of them (docs/FASE2.md). In a run, on the way
            // out, only what stands beside it: it stood fighting by La Lata till the horde had it (a Saqueo, 16/11).
            var going = s.Run != null && leaving && !Holding(s);
            var shot = BestShot(s, u, going ? 1.5 : Close);
            var nearest = Enemies(s).Select((e) => Grid.Distance(u.Pos, e.Pos)).DefaultIfEmpty(double.MaxValue).Min();
            // Mordido (G23.2): a medicine cabinet or shelf beside it searched before any shot while the antiviral is still in
            // one: the count runs.
            if (BittenFight(s) && Looting.AntiviralWhere(s) == AntiviralPlaces.Piece && Usable(s, u, AbilityIds.Search)
                && Looting.SearchableTiles(s, u).FirstOrDefault((p) => MedicalPiece(s, p)) is Pos cabinet)
                return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = cabinet };
            // La llave (G23.1): a body in uniform beside it searched before any shot: with the dead coming on every turn,
            // the holder's lay by the squad for the whole fight, its key never taken (Gameplay, 19/11).
            // So too the armoury's furniture once it is open: standing in it, it shot through the door the whole time.
            // A body in uniform beside it even on the way out, while the key is still on the dead: the squad went home
            // from beside the holder's body 3 times in 8 (R10, 19/11).
            if (KeyFight(s) && Usable(s, u, AbilityIds.Search)
                && Looting.SearchableTiles(s, u).FirstOrDefault((p) => (UniformBody(s, p) && Looting.KeyWhere(s) == KeyPlaces.Holder) || (!leaving && Looting.KeyWhere(s) == KeyPlaces.Used && InArmory(s, p))) is Pos body)
                return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = body };
            if (shot != null && !carrier && shot.Value.hit >= 30) return shot.Value.cmd;
            // A run's Saqueo, before going: the room it stands in, then a box, a boot or a body beside it.
            if (RunSaqueo(s) && !leaving)
            {
                if (Usable(s, u, AbilityIds.SearchRoom)) return Ability(u, AbilityIds.SearchRoom);
                var spot = Usable(s, u, AbilityIds.Search) ? Looting.SearchableTiles(s, u).FirstOrDefault() : null;
                if (spot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = spot };
            }
            // La llave (G23.1), before going: a body in uniform beside it, then the armoury's furniture; nothing else.
            if (KeyFight(s) && !leaving && Usable(s, u, AbilityIds.Search))
            {
                var spot = Looting.SearchableTiles(s, u).Where((p) => UniformBody(s, p) || InArmory(s, p)).OrderBy((p) => UniformBody(s, p) ? 0 : 1).FirstOrDefault();
                if (spot != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = spot };
            }
            // The blade alone: a step up to one of the dead, keeping an action to strike (not on a run's way out, nor out of
            // La Lata in an Asedio).
            if (!armed && !carrier && !going && !Holding(s))
            {
                var close = MoveToStrike(s, u);
                if (close != null) return close;
            }
            var goal = (!armed && !leaving ? GunGoal(s, u, walks) : null) ?? target;
            // La llave, after its key: never far ahead of the squad's last (Leash), so the dead come to it together.
            if (goal != null && !leaving && KeyFight(s) && Looting.KeyWhere(s) == KeyPlaces.Holder && Ahead(s, u, goal, walks, walksOf) > Leash * Paths.CostOrtho) goal = null;
            if (goal != null && !(leaving && Abilities.InEvacZone(s, u.Pos)))
            {
                var pry = Pry(s, u, goal, leaving);
                if (pry != null) return pry;
                var stride = Stride(s, u);
                // Mordido's antiviral in hand, the count running: on regardless of the danger (with the dead by the stairs,
                // it stood on watch upstairs till the bitten turned: R11, 19/11).
                var rush = BittenFight(s) && Looting.AntiviralOf(u) != null;
                var move = stride >= Paths.CostOrtho ? MoveToward(s, u, goal, stride, leaving, walks, reckless: rush) : null;
                if (move != null) return move;
            }
            // On a run's way out with no step to take, what is near is shot after all: the horde stood in the way to La Lata,
            // four tiles off, and the squad waited for it for ever (R3, 17/11: a Guarida in the hospital, 700 turns).
            if (going && !carrier && BestShot(s, u, Close) is (Command, int) way && way.hit >= 30) return way.cmd;
            // And with nothing to shoot, a step on regardless of the danger: inside, the horde at the door can't be seen,
            // and the blade alone waited by it for ever (R3: the offices' radio, 3000 turns).
            if (going && goal != null && !Abilities.InEvacZone(s, u.Pos) && Stride(s, u) >= Paths.CostOrtho && MoveToward(s, u, goal, Stride(s, u), leaving, walks, reckless: true) is Command push) return push;
            if (shot != null && shot.Value.hit >= 15) return shot.Value.cmd;
            if (Usable(s, u, AbilityIds.Overwatch) && nearest <= Close * 2) return Ability(u, AbilityIds.Overwatch);
            if (Usable(s, u, AbilityIds.OverwatchMelee) && nearest <= Close) return Ability(u, AbilityIds.OverwatchMelee);
            if (Usable(s, u, AbilityIds.Hunker)) return Ability(u, AbilityIds.Hunker);
            return null;
        }

        private static IEnumerable<Unit> Enemies(GameState s) => Grid.Combatants(s, Teams.Alien);

        /// <summary>Where `u` goes: La Lata (leaving, or carrying the item), the item, the radio, the one to free, the alarm, or the nearest enemy by the walk.</summary>
        private static Pos? Goal(GameState s, Unit u, bool leaving, System.Func<Unit, Reach> walksOf, System.Func<Unit, Pos?> roomOf)
        {
            var m = s.Mission;
            // La Lata's tiles still open (T24): one shut by someone in it takes nobody.
            var bus = Rules.OpenTiles(s).Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).OrderBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
            if (leaving && bus != null) return bus;
            if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Pos != null) return m.Item.Pos;
            if (m.Kind == MissionKinds.Hack && m.Terminal != null && !m.ObjectiveDone) return m.Terminal;
            if (BittenFight(s) && !leaving) return BittenGoal(s, u, walksOf(u)) ?? bus;
            if ((Vip(s) ?? Alarm(s)) is Unit objective) return objective.Pos;
            if (s.Run != null && m.Lair != null) return m.Lair;
            if (Bull(s) is Unit bull) return bull.Pos;
            if (KeyFight(s) && !leaving) return KeyGoal(s, u, walksOf(u)) ?? bus;
            if (RunSaqueo(s)) return roomOf(u) ?? bus;
            return NearestEnemy(s, u, walksOf(u)) ?? bus;
        }

        /// <summary>
        /// The nearest of the dead by the walk (`walks`, past them), as the crow flies if none can be walked to. By the walk,
        /// a stretch toward one keeps it the nearest, since the walk to any other drops by no more than the stretch; as the
        /// crow flies, the nearest changed with the stretches, and one went back and forth every turn (5/11).
        /// </summary>
        private static Pos? NearestEnemy(GameState s, Unit u, Reach walks)
        {
            int Walk(Pos p) => walks.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) ? w : int.MaxValue;
            // The caged (H9) are none to go for but in a Limpieza, and there by the tile across their bars nearest by the
            // walk, where the blade reaches them (Rules.CagedReach); never their own, behind the bars.
            var targets = Enemies(s).Where((e) => e.Caged != true).Select((e) => (e.Id, At: e.Pos)).ToList();
            if (Hunting(s))
                foreach (var e in Enemies(s).Where((e) => e.Caged == true))
                {
                    var spot = Rules.CagedReach(s, e).OrderBy(Walk).ThenBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
                    if (spot != null) targets.Add((e.Id, spot));
                }
            var walked = targets.Where((t) => Walk(t.At) < int.MaxValue).OrderBy((t) => Walk(t.At)).ThenBy((t) => t.Id, System.StringComparer.Ordinal).Select((t) => t.At).FirstOrDefault();
            return walked ?? targets.OrderBy((t) => Grid.Distance(u.Pos, t.At)).ThenBy((t) => t.Id, System.StringComparer.Ordinal).Select((t) => t.At).FirstOrDefault();
        }

        /// <summary>A Limpieza: every one of the dead to kill, the caged too (H9).</summary>
        private static bool Hunting(GameState s) => s.Mission.Kind == MissionKinds.Elimination;

        /// <summary>
        /// One of the dead `u` has reason to fight: any free one; a caged one (H9) only in a Limpieza, or when it reaches `u`
        /// across its bars. Kept off otherwise: a shot is heard, and at its bars it bites.
        /// </summary>
        private static bool Worth(GameState s, Unit u, Unit e) => e.Caged != true || Hunting(s) || Rules.CagedReach(s, e).Any((p) => Grid.SamePos(p, u.Pos));

        /// <summary>
        /// Whether all the squad not gone is in La Lata, but for any that can't walk there from where it is (walled off, no
        /// crowbar): with no clock to call the bus now (Arturo, 5/11 #1), it would wait for them for ever.
        /// </summary>
        private static bool AllAboard(GameState s)
        {
            var bus = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            // (Mordido's bitten not cured never gets in: none to wait for, G23.2.)
            foreach (var o in Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos) && !Rules.BittenUncured(s, o)))
            {
                var reach = Paths.ComputeReach(s, o, double.MaxValue);
                if (bus.Any((n) => reach.Cost.TryGetValue(n, out _))) return false;
            }
            return true;
        }

        /// <summary>
        /// In La Lata, waiting for the rest: a stretch further in, away from the nearest still outside, so as not to stand in
        /// their way (5/11: one in the door of the bus kept the others out, and only the clock used to end it).
        /// </summary>
        private static Command? MakeRoom(GameState s, Unit u)
        {
            var outside = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos)).ToList();
            if (outside.Count == 0 || Paths.MoveBudget(s, u) <= 0) return null;
            double Away(Pos p) => outside.Min((o) => Grid.Distance(p, o.Pos));
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            foreach (var o in Paths.Destinations(s, u, reach))
                if (Abilities.InEvacZone(s, o.Pos) && (best == null || Away(o.Pos) > Away(best.Pos) + 1e-9 || (System.Math.Abs(Away(o.Pos) - Away(best.Pos)) < 1e-9 && o.Cost < best.Cost))) best = o;
            if (best == null || Away(best.Pos) < Away(u.Pos) + 1 || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// The next stretch, within what is left, never a run (Paths.IsRun: all the turn's movement at once, heard; Arturo,
        /// 6/11): with all of it, a tile short; with less, the rest. Seven tiles and then one are the same eight in silence:
        /// on a bench of 90 missions with bots (6/11), 29 won so, against 25 running once the squad was seen, on the way out
        /// or to strike. Hidden, with the squad's extra (Paths.ConcealedBonus, Arturo 16/11).
        /// </summary>
        private static double Stride(GameState s, Unit u)
        {
            var left = Paths.MoveBudget(s, u);
            var full = Paths.FullMoves(s, u);
            return left < full ? left : full - Paths.CostOrtho;
        }

        /// <summary>A run's Saqueo: the normal node (Gameplay, 16/11), no objective but to search and go.</summary>
        private static bool RunSaqueo(GameState s) => s.Run?.NodeKind == NodeKinds.Normal && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

        /// <summary>El Toro alive, in his fight (H6); null in any other.</summary>
        private static Unit? Bull(GameState s) => !string.IsNullOrEmpty(s.Mission.Boss) && Grid.GetUnit(s, s.Mission.Boss!) is Unit b && b.Alive ? b : null;

        /// <summary>A run's Asedio (H5): the squad holds by La Lata till it comes (MissionState.HoldUntil).</summary>
        private static bool Holding(GameState s) => s.Run != null && s.Mission.HoldUntil != null;

        /// <summary>La Lata takes the squad now: in an Asedio, from its turn on («La Lata llega en el turno 8»).</summary>
        private static bool BusHere(GameState s) => s.Mission.HoldUntil == null || s.Turn >= s.Mission.HoldUntil;

        /// <summary>
        /// The squad broken: one or none of it left, or all at half health or less. A Guarida or El Toro is given up then: lost,
        /// but whoever is left comes home.
        /// </summary>
        private static bool Routed(GameState s)
        {
            var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
            return squad.Count <= 1 || squad.All((o) => o.Hp * 2 <= o.MaxHp);
        }

        /// <summary>
        /// Whether `p` lies on a line El Toro could charge along (Toro.ChargePath: straight, one of eight ways, 2 to ChargeRun + 1
        /// tiles off, at his level), walls and the rest aside: a place to keep out of.
        /// </summary>
        private static bool InChargeLine(Unit bull, Pos p)
        {
            int dx = p.X - bull.Pos.X, dy = p.Y - bull.Pos.Y;
            var steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            return p.Z == bull.Pos.Z && steps >= 2 && steps <= Toro.ChargeRun + 1 && (dx == 0 || dy == 0 || System.Math.Abs(dx) == System.Math.Abs(dy));
        }

        /// <summary>
        /// A run's Saqueo is over for the squad: the turn RunLeaveTurn has come, RoomsWanted rooms are searched, one of it is
        /// at half health or less, ThreatMany of the dead it sees are within ThreatNear of it, or none of it has a room or a
        /// box left to search that it can walk to (`roomOf`). Hunting the dead it fell on every one (Gameplay, 16/11: 4 of 4,
        /// with rifles too): the horde grows with the noise (Rules.HordeClock), and kept coming.
        /// </summary>
        private static bool RunLeaving(GameState s, System.Func<Unit, Pos?> roomOf)
        {
            if (s.Turn >= RunLeaveTurn || (s.RoomsSearched?.Count ?? 0) >= RoomsWanted) return true;
            var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
            if (squad.Any((o) => o.Hp * 2 <= o.MaxHp)) return true;
            var near = Enemies(s).Count((e) => e.Caged != true && Aim.TeamSees(s, Teams.Xcom, e) && squad.Any((o) => Grid.Distance(o.Pos, e.Pos) <= ThreatNear));
            return near >= ThreatMany || squad.All((o) => roomOf(o) == null);
        }

        /// <summary>
        /// The nearest place by the walk (`walks`, within RoomTurns of movement) in a room nobody has searched (Looting.RoomAt,
        /// RoomSearched), or beside a box, a boot or a body still shut (Looting.IsSearchable): whichever way, La Lata waiting
        /// near since 16/11 (Gameplay: «las 3–4 más cercanas a la escuadra», RoomsWanted). Null with none. `known`: each tile's
        /// room as found, for the next soldier (a room is a flood of its floor).
        /// </summary>
        private static Pos? RoomGoal(GameState s, Unit u, Reach walks, Dictionary<int, LootRoom?> known)
        {
            var most = RoomTurns * Paths.FullMoves(u);
            foreach (var kv in walks.Cost.Where((kv) => kv.Value <= most).OrderBy((kv) => kv.Value).ThenBy((kv) => kv.Key))
            {
                if (!known.TryGetValue(kv.Key, out var room))
                {
                    room = Looting.RoomAt(s, Grid.PosOf(s, kv.Key));
                    if (room == null) known[kv.Key] = null;
                    else foreach (var p in room.Tiles) known[Grid.NodeOf(s, p)] = room;
                }
                var at = Grid.PosOf(s, kv.Key);
                if ((room != null && !Looting.RoomSearched(s, room))
                    || Grid.Dirs8.Any((d) => new Pos { X = at.X + d.X, Y = at.Y + d.Y, Z = at.Z } is Pos q && Looting.IsSearchable(s, q) && Looting.ReachesToSearch(s, at, q))) return at;
            }
            return null;
        }

        /// <summary>
        /// Whether `u` has something to fire: a gun, and in a run rounds in it or a magazine to reload (Unit.mags, the pack's:
        /// Gameplay, 16/11); empty with none, it goes to the blade.
        /// </summary>
        private static bool Armed(GameState s, Unit u)
        {
            if (!Content.HasGun(u)) return false;
            return s.Run == null || u.Ammo > 0 || (u.Mags ?? 0) > 0 || (u.PistolAmmo ?? 0) > 0;
        }

        /// <summary>
        /// What `u` does with its pack in a run's fight (RunPack), the first that the rules take (RunPack.Refusal): a medicine
        /// on itself at half health or less; what lies at its feet picked up (free), a gun first if it has none to fire; a
        /// gun in the pack put on if it has none to fire; with nobody near, a vest in the pack put on if it wears none.
        /// </summary>
        private static Command? PackOrder(GameState s, Unit u, bool armed, int slot, IReadOnlyList<int> connected)
        {
            if (u.Pack == null || u.Kit == null) return null;
            Command? Try(ItemCommand cmd) => RunPack.Refusal(s, cmd, slot, connected) == null ? cmd : null;
            var packed = u.Pack.Items.Select((p) => p.Gear).ToList();
            if (u.Hp * 2 <= u.MaxHp && packed.FirstOrDefault((g) => g.Kind == GearKinds.Medicine) is Gear medicine)
            {
                var use = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = medicine.Id });
                if (use != null) return use;
            }
            var floor = (s.FloorGear ?? new List<DroppedGear>()).Where((d) => Grid.SamePos(d.Pos, u.Pos)).Select((d) => d.Gear)
                .OrderBy((g) => !armed && IsGunGear(g) ? 0 : 1).ThenBy((g) => g.Id, System.StringComparer.Ordinal);
            foreach (var g in floor)
            {
                var take = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Gear = g.Id });
                if (take != null) return take;
            }
            if (!armed && packed.FirstOrDefault(IsGunGear) is Gear gun)
            {
                var equip = Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Gear = gun.Id });
                if (equip != null) return equip;
            }
            var near = Enemies(s).Any((e) => e.Caged != true && Grid.Distance(u.Pos, e.Pos) <= Close);
            if (!near && u.Kit.Vest == null && packed.FirstOrDefault((g) => g.Kind == GearKinds.Vest) is Gear vest) return Try(new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Gear = vest.Id });
            return null;
        }

        /// <summary>A run's gear that fires: a weapon for the hands or the side, not a blade.</summary>
        private static bool IsGunGear(Gear g) => Content.GearWeapon(g) is FoundWeaponDef w && w.Slot != FoundWeaponSlots.Melee;

        /// <summary>Whether a found weapon of `kind` fires: in the hand or at the side, not a blade.</summary>
        private static bool IsGun(string? kind) => kind != null && Content.FoundWeapons.TryGetValue(kind, out var def) && def.Slot != FoundWeaponSlots.Melee;

        /// <summary>A gun in `u`'s backpack (Loot.weapon), by its index; null with none.</summary>
        private static int? PackedGun(Unit u)
        {
            var i = u.Backpack?.FindIndex((l) => IsGun(l.Weapon)) ?? -1;
            return i >= 0 ? i : (int?)null;
        }

        /// <summary>
        /// Where a soldier without a gun may find one: the boots of police cars still shut (MissionState.PoliceCar, a gun
        /// one time in three), or a gun on the floor; only those `walks` (its walks past the dead) gets to, a boot from a
        /// tile beside it (R3, 17/11: a boot walled in, in a town's Limpieza, kept the last soldier going to and fro by it
        /// for ever). Only as many go as there are, the nearest to them; null for the rest.
        /// </summary>
        private static Pos? GunGoal(GameState s, Unit u, Reach walks)
        {
            bool Walked(Pos p) => walks.Cost.ContainsKey(Grid.NodeOf(s, p));
            var found = new List<Pos>();
            foreach (var i in s.Mission.PoliceCar ?? new List<int>())
            {
                var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
                if (Looting.SearchSpot(s, p) != Looting.PoliceBoot || !Looting.IsSearchable(s, p)) continue;
                if (Grid.Dirs8.Any((d) => Grid.InBounds(s, p.X + d.X, p.Y + d.Y) && Walked(new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z }))) found.Add(p);
            }
            found.AddRange((s.Dropped ?? new List<DroppedLoot>()).Where((d) => IsGun(d.Loot.Weapon) && Walked(d.Pos)).Select((d) => d.Pos));
            found.AddRange((s.FloorGear ?? new List<DroppedGear>()).Where((d) => IsGunGear(d.Gear) && Walked(d.Pos)).Select((d) => d.Pos));
            if (found.Count == 0) return null;
            double Near(Unit x) => found.Min((p) => Grid.Distance(x.Pos, p));
            var going = Grid.Combatants(s, Teams.Xcom).Where((x) => !x.Evacuated && !Armed(s, x) && PackedGun(x) == null && x.Pack?.Items.Any((p) => IsGunGear(p.Gear)) != true && s.Mission.Item?.Carrier != x.Id)
                .OrderBy(Near).ThenBy((x) => x.Id, System.StringComparer.Ordinal).Take(found.Count);
            if (!going.Any((x) => x.Id == u.Id)) return null;
            return found.OrderBy((p) => Grid.Distance(u.Pos, p)).First();
        }

        /// <summary>
        /// The blade's move (Arturo, 16/10: striking is striking and moving is moving): with an action to strike, a stretch
        /// to a tile beside one of the dead the squad sees (moving takes none: Arturo, 5/11 #14). The weakest first, then
        /// where fewest others can bite, then the shortest; null when none is that near.
        /// </summary>
        private static Command? MoveToStrike(GameState s, Unit u)
        {
            if (Paths.MoveBudget(s, u) <= 0 || !Usable(s, u, AbilityIds.Slash)) return null;
            var enemies = Enemies(s).ToList();
            // A caged one in a Limpieza even unseen: where it is, it stays (its cell shows only from right in front of the bars).
            var seen = enemies.Where((e) => e.Caged != true ? Aim.TeamSees(s, u.Team, e) : Hunting(s)).ToList();
            if (seen.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            (int hp, int crowd, double cost) bestKey = default;
            foreach (var o in Paths.Destinations(s, u, reach))
            {
                var beside = seen.Where((e) => Aim.InMeleeReach(s, o.Pos, e.Pos)).ToList();
                if (beside.Count == 0) continue;
                var key = (hp: beside.Min((e) => e.Hp), crowd: enemies.Count((e) => Grid.Distance(o.Pos, e.Pos) <= 2.5), cost: o.Cost);
                if (best == null || key.hp < bestKey.hp || (key.hp == bestKey.hp && (key.crowd < bestKey.crowd || (key.crowd == bestKey.crowd && key.cost < bestKey.cost - 1e-9))))
                {
                    best = o;
                    bestKey = key;
                }
            }
            if (best == null || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        /// <summary>
        /// One move toward `goal`, within `cost`, along the way there (`walks`, its walks past the dead): the place on it
        /// with the least walk left and danger, if better than staying; as the crow flies if the goal can't be walked to.
        /// `bus`: any place of La Lata is as good as the goal (the nearest may be taken by a partner). Null: none nearer.
        /// </summary>
        /// <remarks>
        /// Along the way, the way round a wall or a boarded door counts as walked, and one stretch doesn't undo the one
        /// before: with the movement in stretches (Arturo, 5/11 #14), one went back and forth by a wall every turn, nearer as
        /// the crow flies and then along the way round. The walk is the way there, from the soldier: the walk back from the
        /// goal goes down drops the soldier can't climb, and one stood still under one.
        /// </remarks>
        private static Command? MoveToward(GameState s, Unit u, Pos goal, double cost, bool bus, Reach walks, bool reckless = false)
        {
            var reach = Paths.ComputeReach(s, u, cost);
            // Where it ends up matters as much as how near: beside the dead it is bitten in their turn. Those of a group still
            // asleep, only while searching a Saqueo or on the way to a Guarida's box or El Toro: on the way to an objective or
            // to La Lata they were kept clear of for ever, in one building where they never come out (R1, 17/11: Limpiezas
            // of 1000 turns in the hospital and the police station, the squad waiting a few tiles off; a radio fixed in the
            // offices and La Lata never reached).
            var careful = !bus && !Bold(s.Mission);
            var enemies = Enemies(s).Where((e) => e.Caged != true && (careful || !Asleep(s, e))).ToList();
            // The caged (H9), only on the tiles across their bars: nowhere else can they bite. Not in a Limpieza, where those
            // tiles are where the squad has to stand to kill them.
            var caged = new HashSet<int>(Hunting(s) ? Enumerable.Empty<int>() : Enemies(s).Where((e) => e.Caged == true).SelectMany((e) => Rules.CagedReach(s, e)).Select((p) => Grid.NodeOf(s, p)));
            // El Toro's charge ready (or by the next turn): his lines are kept out of (H6).
            var bull = Bull(s);
            var charging = bull != null && (!bull.Cooldowns.TryGetValue(AbilityIds.Charge, out var wait) || wait <= 1);
            double Danger(Pos p) => reckless ? 0 : enemies.Sum((e) => Grid.Distance(p, e.Pos) <= 2.5 ? 4 : Grid.Distance(p, e.Pos) <= 5 ? 1.5 : 0) + (caged.Contains(Grid.NodeOf(s, p)) ? 4 : 0) + (charging && InChargeLine(bull!, p) ? 6 : 0);
            // Not going for La Lata, never a stop on a tile of it in a run: a walk that ends there gets in (T24), and with the
            // street between the start and the police station, two of four got in on their first walk (R9, La llave).
            var stops = Paths.Destinations(s, u, reach).Where((o) => bus || s.Run == null || !Abilities.InEvacZone(s, o.Pos)).ToList();
            Destination? best = null;
            var end = WayTo(s, walks, goal, bus);
            if (end != null)
            {
                // Along the way: the walk left from each place on it, in tiles; any place of La Lata is there.
                var total = walks.Cost.Get(Grid.NodeOf(s, end), 0);
                var at = stops.ToDictionary((o) => Grid.NodeOf(s, o.Pos));
                var bestScore = (double)total / Paths.CostOrtho + Danger(u.Pos) - 0.5;
                foreach (var n in Way(s, walks, end))
                {
                    if (!at.TryGetValue(n, out var o)) continue;
                    var p = o.Pos;
                    var left = bus && Abilities.InEvacZone(s, p) ? 0 : (double)(total - walks.Cost.Get(n, 0)) / Paths.CostOrtho;
                    var score = left + Danger(p);
                    if (score < bestScore - 1e-9)
                    {
                        best = o;
                        bestScore = score;
                    }
                }
            }
            else
            {
                double Far(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : Grid.Distance(p, goal) + 3 * System.Math.Abs(p.Z - goal.Z);
                var bestScore = double.MaxValue;
                foreach (var o in stops)
                {
                    var d = Far(o.Pos) + Danger(o.Pos);
                    if (best == null || d < bestScore - 1e-9 || (System.Math.Abs(d - bestScore) < 1e-9 && o.Cost < best.Cost))
                    {
                        best = o;
                        bestScore = d;
                    }
                }
                // Only a move that brings it nearer, and is no worse than staying.
                if (best != null && (Far(best.Pos) >= Far(u.Pos) - 0.5 || bestScore >= Far(u.Pos) + Danger(u.Pos) - 0.5)) best = null;
            }
            if (best == null || Paths.BuildPath(s, u, reach, best.Pos) == null) return null;
            return new MoveCommand { Unit = u.Id, To = best.Pos };
        }

        // ------------------------------------------------------------ Mordido (G23.2)

        /// <summary>Mordido: past this many of the count left, with the antiviral not in hand, the squad goes without the bitten.</summary>
        public const int BiteGiveUp = 2;

        /// <summary>Mordido: the turn the squad goes, cured or not.</summary>
        public const int BittenLeaveTurn = 20;

        /// <summary>A run's Mordido: a Rescate whose survivor is bitten, cured only with the antiviral in one medicine cabinet or shelf.</summary>
        private static bool BittenFight(GameState s) => s.Run != null && s.Mission.Bitten == true;

        /// <summary>A medicine cabinet or shelf on `p` still to search (the antiviral is in one of them; never a cart).</summary>
        private static bool MedicalPiece(GameState s, Pos p) => Looting.PieceAt(s, p) is string k && (k == SearchPieces.MedicalCabinet || k == SearchPieces.MedicalShelf) && Looting.IsSearchable(s, p);

        [System.ThreadStatic] private static GameState? cabinetsOf;
        [System.ThreadStatic] private static int cabinetsSeq;
        [System.ThreadStatic] private static List<Pos>? cabinets;

        /// <summary>Every medicine cabinet and shelf of the map still to search, on any floor (once a state's seq).</summary>
        private static List<Pos> MedicalLeft(GameState s)
        {
            if (cabinets != null && ReferenceEquals(cabinetsOf, s) && cabinetsSeq == s.Seq) return cabinets;
            var left = new List<Pos>();
            for (var z = 0; z <= (s.Levels?.Count ?? 0); z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = z };
                        if (MedicalPiece(s, p)) left.Add(p);
                    }
            cabinetsOf = s;
            cabinetsSeq = s.Seq;
            return cabinets = left;
        }

        /// <summary>
        /// Where `u` goes in Mordido, by where the antiviral is (Looting.AntiviralWhere; never MissionState.AntiviralAt): in a
        /// piece, the nearest place by the walk beside a medicine cabinet or shelf left; on the floor, there; in a pack, the
        /// bitten (the one who has it, a free place beside them; the rest with that one); cured, the bitten (to talk to them,
        /// if not yet). The bitten, one of the squad now, with the one who has it, or the nearest of the squad.
        /// </summary>
        private static Pos? BittenGoal(GameState s, Unit u, Reach walks)
        {
            if (Vip(s) is not Unit bitten) return null;
            var where = Looting.AntiviralWhere(s);
            // The bitten, one of the squad now: with the one who has the antiviral, or else with the nearest of the squad (left
            // alone where they were found, the dead had them while the squad searched upstairs: R11, 19/11).
            if (u.Id == bitten.Id)
            {
                var mates = Grid.Combatants(s, Teams.Xcom).Where((o) => o.Id != u.Id && !o.Evacuated).ToList();
                var holder = mates.FirstOrDefault((o) => Looting.AntiviralOf(o) != null);
                if (holder != null) return holder.Pos;
                return mates.OrderBy((o) => WalkTo(s, walks, o.Pos)).ThenBy((o) => o.Id, System.StringComparer.Ordinal).Select((o) => (Pos?)o.Pos).FirstOrDefault() ?? u.Pos;
            }
            // The one with it: a free place beside them that reaches them, by the walk past the squad (two of it stood by
            // them, and the place left was none it walked to: R11).
            if (where == AntiviralPlaces.Pack && Looting.AntiviralOf(u) != null)
            {
                var past = Paths.ComputeReach(s, u, double.MaxValue, null, (o) => o.Id != u.Id);
                return Grid.Dirs8.Select((d) => new Pos { X = bitten.Pos.X + d.X, Y = bitten.Pos.Y + d.Y, Z = bitten.Pos.Z })
                    .Where((p) => Grid.InBounds(s, p.X, p.Y) && (Grid.UnitAt(s, p) == null || Grid.UnitAt(s, p) == u) && Aim.InMeleeReach(s, p, bitten.Pos) && past.Cost.ContainsKey(Grid.NodeOf(s, p)))
                    .OrderBy((p) => past.Cost[Grid.NodeOf(s, p)]).ThenBy((p) => Grid.NodeOf(s, p)).Select((p) => (Pos?)p).FirstOrDefault() ?? bitten.Pos;
            }
            switch (where)
            {
                case AntiviralPlaces.Piece:
                {
                    var left = MedicalLeft(s);
                    foreach (var kv in walks.Cost.OrderBy((kv) => kv.Value).ThenBy((kv) => kv.Key))
                    {
                        var at = Grid.PosOf(s, kv.Key);
                        if (left.Any((q) => q.Z == at.Z && Grid.Distance(at, q) < 1.5 && Looting.ReachesToSearch(s, at, q))) return at;
                    }
                    return bitten.Pos;
                }
                case AntiviralPlaces.Floor:
                    return (s.FloorGear ?? new List<DroppedGear>()).First((d) => d.Gear.Kind == GearKinds.Antiviral).Pos;
                case AntiviralPlaces.Pack:
                    // The rest with the one who has it, not crowding the bitten: two stood by them, and the places beside
                    // them left were walls (R11).
                    return s.Units.FirstOrDefault((o) => o.Alive && o.Team == Teams.Xcom && Looting.AntiviralOf(o) != null)?.Pos ?? bitten.Pos;
                default:
                    return bitten.Pos;
            }
        }

        /// <summary>
        /// Mordido: the squad goes broken, with the bitten gone (turned, or fallen), cured once they have been talked to (to
        /// come along into La Lata), at BittenLeaveTurn, or with the count at BiteGiveUp and the antiviral not in hand.
        /// </summary>
        private static bool BittenLeaving(GameState s)
        {
            if (Routed(s) || s.Turn >= BittenLeaveTurn) return true;
            if (Vip(s) is not Unit bitten) return true;
            if (s.Mission.Cured == true) return !bitten.Captive;
            var where = Looting.AntiviralWhere(s);
            return s.Mission.BiteTurns is int left && left <= BiteGiveUp && where != AntiviralPlaces.Pack && where != AntiviralPlaces.Floor;
        }

        // ------------------------------------------------------------ La llave (G23.1)

        /// <summary>A run's La llave: the police station's armoury locked, its key on one of the dead in uniform.</summary>
        private static bool KeyFight(GameState s) => s.Run != null && s.Mission.KeyArmory == true;

        private static bool InUniform(Unit e) => e.Outfit == Outfits.Police || e.Outfit == Outfits.Military;

        /// <summary>A body in uniform on `p` still to search (Looting.BodyAt, IsSearchable): one of those that may hold the key.</summary>
        private static bool UniformBody(GameState s, Pos p) => Looting.BodyAt(s, p) is Unit b && InUniform(b) && Looting.IsSearchable(s, p);

        /// <summary>The armoury (PoliceUses.Armory) of the police station, its ground floor; null with none.</summary>
        private static InteriorRoom? Armory(GameState s) => s.Interiors?.FirstOrDefault((i) => i.Kind == InteriorKinds.Police)?.Rooms.FirstOrDefault((r) => r.Use == PoliceUses.Armory && r.Z == 0);

        private static bool InArmory(GameState s, Pos p) => Armory(s) is InteriorRoom a && p.Z == a.Z && p.X >= a.X && p.Y >= a.Y && p.X < a.X + a.W && p.Y < a.Y + a.H;

        /// <summary>The armoury's furniture still to search (Looting.IsSearchable).</summary>
        private static List<Pos> ArmoryLeft(GameState s)
        {
            var left = new List<Pos>();
            if (Armory(s) is InteriorRoom a)
                for (var y = a.Y; y < a.Y + a.H; y++)
                    for (var x = a.X; x < a.X + a.W; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = a.Z };
                        if (Looting.IsSearchable(s, p)) left.Add(p);
                    }
            return left;
        }

        /// <summary>
        /// Where `u` goes in La llave, by where its key is (Looting.KeyWhere): the nearest by the walk of the dead in uniform
        /// and their bodies not searched; the key on the floor; with it, the locked door (the others with the one who has
        /// it); the door open, the nearest place in the armoury beside its furniture left. Null with nowhere: the squad goes.
        /// </summary>
        private static Pos? KeyGoal(GameState s, Unit u, Reach walks)
        {
            switch (Looting.KeyWhere(s))
            {
                case KeyPlaces.Holder:
                {
                    // The bodies first, then those still on their feet: the one nearest to the squad, the same for all of
                    // it (each its own nearest, the one ahead went for it alone and fell: R10, 19/11).
                    var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
                    Pos? Nearest(IEnumerable<Pos> spots) => spots.Select((p) => (At: p, Walk: squad.Select((o) => WalkTo(s, o.Id == u.Id ? walks : Paths.ComputeReach(s, o, double.MaxValue, null, (x) => x.Team != o.Team), p)).DefaultIfEmpty(int.MaxValue).Min()))
                        .Where((x) => x.Walk < int.MaxValue).OrderBy((x) => x.Walk).ThenBy((x) => Grid.NodeOf(s, x.At)).Select((x) => (Pos?)x.At).FirstOrDefault();
                    return Nearest(s.Units.Where((b) => !b.Alive && b.Team == Teams.Alien && InUniform(b) && UniformBody(s, b.Pos)).Select((b) => b.Pos))
                        ?? Nearest(Enemies(s).Where((e) => e.Caged != true && InUniform(e)).Select((e) => e.Pos));
                }
                case KeyPlaces.Floor:
                    return (s.FloorGear ?? new List<DroppedGear>()).First((d) => d.Gear.Kind == GearKinds.Key).Pos;
                case KeyPlaces.Pack:
                    if (Looting.KeyOf(u) != null) return s.Mission.Locked is List<int> locked && locked.Count > 0 ? Grid.PosOf(s, locked[0]) : null;
                    return s.Units.FirstOrDefault((o) => o.Alive && o.Team == Teams.Xcom && Looting.KeyOf(o) != null)?.Pos;
                case KeyPlaces.Used:
                {
                    // Inside it (from its doorway one reaches a piece too, and standing there kept the others out: R9), and
                    // not by its door if it can be helped (one just in did the same: R10); by the walk past the squad, which
                    // stands in the way only for now.
                    var left = ArmoryLeft(s);
                    var past = Paths.ComputeReach(s, u, double.MaxValue, null, (o) => o.Id != u.Id);
                    bool ByDoor(Pos p) => Grid.Dirs8.Any((d) => Grid.KindAt(s, new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z }) is TileKinds.Door or TileKinds.DoorOpen);
                    return past.Cost.Select((kv) => (At: Grid.PosOf(s, kv.Key), Walk: kv.Value, Node: kv.Key))
                        .Where((x) => InArmory(s, x.At) && left.Any((q) => Grid.Distance(x.At, q) < 1.5 && Looting.ReachesToSearch(s, x.At, q))
                            && (Grid.UnitAt(s, x.At) == null || Grid.UnitAt(s, x.At) == u))
                        .OrderBy((x) => ByDoor(x.At) ? 1 : 0).ThenBy((x) => x.Walk).ThenBy((x) => x.Node).Select((x) => (Pos?)x.At).FirstOrDefault();
                }
            }
            return null;
        }

        /// <summary>La llave, before its armoury is open: so many of the squad fallen, and it goes (Hurt).</summary>
        /// (At the second: 2,65 fallen a fight against 1,3, for one key more in 20: R10, 19/11.)
        public const int KeyFallen = 1;

        /// <summary>La llave: how far ahead of the squad's last `u` may go after the key, in tiles of walk to its goal.</summary>
        public const int Leash = 4;

        /// <summary>How much nearer to `goal` (walk) `u` is than the farthest of the rest of the squad; 0 alone.</summary>
        private static int Ahead(GameState s, Unit u, Pos goal, Reach walks, System.Func<Unit, Reach> walksOf)
        {
            var mine = WalkTo(s, walks, goal);
            if (mine == int.MaxValue) return 0;
            var rest = Grid.Combatants(s, Teams.Xcom).Where((o) => o.Id != u.Id && !o.Evacuated).Select((o) => WalkTo(s, walksOf(o), goal)).Where((w) => w < int.MaxValue).ToList();
            return rest.Count == 0 ? 0 : rest.Max() - mine;
        }

        /// <summary>La llave: a body in uniform still to search this near (tiles of walk) to one of the squad is gone for, hurt or not.</summary>
        public const int KeyGrab = 6;

        private static bool UniformNear(GameState s, System.Func<Unit, Reach> walksOf)
        {
            var bodies = s.Units.Where((b) => !b.Alive && b.Team == Teams.Alien && InUniform(b) && UniformBody(s, b.Pos)).Select((b) => b.Pos).ToList();
            return bodies.Count > 0 && Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).Any((o) => bodies.Any((p) => WalkTo(s, walksOf(o), p) <= KeyGrab * Paths.CostOrtho));
        }

        /// <summary>The squad hurt (La llave, before its armoury is open): one of it fallen, or half of it at half health or less.</summary>
        private static bool Hurt(GameState s)
        {
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();
            if (squad.Count((u) => !u.Alive && u.Aboard != true && !u.Evacuated) >= KeyFallen) return true;
            var up = squad.Where((u) => u.Alive && !u.Evacuated).ToList();
            return up.Count((u) => u.Hp * 2 <= u.MaxHp) * 2 >= up.Count;
        }

        /// <summary>The walk to `p` in `walks`, or to the nearest place beside it (one of the dead, a locked door); int.MaxValue with none.</summary>
        private static int WalkTo(GameState s, Reach walks, Pos p)
        {
            if (walks.Cost.TryGetValue(Grid.NodeOf(s, p), out var w)) return w;
            var best = int.MaxValue;
            foreach (var d in Grid.Dirs8)
            {
                var q = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z };
                if (Grid.InBounds(s, q.X, q.Y) && walks.Cost.TryGetValue(Grid.NodeOf(s, q), out var v) && v < best) best = v;
            }
            return best;
        }

        /// <summary>
        /// La llave: the squad goes broken (Routed, as a Guarida: its prize is to be fought for; gone at the first at half
        /// health, as a Saqueo, it went home without the key 19 times in 20); with the armoury open, once it is searched or
        /// at KeyArmouryTurn; before, at KeyLeaveTurn, or with its key still on the dead once none in uniform is left that it
        /// gets to.
        /// </summary>
        private static bool KeyLeaving(GameState s, System.Func<Unit, Reach> walksOf)
        {
            if (Routed(s)) return true;
            var where = Looting.KeyWhere(s);
            if (where == KeyPlaces.Used) return ArmoryLeft(s).Count == 0 || s.Turn >= KeyArmouryTurn;
            // The key in hand or lying there: the armoury is a few steps off; only broken, or at KeyArmouryTurn.
            if (where == KeyPlaces.Pack || where == KeyPlaces.Floor) return s.Turn >= KeyArmouryTurn;
            if (s.Turn >= KeyLeaveTurn) return true;
            // Hurt, it goes; but for a body in uniform a few steps off (KeyGrab), which may hold the key: of 12 fights whose
            // holder fell, 7 went home with its body unsearched there (R10, 19/11).
            if (Hurt(s) && !UniformNear(s, walksOf)) return true;
            var squad = Grid.Combatants(s, Teams.Xcom).Where((o) => !o.Evacuated).ToList();
            return where == KeyPlaces.Holder && !squad.Any((o) => KeyGoal(s, o, walksOf(o)) != null);
        }

        /// <summary>A mission whose objective has to be reached, past whoever sleeps by it: the objectives of before (a Sigilo is a Recuperación).</summary>
        private static bool Bold(MissionState m) => m.Kind == MissionKinds.Elimination || m.Kind == MissionKinds.Hack || m.Kind == MissionKinds.Recovery
            || m.Kind == MissionKinds.Rescue || m.Kind == MissionKinds.Sabotage;

        /// <summary>One of the dead in a group not woken yet (Pod.active).</summary>
        private static bool Asleep(GameState s, Unit e) => e.PodId != null && s.Pods.Any((p) => p.Id == e.PodId && !p.Active);

        /// <summary>The places of the way to `end` in `walks`, from the first step on (Paths.BuildPath, but to a place taken too: one of the dead).</summary>
        private static List<int> Way(GameState s, Reach walks, Pos end)
        {
            var nodes = new List<int>();
            var start = Grid.NodeOf(s, walks.Origin);
            int? i = Grid.NodeOf(s, end);
            while (i != null && i != start)
            {
                nodes.Add(i.Value);
                i = walks.Prev.TryGetValue(i.Value, out var back) ? back : (int?)null;
            }
            nodes.Reverse();
            return nodes;
        }

        /// <summary>
        /// Where the way to `goal` ends in `walks`: the goal, or the place beside it nearest by the walk if it can't be stood
        /// on (a radio, a car's boot); for La Lata, its place nearest by the walk. Null if none can be walked to.
        /// </summary>
        private static Pos? WayTo(GameState s, Reach walks, Pos goal, bool bus)
        {
            var ends = bus ? Rules.OpenTiles(s).Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 })
                : walks.Cost.TryGetValue(Grid.NodeOf(s, goal), out _) ? new[] { goal }
                : Grid.Dirs8.Select((d) => new Pos { X = goal.X + d.X, Y = goal.Y + d.Y, Z = goal.Z });
            Pos? best = null;
            var bestWalk = int.MaxValue;
            foreach (var p in ends)
            {
                if (!Grid.InBounds(s, p.X, p.Y) || !walks.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) || w >= bestWalk) continue;
                best = p;
                bestWalk = w;
            }
            return best;
        }

        /// <summary>
        /// With the crowbar, the boards off a door beside it on `goal`'s side (Arturo, 2/11), if the way through saves a
        /// turn's movement on the way round (which MoveToward walks); null otherwise.
        /// </summary>
        private static Command? Pry(GameState s, Unit u, Pos goal, bool bus)
        {
            if (!Usable(s, u, AbilityIds.Pry)) return null;
            var boarded = Abilities.BoardedBeside(s, u.Pos).Where((p) => Grid.Distance(p, goal) < Grid.Distance(u.Pos, goal)).ToList();
            if (boarded.Count == 0) return null;
            // The walk from the goal to each place (the same both ways, but for the drops).
            var back = Paths.ComputeReach(s, u, double.MaxValue, goal);
            int? Walk(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : back.Cost.TryGetValue(Grid.NodeOf(s, p), out var w) ? w : (int?)null;
            var here = Walk(u.Pos);
            foreach (var b in boarded)
            {
                // Through it: the walk left from beyond, two steps on.
                var beyond = Grid.Dirs8.Select((d) => Walk(new Pos { X = b.X + d.X, Y = b.Y + d.Y, Z = b.Z })).Where((w) => w != null).Select((w) => w!.Value + 2 * Paths.CostOrtho).DefaultIfEmpty(int.MaxValue).Min();
                if (beyond < int.MaxValue && (here == null || beyond + Paths.FullMoves(s, u) < here)) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Pry, Tile = b };
            }
            return null;
        }

        /// <summary>The likeliest attack `u` has now (rifle, pistol or the blade) at an enemy `within` tiles (only `at`, if given), and how likely.</summary>
        private static (Command cmd, int hit)? BestShot(GameState s, Unit u, double within, string? at = null)
        {
            (Command cmd, int hit)? best = null;
            var abilities = Abilities.AbilitiesOf(u);
            foreach (var a in new[] { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash })
            {
                if (!abilities.Contains(a) || !Usable(s, u, a)) continue;
                foreach (var t in Abilities.AbilityTargets(s, u, a))
                {
                    if (Grid.Distance(u.Pos, t.Pos) > within || (at != null && t.Id != at) || !Worth(s, u, t)) continue;
                    var p = Aim.PreviewShot(s, u, t, new ShotOptions { Ability = a });
                    if (p != null && (best == null || p.Hit > best.Value.hit)) best = (Ability(u, a, t.Id), p.Hit);
                }
            }
            return best;
        }

        private static bool Usable(GameState s, Unit u, string ability) => Abilities.AbilitiesOf(u).Contains(ability) && Abilities.AbilityBlocker(s, u, ability) == null;

        private static Command Ability(Unit u, string ability, string? target = null) => new AbilityCommand { Unit = u.Id, Ability = ability, Target = target };
    }
}
