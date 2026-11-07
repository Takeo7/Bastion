using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The marks over the special loot boxes (lootMarks.ts; mapView.ts markLoot, docs/FASE8.md §B): the icon of what is in
    /// each on a dark plate ringed in its colour, hung over the box at the map's scale. One shows once its box has been in
    /// sight (the fog's explored, in a siege too: Arturo, 31/10 #5), with a soldier of the squad within four tiles (Arturo,
    /// 22/10: not across the map), and not on a storey above the one in view; once the box is opened it goes.
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>How near a soldier of the squad must be (mapView.ts LOOT_MARK_REACH, tiles).</summary>
        const float LootMarkReach = 4;

        /// <summary>A mark's width (lootMarks.ts SIZE, tiles), and how high over its floor it hangs (the box's top and 0.95 more).</summary>
        const float LootMarkSize = 0.52f;
        const float LootMarkHeight = 1.45f;

        /// <summary>Each kind's icon and ring (lootMarks.ts LOOKS): weapons the hazard yellow, medicines green, ammunition amber, food brass.</summary>
        static readonly Dictionary<string, (string icon, string ring)> LootLooks = new Dictionary<string, (string, string)>
        {
            ["lootWeapons"] = ("weapon:rifle", "#f2c230"),
            ["lootMedical"] = ("firstAid", "#8fd05a"),
            ["lootAmmo"] = ("stat:ammo", "#f5a524"),
            ["lootFood"] = ("store:food", "#d9a464"),
        };

        /// <summary>
        /// The icon over a thing to act on (Arturo, 5/11 #12, and 7/11 for the rest): in the bone of the lines where a click
        /// does it, greyed where it can't from here; how high over its floor (a body lies on the ground, a boot is a car's
        /// height, a box, a door or the boards LootMarkHeight).
        /// </summary>
        const string SearchLit = "#f3ecdc";
        const string SearchFar = "#7d868c";
        static readonly Color SearchLitColor = Ui.Hex(SearchLit, Color.white);
        static readonly Color SearchFarColor = Ui.Hex(SearchFar, Color.grey);
        const float SearchBodyHeight = 0.75f;
        const float SearchBootHeight = 1.25f;
        /// <summary>How far over the thing's top its icon hangs (metres; SearchOutline.TopOf).</summary>
        const float SearchOverTop = 0.45f;

        /// <summary>The bodies and boots under the pointer or within Registrar's reach, each lit or not (ShowSearchMarks).</summary>
        List<(Pos at, bool lit)> searchMarks = new List<(Pos, bool)>();
        string searchKey = "";
        /// <summary>What ShowWay's pass puts there (its branches and ShowArmed), shown as it ends.</summary>
        List<(Pos at, bool lit)> searchNext;

        static bool IsBodyOrBoot(string spot) => spot == Looting.Body || spot == Looting.CarBoot || spot == Looting.PoliceBoot;

        /// <summary>
        /// What a click on a thing does, as its icon (Arturo, 7/11): the boards off (the crowbar), a door opened or shut
        /// (LineIcon's), else searched (the rucksack).
        /// </summary>
        static string MarkIcon(GameState v, Pos at)
        {
            if (Abilities.Boards(v, at) != null) return AbilityIds.Pry;
            var kind = Bastion.Engine.Grid.KindAt(v, at);
            if (kind == TileKinds.Door) return AbilityIds.OpenDoor;
            if (kind == TileKinds.DoorOpen) return AbilityIds.CloseDoor;
            return AbilityIds.Search;
        }

        /// <summary>
        /// A thing to act on shown as itself (#12 of 5/11; boxes, doors, boards and furniture since 7/11): its action's icon
        /// over each and, Arte's, the thing outlined; null or empty, none. The tags over the map again only when they change.
        /// </summary>
        void ShowSearchMarks(List<(Pos at, bool lit)> marks)
        {
            marks ??= new List<(Pos, bool)>();
            var key = string.Join("|", marks.Select(m => $"{m.at.X},{m.at.Y},{m.at.Z}:{m.lit}"));
            if (key == searchKey) return;
            searchKey = key;
            searchMarks = marks;
            if (marks.Count > 0) Debug.Log($"[registro] {key}");
            // The body or the whole car outlined in the same colours (Arte's SearchOutline; empty, none).
            if (State != null) Bastion.Client.Units.SearchOutline.Show(State, marks, SearchLitColor, SearchFarColor);
            if (Hud != null && State != null) Hud.SetWorldTags(WorldTags(Shown));
        }

        /// <summary>The rucksacks of ShowSearchMarks, added to the tags over the map.</summary>
        void SearchMarks(GameState v, List<WorldTagView> list)
        {
            foreach (var (at, lit) in searchMarks)
            {
                // Over the thing's top (Arte's SearchOutline.TopOf: a box's lid, a door's leaf, the furniture against a
                // window), else at the height each kind had.
                var spot = Looting.SearchSpot(v, at);
                var high = spot == Looting.Body ? SearchBodyHeight : spot == Looting.CarBoot || spot == Looting.PoliceBoot ? SearchBootHeight : LootMarkHeight;
                var world = Board.TileToWorld(v, at, high * Board.Tile);
                if (Bastion.Client.Units.SearchOutline.TopOf(v, at) is float top) world.y = top + SearchOverTop;
                list.Add(new WorldTagView
                {
                    Kind = "loot", Icon = MarkIcon(v, at), Ring = lit ? SearchLit : SearchFar, Size = LootMarkSize * Board.Tile,
                    World = world,
                });
            }
        }

        /// <summary>The marks of the boxes to show now, added to the tags over the map.</summary>
        void LootMarks(GameState v, List<WorldTagView> list)
        {
            var squad = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && !u.Evacuated).ToList();
            if (squad.Count == 0) return;
            var fog = Bastion.Client.Map.Fog.Current;
            var all = fog == null;
            var area = v.Width * v.Height;
            var top = Mathf.Min(Level, v.Levels?.Count ?? 0);
            for (var z = 0; z <= top; z++)
                for (var i = 0; i < area; i++)
                {
                    var p = new Pos { X = i % v.Width, Y = i / v.Width, Z = z };
                    var kind = Looting.SearchSpot(v, p);
                    if (kind == null || !LootLooks.TryGetValue(kind, out var look) || Looting.WasSearched(v, p)) continue;
                    if (!all && !fog.Explored(z * area + i)) continue;
                    if (!squad.Any(u => Mathf.Sqrt((u.Pos.X - p.X) * (u.Pos.X - p.X) + (u.Pos.Y - p.Y) * (u.Pos.Y - p.Y)) <= LootMarkReach)) continue;
                    list.Add(new WorldTagView
                    {
                        Kind = "loot", Icon = look.icon, Ring = look.ring, Size = LootMarkSize * Board.Tile,
                        World = Board.TileToWorld(v, p, LootMarkHeight * Board.Tile),
                    });
                }
        }
    }
}
