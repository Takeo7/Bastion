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
    /// Separados (G23.4; Producto, 20/11), a Saqueo with the squad in two groups on either side of La Lata: each group goes
    /// for La Lata from the start, searching only what it stands in or beside on the way; nobody asks it to go, nor says yes
    /// to the partner's call, while the group with nobody going has one that can walk to it (SplitWaits).
    /// Saqueadores (G23.5; Producto, 20/11), a Saqueo with a camp of the living and their stash: while the camp sleeps, the
    /// squad goes for the stash's crates by the tiles none of its watchers would notice it on (Sim.PodsNoticing, what the
    /// rules check on every step), stops only where none of those walking in the enemies' next turns would see it (the
    /// round's guard, the dead), takes down the guards it gets behind, searches a crate only where nobody who hears it and
    /// turns to it would see one of the squad, and shoots none of those unaware; with no way in unseen, or the camp awake,
    /// it goes at it only with the guards outnumbered by RaidersEdge, and otherwise goes (RaidMode). With the stash robbed,
    /// broken, or at RaidersLeaveTurn, La Lata.
    /// Evacuación (G23.6; Gameplay, 21/11), a Saqueo with four civilians hidden in buildings, each in its circle: the squad
    /// goes for the nearest still hidden, shoots first whatever of the dead nears one found walking to La Lata, and goes
    /// with two aboard, or with no more to get aboard; nobody asks La Lata to go, nor says yes to the partner's call, while
    /// one found walks to it and fewer than two are aboard (EvacWaits). The civilians are of the squad to the dead but
    /// nobody's (Rules.IsCivilian): never ordered, nor counted in it (Squad).
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
            // The partner calls La Lata: yes; in Separados, no while a group still to come can (G23.4); in Evacuación, while a
            // civilian found walks to it short of two aboard (G23.6). One's own call waits for the partner.
            if (s.EvacCall != null) return s.EvacCall.By != slot ? new EvacAnswerCommand { Accept = !SplitWaits(s) && !EvacWaits(s) } : null;
            var holds = s.Command == slot || s.Command == null || !connected.Contains(s.Command.Value);
            if (!holds || s.Ready[slot]) return null;
            // Inside La Lata (T24, Gameplay 18/11), with nobody left outside that can get to it: one of them asks it to go
            // (it goes by itself only with all the living in).
            var inside = s.Run != null ? s.Units.FirstOrDefault((u) => u.Aboard == true && Content.CommandsFor(slot, u.Owner, connected)) : null;
            if (inside != null && s.Mission.EvacDone != true && AllAboard(s)) return new EvacCommand { Unit = inside.Id };
            // El herido alive and out with the squad's living all in La Lata, which won't go without them and isn't a wipe
            // (G23.3): one gets down, the nearest tile to them, to go for them (R1, 19/11: fábrica, cuchillos, semilla 16,
            // one in La Lata for 2900 turns).
            if (s.Run != null && Rules.WoundedOut(s) && Rules.WoundedOf(s) is Unit hurt && !Squad(s).Any((o) => !o.Evacuated && o.Lame != true))
            {
                var tile = Rules.AlightTiles(s).OrderBy((p) => Grid.Distance(p, hurt.Pos)).ThenBy((p) => Grid.NodeOf(s, p)).FirstOrDefault();
                var down = s.Units.Where((u) => u.Aboard == true && u.Lame != true && Rules.AlightRefusal(s, u, slot, connected, tile) == null)
                    .OrderBy((u) => u.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (down != null) return new AbilityCommand { Unit = down.Id, Ability = AbilityIds.Alight, Tile = tile };
            }
            // Movement left counts too: the turn doesn't end by itself while there is some (Arturo, 5/11 #14).
            var able = Squad(s).Where((u) => !u.Evacuated && Paths.HasTurnLeft(u) && Content.CommandsFor(slot, u.Owner, connected)).ToList();
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
            // (El herido: the one carrying them before the rest, who follow it.)
            var order = able.OrderBy((x) => x.Id == s.ActiveUnit ? 0 : 1)
                .ThenBy((x) => x.Carrying != null ? 0 : 1)
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
            // El herido (G23.3): never without them while they live (Rules.WoundedOut: La Lata wouldn't go).
            if (WoundedFight(s)) return WoundedLeaving(s);
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
                // Separados (G23.4): La Lata from the start, each group by its side.
                if (SplitFight(s)) return true;
                // Saqueadores (G23.5): once the stash is robbed, or there is no going for it (RaidMode).
                if (RaidersFight(s)) return RaidMode(s) == Raid.Go;
                // Evacuación (G23.6): with two civilians aboard, or no more to get aboard (EvacLeaving).
                if (EvacFight(s)) return EvacLeaving(s);
            }
            if (RunSaqueo(s)) return RunLeaving(s, roomOf);
            if (m.Kind == MissionKinds.Scavenge && m.EvacZone.Count > 0)
            {
                var enemies = Foes(s).Select((e) => Grid.NodeOf(s, e.At)).ToList();
                return !Squad(s).Any((o) => !o.Evacuated && enemies.Any((n) => walksOf(o).Cost.TryGetValue(n, out _)));
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
                    && Foes(s).Any((e) => Grid.Distance(holder.Pos, e.At) <= 2);
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
                // El herido (G23.3): taken up from beside them, set down on a tile of La Lata (they get in), or passed on.
                if (WoundedFight(s) && WoundedOrder(s, u) is Command carry) return carry;
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
            // Saqueadores (G23.5): a crate of the stash beside it, searched before any shot.
            // Unseen, not where its noise gets a guard up from his chair to see one of the squad (Quietly).
            if (RaidersFight(s) && Usable(s, u, AbilityIds.Search) && Looting.SearchableTiles(s, u).FirstOrDefault((p) => StashCrate(s, p) && (!Hush(s) || Quietly(s, u, p))) is Pos crate)
                return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = crate };
            // Arco y ballesta (cuadro 102): a body beside it with arrows in it, searched (they go back to the pack), with none of
            // the dead near to make it wait; hidden, only where nobody would hear it and turn to see the squad.
            if (s.Run != null && Content.FiresArrows(u) && Usable(s, u, AbilityIds.Search) && !Foes(s).Any((e) => e.U.Caged != true && !e.Asleep && Grid.Distance(u.Pos, e.At) <= Close)
                && Looting.SearchableTiles(s, u).FirstOrDefault((p) => ArrowBody(s, p) && (!Hush(s) || Quietly(s, u, p))) is Pos quiver)
                return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = quiver };
            // A Guarida's box beside it: opened, the job done (H5).
            var lair = s.Run != null && s.Mission.Lair is Pos box && !s.Mission.ObjectiveDone && Usable(s, u, AbilityIds.Search)
                ? Looting.SearchableTiles(s, u).FirstOrDefault((p) => Grid.SameTile(p, box)) : null;
            if (lair != null) return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Search, Tile = lair };
            // El Toro first, at any fair odds and farther than the rest: his group comes on, he doesn't stop (H6).
            var bull = Bull(s);
            var atBull = bull != null && !carrier ? BestShot(s, u, Close * 2, bull.Id) : null;
            if (atBull != null && atBull.Value.hit >= 25) return atBull.Value.cmd;
            // Only what is close is shot at: a shot is heard, and draws more of them (docs/FASE2.md). In a run, on the way
            // out, only what stands beside it: it stood fighting by La Lata till the horde had it (a Saqueo, 16/11). Not with
            // a camp of the living awake, who shoot from afar: going so, the squad fell 4 times in 5 in the camping (R15).
            var going = s.Run != null && leaving && !Holding(s) && !(RaidersFight(s) && CampAwake(s));
            var shot = BestShot(s, u, going ? 1.5 : Close);
            var nearest = Foes(s).Select((e) => Grid.Distance(u.Pos, e.At)).DefaultIfEmpty(double.MaxValue).Min();
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
            // Evacuación (G23.6): whatever of the dead nears a civilian walking to La Lata (it panics them), at any fair odds
            // and farther than the rest.
            if (EvacFight(s) && !carrier && Walkers(s) is List<Unit> walking && walking.Count > 0
                && BestShot(s, u, Close * 2, null, (e) => walking.Any((c) => Grid.Distance(c.Pos, e.Pos) <= Rules.PanicRange + 2)) is (Command, int) cover && cover.hit >= 15) return cover.cmd;
            // Careful, on the way out (improvement 1): away to a place that risks fewer bites before striking what stands by
            // it, unless the blow kills it all but surely and none else reaches it then (on the way back, a strike was the last
            // order of 22 % of those who fell in the dead's turn: row 1's bench, 24/11).
            if (Careful && going && !carrier && shot != null && target is Pos homeward && Stride(s, u) >= Paths.CostOrtho && !SureKill(s, u, shot.Value.cmd))
            {
                var stops = Stops(s, u, Paths.ComputeReach(s, u, Stride(s, u)));
                var step = CarefulMove(s, u, homeward, leaving, Paths.ComputeReach(s, u, Stride(s, u)), stops.Where((o) => leaving || s.Run == null || !Abilities.InEvacZone(s, o.Pos)).ToList(), WayTo(s, walks, homeward, leaving), walks);
                if (step.Cmd != null && step.Tier < step.Here) return step.Cmd;
            }
            if (shot != null && !carrier && shot.Value.hit >= 30) return shot.Value.cmd;
            // A run's Saqueo, before going: the room it stands in, then a box, a boot or a body beside it. Separados on the way
            // to La Lata, the same, never a step aside for one (G23.4: the walk is movement, the search an action).
            if ((RunSaqueo(s) && !leaving) || (SplitFight(s) && !Abilities.InEvacZone(s, u.Pos)))
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
            // (Nor in Saqueadores hidden: the walk up to them is in sight of whoever.)
            var hush = Hush(s);
            if (!armed && !carrier && !going && !Holding(s) && !hush)
            {
                var close = MoveToStrike(s, u, leaving);
                if (close != null) return close;
            }
            var goal = (!armed && !leaving && u.Carrying == null && !hush ? GunGoal(s, u, walks) : null) ?? target;
            // With few arrows left, a body with some a few steps off, nobody near (ArrowGoal).
            if (!leaving && !hush && u.Carrying == null && ArrowGoal(s, u, walks) is Pos arrows) goal = arrows;
            // Saqueadores hidden (G23.5): only by the tiles none of the camp's watchers would notice it on; none, it waits.
            if (goal != null && !leaving && hush)
            {
                var sneak = Sneak(s, u, goal, false);
                if (sneak != null) return sneak;
                goal = null;
            }
            // Going with the camp asleep and the squad hidden, so too while it can: by the camping's camp to La Lata, the
            // round's guard saw it go by on the first turn, and the camp was on it (R15). With no hidden step, on as ever.
            // (Found by then, too, it woke the camp hardly less, 6 times in 120 against 8, and the fight took a third longer.)
            if (goal != null && leaving && RaidersFight(s) && s.Concealed && Guards(s).Count > 0 && !CampAwake(s) && !Abilities.InEvacZone(s, u.Pos)
                && Sneak(s, u, goal, true) is Command away) return away;
            // La llave, after its key: never far ahead of the squad's last (Leash), so the dead come to it together.
            if (goal != null && !leaving && KeyFight(s) && Looting.KeyWhere(s) == KeyPlaces.Holder && Ahead(s, u, goal, walks, walksOf) > Leash * Paths.CostOrtho) goal = null;
            if (goal != null && !(leaving && Abilities.InEvacZone(s, u.Pos)))
            {
                var pry = Pry(s, u, goal, leaving);
                if (pry != null) return pry;
                var stride = Stride(s, u);
                // Mordido's antiviral in hand, the count running: on regardless of the danger (with the dead by the stairs,
                // it stood on watch upstairs till the bitten turned: R11, 19/11).
                var rush = (BittenFight(s) && Looting.AntiviralOf(u) != null) || u.Carrying != null;
                var move = stride >= Paths.CostOrtho ? MoveToward(s, u, goal, stride, leaving, walks, reckless: rush) : null;
                if (move != null) return move;
            }
            // On a run's way out with no step to take, what is near is shot after all: the horde stood in the way to La Lata,
            // four tiles off, and the squad waited for it for ever (R3, 17/11: a Guarida in the hospital, 700 turns).
            if (going && !carrier && BestShot(s, u, Close) is (Command, int) way && way.hit >= 30) return way.cmd;
            // And with nothing to shoot, a step on regardless of the danger: inside, the horde at the door can't be seen,
            // and the blade alone waited by it for ever (R3: the offices' radio, 3000 turns).
            // (Careful, not so: MoveToward has weighed every place by its bites already, and walking blind into them is what it
            // keeps from; standing still for ever it is kept from by Stalled.)
            if (!Careful && going && goal != null && !Abilities.InEvacZone(s, u.Pos) && Stride(s, u) >= Paths.CostOrtho && MoveToward(s, u, goal, Stride(s, u), leaving, walks, reckless: true) is Command push) return push;
            if (shot != null && shot.Value.hit >= 15) return shot.Value.cmd;
            // Separados hidden (G23.4): no watch kept. Its shot at one of the dead going by gave the squad away on the first
            // turn, both groups at once (R13, 20/11: city, 2 seats, seed 1).
            // So too in Saqueadores (G23.5), and with the camp asleep, even with the squad found by the dead.
            // Careful (improvement 1): with walk left where bites reach it, to a place that risks fewer, before keeping watch.
            if (Careful && Paths.MoveBudget(s, u) >= Paths.CostOrtho && BiteRisk(s, u, u.Pos, null, leaving).Tier > 0)
            {
                var budget = Paths.MoveBudget(s, u) < Paths.FullMoves(s, u) ? Paths.MoveBudget(s, u) : Stride(s, u);
                var reach = Paths.ComputeReach(s, u, budget);
                var stops = Stops(s, u, reach).Where((o) => leaving || s.Run == null || !Abilities.InEvacZone(s, o.Pos)).ToList();
                var safer = CarefulMove(s, u, goal ?? u.Pos, leaving, reach, stops, goal != null ? WayTo(s, walks, goal, leaving) : null, walks);
                if (safer.Cmd != null && safer.Tier < safer.Here) return safer.Cmd;
            }
            var watch = !((SplitFight(s) || RaidersFight(s)) && s.Concealed) && !Quiet(s);
            if (watch && Usable(s, u, AbilityIds.Overwatch) && nearest <= Close * 2) return Ability(u, AbilityIds.Overwatch);
            if (watch && Usable(s, u, AbilityIds.OverwatchMelee) && nearest <= Close) return Ability(u, AbilityIds.OverwatchMelee);
            if (Usable(s, u, AbilityIds.Hunker)) return Ability(u, AbilityIds.Hunker);
            return null;
        }

        private static IEnumerable<Unit> Enemies(GameState s) => Grid.Combatants(s, Teams.Alien);

        // ------------------------------------------------------------ The prudent bot (Bots, 24/11)

        /// <summary>
        /// How the bot plays (Bots, 24/11): off by default, so that the apps, Measure, the determinism probe and every seed of
        /// the tests play as before; the benches and the prudent bot's tests turn it on, per thread (they play several fights
        /// at once). Careful plays Fair too.
        /// </summary>
        public sealed class BotStyle
        {
            /// <summary>
            /// Only what the squad knows of the dead (Bots' rule 2: no cheating): those it sees now, and those it saw before,
            /// where it saw them last (BotMemory); never where one it doesn't see stands now.
            /// </summary>
            public bool Fair { get; set; }

            /// <summary>
            /// Improvement 1, moving without being bitten (.dev-snapshots/bots/MEJORA-1-DISENO.md): each place to walk to by
            /// the bites it risks (BiteRisk: the dead awake it knows that reach it next turn, the bites going past on the way),
            /// away before striking on the way out, the blade only where it holds, the walk left spent getting safe.
            /// </summary>
            public bool Careful { get; set; }
        }

        [System.ThreadStatic] public static BotStyle? Style;

        private static bool Careful => Style?.Careful == true;

        private static bool Fair => Style?.Fair == true || Careful;

        /// <summary>One of the other side as the squad knows it: where it was last seen (At), and asleep then or not.</summary>
        private readonly struct Foe
        {
            public readonly Unit U;
            public readonly Pos At;
            public readonly bool Asleep;

            public Foe(Unit u, Pos at, bool asleep)
            {
                U = u;
                At = at;
                Asleep = asleep;
            }
        }

        /// <summary>
        /// What the squad has seen of the other side in a fight (Fair): each one by its id, where it was seen last and whether
        /// it was asleep then. Kept per state (a fight's GameState), the bot being called for each order; a state copied anew
        /// (an app's snapshot) starts with none, and knows only what it sees.
        /// </summary>
        private sealed class BotMemory
        {
            public int Seq = -1;
            public readonly Dictionary<string, (Pos At, bool Asleep)> Seen = new Dictionary<string, (Pos At, bool Asleep)>();
            public List<Foe> Known = new List<Foe>();
            /// <summary>Careful: each soldier's walk left to its goal at the first careful move of a turn, and how many turns running it got no nearer (Stalled).</summary>
            public readonly Dictionary<string, (int Turn, double Left, int Stalls)> Progress = new Dictionary<string, (int Turn, double Left, int Stalls)>();
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameState, BotMemory> memories = new System.Runtime.CompilerServices.ConditionalWeakTable<GameState, BotMemory>();

        /// <summary>
        /// The other side as the squad knows it now. Not Fair: all of them where they stand (as before). Fair: those it sees
        /// (Los.VisibleEnemies), and those seen before at their last place, forgotten once that place is in sight with nobody
        /// on it, or once seen elsewhere.
        /// </summary>
        private static List<Foe> Foes(GameState s)
        {
            if (!Fair) return Enemies(s).Select((e) => new Foe(e, e.Pos, Asleep(s, e))).ToList();
            var m = memories.GetValue(s, (_) => new BotMemory());
            if (m.Seq == s.Seq) return m.Known;
            var visible = Los.VisibleEnemies(s, Teams.Xcom).Where((e) => e.Alive && e.Team == Teams.Alien && !e.Objective && !e.Captive).ToList();
            foreach (var e in visible) m.Seen[e.Id] = (Events.CopyPos(e.Pos), Asleep(s, e));
            var view = Los.TeamView(s, Teams.Xcom);
            foreach (var id in m.Seen.Keys.ToList())
            {
                if (visible.Any((e) => e.Id == id)) continue;
                var at = m.Seen[id].At;
                var node = Grid.NodeOf(s, at);
                if (Grid.GetUnit(s, id) == null || (node >= 0 && node < view.Length && view[node])) m.Seen.Remove(id);
            }
            m.Known = m.Seen.Select((kv) => new Foe(Grid.GetUnit(s, kv.Key)!, kv.Value.At, kv.Value.Asleep)).ToList();
            m.Seq = s.Seq;
            return m.Known;
        }

        /// <summary>
        /// The squad on the field: of the squad's side, but not an Evacuación's civilians (G23.6: of it to the dead, nobody's,
        /// never ordered: Rules.IsCivilian). Counted with them, the hidden ones kept La Lata waiting for ever.
        /// </summary>
        private static List<Unit> Squad(GameState s) => Grid.Combatants(s, Teams.Xcom).Where((u) => !Rules.IsCivilian(u)).ToList();

        /// <summary>Where `u` goes: La Lata (leaving, or carrying the item), the item, the radio, the one to free, the alarm, or the nearest enemy by the walk.</summary>
        private static Pos? Goal(GameState s, Unit u, bool leaving, System.Func<Unit, Reach> walksOf, System.Func<Unit, Pos?> roomOf)
        {
            var m = s.Mission;
            // La Lata's tiles still open (T24): one shut by someone in it takes nobody.
            var bus = Rules.OpenTiles(s).Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).OrderBy((p) => Grid.Distance(u.Pos, p)).FirstOrDefault();
            // Mordido (G23.2): La Lata's last seat is the cured bitten's while they can walk to it, not a soldier's, who stays
            // out (R1, 19/11, hospital, knives, seed 12: four seats, four soldiers in first, the bitten left on the ground and
            // the stage lost). Not in a Rescate: there the survivor left behind loses nothing but a recruit.
            if (leaving && bus != null && LastSeatIsTheBittens(s, u, walksOf)) return null;
            // Separados (G23.4): so too the group with nobody going's, not a survivor's nor another of the group already in.
            if (leaving && bus != null && LastSeatIsTheShorts(s, u, walksOf)) return null;
            if (leaving && bus != null) return bus;
            if (WoundedFight(s) && !leaving) return WoundedGoal(s, u, walksOf(u)) ?? bus;
            if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Pos != null) return m.Item.Pos;
            if (m.Kind == MissionKinds.Hack && m.Terminal != null && !m.ObjectiveDone) return m.Terminal;
            if (BittenFight(s) && !leaving) return BittenGoal(s, u, walksOf(u)) ?? bus;
            if ((Vip(s) ?? Alarm(s)) is Unit objective) return objective.Pos;
            if (s.Run != null && m.Lair != null) return m.Lair;
            if (Bull(s) is Unit bull) return bull.Pos;
            if (KeyFight(s) && !leaving) return KeyGoal(s, u, walksOf(u)) ?? bus;
            // Saqueadores (G23.5): a place beside a crate of the stash; none, it waits where it is.
            if (RaidersFight(s) && !leaving) return StashGoal(s, u, walksOf);
            // Evacuación (G23.6): the nearest civilian still hidden, by their circle; then one found walking, alongside them.
            if (EvacFight(s) && !leaving) return EvacGoal(s, u, walksOf(u)) ?? bus;
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
            var targets = Foes(s).Where((e) => e.U.Caged != true).Select((e) => (e.U.Id, e.At)).ToList();
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
        /// across its bars. Kept off otherwise: a shot is heard, and at its bars it bites. (In Saqueadores with the camp
        /// asleep, none still unaware but by a silent kill: BestShot, SilentShot.)
        /// </summary>
        private static bool Worth(GameState s, Unit u, Unit e) => e.Caged != true || Hunting(s) || Rules.CagedReach(s, e).Any((p) => Grid.SamePos(p, u.Pos));

        /// <summary>
        /// Careful: `cmd`, an attack, kills its target SilentOdds or likelier (KillOdds), and then none of the dead awake the
        /// squad knows reaches `u` where it stands (BiteRisk with the target spared): then it is struck, not walked away from.
        /// </summary>
        private static bool SureKill(GameState s, Unit u, Command cmd)
        {
            if (cmd is not AbilityCommand a || a.Target == null || Grid.GetUnit(s, a.Target) is not Unit t) return false;
            var p = Aim.PreviewShot(s, u, t, new ShotOptions { Ability = a.Ability });
            return p != null && KillOdds(u, t, a.Ability, p) >= SilentOdds && BiteRisk(s, u, u.Pos, null, false, t.Id).Tier == 0;
        }

        /// <summary>
        /// Arco y ballesta: a silent shot is taken when it kills this likely or likelier (KillOdds): one that misses or only
        /// wounds gives the squad away, and wakes the target's group (Rules.Attack). Taken at a coin's odds, as a rookie's
        /// from afar are (a hit 45 to 60 times in 100 from 10 to 17 tiles off), it woke the camp more (83 % against 62 %) and
        /// robbed less (the three crates 50 % against 73 %; Red, 21/11, banco-arco).
        /// </summary>
        public const double SilentOdds = 0.8;

        /// <summary>
        /// Arco y ballesta (cuadro 102): `u`'s `ability` at `t`, unaware, would kill it unseen and keep the squad hidden
        /// (Rules.SilentKill: arrows, and nobody else notices `u`), SilentOdds or likelier (KillOdds).
        /// </summary>
        private static bool SilentShot(GameState s, Unit u, Unit t, string ability, ShotPreview p)
        {
            return ability == AbilityIds.Shoot && Rules.SilentKill(s, u, t) && KillOdds(u, t, ability, p) >= SilentOdds;
        }

        /// <summary>
        /// The odds `u`'s `ability` kills `t`, as `p` sees it: its crit, graze and plain hit bands (Aim.ResolveShot), each
        /// killing as many of its damages as reach `t`'s hp (Aim.ShotDamage after its armour), each damage as likely.
        /// </summary>
        private static double KillOdds(Unit u, Unit t, string ability, ShotPreview p)
        {
            var damage = Aim.ShotDamage(u, ability, t, p);
            double Kills(DamageRange? r) => r == null || r.Max < t.Hp ? 0 : r.Min >= t.Hp ? 1 : (r.Max - t.Hp + 1) / (double)(r.Max - r.Min + 1);
            var crit = System.Math.Min(p.Crit, p.Hit);
            var graze = System.Math.Min(p.Graze, p.Hit - crit);
            return (crit * Kills(damage.Crit) + graze * Kills(damage.Graze) + (p.Hit - crit - graze) * Kills(damage.Hit)) / 100.0;
        }

        /// <summary>
        /// Whether all the squad not gone is in La Lata, but for any that can't walk there from where it is (walled off, no
        /// crowbar): with no clock to call the bus now (Arturo, 5/11 #1), it would wait for them for ever.
        /// </summary>
        private static bool AllAboard(GameState s)
        {
            // El herido alive and out of it: La Lata doesn't go (G23.3).
            if (Rules.WoundedOut(s)) return false;
            // Separados: not while the group with nobody going has one that can walk to it, past the dead too (G23.4).
            if (SplitWaits(s)) return false;
            // Evacuación: not while a civilian found walks to it, short of two aboard (G23.6).
            if (EvacWaits(s)) return false;
            var bus = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            // (Mordido's bitten not cured never gets in: none to wait for, G23.2.)
            // La Camilla's downed doesn't walk, and isn't waited for: the bot doesn't carry them (C6 of the 0.14.11's review: taken
            // for one who walks, with all the rest in La Lata it never called it). Carrying them, as Gameplay's probe did, saved 6 %
            // of them and lost the whole squad more often (375 of 450 against 340: notas-26-11/camilla).
            foreach (var o in Squad(s).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos) && !Rules.BittenUncured(s, o) && o.Downed != true))
            {
                // Careful: past the dead too, who move (a mate kept off by them, hanging back, was left on the ground 29 times
                // in 708 fights against 2: row 1's bench, 24/11); but not once it has got no nearer two turns running: the dead
                // round the foot of the only ladder down never left, nor climbed, and La Lata waited 2900 turns (hard, seed 35).
                var past = Careful && StallsOf(s, o) < 2;
                var reach = past ? Paths.ComputeReach(s, o, double.MaxValue, null, (x) => x.Team != o.Team) : Paths.ComputeReach(s, o, double.MaxValue);
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
            var outside = Squad(s).Where((o) => !o.Evacuated && !Abilities.InEvacZone(s, o.Pos)).ToList();
            if (outside.Count == 0 || Paths.MoveBudget(s, u) <= 0) return null;
            double Away(Pos p) => outside.Min((o) => Grid.Distance(p, o.Pos));
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            foreach (var o in Stops(s, u, reach))
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

        /// <summary>
        /// Where a move may end (Paths.Destinations), never one whose way takes all its health (N2, Arturo 22/11, cuadro 119:
        /// the bot never jumps to die), weighed on the way the order will take (Rules: the reach of all the movement left,
        /// BuildPath, its way round a fire too: Producto's review of the 0.14.0, point 6, and Gameplay's), not on this reach's.
        /// The jumps it does take are the ones the way gives (cuadro 122: by the stairs wherever they get it this turn;
        /// carrying someone, none). Never a stop on fire either: the rules let one end there (whoever does chose it), and it
        /// burns at once and again at the turn's end (a recruit at 1 hp ended on a burning ladder).
        /// </summary>
        private static List<Destination> Stops(GameState s, Unit u, Reach reach)
        {
            var all = Paths.Destinations(s, u, reach);
            if (Burning.Nodes(s) != null) all = all.Where((o) => !Burning.At(s, o.Pos)).ToList();
            var order = OrderReach(s, u);
            return all.Where((o) => !Deadly(s, u, order, o.Pos)).ToList();
        }

        /// <summary>The last OrderReach, per thread (the benches play four fights at once).</summary>
        [System.ThreadStatic] private static OrderKept? orderKept;

        private sealed class OrderKept
        {
            public GameState? State;
            public int Seq, Node;
            public string Unit = "";
            public double Budget;
            public Reach? Reach;
        }

        /// <summary>The reach a move order of `u`'s goes by (Rules: all the movement left), kept while nothing has changed.</summary>
        private static Reach OrderReach(GameState s, Unit u)
        {
            var budget = Paths.MoveBudget(s, u);
            var node = Grid.NodeOf(s, u.Pos);
            var k = orderKept;
            if (k != null && ReferenceEquals(k.State, s) && k.Seq == s.Seq && k.Unit == u.Id && k.Node == node && k.Budget == budget && k.Reach != null) return k.Reach;
            var reach = Paths.ComputeReach(s, u, budget);
            orderKept = new OrderKept { State = s, Seq = s.Seq, Node = node, Unit = u.Id, Budget = budget, Reach = reach };
            return reach;
        }

        /// <summary>
        /// The way to `to` takes all its health or more: its falls off storeys (N2: Paths.FallAlong, Content.FallDamage each,
        /// through armour) and the burning tiles it steps on (Rules.MoveAlong: Content.Flames.Damage each).
        /// </summary>
        public static bool Deadly(GameState s, Unit u, Reach reach, Pos to)
        {
            var path = Paths.BuildPath(s, u, reach, to);
            if (path == null) return false;
            return Paths.FallAlong(s, reach.Origin, path) * Content.FallDamage + path.Count((p) => Burning.At(s, p)) * Content.Flames.Damage >= u.Hp;
        }

        /// <summary>
        /// A run's Saqueo: the normal node (Gameplay, 16/11), no objective but to search and go; and a Separados without its
        /// two groups (one soldier, or no room on the map: G23.4), a Saqueadores without its camp (no room for it: G23.5), or
        /// an Evacuación without its civilians (too few buildings for them: G23.6), which are one.
        /// </summary>
        private static bool RunSaqueo(GameState s) => (s.Run?.NodeKind == NodeKinds.Normal || (s.Run?.NodeKind == NodeKinds.Split && s.Mission.Split == null)
            || (s.Run?.NodeKind == NodeKinds.Raiders && s.Mission.Raiders == null) || (s.Run?.NodeKind == NodeKinds.Evacuation && s.Mission.Civilians == null))
            && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

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
            var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
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
            var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
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
        /// Gameplay, 16/11), or with a bow or a crossbow, arrows (Unit.Arrows); empty with none, it goes to the blade.
        /// </summary>
        private static bool Armed(GameState s, Unit u)
        {
            if (!Content.HasGun(u)) return false;
            if (s.Run == null) return true;
            // Arco y ballesta (cuadro 102): the bow's shots and the crossbow's reloads are its arrows (Unit.Arrows); the bow's
            // Ammo says nothing (Clip 0).
            if (Content.FiresArrows(u)) return u.Ammo > 0 || (u.Arrows ?? 0) > 0 || (u.PistolAmmo ?? 0) > 0;
            return u.Ammo > 0 || (u.Mags ?? 0) > 0 || (u.PistolAmmo ?? 0) > 0;
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
            var near = Foes(s).Any((e) => e.U.Caged != true && Grid.Distance(u.Pos, e.At) <= Close);
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
            // A run's gear only with a pack to take it in (PackOrder).
            if (u.Pack != null) found.AddRange((s.FloorGear ?? new List<DroppedGear>()).Where((d) => IsGunGear(d.Gear) && Walked(d.Pos)).Select((d) => d.Pos));
            // Never the tile it stands on: what lies there it would have taken by now if it could. A recruit with no pack
            // stood on a machine gun, crouched, for 40 turns of a Limpieza (Red's R2 by level, 23/11: easy, 2 seats, seed 5).
            found.RemoveAll((p) => Grid.SamePos(p, u.Pos));
            if (found.Count == 0) return null;
            double Near(Unit x) => found.Min((p) => Grid.Distance(x.Pos, p));
            var going = Squad(s).Where((x) => !x.Evacuated && !Armed(s, x) && PackedGun(x) == null && x.Pack?.Items.Any((p) => IsGunGear(p.Gear)) != true && s.Mission.Item?.Carrier != x.Id)
                .OrderBy(Near).ThenBy((x) => x.Id, System.StringComparer.Ordinal).Take(found.Count);
            if (!going.Any((x) => x.Id == u.Id)) return null;
            return found.OrderBy((p) => Grid.Distance(u.Pos, p)).First();
        }

        /// <summary>
        /// The blade's move (Arturo, 16/10: striking is striking and moving is moving): with an action to strike, a stretch
        /// to a tile beside one of the dead the squad sees (moving takes none: Arturo, 5/11 #14). The weakest first, then
        /// where fewest others can bite, then the shortest; null when none is that near. In a run, not leaving, never to a
        /// tile of La Lata: the walk gets in (T24; R1, 19/11: El herido in the fábrica, one in before they were found).
        /// </summary>
        private static Command? MoveToStrike(GameState s, Unit u, bool leaving)
        {
            if (Paths.MoveBudget(s, u) <= 0 || !Usable(s, u, AbilityIds.Slash)) return null;
            var enemies = Foes(s);
            // A caged one in a Limpieza even unseen: where it is, it stays (its cell shows only from right in front of the bars).
            var seen = Enemies(s).Where((e) => e.Caged != true ? Aim.TeamSees(s, u.Team, e) : Hunting(s)).ToList();
            if (seen.Count == 0) return null;
            var reach = Paths.ComputeReach(s, u, Stride(s, u));
            Destination? best = null;
            (int hp, int crowd, double cost) bestKey = default;
            foreach (var o in Stops(s, u, reach))
            {
                if (s.Run != null && !leaving && Abilities.InEvacZone(s, o.Pos)) continue;
                var beside = seen.Where((e) => Aim.InMeleeReach(s, o.Pos, e.Pos)).ToList();
                if (beside.Count == 0) continue;
                // Careful: only where, the one it strikes aside, the bites that reach it there and on the way leave it standing
                // (BiteRisk: tier 1 at most).
                if (Careful)
                {
                    var weakest = beside.OrderBy((e) => e.Hp).ThenBy((e) => e.Id, System.StringComparer.Ordinal).First();
                    if (BiteRisk(s, u, o.Pos, Paths.BuildPath(s, u, reach, o.Pos), false, weakest.Id).Tier > 1) continue;
                }
                var key = (hp: beside.Min((e) => e.Hp), crowd: enemies.Count((e) => Grid.Distance(o.Pos, e.At) <= 2.5), cost: o.Cost);
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
            // To one of the other side's own place, the way round it, not through it (the walks go past the dead): through
            // a window, the way went over the zombie behind it to the far side of the house, and the recruit stood under the
            // window for ever (Gameplay's R2 of 21/11, seed 393: a Limpieza, the blade alone).
            if (!bus && Grid.UnitAt(s, goal) is Unit there && there.Team != u.Team) walks = Paths.ComputeReach(s, u, double.MaxValue, null, (o) => o.Team != u.Team && o != there);
            var reach = Paths.ComputeReach(s, u, cost);
            // Where it ends up matters as much as how near: beside the dead it is bitten in their turn. Those of a group still
            // asleep, only while searching a Saqueo or on the way to a Guarida's box or El Toro: on the way to an objective or
            // to La Lata they were kept clear of for ever, in one building where they never come out (R1, 17/11: Limpiezas
            // of 1000 turns in the hospital and the police station, the squad waiting a few tiles off; a radio fixed in the
            // offices and La Lata never reached).
            var careful = !bus && !Bold(s.Mission);
            var enemies = Foes(s).Where((e) => e.U.Caged != true && (careful || !e.Asleep)).ToList();
            // The caged (H9), only on the tiles across their bars: nowhere else can they bite. Not in a Limpieza, where those
            // tiles are where the squad has to stand to kill them.
            var caged = new HashSet<int>(Hunting(s) ? Enumerable.Empty<int>() : Enemies(s).Where((e) => e.Caged == true).SelectMany((e) => Rules.CagedReach(s, e)).Select((p) => Grid.NodeOf(s, p)));
            // El Toro's charge ready (or by the next turn): his lines are kept out of (H6).
            var bull = Bull(s);
            var charging = bull != null && (!bull.Cooldowns.TryGetValue(AbilityIds.Charge, out var wait) || wait <= 1);
            double Danger(Pos p) => reckless ? 0 : enemies.Sum((e) => Grid.Distance(p, e.At) <= 2.5 ? 4 : Grid.Distance(p, e.At) <= 5 ? 1.5 : 0) + (caged.Contains(Grid.NodeOf(s, p)) ? 4 : 0) + (charging && InChargeLine(bull!, p) ? 6 : 0);
            // Not going for La Lata, never a stop on a tile of it in a run: a walk that ends there gets in (T24), and with the
            // street between the start and the police station, two of four got in on their first walk (R9, La llave).
            var stops = Stops(s, u, reach).Where((o) => bus || s.Run == null || !Abilities.InEvacZone(s, o.Pos)).ToList();
            Destination? best = null;
            var end = WayTo(s, walks, goal, bus);
            // Careful (improvement 1): by the bites each place risks first, then by how near it is (CarefulMove).
            if (Careful && !reckless)
            {
                bool Extra(Pos p) => caged.Contains(Grid.NodeOf(s, p)) || (charging && InChargeLine(bull!, p));
                return CarefulMove(s, u, goal, bus, reach, stops, end, walks, Extra).Cmd;
            }
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

        // ------------------------------------------------------------ Improvement 1: moving without being bitten (Bots, 24/11)

        /// <summary>A known one of the dead awake: the tiles (nodes) its bite reaches next turn, and its worst bite.</summary>
        private sealed class Zone
        {
            public string Id = "";
            public readonly HashSet<int> Tiles = new HashSet<int>();
            public int Bite;
        }

        [System.ThreadStatic] private static GameState? zonesOf;
        [System.ThreadStatic] private static int zonesSeq;
        [System.ThreadStatic] private static List<Zone>? zones;

        /// <summary>
        /// The bite zones of the dead awake the squad knows (Foes, not asleep, not caged): from where each was seen, its walk
        /// of a whole turn (Paths.FullMoves, past its own side), and a bite from any tile of it (Aim.InMeleeReach). Kept a
        /// state's seq.
        /// </summary>
        private static List<Zone> Zones(GameState s)
        {
            if (zones != null && ReferenceEquals(zonesOf, s) && zonesSeq == s.Seq) return zones;
            var list = new List<Zone>();
            foreach (var f in Foes(s))
            {
                if (f.Asleep || f.U.Caged == true || !f.U.Alive || !Content.Templates[f.U.Template].Undead) continue;
                var z = new Zone { Id = f.U.Id, Bite = WorstBite(f.U) };
                var reach = Paths.ComputeReach(s, f.U, Paths.FullMoves(s, f.U), f.At, (o) => o.Team == f.U.Team);
                foreach (var n in reach.Cost.Keys.Append(Grid.NodeOf(s, f.At)).Distinct())
                {
                    var q = Grid.PosOf(s, n);
                    foreach (var d in Grid.Dirs8)
                    {
                        var p = new Pos { X = q.X + d.X, Y = q.Y + d.Y, Z = q.Z };
                        if (Grid.InBounds(s, p.X, p.Y) && Aim.InMeleeReach(s, q, p)) z.Tiles.Add(Grid.NodeOf(s, p));
                    }
                }
                list.Add(z);
            }
            zonesOf = s;
            zonesSeq = s.Seq;
            return zones = list;
        }

        /// <summary>The worst a bite of `e` takes without a crit (its melee weapon's damage and spread; a crit adds more).</summary>
        private static int WorstBite(Unit e)
        {
            var t = Content.Templates[e.Template];
            return Content.Weapons.TryGetValue(t.MeleeWeapon ?? t.Weapon, out var w) ? w.Damage + w.Spread : 4;
        }

        /// <summary>
        /// What `u` risks walking `path` to `p` and ending its turn there (improvement 1): the worst bites of the dead awake
        /// whose zone takes `p` (Zones; but `spared`, the one it is to strike), and those going past on the way (Rules
        /// .ReactionsAlong: the squad's sight, as the screen draws it). A tile of La Lata in a run, going for it, takes no
        /// bite at the end (in it, nobody was bitten in the 708 fights of row 1). Tier 0: none; 1: fewer than its health; 2:
        /// as many or more, or the place is otherwise kept out of (`extra`: a caged one's bars, El Toro's line).
        /// </summary>
        private static (int Tier, int Damage) BiteRisk(GameState s, Unit u, Pos p, List<Pos>? path, bool bus, string? spared = null, System.Func<Pos, bool>? extra = null)
        {
            var damage = 0;
            var node = Grid.NodeOf(s, p);
            if (!(bus && s.Run != null && Abilities.InEvacZone(s, p)))
                foreach (var z in Zones(s))
                    if (z.Id != spared && z.Tiles.Contains(node)) damage += z.Bite;
            if (path != null && path.Count > 0 && !Grid.SamePos(p, u.Pos))
                foreach (var r in Rules.ReactionsAlong(s, u, path))
                    if (r.Kind != PathReactions.Suppress && r.Unit != spared && Grid.GetUnit(s, r.Unit) is Unit o) damage += WorstBite(o);
            var tier = damage == 0 ? 0 : damage < u.Hp ? 1 : 2;
            if (extra != null && extra(p)) tier = 2;
            return (tier, damage);
        }

        /// <summary>Careful: Stalled as last reckoned for `u`, without reckoning it anew (0 if never).</summary>
        private static int StallsOf(GameState s, Unit u) => memories.TryGetValue(s, out var m) && m.Progress.TryGetValue(u.Id, out var p) ? p.Stalls : 0;

        /// <summary>
        /// Careful: how many turns running `u` got no nearer its goal (`left`, its walk left, at its first careful move of each
        /// turn), at most 2: the tiers of bite taken as none (CarefulMove). Kept in the fight's BotMemory.
        /// </summary>
        private static int Stalled(GameState s, Unit u, double left)
        {
            var m = memories.GetValue(s, (_) => new BotMemory());
            if (!m.Progress.TryGetValue(u.Id, out var was))
            {
                m.Progress[u.Id] = (s.Turn, left, 0);
                return 0;
            }
            if (was.Turn == s.Turn) return was.Stalls;
            var stalls = left < was.Left - 0.5 ? 0 : System.Math.Min(2, was.Stalls + 1);
            m.Progress[u.Id] = (s.Turn, left, stalls);
            return stalls;
        }

        /// <summary>
        /// Improvement 1's move (MoveToward when Careful): of the places `u` can stop on this stretch (`stops`), the one with the
        /// lowest BiteRisk tier, then the nearest its goal by the walk back from the way's end (`end`; as the crow flies
        /// without one), then the least walked; only if better than staying (a lower tier, or the same and a tile nearer).
        /// So it steps back with nowhere safer ahead. Here: the tier where it stands.
        /// </summary>
        private static (Command? Cmd, int Tier, int Here) CarefulMove(GameState s, Unit u, Pos goal, bool bus, Reach reach, List<Destination> stops, Pos? end, Reach walks, System.Func<Pos, bool>? extra = null)
        {
            var back = end != null ? Paths.ComputeReach(s, u, double.MaxValue, end, (o) => o.Team != u.Team) : null;
            // On the way there (`walks`, from `u`), what is left of it; the walk back from the end can't climb what the way
            // drops down (a survivor found upstairs, up by a way that only goes down, stood still 2900 turns: hard, seed 35).
            var along = new Dictionary<int, double>();
            if (end != null)
            {
                var total = walks.Cost.Get(Grid.NodeOf(s, end), 0);
                foreach (var n in Way(s, walks, end)) along[n] = (double)(total - walks.Cost.Get(n, 0)) / Paths.CostOrtho;
                along[Grid.NodeOf(s, u.Pos)] = (double)total / Paths.CostOrtho;
            }
            double Left(Pos p)
            {
                if (bus && Abilities.InEvacZone(s, p)) return 0;
                if (along.TryGetValue(Grid.NodeOf(s, p), out var a)) return back != null && back.Cost.TryGetValue(Grid.NodeOf(s, p), out var b) ? System.Math.Min(a, (double)b / Paths.CostOrtho) : a;
                if (back != null && back.Cost.TryGetValue(Grid.NodeOf(s, p), out var w)) return (double)w / Paths.CostOrtho;
                return Grid.Distance(p, goal) + 3 * System.Math.Abs(p.Z - goal.Z) + (back != null ? 1000 : 0);
            }
            // Waiting where it is safe has its own cost (the horde grows every turn, and a group between it and La Lata may never
            // move): each turn running it got no nearer, one tier more is taken as none (Stalled). Without it, 5 fights of 708
            // stood still for ever (row 1's bench, 24/11).
            var allowed = Stalled(s, u, Left(u.Pos));
            int Eff(int tier) => tier <= allowed ? 0 : tier;
            var here = Eff(BiteRisk(s, u, u.Pos, null, bus, null, extra).Tier);
            var stay = (Tier: here, Left: Left(u.Pos));
            // By the tier at the end first (the way's bites only add to it), then the walk left, then the walk taken.
            var order = stops.Where((o) => !Grid.SamePos(o.Pos, u.Pos))
                .Select((o) => (Stop: o, End: Eff(BiteRisk(s, u, o.Pos, null, bus, null, extra).Tier), Left: Left(o.Pos)))
                .OrderBy((x) => x.End).ThenBy((x) => x.Left).ThenBy((x) => x.Stop.Cost).ThenBy((x) => Grid.NodeOf(s, x.Stop.Pos)).ToList();
            Destination? best = null;
            (int Tier, double Left, int Cost) bestKey = (int.MaxValue, double.MaxValue, int.MaxValue);
            foreach (var x in order)
            {
                if (x.End > bestKey.Tier) break;
                var path = Paths.BuildPath(s, u, reach, x.Stop.Pos);
                if (path == null) continue;
                var tier = Eff(BiteRisk(s, u, x.Stop.Pos, path, bus, null, extra).Tier);
                var key = (tier, x.Left, x.Stop.Cost);
                if (key.tier < bestKey.Tier || (key.tier == bestKey.Tier && (key.Left < bestKey.Left - 1e-9 || (System.Math.Abs(key.Left - bestKey.Left) < 1e-9 && key.Cost < bestKey.Cost))))
                {
                    best = x.Stop;
                    bestKey = key;
                }
            }
            if (best == null || !(bestKey.Tier < stay.Tier || (bestKey.Tier == stay.Tier && bestKey.Left < stay.Left - 0.5))) return (null, here, here);
            return (new MoveCommand { Unit = u.Id, To = best.Pos }, bestKey.Tier, here);
        }

        // ------------------------------------------------------------ Arco y ballesta (cuadros 101–106)

        /// <summary>Arco y ballesta: with this many arrows or fewer, one with a bow or a crossbow goes for those in a body near.</summary>
        public const int ArrowsLow = 3;

        /// <summary>Arco y ballesta: how far (tiles of walk) it goes for a body's arrows.</summary>
        public const int ArrowWalk = 8;

        /// <summary>A body on `p` still to search with arrows in it (Looting.BodyArrows: those that hit it; a Cazador's own).</summary>
        private static bool ArrowBody(GameState s, Pos p) => Looting.BodyAt(s, p) is Unit b && Looting.BodyArrows(b) > 0 && Looting.IsSearchable(s, p);

        /// <summary>
        /// Arco y ballesta: where `u`, with a bow or a crossbow and ArrowsLow arrows or fewer, goes for more: the nearest place
        /// by `walks` beside a body with some (ArrowBody), within ArrowWalk, with none of the dead awake within Close of it.
        /// Null otherwise.
        /// </summary>
        private static Pos? ArrowGoal(GameState s, Unit u, Reach walks)
        {
            if (s.Run == null || !Content.FiresArrows(u) || (u.Arrows ?? 0) > ArrowsLow) return null;
            var awake = Foes(s).Where((e) => e.U.Caged != true && !e.Asleep).Select((e) => e.At).ToList();
            Pos? best = null;
            var bestWalk = ArrowWalk * Paths.CostOrtho + 1;
            foreach (var b in s.Units.Where((b) => !b.Alive && ArrowBody(s, b.Pos)))
            {
                if (awake.Any((e) => Grid.Distance(e, b.Pos) <= Close)) continue;
                var w = WalkTo(s, walks, b.Pos);
                if (w >= bestWalk) continue;
                best = b.Pos;
                bestWalk = w;
            }
            return best;
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
                var mates = Squad(s).Where((o) => o.Id != u.Id && !o.Evacuated).ToList();
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

        /// <summary>
        /// Mordido: one seat of La Lata left, and the bitten cured and come along (talked to) still out of it and able to walk
        /// to it: that seat is theirs, and `u`, one of the squad, doesn't take it.
        /// </summary>
        private static bool LastSeatIsTheBittens(GameState s, Unit u, System.Func<Unit, Reach> walksOf)
        {
            if (!BittenFight(s) || s.Mission.Cured != true || Vip(s) is not Unit bitten || bitten.Id == u.Id) return false;
            if (!bitten.Alive || bitten.Captive || bitten.Aboard == true || bitten.Evacuated) return false;
            var open = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            if (open.Count != 1) return false;
            return walksOf(bitten).Cost.ContainsKey(open[0]);
        }

        // ------------------------------------------------------------ El herido (G23.3)

        /// <summary>A run's El herido: the Rescate's survivor can't walk, and is carried to La Lata.</summary>
        private static bool WoundedFight(GameState s) => s.Run != null && s.Mission.Wounded == true;

        /// <summary>El herido: the squad goes once they are in La Lata, or dead; never while they live out of it.</summary>
        private static bool WoundedLeaving(GameState s)
        {
            var w = Rules.WoundedOf(s);
            return w == null || w.Evacuated || (!w.Alive && w.CarriedBy == null);
        }

        /// <summary>
        /// Where `u` goes in El herido while they are out: the one carrying them, beside La Lata (to set them down in it); the
        /// rest, with that one; with them on the ground, a free place beside them to take them up from.
        /// </summary>
        private static Pos? WoundedGoal(GameState s, Unit u, Reach walks)
        {
            var w = Rules.WoundedOf(s);
            if (w == null) return null;
            if (u.Carrying == w.Id)
            {
                // La Lata's nearest free tile by the walk: they need no seat (they get in without one).
                return s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).Where((p) => Grid.UnitAt(s, p) == null)
                    .OrderBy((p) => WalkTo(s, walks, p)).ThenBy((p) => Grid.NodeOf(s, p)).Select((p) => (Pos?)p).FirstOrDefault();
            }
            if (w.CarriedBy != null && Grid.GetUnit(s, w.CarriedBy) is Unit carrier) return carrier.Pos;
            if (!w.Alive) return null;
            // A free place beside them that takes them up, by the walk past the squad; one to stop on (R2 by level, 23/11, hard,
            // 4 seats, seed 49: a window beside them, and the soldier crouched by it 41 turns).
            var past = Paths.ComputeReach(s, u, double.MaxValue, null, (o) => o.Id != u.Id);
            return Grid.Dirs8.Select((d) => new Pos { X = w.Pos.X + d.X, Y = w.Pos.Y + d.Y, Z = w.Pos.Z })
                .Where((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && (Grid.UnitAt(s, p) == null || Grid.UnitAt(s, p) == u) && Abilities.CarryTarget(s, u, p) != null && past.Cost.ContainsKey(Grid.NodeOf(s, p)))
                .OrderBy((p) => past.Cost[Grid.NodeOf(s, p)]).ThenBy((p) => Grid.NodeOf(s, p)).Select((p) => (Pos?)p).FirstOrDefault() ?? w.Pos;
        }

        /// <summary>
        /// El herido, before anything else: beside them on the ground, they are taken up; carried beside a free tile of La
        /// Lata, set down on it (they get in); carried with no step left with them this turn, passed to one beside that has
        /// walk enough left (PassOn tiles) to take them on.
        /// </summary>
        private static Command? WoundedOrder(GameState s, Unit u)
        {
            if (Abilities.CarryAbilities(s, u).Contains(AbilityIds.Carry) && Abilities.AbilityBlocker(s, u, AbilityIds.Carry) == null)
                return Ability(u, AbilityIds.Carry);
            if (u.Carrying == null) return null;
            if (Abilities.AbilityBlocker(s, u, AbilityIds.SetDown) == null && Abilities.SetDownTiles(s, u).FirstOrDefault((p) => Abilities.InEvacZone(s, p)) is Pos inside)
                return new AbilityCommand { Unit = u.Id, Ability = AbilityIds.SetDown, Tile = inside };
            if (Paths.MoveBudget(s, u) >= Paths.CostOrtho || Abilities.AbilityBlocker(s, u, AbilityIds.Pass) != null) return null;
            var mate = Abilities.PassTargets(s, u).Where((o) => Paths.MoveBudget(s, o) >= PassOn * Paths.CostOrtho)
                .OrderByDescending((o) => Paths.MoveBudget(s, o)).ThenBy((o) => o.Id, System.StringComparer.Ordinal).FirstOrDefault();
            return mate != null ? Ability(u, AbilityIds.Pass, mate.Id) : null;
        }

        /// <summary>El herido: the tiles of walk one beside the carrier needs left to be worth passing them to (an action).</summary>
        public const int PassOn = 3;

        // ------------------------------------------------------------ Separados (G23.4)

        /// <summary>
        /// A run's Separados with its two groups (MissionState.Split): a Saqueo whose reward asks one of each aboard as La
        /// Lata goes (Rules.SplitSafe). Without them (one soldier, or no room on the map), a Saqueo (RunSaqueo).
        /// </summary>
        private static bool SplitFight(GameState s) => s.Run != null && s.Mission.Split != null && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

        /// <summary>
        /// Separados: La Lata leaving now would lose the reward (Rules.SplitShort: a group with nobody going), and one of that
        /// group can still walk to it, past the dead too (they move; the reward waits on them). With a group all fallen
        /// (SplitLost), nothing to wait for: it goes as a Saqueo's (Producto, 20/11).
        /// </summary>
        private static bool SplitWaits(GameState s)
        {
            if (!SplitFight(s) || Rules.SplitShort(s) is not List<Unit> behind) return false;
            var bus = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            // (La Camilla's downed doesn't walk: not waited for, as in AllAboard.)
            return behind.Any((o) => !o.Captive && o.Downed != true && Paths.ComputeReach(s, o, double.MaxValue, null, (x) => x.Team != o.Team) is Reach r && bus.Any((n) => r.Cost.TryGetValue(n, out _)));
        }

        /// <summary>
        /// Separados: one seat of La Lata left, and the group with nobody going has one that can walk to it: that seat is the
        /// group's, and `u`, of the other group or a survivor who joined (of neither), doesn't take it.
        /// </summary>
        private static bool LastSeatIsTheShorts(GameState s, Unit u, System.Func<Unit, Reach> walksOf)
        {
            if (!SplitFight(s) || Rules.SplitShort(s) is not List<Unit> behind || behind.Any((o) => o.Id == u.Id)) return false;
            var open = Rules.OpenTiles(s).Select((z) => Grid.NodeOf(s, new Pos { X = z.X, Y = z.Y, Z = 0 })).ToList();
            return open.Count == 1 && behind.Any((o) => !o.Captive && walksOf(o).Cost.ContainsKey(open[0]));
        }

        // ------------------------------------------------------------ Saqueadores (G23.5)

        /// <summary>Saqueadores: the turn the squad goes for La Lata, the stash robbed or not.</summary>
        public const int RaidersLeaveTurn = 16;

        /// <summary>Saqueadores: the squad goes at the camp by force only this many more on its feet than the guards alive.</summary>
        public const int RaidersEdge = 3;

        /// <summary>Saqueadores: till this turn the squad waits for the dead to move off its only hidden way to the stash.</summary>
        public const int RaidersWait = 4;

        /// <summary>What the squad does in Saqueadores (RaidMode): for the stash unseen, for it by force, or La Lata.</summary>
        private enum Raid { Sneak, Loud, Go }

        /// <summary>A run's Saqueadores with its camp (MissionState.Raiders). Without it (no room on the map), a Saqueo (RunSaqueo).</summary>
        private static bool RaidersFight(GameState s) => s.Run != null && s.Mission.Raiders != null && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

        /// <summary>One of the stash's crates on `p`, still to search.</summary>
        private static bool StashCrate(GameState s, Pos p) => s.Mission.Raiders is RaidersCamp camp && p.Z == 0 && camp.Crates.Contains(Grid.NodeOf(s, p)) && Looting.IsSearchable(s, p);

        /// <summary>The stash's crates still to search.</summary>
        private static List<Pos> StashLeft(GameState s) => (s.Mission.Raiders?.Crates ?? new List<int>()).Select((i) => Grid.PosOf(s, i)).Where((p) => Looting.IsSearchable(s, p)).ToList();

        /// <summary>The camp's guards alive (Unit.Guard).</summary>
        private static List<Unit> Guards(GameState s) => s.Units.Where((g) => g.Alive && g.Guard != null).ToList();

        /// <summary>The camp woken: one of its guards is, and so all of them (Sim.CampWakes).</summary>
        private static bool CampAwake(GameState s) => Guards(s).Any((g) => !Paths.OnPatrol(s, g));

        /// <summary>Saqueadores, going for the stash unseen (Raid.Sneak): nothing but by hidden tiles (Sneak), and Quiet.</summary>
        private static bool Hush(GameState s) => RaidersFight(s) && RaidMode(s) == Raid.Sneak;

        /// <summary>
        /// Saqueadores with the camp asleep, not going at it by force: no shot at anyone unaware, nor any its guards would hear
        /// (CampHears), no watch. Going so, with no step left, the squad shot the round's guard six tiles off and woke the camp
        /// (R15: campo, 2 seats, seed 105).
        /// </summary>
        private static bool Quiet(GameState s) => RaidersFight(s) && Guards(s).Count > 0 && !CampAwake(s) && RaidMode(s) != Raid.Loud;

        /// <summary>What a state's order reckons once (RaidMode): the mode and each soldier's hidden walk.</summary>
        private sealed class RaidPlan
        {
            public Raid Mode;
            public readonly Dictionary<string, Reach> Hidden = new Dictionary<string, Reach>();
        }

        [System.ThreadStatic] private static GameState? planOf;
        [System.ThreadStatic] private static int planSeq;
        [System.ThreadStatic] private static RaidPlan? plan;

        private static RaidPlan PlanOf(GameState s)
        {
            if (plan != null && ReferenceEquals(planOf, s) && planSeq == s.Seq) return plan;
            planOf = s;
            planSeq = s.Seq;
            // (ModeOf fills the hidden walks it reckons in it.)
            plan = new RaidPlan();
            plan.Mode = ModeOf(s);
            return plan;
        }

        private static Raid RaidMode(GameState s) => PlanOf(s).Mode;

        /// <summary>
        /// Saqueadores: La Lata with the stash robbed, broken (Routed) or at RaidersLeaveTurn. Else, the camp awake, at it by
        /// force with the guards alive outnumbered by RaidersEdge, or La Lata; asleep, for the stash unseen while one of the
        /// squad has a hidden way to a crate (HiddenSpot), else by force on the same terms, or La Lata («si le ven, pelear
        /// como con los Cuervos y salir»; Gameplay, 20/11).
        /// </summary>
        private static Raid ModeOf(GameState s)
        {
            if (StashLeft(s).Count == 0 || Routed(s) || s.Turn >= RaidersLeaveTurn) return Raid.Go;
            var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
            // Strong: those with a gun to fire (the blade doesn't go at the living), outnumbering the guards alive by RaidersEdge.
            var strong = squad.Count((o) => Armed(s, o)) >= Guards(s).Count + RaidersEdge;
            // The camp awake, or with none of it left (nobody to hide from: it hid from the dead, and they had it, R15).
            if (CampAwake(s) || Guards(s).Count == 0) return strong ? Raid.Loud : Raid.Go;
            // A hidden way, off the dead too; one kept off the camp alone, the dead in it, is waited on till RaidersWait (those
            // asleep stay where they are: R15, the área de servicio of seed 105 waited for two groups six turns, and the
            // patrols found it). The round's guard walks on, and Sneak keeps off him.
            if (squad.Any((o) => Nearer(s, o, HiddenWalk(s, o, true)) != null)) return Raid.Sneak;
            if (s.Turn < RaidersWait && squad.Any((o) => Nearer(s, o, HiddenWalk(s, o, false)) != null)) return Raid.Sneak;
            return strong ? Raid.Loud : Raid.Go;
        }

        /// <summary>Where `u` goes for the stash, by the mode: unseen (HiddenSpot), by force (by its walk past the dead), or nowhere.</summary>
        private static Pos? StashGoal(GameState s, Unit u, System.Func<Unit, Reach> walksOf)
        {
            switch (RaidMode(s))
            {
                case Raid.Sneak: return HiddenSpot(s, u);
                case Raid.Loud: return SpotBeside(s, u, walksOf(u), false);
                default: return null;
            }
        }

        /// <summary>
        /// The nearest place by `u`'s hidden walk (HiddenWalk) beside a crate left, or behind or beside a guard the Golpe
        /// silencioso takes (TakedownSpot): kept off the dead too, if it can be; else kept off the camp alone, and `u` waits for
        /// the dead to move (Sneak). Null with none.
        /// </summary>
        private static Pos? HiddenSpot(GameState s, Unit u) => Nearer(s, u, HiddenWalk(s, u, true)) ?? Nearer(s, u, HiddenWalk(s, u, false));

        /// <summary>The nearer by `walk` of a place to search a crate from quietly (SpotBeside) and one to take a guard down from.</summary>
        private static Pos? Nearer(GameState s, Unit u, Reach walk)
        {
            var crate = SpotBeside(s, u, walk, true);
            var guard = TakedownSpot(s, u, walk);
            if (crate == null || guard == null) return crate ?? guard;
            return walk.Cost.Get(Grid.NodeOf(s, guard), 0) < walk.Cost.Get(Grid.NodeOf(s, crate), 0) ? guard : crate;
        }

        /// <summary>
        /// The nearest place by `walk` from which `u` would take down a guard of the camp still unaware (Abilities.TakenDown:
        /// a Saqueador or a Cazador, never the Cabecilla): beside him, out of his cone proper (Los.AheadOf), free and out of La
        /// Lata. Silent, it leaves one fewer to see the squad, and to shoot at it if the camp wakes. Null with none.
        /// </summary>
        private static Pos? TakedownSpot(GameState s, Unit u, Reach walk)
        {
            Pos? best = null;
            var bestWalk = int.MaxValue;
            if (!Abilities.AbilitiesOf(u).Contains(AbilityIds.Takedown)) return null;
            foreach (var g in Guards(s).Where((g) => Abilities.TakenDown.Contains(g.Template) && Paths.OnPatrol(s, g)))
                foreach (var d in Grid.Dirs8)
                {
                    var q = new Pos { X = g.Pos.X + d.X, Y = g.Pos.Y + d.Y, Z = g.Pos.Z };
                    if (!Grid.InBounds(s, q.X, q.Y) || !walk.Cost.TryGetValue(Grid.NodeOf(s, q), out var w) || w >= bestWalk) continue;
                    if ((Grid.UnitAt(s, q) is Unit o && o != u) || !Flights.CanStopAt(s, q) || Abilities.InEvacZone(s, q)) continue;
                    if (!Aim.InMeleeReach(s, q, g.Pos) || Los.AheadOf(g, q)) continue;
                    best = q;
                    bestWalk = w;
                }
            return best;
        }

        /// <summary>
        /// `u`'s walk by the tiles none of the camp's posted guards would notice it on now (Watch.Fixed), past the dead; with
        /// `dead`, by those none of the dead would notice it on nor bite it going past either (Watch.Off). The round's guard
        /// walks on, and is kept off as he goes (Sneak).
        /// </summary>
        private static Reach HiddenWalk(GameState s, Unit u, bool dead)
        {
            var hidden = PlanOf(s).Hidden;
            var key = dead ? u.Id + "+" : u.Id;
            if (hidden.TryGetValue(key, out var r)) return r;
            var w = Watched(s);
            return hidden[key] = Paths.ComputeReach(s, u, double.MaxValue, null, (o) => o.Team != u.Team, dead ? w.Off : w.Fixed);
        }

        /// <summary>
        /// The nearest place by `walk` beside a crate left (Looting.ReachesToSearch), free and out of La Lata; `quiet` (going
        /// for it unseen), only where searching it gives nobody away (Quietly). Null with none.
        /// </summary>
        private static Pos? SpotBeside(GameState s, Unit u, Reach walk, bool quiet)
        {
            var left = StashLeft(s);
            if (left.Count == 0) return null;
            foreach (var kv in walk.Cost.OrderBy((kv) => kv.Value).ThenBy((kv) => kv.Key))
            {
                var at = Grid.PosOf(s, kv.Key);
                if (at.Z != 0 || (Grid.UnitAt(s, at) is Unit o && o != u) || !Flights.CanStopAt(s, at) || Abilities.InEvacZone(s, at)) continue;
                if (left.Any((c) => Looting.ReachesToSearch(s, at, c) && (!quiet || Quietly(s, u, c, at)))) return at;
            }
            return null;
        }

        /// <summary>Whether a guard of the camp still unaware would hear `u` use `ability` where it stands (Rules.SquadNoiseRadius, as Sim.MakeNoise).</summary>
        private static bool CampHears(GameState s, Unit u, string ability)
        {
            var radius = Rules.SquadNoiseRadius(s, Abilities.NoiseOf(s, u, ability), u);
            return radius > 0 && Guards(s).Any((g) => Paths.OnPatrol(s, g) && Grid.Distance(g.Pos, u.Pos) + Grid.StoreysApart(s, g.Pos, u.Pos) <= radius);
        }

        /// <summary>
        /// Whether `u` searching `crate` from `at` (null: where it is) gives the squad away: whoever not woken hears it turns to
        /// it (Events, NoiseEvent), and a guard by the fire is up from his chair (Rules.Sitting: no longer only Rules.FireWatch),
        /// and notices whoever of the squad is then in his view as far as his detection goes (R15: 7 hidden squads of 60 were
        /// found so, at the noise of their own crate, by the guard with his back to it).
        /// </summary>
        private static bool Quietly(GameState s, Unit u, Pos crate, Pos? at = null)
        {
            var radius = Rules.SquadNoiseRadius(s, Abilities.NoiseOf(s, u, AbilityIds.Search), u);
            var hear = s.Units.Where((o) => o.Alive && o.Team != Teams.Xcom && Paths.OnPatrol(s, o) && Grid.Distance(o.Pos, crate) + Grid.StoreysApart(s, o.Pos, crate) <= radius).ToList();
            if (hear.Count == 0) return true;
            var spots = Squad(s).Where((o) => !o.Evacuated && o.Id != u.Id).Select((o) => o.Pos).Append(at ?? u.Pos).ToList();
            foreach (var g in hear)
            {
                var facing = Los.FacingTo(g.Pos, crate) ?? g.Facing;
                foreach (var p in spots)
                {
                    var range = s.Concealed ? Los.DetectionTo(s, g, p) : Los.SightTo(s, g, p);
                    if (Los.InNoticeCone(s, g, p, g.Pos, facing) && Los.SightOrigin(s, g.Pos, p, range, Los.WindowReachOf(g.Team)) != null) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The tiles one of the squad would be noticed on (Sim.PodsNoticing: what the rules check on each step of a walk and
        /// each guard on watch at its turn): by a posted guard of the camp (Fixed); by the dead not woken, or beside one of
        /// the dead, who bites whoever goes past (Rules.PassBite: R15, a zombie asleep bit the one going by and gave the squad
        /// away) (Dead); by the round's guard where he stands (Round); and by whoever walks in the enemies' next two turns, on
        /// its walk, or all round if it stands about (Ahead, WalkAhead).
        /// </summary>
        private sealed class Watch
        {
            public readonly HashSet<int> Fixed = new HashSet<int>(), Dead = new HashSet<int>(), Round = new HashSet<int>(), Ahead = new HashSet<int>();

            /// <summary>Fixed and Dead, and the shut doors and windows: what a hidden way keeps off.</summary>
            public readonly HashSet<int> Off = new HashSet<int>();
        }

        [System.ThreadStatic] private static GameState? watchOf;
        [System.ThreadStatic] private static string? watchKey;
        [System.ThreadStatic] private static Watch? watch;

        /// <summary>
        /// Watch, reckoned once while nothing it hangs on changes: in the squad's turn the enemies stand still, but for those
        /// woken (they scamper), those killed, and the squad found.
        /// </summary>
        private static Watch Watched(GameState s)
        {
            var key = $"{s.Turn}|{s.ActiveTeam}|{s.Concealed}|{s.Pods.Count}|{s.Pods.Count((p) => p.Active)}|{s.Units.Count((x) => x.Alive)}|{s.Units.Count((x) => x.Alive && x.Heard != null)}";
            if (watch != null && ReferenceEquals(watchOf, s) && watchKey == key) return watch;
            watchOf = s;
            watchKey = key;
            var w = watch = new Watch();
            var who = Squad(s).Where((o) => !o.Evacuated).OrderBy((o) => o.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (who == null) return w;
            var eyes = s.Units.Where((e) => e.Alive && Paths.OnPatrol(s, e)).ToList();
            var round = Guards(s).FirstOrDefault((g) => g.Guard == GuardPosts.Round && Paths.OnPatrol(s, g));
            int Reach(Unit e) => (int)System.Math.Ceiling(System.Math.Max(Los.AwareRange, s.Concealed ? Content.Templates[e.Template].Detection : Content.Templates[e.Template].Sight)) + 1;
            var tiles = new HashSet<int>();
            var levels = s.Levels?.Count ?? 0;
            foreach (var e in eyes)
            {
                var r = Reach(e);
                for (var z = 0; z <= levels; z++)
                    for (var y = System.Math.Max(0, e.Pos.Y - r); y <= System.Math.Min(s.Height - 1, e.Pos.Y + r); y++)
                        for (var x = System.Math.Max(0, e.Pos.X - r); x <= System.Math.Min(s.Width - 1, e.Pos.X + r); x++)
                            tiles.Add(Grid.NodeOf(s, new Pos { X = x, Y = y, Z = z }));
            }
            var sim = new Sim(s, new Rng(0), new List<int>());
            var camp = new HashSet<string>(Guards(s).Where((g) => g.PodId != null).Select((g) => g.PodId!));
            foreach (var n in tiles)
            {
                var p = Grid.PosOf(s, n);
                if (!Flights.CanStopAt(s, p) && !Grid.CanStandOn(Grid.KindAt(s, p))) continue;
                var pods = sim.PodsNoticing(p, who);
                if (pods.Any((id) => camp.Contains(id) && id != round?.PodId)) w.Fixed.Add(n);
                if (pods.Any((id) => !camp.Contains(id))) w.Dead.Add(n);
                if (round != null && pods.Contains(round.PodId!)) w.Round.Add(n);
            }
            foreach (var o in s.Units.Where((o) => o.Alive && o.Team != Teams.Xcom && Content.Templates[o.Template].Undead))
                foreach (var d in Grid.Dirs8)
                {
                    var p = new Pos { X = o.Pos.X + d.X, Y = o.Pos.Y + d.Y, Z = o.Pos.Z };
                    if (Grid.InBounds(s, p.X, p.Y) && Rules.PassBite(s, o, who, p)) w.Dead.Add(Grid.NodeOf(s, p));
                }
            w.Off.UnionWith(w.Fixed);
            w.Off.UnionWith(w.Dead);
            // A shut door or a window gone through is heard (Content.Noise.Door, Breach: six tiles and more), and whoever
            // hears it comes to look (R15: the camping's camp, its guards up from their chairs).
            for (var z = 0; z <= levels; z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var p = new Pos { X = x, Y = y, Z = z };
                        if (Grid.KindAt(s, p) is TileKinds.Door or TileKinds.Window) w.Off.Add(Grid.NodeOf(s, p));
                    }
            // Whoever walks in the enemies' next turns, and what each would notice on the way (Ahead): the round's guard round the
            // fence; one gone to look where it heard something (Ai.PatrolPod); the dead on their rounds; the dead standing
            // about, who turn their heads (Ai.LookRound), all round. Of the dead, only those near enough to the squad to matter.
            // (R15: with the round's guard alone, of 60 hidden squads the dead on their rounds found 25.)
            var squadAt = Squad(s).Where((o) => !o.Evacuated).Select((o) => o.Pos).ToList();
            foreach (var e in eyes)
            {
                var pod = s.Pods.Find((p) => p.Id == e.PodId);
                if (pod == null) continue;
                var near = squadAt.Any((p) => Grid.Distance(p, e.Pos) <= 2.0 * Paths.FullMoves(e) / Paths.CostOrtho + Reach(e) + 2);
                if (!near) continue;
                var r = Reach(e);
                List<(Pos At, double? Facing)> walk;
                // Two turns' walk: with one, the squad stopped nearer, and the dead on their rounds found it more often (R15: 21
                // hidden squads of 60 against 16).
                var budget = 2 * Paths.FullMoves(e);
                if (e.Heard != null) walk = WalkAhead(s, e, new List<Pos> { Grid.ToPos(e.Heard) }, 0, budget);
                else if (e.Guard is GuardPosts.Fire or GuardPosts.Stash) continue;
                else walk = WalkAhead(s, e, pod.Patrol, pod.PatrolIndex, budget);
                // Nowhere to walk (no round, or at its one stop): it stands about and turns its head any way (Ai.LookRound).
                if (walk.Count == 0)
                {
                    for (var y = System.Math.Max(0, e.Pos.Y - r); y <= System.Math.Min(s.Height - 1, e.Pos.Y + r); y++)
                        for (var x = System.Math.Max(0, e.Pos.X - r); x <= System.Math.Min(s.Width - 1, e.Pos.X + r); x++)
                        {
                            var p = new Pos { X = x, Y = y, Z = e.Pos.Z };
                            var range = s.Concealed ? Los.DetectionTo(s, e, p) : Los.SightTo(s, e, p);
                            if (Los.SightOrigin(s, e.Pos, p, range, Los.WindowReachOf(e.Team)) != null) w.Ahead.Add(Grid.NodeOf(s, p));
                        }
                    continue;
                }
                var seen = new HashSet<(int, double?)>();
                foreach (var (at, facing) in walk)
                {
                    if (!seen.Add((Grid.NodeOf(s, at), facing))) continue;
                    for (var y = System.Math.Max(0, at.Y - r); y <= System.Math.Min(s.Height - 1, at.Y + r); y++)
                        for (var x = System.Math.Max(0, at.X - r); x <= System.Math.Min(s.Width - 1, at.X + r); x++)
                        {
                            var p = new Pos { X = x, Y = y, Z = at.Z };
                            var n = Grid.NodeOf(s, p);
                            if (w.Ahead.Contains(n) || !Los.InNoticeCone(s, e, p, at, facing)) continue;
                            var range = s.Concealed ? Los.DetectionTo(s, e, p) : Los.SightTo(s, e, p);
                            if (Los.SightOrigin(s, at, p, range, Los.WindowReachOf(e.Team)) != null) w.Ahead.Add(n);
                        }
                }
            }
            return w;
        }

        /// <summary>
        /// Where `g` walks within `budget` (Ai.PatrolPod: to the stop it goes for, `stops` from `first` on, the next once within
        /// 2 of it): every place on any of the shortest ways there (the rules' own picks one of them, and R15's first guess at
        /// which was wrong: the camping's round went south first, not east, and saw the squad), each with the ways it may face
        /// walking onto it.
        /// </summary>
        private static List<(Pos At, double? Facing)> WalkAhead(GameState s, Unit g, IReadOnlyList<Pos> stops, int first, double budget)
        {
            var steps = new List<(Pos, double?)>();
            var n = stops.Count;
            if (n == 0) return steps;
            var at = new List<Pos> { g.Pos };
            var index = first % n;
            for (var leg = 0; leg <= n && budget >= Paths.CostOrtho && at.Count > 0; leg++)
            {
                var corner = Grid.ToPos(stops[index]);
                index = (index + 1) % n;
                if (at.Any((p) => Grid.Distance(p, corner) <= 2)) continue;
                var from = at.OrderBy((p) => Grid.Distance(p, corner)).First();
                var there = Paths.ComputeReach(s, g, double.MaxValue, from, (o) => true);
                var back = Paths.ComputeReach(s, g, double.MaxValue, corner, (o) => true);
                if (!there.Cost.TryGetValue(Grid.NodeOf(s, corner), out var total)) break;
                var ends = new List<Pos>();
                var spent = 0;
                foreach (var kv in there.Cost)
                {
                    if (kv.Value == 0 || kv.Value > budget || !back.Cost.TryGetValue(kv.Key, out var left) || kv.Value + left != total) continue;
                    var p = Grid.PosOf(s, kv.Key);
                    // Each way he may come onto it: from a place before it on a shortest way.
                    foreach (var d in Grid.Dirs4)
                    {
                        var q = new Pos { X = p.X - d.X, Y = p.Y - d.Y, Z = p.Z };
                        if (Grid.InBounds(s, q.X, q.Y) && there.Cost.TryGetValue(Grid.NodeOf(s, q), out var c) && c < kv.Value && back.Cost.TryGetValue(Grid.NodeOf(s, q), out var l) && c + l == total)
                            steps.Add((p, Los.FacingTo(q, p)));
                    }
                    if (kv.Value > spent) { spent = kv.Value; ends.Clear(); }
                    if (kv.Value == spent) ends.Add(p);
                }
                budget -= spent;
                at = ends;
                if (spent == 0) break;
            }
            return steps;
        }

        /// <summary>
        /// Saqueadores hidden: a stretch toward `goal` by the hidden walk, along which the rules' walk (Paths.BuildPath) steps
        /// on no tile anyone not woken would notice it on now nor beside one of the dead (Watch.Off, Round), to a place the
        /// round's guard won't see on his walk either (Ahead); off a place he will, wherever is hidden. `bus`: `goal` is La
        /// Lata, any of whose places is as good, and inside it nobody sees it. Null: none nearer by half a tile (the dead in
        /// the way: it waits for them to move).
        /// </summary>
        private static Command? Sneak(GameState s, Unit u, Pos goal, bool bus)
        {
            var stride = Stride(s, u);
            if (stride < Paths.CostOrtho) return null;
            var w = Watched(s);
            bool Seen(int n) => w.Off.Contains(n) || w.Round.Contains(n);
            bool Unsafe(int n) => Seen(n) || w.Ahead.Contains(n);
            var back = Paths.ComputeReach(s, u, double.MaxValue, goal, (o) => o.Team != u.Team, w.Off);
            const double Off = 1e9;
            double Left(Pos p) => bus && Abilities.InEvacZone(s, p) ? 0 : back.Cost.TryGetValue(Grid.NodeOf(s, p), out var c) ? c : Off;
            var stay = Left(u.Pos) + (Unsafe(Grid.NodeOf(s, u.Pos)) ? 2 * Off : 0);
            var reach = Paths.ComputeReach(s, u, stride);
            var stops = Stops(s, u, reach)
                .Where((o) => (bus ? Abilities.InEvacZone(s, o.Pos) || !Unsafe(Grid.NodeOf(s, o.Pos)) : !Unsafe(Grid.NodeOf(s, o.Pos)) && !Abilities.InEvacZone(s, o.Pos)) && Left(o.Pos) < stay - Paths.CostOrtho / 2.0)
                .OrderBy((o) => Left(o.Pos)).ThenBy((o) => o.Cost).ThenBy((o) => Grid.NodeOf(s, o.Pos));
            foreach (var o in stops)
            {
                var path = Paths.BuildPath(s, u, reach, o.Pos);
                if (path != null && !path.Any((p) => Seen(Grid.NodeOf(s, p)))) return new MoveCommand { Unit = u.Id, To = o.Pos };
            }
            return null;
        }

        // ------------------------------------------------------------ Evacuación (G23.6)

        /// <summary>Evacuación: the turn the squad goes for La Lata, with the civilians aboard or not.</summary>
        public const int EvacLeaveTurn = 20;

        /// <summary>A run's Evacuación with its civilians (MissionState.Civilians). Without them (too few buildings), a Saqueo (RunSaqueo).</summary>
        private static bool EvacFight(GameState s) => s.Run != null && s.Mission.Civilians != null && s.Mission.Kind == MissionKinds.Scavenge && s.Mission.EvacZone.Count > 0;

        /// <summary>Evacuación: each civilian alive and not aboard, with their spot (hidden, with its circle, or found).</summary>
        private static List<(Unit Unit, CivilianSpot Spot)> Civilians(GameState s) => (s.Mission.Civilians ?? new List<CivilianSpot>())
            .Select((c) => (Unit: s.Units.FirstOrDefault((u) => u.Id == c.Unit), Spot: c)).Where((c) => c.Unit != null && c.Unit.Alive && c.Unit.Aboard != true && !c.Unit.Evacuated)
            .Select((c) => (c.Unit!, c.Spot)).ToList();

        /// <summary>Evacuación: the civilians found and walking to La Lata (Sim.CivilianStep), alive and not aboard.</summary>
        private static List<Unit> Walkers(GameState s) => Civilians(s).Where((c) => c.Spot.Found).Select((c) => c.Unit).ToList();

        /// <summary>
        /// Evacuación: the squad goes broken or hurt (EvacBroken: one of it fallen, or half of it at half health; staying on,
        /// it fell in half the fights with four seats, R17), at EvacLeaveTurn, with two civilians aboard (Rules.CiviliansSafe: the
        /// reward; Gameplay, 21/11: «pedir salir con dos o más»), or with no more to get aboard (Rules.CiviliansShort null: too
        /// few left alive).
        /// </summary>
        private static bool EvacLeaving(GameState s) => EvacBroken(s) || s.Turn >= EvacLeaveTurn || Rules.CiviliansSafe(s) || Rules.CiviliansShort(s) == null;

        /// <summary>
        /// Evacuación: the squad broken or hurt, those in La Lata counted (Routed and Hurt count those out of it only, and with
        /// three in and one out it was «broken»): one of it fallen, one or none left, or half of it at half health or less.
        /// </summary>
        private static bool EvacBroken(GameState s)
        {
            var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && !Rules.IsCivilian(u)).ToList();
            if (squad.Count((u) => !u.Alive && u.Aboard != true && !u.Evacuated) >= KeyFallen) return true;
            var up = squad.Where((u) => u.Alive || u.Aboard == true || u.Evacuated).ToList();
            return up.Count <= 1 || up.Count((u) => u.Hp * 2 <= u.MaxHp) * 2 >= up.Count;
        }

        /// <summary>
        /// Evacuación: La Lata going now would leave a civilian found and walking to it (they get in by themselves, a step a
        /// turn) short of two aboard, and the squad isn't going for good (EvacLeaving, but for those two). Only while one of
        /// them has a way to it (Rules.CivilianWalk).
        /// </summary>
        private static bool EvacWaits(GameState s)
        {
            if (!EvacFight(s) || EvacBroken(s) || s.Turn >= EvacLeaveTurn || Rules.CiviliansSafe(s)) return false;
            return Walkers(s).Any((c) => Rules.CivilianWalk(s, c) != null);
        }

        /// <summary>Evacuación: how far ahead of a civilian walking to La Lata the squad goes, along their way (they walk 5 a turn).</summary>
        public const int EscortAhead = 7;

        /// <summary>
        /// Where the squad escorts civilian `c` walking to La Lata from: EscortAhead tiles on along their way there (La Lata's
        /// open tile nearest them by the walk, the squad gone round), clearing it and out of this turn's walk. Round them, the
        /// squad stood in their way, and they never moved (R17: the town of seed 101, the four of it fell waiting).
        /// </summary>
        private static Pos? EscortSpot(GameState s, Unit c)
        {
            var way = CivilianWay(s, c);
            // Near La Lata, nobody in their way: at its door the squad kept one 3 tiles off for 10 turns (R17, the mall).
            return way.Count <= EscortAhead + (int)Content.CivilianMobility ? null : Grid.PosOf(s, way[EscortAhead]);
        }

        /// <summary>Civilian `c`'s way to La Lata's open tile nearest them by the walk, the squad gone round (empty: none, or there).</summary>
        private static List<int> CivilianWay(GameState s, Unit c)
        {
            var reach = Paths.ComputeReach(s, c, double.MaxValue, null, (o) => o.Team == Teams.Xcom && !Rules.IsCivilian(o));
            var end = WayTo(s, reach, s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).FirstOrDefault() ?? c.Pos, true);
            return end == null ? new List<int>() : Way(s, reach, end);
        }

        /// <summary>
        /// Where `u` goes in Evacuación, the same civilian for all of the squad, which keeps together (each its own nearest,
        /// it went four ways and fell, R17): one found walking to La Lata first, the nearest to the squad, alongside them till
        /// they are in (the dead near them shot first: Choose); with none, the one still hidden nearest La Lata, by their
        /// circle's middle, and within it where they are (there one sees them). Null with neither.
        /// </summary>
        private static Pos? EvacGoal(GameState s, Unit u, Reach walks)
        {
            var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
            double Near(Pos p) => squad.Count == 0 ? 0 : squad.Min((o) => Grid.Distance(o.Pos, p));
            var escort = Walkers(s).OrderBy((c) => Near(c.Pos)).ThenBy((c) => c.Id, System.StringComparer.Ordinal).Select((c) => EscortSpot(s, c)).FirstOrDefault((p) => p != null);
            if (escort != null) return escort;
            var bus = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).ToList();
            double FromBus(Pos p) => bus.Count == 0 ? 0 : bus.Min((z) => Grid.Distance(z, p));
            Pos Middle((Unit Unit, CivilianSpot Spot) c) => c.Spot.Area is SearchArea a ? new Pos { X = (int)a.Center.X, Y = (int)a.Center.Y, Z = 0 } : c.Unit.Pos;
            var hidden = Civilians(s).Where((c) => !c.Spot.Found).OrderBy((c) => FromBus(Middle(c))).ThenBy((c) => c.Unit.Id, System.StringComparer.Ordinal).FirstOrDefault();
            // Nobody hidden, and those walking near La Lata: where it stands, out of their way.
            if (hidden.Unit == null) return null;
            var middle = Middle(hidden);
            var radius = hidden.Spot.Area?.Radius ?? 0;
            return Grid.Distance(u.Pos, middle) <= radius ? hidden.Unit.Pos : middle;
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
                    var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
                    Pos? Nearest(IEnumerable<Pos> spots) => spots.Select((p) => (At: p, Walk: squad.Select((o) => WalkTo(s, o.Id == u.Id ? walks : Paths.ComputeReach(s, o, double.MaxValue, null, (x) => x.Team != o.Team), p)).DefaultIfEmpty(int.MaxValue).Min()))
                        .Where((x) => x.Walk < int.MaxValue).OrderBy((x) => x.Walk).ThenBy((x) => Grid.NodeOf(s, x.At)).Select((x) => (Pos?)x.At).FirstOrDefault();
                    return Nearest(s.Units.Where((b) => !b.Alive && b.Team == Teams.Alien && InUniform(b) && UniformBody(s, b.Pos)).Select((b) => b.Pos))
                        ?? Nearest(Foes(s).Where((e) => e.U.Caged != true && InUniform(e.U)).Select((e) => e.At));
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
            var rest = Squad(s).Where((o) => o.Id != u.Id && !o.Evacuated).Select((o) => WalkTo(s, walksOf(o), goal)).Where((w) => w < int.MaxValue).ToList();
            return rest.Count == 0 ? 0 : rest.Max() - mine;
        }

        /// <summary>La llave: a body in uniform still to search this near (tiles of walk) to one of the squad is gone for, hurt or not.</summary>
        public const int KeyGrab = 6;

        private static bool UniformNear(GameState s, System.Func<Unit, Reach> walksOf)
        {
            var bodies = s.Units.Where((b) => !b.Alive && b.Team == Teams.Alien && InUniform(b) && UniformBody(s, b.Pos)).Select((b) => b.Pos).ToList();
            return bodies.Count > 0 && Squad(s).Where((o) => !o.Evacuated).Any((o) => bodies.Any((p) => WalkTo(s, walksOf(o), p) <= KeyGrab * Paths.CostOrtho));
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
            var squad = Squad(s).Where((o) => !o.Evacuated).ToList();
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
        /// on (a radio, a car's boot) or one of the other side stands on it (the walks go past them), a place to stop on; for
        /// La Lata, its place nearest by the walk. Null if none can be walked to.
        /// </summary>
        /// <remarks>
        /// To one of the dead's own place, the way through a window ended on it: nowhere on it to stop, and the recruit alone
        /// with the blade stood under the window for ever, in a Limpieza of 40 turns (Gameplay's R2 of 21/11, seed 393). So
        /// too to a Rescate's survivor behind a window, the way's end the window itself (Red's R2 by level, 23/11: hard, 2
        /// seats, seed 39, 41 turns crouched two tiles off): beside the goal, only a place to stop on, whoever stands on it.
        /// </remarks>
        private static Pos? WayTo(GameState s, Reach walks, Pos goal, bool bus)
        {
            var taken = Grid.UnitAt(s, goal) is Unit there && there.Team != Teams.Xcom;
            var ends = bus ? Rules.OpenTiles(s).Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 })
                : !taken && walks.Cost.TryGetValue(Grid.NodeOf(s, goal), out _) ? new[] { goal }
                : Grid.Dirs8.Select((d) => new Pos { X = goal.X + d.X, Y = goal.Y + d.Y, Z = goal.Z }).Where((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p));
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

        /// <summary>The likeliest attack `u` has now (rifle, pistol or the blade) at an enemy `within` tiles (only `at`, or `which`, if given), and how likely.</summary>
        private static (Command cmd, int hit)? BestShot(GameState s, Unit u, double within, string? at = null, System.Func<Unit, bool>? which = null)
        {
            (Command cmd, int hit)? best = null;
            var abilities = Abilities.AbilitiesOf(u);
            foreach (var a in new[] { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash })
            {
                if (!abilities.Contains(a) || !Usable(s, u, a)) continue;
                // Saqueadores with the camp asleep (Quiet): nothing its guards would hear (Gameplay, 21/11: of 149 camps woken
                // as the squad went, in 112 the horde's trickle had found it first, it shot near the camp, and the guards by the
                // fire, up at the noise, saw it). The blade is heard a tile off.
                if (Quiet(s) && CampHears(s, u, a)) continue;
                foreach (var t in Abilities.AbilityTargets(s, u, a))
                {
                    if (Grid.Distance(u.Pos, t.Pos) > within || (at != null && t.Id != at) || (which != null && !which(t)) || !Worth(s, u, t)) continue;
                    var p = Aim.PreviewShot(s, u, t, new ShotOptions { Ability = a });
                    // One unaware while the camp sleeps (Quiet): only a silent kill (SilentShot).
                    if (p != null && Quiet(s) && Paths.OnPatrol(s, t) && !SilentShot(s, u, t, a, p)) continue;
                    if (p != null && (best == null || p.Hit > best.Value.hit)) best = (Ability(u, a, t.Id), p.Hit);
                }
            }
            return best;
        }

        private static bool Usable(GameState s, Unit u, string ability) => Abilities.AbilitiesOf(u).Contains(ability) && Abilities.AbilityBlocker(s, u, ability) == null;

        private static Command Ability(Unit u, string ability, string? target = null) => new AbilityCommand { Unit = u.Id, Ability = ability, Target = target };
    }
}
