using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// A siege on the screen (docs/FASE12.md §10; game.ts refreshView, marks, breaches, breakables, noteBreach): the compass
    /// with the next wave's sides in red while the Sala de radio stands; each breach (a door or a barricade at 0, seen or
    /// not) an orange beam on the board and an arrow at the screen's edge, one a side; the doors and barricades that have
    /// taken blows a bar each, where the squad sees them, and what they hold under the pointer. The fallen's rucksacks a
    /// tag each, in any mission.
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>The breaches' beams (game.ts: 0xff8a2a).</summary>
        const int BreachColor = 0xff8a2a;

        /// <summary>The side of the map a tile is nearest (game.ts sideOf): a SIDES index, north 0, east 1, south 2, west 3.</summary>
        internal static int SideOf(GameState s, Pos p)
        {
            var gaps = new[] { p.Y, s.Width - 1 - p.X, s.Height - 1 - p.Y, p.X };
            return System.Array.IndexOf(gaps, gaps.Min());
        }

        /// <summary>A siege's door or barricade at `p` (game.ts breakableOf): which it is, and what it holds whole.</summary>
        internal static (string kind, int full) BreakableOf(GameState s, Pos p)
        {
            var k = Grid.KindAt(s, p);
            return k == "door" || k == "doorOpen" ? ("door", Content.Siege.Door) : ("barricade", Content.Siege.Barricade);
        }

        /// <summary>A siege's doors and barricades that gave way (GameState.breakables at 0), seen or not: a breach is what the player must know.</summary>
        static List<Pos> Breaches(GameState s) =>
            (s.Breakables ?? new Dictionary<int, int>()).Where(b => b.Value <= 0).Select(b => Grid.PosOf(s, b.Key)).ToList();

        /// <summary>The compass's next wave (game.ts: the sides of the next wave not in yet), only with the Sala de radio standing.</summary>
        CompassView CompassOf(GameState v)
        {
            if (v.Mission.Siege == null) return new CompassView { Shown = false };
            var view = new CompassView { Shown = true };
            var radio = (v.Base?.Pieces ?? new List<BasePiece>()).Any(p => p.Piece == "radio" && Bastion.Engine.Campaign.BuildingWorks(p));
            var coming = v.Mission.Reinforcements.Where(r => !r.Landed && r.Side != null).ToList();
            if (radio && coming.Count > 0)
            {
                var next = coming.Min(r => r.Turn);
                view.Hot.AddRange(coming.Where(r => r.Turn == next).Select(r => r.Side.Value).Distinct().OrderBy(x => x));
            }
            return view;
        }

        /// <summary>The breaches' beams, added to the mission's; and their marks at the screen's edge, one a side, at their middle.</summary>
        void BreachMarks(GameState v, List<(Pos, int)> beacons, EdgeMarksView edges)
        {
            var breaches = Breaches(v);
            foreach (var p in breaches) beacons.Add((p, BreachColor));
            if (edges == null) return;
            foreach (var side in breaches.GroupBy(p => SideOf(v, p)))
            {
                var at = new Pos { X = Mathf.RoundToInt((float)side.Average(p => p.X)), Y = Mathf.RoundToInt((float)side.Average(p => p.Y)) };
                var label = Lore.BreachMark(side.Key);
                edges.Marks.Add(new EdgeMarkView { Kind = "alert", Name = label, Label = label, World = Board.TileToWorld(v, at, Board.Tile) });
            }
        }

        /// <summary>
        /// The tags over the map: a bar over each door or barricade that has taken blows and that the squad sees (game.ts
        /// breakables; red at a third), and «MOCHILA DE CHIARA» over each fallen soldier's rucksack, once a tile and soldier.
        /// </summary>
        List<WorldTagView> WorldTags(GameState v)
        {
            var list = new List<WorldTagView>();
            // The loot boxes' marks (CombatController.Loot).
            LootMarks(v, list);
            var hurt = (v.Breakables ?? new Dictionary<int, int>()).Where(b => b.Value > 0).Select(b => (pos: Grid.PosOf(v, b.Key), left: b.Value)).ToList();
            hurt = hurt.Where(b => b.left < BreakableOf(v, b.pos).full).ToList();
            if (hurt.Count > 0)
            {
                var sight = Los.TeamVisibility(v, Teams.Xcom);
                foreach (var (pos, left) in hurt)
                {
                    if (!Grid.InBounds(v, pos.X, pos.Y) || !sight[pos.Y * v.Width + pos.X]) continue;
                    var (kind, full) = BreakableOf(v, pos);
                    list.Add(new WorldTagView
                    {
                        Kind = "bar", Text = left.ToString(), Fill = (float)left / full, Low = left * 3 <= full,
                        World = Board.TileToWorld(v, pos, (kind == "door" ? 2.4f : 1.3f) * Board.Tile),
                    });
                }
            }
            var packs = new HashSet<string>();
            foreach (var d in v.Dropped ?? new List<DroppedLoot>())
            {
                var who = UnitById(v, d.From);
                if (who == null || !packs.Add($"{d.From}:{d.Pos.X},{d.Pos.Y},{d.Pos.Z}")) continue;
                list.Add(new WorldTagView { Kind = "pack", Text = Lore.FallenPackMark(who.Name), World = Board.TileToWorld(v, d.Pos, 0.6f * Board.Tile) });
            }
            return list;
        }

        /// <summary>What a siege's door or barricade at `tile` still holds (game.ts breakableNote: «Puerta: aguanta 12 de 18»); none elsewhere, or out of sight.</summary>
        string BreakableNote(Pos tile)
        {
            var v = Shown;
            if (tile == null || v.Breakables == null || !v.Breakables.TryGetValue(Grid.NodeOf(v, tile), out var left) || left <= 0) return null;
            var sight = Los.TeamVisibility(v, Teams.Xcom);
            if (!Grid.InBounds(v, tile.X, tile.Y) || !sight[tile.Y * v.Width + tile.X]) return null;
            var (kind, full) = BreakableOf(v, tile);
            return Lore.BreakableTip(kind, left, full);
        }
    }
}
