using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    public sealed partial class CombatController
    {
        /// <summary>
        /// Moves planned without the command (UX-REVISION-2 #35; playtest 18/10 #3), in the order they were planned (#24: a
        /// changed one counts from when it changed): whose, and where to. The partner's come by the network.
        /// </summary>
        readonly List<(string unit, int slot, Pos tile)> plans = new List<(string, int, Pos)>();

        /// <summary>One's routes walked when the command comes (Ajustes' «Seguir las rutas planeadas»), kept on this computer.</summary>
        public static bool AutoPlan
        {
            get => PlayerPrefs.GetInt("bastion.autoPlan", 1) == 1;
            set => PlayerPrefs.SetInt("bastion.autoPlan", value ? 1 : 0);
        }

        /// <summary>Walking one's routes now, the one ordered last (if it is still there next time, the engine refused it), and those passed over.</summary>
        bool following;
        string planSent;
        readonly HashSet<string> planSkip = new HashSet<string>();
        float lastPing = -10;

        /// <summary>Plans or withdraws `u`'s move to `tile` (the same tile again takes it away); the partner sees it.</summary>
        void TogglePlan(Unit u, Pos tile)
        {
            var current = plans.FindIndex(p => p.unit == u.Id);
            if (current >= 0 && Same(plans[current].tile, tile))
            {
                WithdrawPlan(u);
                return;
            }
            if (current >= 0) plans.RemoveAt(current);
            plans.Add((u.Id, MySlot, tile));
            Send(new PlanMessage { Unit = u.Id, Tile = tile });
            DrawPlans();
            Toast($"Plan para {u.Name}: lo ve {PlayerName(State.Command)}. Otro clic ahí lo quita.");
        }

        void WithdrawPlan(Unit u)
        {
            plans.RemoveAll(p => p.unit == u.Id);
            Send(new PlanMessage { Unit = u.Id, Tile = null });
            DrawPlans();
            Toast("Plan retirado.");
        }

        /// <summary>The partner planned a move (or withdrew one, `tile` null): it counts from now (#24); the log says so.</summary>
        public void PartnerPlan(int slot, string unit, Pos tile)
        {
            if (slot == MySlot) return;
            plans.RemoveAll(p => p.unit == unit);
            if (tile != null) plans.Add((unit, slot, tile));
            DrawPlans();
            var u = UnitById(State, unit);
            if (tile != null && u != null) Hud?.Log($"{PlayerName(slot)} planea mover a {u.Name}", PlayerColor(slot));
        }

        /// <summary>A unit's plan gone (it moved, fell or left in the bus).</summary>
        public void DropPlan(string unit)
        {
            if (plans.RemoveAll(p => p.unit == unit) > 0) DrawPlans();
            if (planSent != null && planSent.StartsWith(unit + ":")) planSent = null;
        }

        /// <summary>
        /// A turn begins: the partner's plans go, and one's own too unless the command is this player's; then they are
        /// walked once the turn has been seen (FollowPlans).
        /// </summary>
        public void PlansAtTurn(bool squad, bool mine)
        {
            plans.RemoveAll(p => p.slot != MySlot || (squad && !mine));
            following = false;
            DrawPlans();
        }

        /// <summary>
        /// Draws every plan along the path it would take now; one it no longer reaches keeps its ring alone (18/10 #3).
        /// One's own while the partner holds the command, in the planning blue; theirs, in their colour.
        /// </summary>
        void DrawPlans()
        {
            if (AimMarks == null || State == null) return;
            var list = new List<(Pos, IList<Pos>, Color32)>();
            foreach (var (id, slot, tile) in plans.ToList())
            {
                var u = UnitById(State, id);
                if (u == null || !u.Alive)
                {
                    plans.RemoveAll(p => p.unit == id);
                    continue;
                }
                var colour = slot == MySlot && !HasCommand() ? MoveOverlay.Hex(0x7fb2d4) : ColorUtility.TryParseHtmlString(PlayerColor(slot), out var c) ? (Color32)c : MoveOverlay.Hex(0xffffff);
                var reach = Paths.ComputeReach(State, u, Paths.MaxMoveCost(u));
                var path = reach.Cost.Has(Grid.NodeOf(State, tile)) ? Paths.BuildPath(State, u, reach, tile) : null;
                list.Add(path != null && path.Count > 0 ? (u.Pos, (IList<Pos>)path, colour) : (tile, new List<Pos> { tile }, colour));
            }
            AimMarks.ShowPlans(State, list);
        }

        /// <summary>The command has come to this player (its turn, or passed to it): one's routes are walked, from the first planned.</summary>
        public void StartFollowing()
        {
            following = true;
            planSent = null;
            planSkip.Clear();
        }

        /// <summary>
        /// With the command (playtest 18/10 #3), one's planned routes walked in the order they were planned (#24), each an
        /// ordinary move the engine checks as any other, the next once that one has been seen whole (the event player calls
        /// this when it is done). One soldier at a time still (15/10 #9). One that no longer reaches its tile keeps its plan,
        /// said once, and the next goes.
        /// </summary>
        public void FollowPlans()
        {
            if (!following) return;
            if (!AutoPlan || !HasCommand())
            {
                following = false;
                return;
            }
            if (!CanAct()) return;
            foreach (var (id, slot, tile) in plans.ToList())
            {
                if (slot != MySlot) continue;
                var u = UnitById(State, id);
                if (u == null || !u.Alive || u.Ap <= 0) continue;
                var key = $"{id}:{tile.X},{tile.Y},{tile.Z}";
                if (planSkip.Contains(key)) continue;
                // Another of one's own still in play: this one waits, as it is, and so do those after it.
                if (WaitingFor(u.Id) != null) return;
                // Ordered last time and still here: the engine refused it. Passed over, as one that doesn't reach.
                var refused = planSent == key;
                var reach = Paths.ComputeReach(State, u, Paths.MaxMoveCost(u));
                if (refused || !reach.Cost.Has(Grid.NodeOf(State, tile)))
                {
                    planSkip.Add(key);
                    var line = Lore.PlanBlockedLine(u.Name);
                    Hud?.Log(line, PlayerColor(MySlot));
                    Toast(line);
                    DrawPlans();
                    continue;
                }
                if (picked != u.Id) Pick(u.Id);
                Hud?.Log(Lore.PlanFollowedLine(u.Name), PlayerColor(MySlot));
                planSent = key;
                Send(new MoveCommand { Unit = u.Id, To = tile });
                return;
            }
            following = false;
        }

        /// <summary>G: the tile under the pointer marked for both (playtest 18/10 #7), an enemy's in red; at most a few a second.</summary>
        void Ping()
        {
            if (hover == null || Time.unscaledTime - lastPing < 0.4f) return;
            lastPing = Time.unscaledTime;
            var occupant = Grid.UnitAt(State, hover);
            Send(new PingMessage { Tile = hover, Kind = occupant != null && occupant.Team == Teams.Alien && Visible(occupant) ? "enemy" : "point" });
        }

        /// <summary>A ping heard (one's own too, which the host sends back): its beam and rings, whose it is over it, and the partner's in the log.</summary>
        public void OnPing(int slot, Pos tile, string kind)
        {
            if (State == null) return;
            var at = Board.TileToWorld(Shown, tile);
            var colour = PlayerColor(slot);
            var enemy = kind == "enemy";
            AimMarks?.Ping(at, enemy ? MoveOverlay.Hex(0xff4a4a) : ColorUtility.TryParseHtmlString(colour, out var c) ? (Color32)c : MoveOverlay.Hex(0xffffff));
            Sfx.Mark(at);
            Hud?.Pin(Board.TileToWorld(Shown, tile, 2.9f * Board.Tile), PlayerName(slot), colour);
            if (slot != MySlot) Hud?.Log($"{PlayerName(slot)} marca {(enemy ? "un enemigo" : "una posición")}", colour);
        }
    }
}
