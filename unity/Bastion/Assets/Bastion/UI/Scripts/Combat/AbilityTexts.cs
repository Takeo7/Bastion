using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The bar's and the aim's words and figures (game.ts's helpers, UI's: docs/UNITY-UI.md §3.2), worked out from the
    /// engine's own rules so a card says what the server charges: what an ability costs, what is left of it, its damage,
    /// reach and noise, what armour does, whether a hit kills.
    /// </summary>
    public static class AbilityTexts
    {
        /// <summary>Out of rounds in the magazine, on the key itself (Ana, 20/10 #24).</summary>
        public const string ReloadFirst = "Sin balas: recarga";

        /// <summary>Space with nothing armed: the first of these that can be used (game.ts PRIMARY_ATTACKS).</summary>
        public static readonly string[] PrimaryAttacks = { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash };

        /// <summary>Done by hand on a tile beside the soldier: Registrar, and opening or closing a door (playtest 13/10 #2).</summary>
        public static readonly HashSet<string> Hand = new HashSet<string> { AbilityIds.Search, AbilityIds.OpenDoor, AbilityIds.CloseDoor };

        /// <summary>A key that folds several abilities into a menu (docs/FASE10.md §4).</summary>
        public sealed class Menu
        {
            public string Id;
            public string Name;
            public string Icon;
            public string[] Members;
            /// <summary>Whether a choice shows, with a box beside the soldier or not.</summary>
            public Func<AbilityView, bool, bool> Shows;
        }

        public static readonly Menu[] Menus =
        {
            new Menu { Id = "menu:watch", Name = "Vigilancia", Icon = AbilityIds.Overwatch, Members = new[] { AbilityIds.Overwatch, AbilityIds.OverwatchMelee }, Shows = (v, crate) => true },
            new Menu { Id = "menu:throw", Name = "Lanzar", Icon = AbilityIds.Stone, Members = new[] { AbilityIds.Stone, AbilityIds.Grenade, AbilityIds.Smoke, AbilityIds.Flashbang }, Shows = (v, crate) => true },
            new Menu { Id = "menu:investigate", Name = "Investigar", Icon = AbilityIds.SearchRoom, Members = new[] { AbilityIds.SearchRoom, AbilityIds.Search }, Shows = (v, crate) => v.Id == AbilityIds.SearchRoom || crate },
        };

        public static bool IsMenu(string id) => id != null && id.StartsWith("menu:", StringComparison.Ordinal);

        /// <summary>How a menu's choices read inside it: what the menu already says is left out.</summary>
        public static readonly Dictionary<string, string> MenuLabels = new Dictionary<string, string>
        {
            [AbilityIds.Overwatch] = "A distancia",
            [AbilityIds.OverwatchMelee] = "Cuerpo a cuerpo",
            [AbilityIds.Stone] = "Piedra",
            [AbilityIds.Grenade] = "Granada",
            [AbilityIds.Smoke] = "Humo",
            [AbilityIds.Flashbang] = "Aturdidora",
            [AbilityIds.SearchRoom] = "Habitación",
            [AbilityIds.Search] = "Caja",
        };

        /// <summary>What attack variants do besides the odds shown in the panel.</summary>
        public static readonly Dictionary<string, string> AttackNotes = new Dictionary<string, string>
        {
            [AbilityIds.RapidFire] = "Dos disparos seguidos",
            [AbilityIds.ChainShot] = "Si acierta, dispara otra vez",
            [AbilityIds.AimedShot] = "+50 % de daño",
            [AbilityIds.Rupture] = "+3 de daño; el objetivo recibirá +3 de todos los ataques",
        };

        /// <summary>What a support ability does, where its description says less (game.ts targetPanel).</summary>
        public static readonly Dictionary<string, string> SupportEffects = new Dictionary<string, string>
        {
            [AbilityIds.Revival] = "Quita el aturdimiento, la desorientación y el pánico",
            [AbilityIds.Suppression] = "−50 de puntería al objetivo hasta tu próximo turno; si se mueve, le disparas",
        };

        /// <summary>How a damage figure spreads, after «Daño 4–6»: per shot, over an area, on several enemies.</summary>
        static readonly Dictionary<string, string> DamageEach = new Dictionary<string, string>
        {
            [AbilityIds.RapidFire] = " por disparo",
            [AbilityIds.Faceoff] = " a cada uno",
            [AbilityIds.Grenade] = " en el área",
            [AbilityIds.Launch] = " en el área",
            [AbilityIds.Missiles] = " en el área",
        };

        static bool Watch(string id) => id == AbilityIds.Overwatch || id == AbilityIds.KillZone;

        /// <summary>«1 acción», «2 acciones», «Sin acción», «Acaba el turno»: one wording on every key (playtest 17/10 #52).</summary>
        public static string CostText(Unit u, string id)
        {
            var need = Abilities.ApNeeded(u, id);
            if (need == 0) return "Sin acción";
            if (Abilities.EndsTurn(u, id)) return "Acaba el turno";
            return need == 1 ? "1 acción" : $"{need} acciones";
        }

        /// <summary>What is left of an ability: its cooldown and its uses; null if nothing.</summary>
        public static string LimitsText(Unit u, string id)
        {
            var def = Content.Abilities[id];
            u.Cooldowns.TryGetValue(id, out var left);
            var parts = new List<string>();
            if (left > 0) parts.Add($"Disponible en {left} {(left == 1 ? "turno" : "turnos")}");
            else if (def.Cooldown is int cd && cd > 0) parts.Add($"Recarga en {cd} turnos");
            if (def.Charge != null) parts.Add($"Usos: {Charge(u, def.Charge)}");
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }

        public static int Charge(Unit u, string charge) => u.Charges.TryGetValue(charge, out var n) ? n : 0;

        /// <summary>«4–6», or «4» when it can't vary.</summary>
        public static string Span(DamageRange r) => r.Min == r.Max ? r.Min.ToString() : $"{r.Min}–{r.Max}";

        /// <summary>A critical next to the hit: «crítico +2» when it adds the same to every roll, else its own range.</summary>
        static string CritText(DamageRange hit, DamageRange crit)
        {
            var extra = crit.Min - hit.Min;
            return extra == crit.Max - hit.Max ? $"crítico +{extra}" : $"crítico {Span(crit)}";
        }

        /// <summary>The hover card's damage line: «Daño 4–6 · crítico +2»; null without damage.</summary>
        public static string DamageText(GameState s, Unit u, string id)
        {
            var value = DamageValue(s, u, id);
            return value != null ? $"Daño {value}" : null;
        }

        /// <summary>The damage alone, for the cards' figures (#18): «4–6 por disparo · crítico +2».</summary>
        public static string DamageValue(GameState s, Unit u, string id)
        {
            // Vigilancia fires the rifle at whoever moves: a reaction shot, which only crits with the talent.
            var watch = Watch(id);
            var d = Rules.AbilityDamage(s, u, watch ? AbilityIds.Shoot : id);
            if (d == null) return null;
            var crit = d.Crit != null && (!watch || Content.HasPerk(u, PerkIds.GuardCrit)) ? d.Crit : null;
            var parts = new[]
            {
                Span(d.Hit) + (watch ? " por disparo" : DamageEach.TryGetValue(id, out var each) ? each : ""),
                crit != null ? CritText(d.Hit, crit) : "",
                d.Shred > 0 ? $"destroza {d.Shred} de blindaje" : "",
            };
            return string.Join(" · ", parts.Where(p => p.Length > 0));
        }

        /// <summary>
        /// Where a weapon hits without losing aim, in tiles (Ana, 22/10 #54: not «medio»), from its range table up to how
        /// far the soldier sees: «hasta 11 casillas», «desde 11 casillas», «al lado».
        /// </summary>
        public static string ReachText(string table, int sight)
        {
            if (table == RangeTableIds.None) return "al lado";
            var values = Content.RangeTables[table];
            bool Ok(int d) => values[Math.Min(d, values.Count - 1)] >= 0;
            var first = 1;
            while (first < sight && !Ok(first)) first++;
            var last = first;
            while (last < sight && Ok(last + 1)) last++;
            return first > 1 ? $"desde {first} casillas" : $"hasta {last} casillas";
        }

        /// <summary>
        /// An ability's figures on its card (playtest 19/10 #18: «siempre igual»), each a name and its value, in this order
        /// and only those that apply: actions, reach, damage, noise, uses, recharge.
        /// </summary>
        public static List<StatPair> AbilityStats(GameState s, Unit u, string id)
        {
            var def = Content.Abilities[id];
            var need = Abilities.ApNeeded(u, id);
            var watch = Watch(id);
            var weapon = Content.IsAttack(id) ? Content.AttackWeapon(u, id) : watch ? Content.AttackWeapon(u, AbilityIds.Shoot) : null;
            var range = Abilities.AbilityRange(u, id);
            var reach = weapon != null ? ReachText(weapon.Range, Los.SightOf(u)) : range > 0 ? $"{Number(range)} casillas" : null;
            var noise = watch ? Content.AttackWeapon(u, AbilityIds.Shoot).Noise : Abilities.NoiseOf(s, u, id);
            u.Cooldowns.TryGetValue(id, out var left);
            var stats = new List<(string, string)>
            {
                ("Acciones", need == 0 ? "ninguna" : Abilities.EndsTurn(u, id) ? "acaba el turno" : need.ToString()),
                ("Alcance", reach),
                ("Daño", DamageValue(s, u, id)),
                // In tiles, as on the weapons' sheet (Lore, 19/10).
                ("Ruido", noise > 0 ? $"{noise} casillas{(watch ? " al disparar" : "")}" : Content.IsAttack(id) ? "ninguno" : null),
                ("Usos", def.Charge != null ? Charge(u, def.Charge).ToString() : null),
                // Ready, how often; waiting, when (Lore: not to mix them up).
                ("Recarga", left > 0 ? $"lista en {left} {(left == 1 ? "turno" : "turnos")}" : def.Cooldown is int cd && cd > 0 ? $"cada {cd} turnos" : null),
            };
            return stats.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => new StatPair { Key = x.Item1, Value = x.Item2 }).ToList();
        }

        /// <summary>How far using it is heard (docs/FASE2.md): «ruido 12», or «sin ruido» for a silent attack; Vigilancia when it fires.</summary>
        public static string NoiseText(GameState s, Unit u, string id)
        {
            if (Watch(id))
            {
                var w = Content.AttackWeapon(u, AbilityIds.Shoot).Noise;
                return w > 0 ? $"ruido {w} al disparar" : null;
            }
            var n = Abilities.NoiseOf(s, u, id);
            if (n > 0) return $"ruido {n}";
            return Content.IsAttack(id) ? "sin ruido" : null;
        }

        /// <summary>What a move sounds like: running, a door, a window's glass; null when it is quiet.</summary>
        public static string MoveNoise(GameState s, IList<Pos> path, bool dash)
        {
            var parts = new List<(int, string)>();
            var kinds = path.Select(p => Grid.KindAt(s, p)).ToList();
            if (dash) parts.Add((Content.Noise.Dash, "corre"));
            if (kinds.Contains("window")) parts.Add((Content.Noise.Breach, "rompe un cristal"));
            if (kinds.Contains("door")) parts.Add((Content.Noise.Door, "abre una puerta"));
            if (parts.Count == 0) return null;
            parts = parts.OrderByDescending(p => p.Item1).ToList();
            return $"Ruido {parts[0].Item1}: {string.Join(", ", parts.Select(p => p.Item2))}";
        }

        /// <summary>The weapon an attack is made with, by name: a found weapon carried as the main one keeps its own (docs/FASE4.md).</summary>
        public static string WeaponLabel(Unit u, string ability)
        {
            var weapon = Content.AttackWeapon(u, ability);
            return u.WeaponName != null && weapon.Id == Content.Templates[u.Template].Weapon ? u.WeaponName : weapon.Name;
        }

        /// <summary>
        /// Whether `a` shoots: an attack on an enemy with a weapon that isn't for close quarters. Only then does cover count
        /// against it: a bite or a blow ignores cover and never flanks (aim.ts).
        /// </summary>
        public static bool ShootsFromRange(Unit a)
        {
            return Abilities.AbilitiesOf(a).Any(id =>
            {
                var def = Content.Abilities[id];
                return def.Target == AbilityTargets.Enemy && def.Weapon != null && !Content.AttackWeapon(a, id).Melee;
            });
        }

        /// <summary>Whether what `d` takes off kills `t`: surely, or only with a critical (playtest 19/10 #20: not «Puede matarlo»).</summary>
        public static string Lethality(DamagePreview d, Unit t, bool critCounts = true)
        {
            if (d.Hit.Min >= t.Hp) return "Lo mata";
            if (critCounts && d.Hit.Max < t.Hp && d.Crit != null && d.Crit.Max >= t.Hp) return "Un crítico lo mata";
            return null;
        }

        /// <summary>What armour does to a hit on `t`: soaks, is pierced, is stripped.</summary>
        public static List<string> ArmorNotes(DamagePreview d, Unit t)
        {
            var notes = new List<string>();
            if (d.Armor > 0) notes.Add($"Blindaje {d.Armor}: −{d.Armor} por impacto");
            if (t.Armor > d.Armor) notes.Add($"Perfora {t.Armor - d.Armor} de blindaje");
            if (d.Shred > 0 && t.Armor > 0) notes.Add($"Destroza {Math.Min(d.Shred, t.Armor)} de blindaje");
            if (t.Ruptured > 0) notes.Add($"Ruptura: +{t.Ruptured} a cada impacto");
            return notes;
        }

        /// <summary>
        /// Where a throw aimed at `tile` lands: the tile itself if it is in range, else the farthest tile in range on the way
        /// there (as in XCOM, the aim stops at the edge of the circle instead of losing the preview).
        /// </summary>
        public static Pos ThrowTile(Unit u, string ability, Pos tile)
        {
            var range = Abilities.AbilityRange(u, ability);
            var far = Grid.Distance(u.Pos, tile);
            if (far <= range) return tile;
            for (var k = range / far; k > 0; k -= 0.01)
            {
                // On the floor that was aimed at (docs/FASE6.md).
                var p = new Pos { X = (int)Math.Round(u.Pos.X + (tile.X - u.Pos.X) * k, MidpointRounding.AwayFromZero), Y = (int)Math.Round(u.Pos.Y + (tile.Y - u.Pos.Y) * k, MidpointRounding.AwayFromZero), Z = tile.Z };
                if (Grid.Distance(u.Pos, p) <= range) return p;
            }
            return u.Pos;
        }

        /// <summary>Players with a soldier that still has actions.</summary>
        public static HashSet<int> BusySlots(GameState s)
        {
            var busy = new HashSet<int>();
            foreach (var u in s.Units)
                if (u.Alive && u.Team == Teams.Xcom && u.Ap > 0 && u.Owner is int o) busy.Add(o);
            return busy;
        }

        /// <summary>A number as JavaScript writes it: 8, 7.5.</summary>
        public static string Number(double n) => n == Math.Floor(n) ? ((long)n).ToString() : n.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
