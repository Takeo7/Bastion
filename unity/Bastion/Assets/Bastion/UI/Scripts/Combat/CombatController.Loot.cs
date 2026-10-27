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
    /// sight (the fog's explored, all of it in a siege), with a soldier of the squad within four tiles (Arturo, 22/10: not
    /// across the map), and not on a storey above the one in view; once the box is opened it goes.
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

        /// <summary>The marks of the boxes to show now, added to the tags over the map.</summary>
        void LootMarks(GameState v, List<WorldTagView> list)
        {
            var squad = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && !u.Evacuated).ToList();
            if (squad.Count == 0) return;
            var fog = Bastion.Client.Map.Fog.Current;
            var all = v.Mission.Siege != null || fog == null;
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
