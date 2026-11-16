using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Gear
{
    /// <summary>
    /// The grids with a made-up base (runs, 16/11): the stash of 10×6 with some finds, Rojas's kit and pack (the player's)
    /// and Ana's soldier's pack (shown only). Its own rules stand in for the engine's until H0 has them (Gears.MoveRefusal):
    /// sizes lying across, piles, what each kit place takes, swaps, splits. `BASTION_CUADRICULA` sets a moment up for a
    /// capture: arrastra, arrastra-no, pila, hueco, hueco-no, girar, cambio, ordenes, partir, tirar, ana, combate (40-point
    /// cells, the pack and the kit only); none, as it starts. `BASTION_CUADRICULA_PASOS`: drags done before it, as
    /// «id>where@x,y[~]» (where: stash, pack, ana, or a kit place; ~ turned), split by «;».
    /// </summary>
    public sealed class GearLab : MonoBehaviour, IGearHandlers
    {
        public string Case = "";
        GearBoard board;
        TooltipLayer tips;
        VisualElement root;
        readonly Dictionary<string, VisualElement> holders = new Dictionary<string, VisualElement>();

        // ------------------------------------------------------------------ the made-up state

        sealed class Thing
        {
            public string Id, Kind, Type, Name, Icon;
            public int W = 1, H = 1, Count = 1, Pile = 1;
            public List<string> Mods = new List<string>();
        }

        sealed class Placed
        {
            public Thing T;
            public int X, Y;
            public bool Turned;
        }

        sealed class Grid
        {
            public int W, H;
            public List<Placed> Items = new List<Placed>();
        }

        sealed class Soldier
        {
            public string Id, Name;
            public bool Mine;
            public readonly Dictionary<string, Thing> Kit = new Dictionary<string, Thing>();
            public Grid Pack = new Grid { W = 4, H = 3 };
        }

        static readonly string[] Places = { "primary", "melee", "pistol", "vest", "belt0", "belt1" };

        Grid stash;
        readonly List<Soldier> soldiers = new List<Soldier>();
        int next = 1;
        string log = "";

        Thing New(string kind, string type)
        {
            var t = new Thing { Id = "g" + next++, Kind = kind, Type = type };
            switch (kind + ":" + type)
            {
                case "weapon:rifle": (t.Name, t.Icon, t.W, t.H) = ("Fusil de asalto", "weapon:rifle", 4, 1); break;
                case "weapon:shotgun": (t.Name, t.Icon, t.W, t.H) = ("Escopeta", "weapon:shotgun", 4, 1); break;
                case "weapon:sniper": (t.Name, t.Icon, t.W, t.H) = ("Fusil de precisión", "weapon:sniper", 4, 1); break;
                case "weapon:lmg": (t.Name, t.Icon, t.W, t.H) = ("Ametralladora", "weapon:cannon", 4, 2); break;
                case "weapon:pistol": (t.Name, t.Icon, t.W, t.H) = ("Pistola", "weapon:pistol", 2, 1); break;
                case "weapon:knife": (t.Name, t.Icon, t.W, t.H) = ("Cuchillo", "overwatchMelee", 2, 1); break;
                case "weapon:bat": (t.Name, t.Icon, t.W, t.H) = ("Bate", "weapon:bat", 3, 1); break;
                case "weapon:crowbar": (t.Name, t.Icon, t.W, t.H) = ("Palanca", "weapon:crowbar", 3, 1); break;
                case "vest:vest": (t.Name, t.Icon, t.W, t.H) = ("Chaleco", "stat:armor", 2, 2); break;
                case "vest:heavyVest": (t.Name, t.Icon, t.W, t.H) = ("Chaleco pesado", "stat:armor", 2, 2); break;
                case "item:grenade": (t.Name, t.Icon, t.Pile) = ("Granada", "grenade", 5); break;
                case "item:molotov": (t.Name, t.Icon, t.Pile) = ("Molotov", "molotov", 5); break;
                case "item:smoke": (t.Name, t.Icon, t.Pile) = ("Humo", "smoke", 5); break;
                case "item:firstAid": (t.Name, t.Icon, t.Pile) = ("Botiquín", "firstAid", 5); break;
                case "medicine:": (t.Name, t.Icon, t.Pile) = ("Medicinas", "store:medicines", 5); break;
                case "mags:": (t.Name, t.Icon, t.Pile) = ("Cargadores", "stat:ammo", 5); break;
                case "mod:scope": (t.Name, t.Icon, t.Pile) = ("Mira", "aimedShot", 3); break;
                case "supplies:": (t.Name, t.Icon, t.Pile) = ("Suministros", "§", 100); break;
            }
            return t;
        }

        Thing New(string kind, string type, int count)
        {
            var t = New(kind, type);
            t.Count = count;
            return t;
        }

        void Seed()
        {
            next = 1;
            stash = new Grid { W = 10, H = 6 };
            void Put(Grid g, Thing t, int x, int y, bool turned = false) => g.Items.Add(new Placed { T = t, X = x, Y = y, Turned = turned });
            Put(stash, New("weapon", "rifle"), 0, 0);
            Put(stash, New("weapon", "shotgun"), 0, 1);
            Put(stash, New("weapon", "lmg"), 4, 0);
            Put(stash, New("weapon", "pistol"), 8, 0);
            Put(stash, New("weapon", "bat"), 0, 2);
            Put(stash, New("weapon", "sniper"), 4, 2);
            Put(stash, New("vest", "vest"), 8, 4);
            Put(stash, New("item", "grenade", 3), 3, 2);
            Put(stash, New("item", "molotov", 2), 0, 3);
            Put(stash, New("medicine", "", 4), 1, 3);
            Put(stash, New("medicine", "", 2), 2, 3);
            Put(stash, New("mags", "", 5), 3, 3);
            Put(stash, New("mod", "scope", 1), 0, 4);
            Put(stash, New("supplies", "", 100), 6, 5);
            Put(stash, New("supplies", "", 35), 7, 5);
            Put(stash, New("weapon", "crowbar"), 0, 5);
            var rojas = new Soldier { Id = "s1", Name = "Rojas", Mine = true };
            rojas.Kit["melee"] = New("weapon", "knife");
            rojas.Kit["primary"] = New("weapon", "rifle");
            rojas.Kit["belt0"] = New("item", "firstAid", 1);
            Put(rojas.Pack, New("mags", "", 2), 0, 0);
            Put(rojas.Pack, New("supplies", "", 20), 1, 0);
            Put(rojas.Pack, New("weapon", "pistol"), 2, 0);
            var ana = new Soldier { Id = "s3", Name = "Medina (de Ana)", Mine = false };
            ana.Kit["melee"] = New("weapon", "knife");
            Put(ana.Pack, New("medicine", "", 1), 0, 0);
            Put(ana.Pack, New("weapon", "shotgun"), 0, 1);
            soldiers.Clear();
            soldiers.Add(rojas);
            soldiers.Add(ana);
        }

        // ------------------------------------------------------------------ where things are

        /// <summary>Where `id` is: its grid and place there, or its soldier and kit place.</summary>
        (Grid grid, Placed placed, Soldier soldier, string place, bool mine) Where(string id)
        {
            foreach (var p in stash.Items)
                if (p.T.Id == id) return (stash, p, null, null, true);
            foreach (var s in soldiers)
            {
                foreach (var p in s.Pack.Items)
                    if (p.T.Id == id) return (s.Pack, p, s, "pack", s.Mine);
                foreach (var k in s.Kit)
                    if (k.Value?.Id == id) return (null, null, s, k.Key, s.Mine);
            }
            return (null, null, null, null, false);
        }

        static (int, int) Size(Thing t, bool turned) => turned ? (t.H, t.W) : (t.W, t.H);

        static bool Overlaps(Placed p, int x, int y, int w, int h)
        {
            var (pw, ph) = Size(p.T, p.Turned);
            return x < p.X + pw && p.X < x + w && y < p.Y + ph && p.Y < y + h;
        }

        /// <summary>Why `t` doesn't go in kit place `place` (Content.SlotTakes); null, it does.</summary>
        static string Takes(string place, Thing t)
        {
            switch (place)
            {
                case "primary": return t.Kind == "weapon" && t.W == 4 ? null : "Ahí va el arma principal: fusil, escopeta, fusil de precisión o ametralladora";
                case "melee": return t.Kind == "weapon" && (t.Type == "knife" || t.W == 3) ? null : "Ahí va un arma de cuerpo a cuerpo";
                case "pistol": return t.Kind == "weapon" && t.Type == "pistol" ? null : "Ahí va una pistola";
                case "vest": return t.Kind == "vest" ? null : "Ahí va un chaleco";
                case "belt0": case "belt1": return t.Kind == "item" ? null : "Al cinturón van granadas, molotovs, humo, aturdidoras y botiquines";
                default: return "Ahí no va nada";
            }
        }

        Grid GridOf(string to, string slot) => to == "stash" ? stash : soldiers.FirstOrDefault(s => s.Id == to)?.Pack;

        // ------------------------------------------------------------------ IGearHandlers: the stand-in rules

        public string GearRefusal(GearMove m)
        {
            var (fromGrid, placed, fromSoldier, fromPlace, mine) = Where(m.Gear);
            var t = placed?.T ?? (fromSoldier != null && fromPlace != null ? fromSoldier.Kit[fromPlace] : null);
            if (t == null) return "Ya no está";
            if (!mine) return "No es tuyo";
            if (m.Onto != null)
            {
                var (weapon, _) = Find(m.Onto);
                var w = Where(m.Onto);
                if (weapon == null || weapon.Kind != "weapon") return "Las mejoras van en un arma";
                if (!w.mine) return "No es tuyo";
                if (weapon.Mods.Contains(t.Type)) return $"Ya lleva {t.Name.ToLowerInvariant()}";
                return weapon.Mods.Count >= 2 ? "Ya lleva dos mejoras" : null;
            }
            var whole = m.Count <= 0 || m.Count >= t.Count;
            var soldier = m.To == "stash" ? null : soldiers.FirstOrDefault(s => s.Id == m.To);
            if (m.To != "stash" && soldier == null) return "No hay nadie así";
            if (soldier != null && !soldier.Mine) return "Es de Ana: solo ella le pone y le quita cosas";
            if (m.Slot != null && m.Slot != "pack")
            {
                var no = Takes(m.Slot, t);
                if (no != null) return no;
                soldier.Kit.TryGetValue(m.Slot, out var there);
                if (there == null || there == t) return null;
                if (there.Kind == t.Kind && there.Type == t.Type && t.Pile > 1) return there.Count >= there.Pile ? "Esa pila ya está llena" : null;
                if (!whole) return "Ahí ya hay otra cosa";
                // The one there goes where this one was.
                if (fromPlace != null && fromPlace != "pack") return Takes(fromPlace, there) == null ? null : $"{there.Name} no va donde estaba {t.Name.ToLowerInvariant()}";
                foreach (var turned in new[] { placed.Turned, !placed.Turned })
                {
                    var (w, h) = Size(there, turned);
                    if (placed.X + w <= fromGrid.W && placed.Y + h <= fromGrid.H && !fromGrid.Items.Any(p => p != placed && Overlaps(p, placed.X, placed.Y, w, h))) return null;
                }
                return $"{there.Name} no cabe donde estaba {t.Name.ToLowerInvariant()}";
            }
            var g = GridOf(m.To, m.Slot);
            var (gw, gh) = Size(t, m.Turned);
            var pile = g.Items.FirstOrDefault(p => p.X == m.X && p.Y == m.Y && p.T != t && p.T.Kind == t.Kind && p.T.Type == t.Type && t.Pile > 1);
            if (pile != null) return pile.T.Count >= pile.T.Pile ? "Esa pila ya está llena" : null;
            if (m.X < 0 || m.Y < 0 || m.X + gw > g.W || m.Y + gh > g.H) return "No cabe ahí";
            if (g.Items.Any(p => (p.T != t || !whole) && Overlaps(p, m.X, m.Y, gw, gh))) return "Ahí ya hay otra cosa";
            return null;
        }

        public void GearMoved(GearMove m)
        {
            if (GearRefusal(m) != null) return;
            log += m + "\n";
            Debug.Log("[cuadricula] " + m);
            var (fromGrid, placed, fromSoldier, fromPlace, _) = Where(m.Gear);
            var t = placed?.T ?? fromSoldier.Kit[fromPlace];
            if (m.Onto != null)
            {
                Find(m.Onto).Item1.Mods.Add(t.Type);
                if (--t.Count <= 0)
                {
                    if (placed != null) fromGrid.Items.Remove(placed);
                    else fromSoldier.Kit[fromPlace] = null;
                }
                Draw();
                return;
            }
            var count = m.Count <= 0 || m.Count >= t.Count ? t.Count : m.Count;
            var moving = t;
            // Taken from where it was: all of it, or `count` of the pile as a new thing.
            if (count < t.Count)
            {
                t.Count -= count;
                moving = New(t.Kind, t.Type, count);
            }
            else if (placed != null) fromGrid.Items.Remove(placed);
            else fromSoldier.Kit[fromPlace] = null;

            if (m.Slot != null && m.Slot != "pack")
            {
                var soldier = soldiers.First(s => s.Id == m.To);
                soldier.Kit.TryGetValue(m.Slot, out var there);
                if (there != null && there.Kind == moving.Kind && there.Type == moving.Type && moving.Pile > 1)
                {
                    Merge(there, moving, fromGrid, placed, fromSoldier, fromPlace);
                    Draw();
                    return;
                }
                soldier.Kit[m.Slot] = moving;
                if (there != null)
                {
                    if (placed == null) fromSoldier.Kit[fromPlace] = there;
                    else
                    {
                        var turned = Size(there, placed.Turned) is var (w, h) && placed.X + w <= fromGrid.W && placed.Y + h <= fromGrid.H && !fromGrid.Items.Any(p => Overlaps(p, placed.X, placed.Y, w, h)) ? placed.Turned : !placed.Turned;
                        fromGrid.Items.Add(new Placed { T = there, X = placed.X, Y = placed.Y, Turned = turned });
                    }
                }
                Draw();
                return;
            }
            var g = GridOf(m.To, m.Slot);
            var pile = g.Items.FirstOrDefault(p => p.X == m.X && p.Y == m.Y && p.T.Kind == moving.Kind && p.T.Type == moving.Type && moving.Pile > 1);
            if (pile != null) Merge(pile.T, moving, fromGrid, placed, fromSoldier, fromPlace);
            else g.Items.Add(new Placed { T = moving, X = m.X, Y = m.Y, Turned = m.Turned });
            Draw();
        }

        /// <summary>`moving` onto the pile `onto`, up to its top; the rest stays where it came from.</summary>
        void Merge(Thing onto, Thing moving, Grid fromGrid, Placed placed, Soldier fromSoldier, string fromPlace)
        {
            var take = Mathf.Min(moving.Count, onto.Pile - onto.Count);
            onto.Count += take;
            var rest = moving.Count - take;
            if (rest <= 0) return;
            moving.Count = rest;
            if (placed != null)
            {
                if (fromGrid.Items.Contains(placed)) placed.T.Count += rest - (placed.T == moving ? rest : 0);
                else fromGrid.Items.Add(new Placed { T = moving, X = placed.X, Y = placed.Y, Turned = placed.Turned });
            }
            else if (fromSoldier.Kit[fromPlace] == null) fromSoldier.Kit[fromPlace] = moving;
            else fromSoldier.Kit[fromPlace].Count += rest;
        }

        public void GearDropped(string gear)
        {
            log += "tirar " + gear + "\n";
            Debug.Log("[cuadricula] tirar " + gear);
            var (g, placed, soldier, place, _) = Where(gear);
            if (placed != null) g.Items.Remove(placed);
            else if (soldier != null) soldier.Kit[place] = null;
            Draw();
        }

        public IReadOnlyList<GearActionView> GearActions(string gear)
        {
            var (_, placed, soldier, place, _) = Where(gear);
            var t = placed?.T ?? (soldier != null ? soldier.Kit[place] : null);
            if (t?.Kind != "medicine") return new GearActionView[0];
            return new[] { new GearActionView { Id = "use", Label = "Usar", Why = soldier == null ? "Desde el almacén no: que la lleve un soldado" : null } };
        }

        public void GearAction(string gear, string action)
        {
            log += action + " " + gear + "\n";
            Debug.Log($"[cuadricula] {action} {gear}");
            var (g, placed, soldier, place, _) = Where(gear);
            var t = placed?.T ?? soldier?.Kit[place];
            if (t == null) return;
            if (--t.Count <= 0)
            {
                if (placed != null) g.Items.Remove(placed);
                else soldier.Kit[place] = null;
            }
            Draw();
        }

        // ------------------------------------------------------------------ the screen

        GearItemView View(Thing t) => new GearItemView
        {
            Id = t.Id,
            Kind = t.Kind,
            Type = t.Type,
            Name = t.Name,
            Icon = t.Icon,
            W = t.W,
            H = t.H,
            Count = t.Count,
            Pile = t.Pile,
            Tip = $"{t.Name}{(t.Pile > 1 ? $" · {t.Count} de {t.Pile}" : "")}\nOcupa {t.W}×{t.H}{(t.Mods.Count > 0 ? "\nMejoras: " + string.Join(", ", t.Mods) : "")}",
        };

        GearGridView View(Grid g, string owner, string slot, bool mine, int rows = 0) => new GearGridView
        {
            Owner = owner,
            Slot = slot,
            W = g.W,
            H = g.H,
            Mine = mine,
            Rows = rows,
            Items = g.Items.Select(p => new PlacedView { Gear = View(p.T), X = p.X, Y = p.Y, Turned = p.Turned }).ToList(),
        };

        static (int, int) Box(string place)
        {
            switch (place)
            {
                case "primary": return (4, 2);
                case "melee": return (3, 1);
                case "pistol": return (2, 1);
                case "vest": return (2, 2);
                default: return (1, 1);
            }
        }

        void Start()
        {
            if (string.IsNullOrEmpty(Case)) Case = System.Environment.GetEnvironmentVariable("BASTION_CUADRICULA") ?? "";
            root = GetComponent<UIDocument>().rootVisualElement;
            foreach (var sheet in new[] { "Hud/Mixta", "Hud/Cards" })
            {
                var s = Resources.Load<StyleSheet>(sheet);
                if (s != null) root.styleSheets.Add(s);
            }
            root.AddToClassList("font-text");
            root.style.backgroundColor = new Color(0.13f, 0.15f, 0.16f);
            var table = Resources.Load<HudIconTable>("HudIcons");
            var screen = Ui.El("gearlab", root);
            screen.style.flexDirection = FlexDirection.Row;
            screen.style.paddingLeft = 40;
            screen.style.paddingTop = 40;
            tips = new TooltipLayer(root);
            board = new GearBoard(root, tips, id => table != null ? table.Get(id) : null, this);
            var combat = Case == "combate";
            if (combat) board.Cell = 40;
            Seed();

            if (!combat)
            {
                var left = Ui.El("panel", screen);
                Pad(left);
                Ui.Text(GearText.Stash, "font-title", left).style.fontSize = 18;
                holders["stash"] = Ui.El(null, left);
                var hint = Ui.Text(GearText.Hint, "muted", left);
                hint.style.fontSize = 12;
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.maxWidth = 10 * board.Cell;
                hint.style.marginTop = 6;
                holders["bin"] = Ui.El(null, left);
                holders["bin"].style.marginTop = 10;
            }

            foreach (var s in soldiers)
            {
                if (combat && !s.Mine) continue;
                var panel = Ui.El("panel", screen);
                Pad(panel);
                panel.style.marginLeft = 24;
                Ui.Text(s.Name.ToUpperInvariant(), "font-title", panel).style.fontSize = 18;
                if (s.Mine)
                {
                    var row1 = Row(panel);
                    Place(row1, s, "primary");
                    var row2 = Row(panel);
                    Place(row2, s, "melee");
                    Place(row2, s, "pistol");
                    var row3 = Row(panel);
                    Place(row3, s, "vest");
                    var belt = Ui.El(null, row3);
                    Ui.Text(GearText.Belt, "muted", belt).style.fontSize = 11;
                    var beltRow = Row(belt);
                    Place(beltRow, s, "belt0", false);
                    Place(beltRow, s, "belt1", false);
                }
                Ui.Text(GearText.Pack, "muted", panel).style.marginTop = 10;
                holders[s.Id + "/pack"] = Ui.El(null, panel);
                if (combat)
                {
                    holders["bin"] = Ui.El(null, panel);
                    holders["bin"].style.marginTop = 10;
                }
            }
            Draw();
            StartCoroutine(Play());
        }

        static void Pad(VisualElement e)
        {
            e.style.paddingLeft = e.style.paddingRight = 14;
            e.style.paddingTop = e.style.paddingBottom = 12;
            e.style.alignSelf = Align.FlexStart;
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = Ui.El(null, parent);
            r.style.flexDirection = FlexDirection.Row;
            r.style.marginTop = 8;
            r.style.alignItems = Align.FlexEnd;
            return r;
        }

        void Place(VisualElement row, Soldier s, string place, bool gap = true)
        {
            var h = Ui.El(null, row);
            h.style.marginRight = gap ? 10 : 4;
            holders[s.Id + "/" + place] = h;
        }

        void Draw()
        {
            board.SetGrid(holders.TryGetValue("stash", out var sh) ? sh : null, View(stash, "stash", null, true, 6));
            foreach (var s in soldiers)
            {
                if (holders.TryGetValue(s.Id + "/pack", out var ph)) board.SetGrid(ph, View(s.Pack, s.Id, "pack", s.Mine));
                foreach (var place in Places)
                {
                    if (!holders.TryGetValue(s.Id + "/" + place, out var h)) continue;
                    var (w, hh) = Box(place);
                    s.Kit.TryGetValue(place, out var t);
                    board.SetSlot(h, new GearSlotView { Owner = s.Id, Slot = place, W = w, H = hh, Mine = s.Mine, Gear = t != null ? View(t) : null });
                }
            }
            if (holders.TryGetValue("bin", out var bin)) board.SetBin(bin);
        }

        void Update() => board?.Tick();

        // ------------------------------------------------------------------ the capture's moment

        /// <summary>
        /// «todos»: each moment held 2 s of game time from 8 s, its time in the log («[cuadricula] pila lista a los 8.0 s»): with
        /// -shotFps 2, the shot of each is the first at or after that time (Shots logs the first shot's time).
        /// </summary>
        static readonly string[] All = { "", "arrastra", "arrastra-no", "pila", "hueco", "hueco-no", "girar", "cambio", "ordenes", "partir", "tirar", "ana", "montar" };

        IEnumerator Play()
        {
            for (var i = 0; i < 4; i++) yield return null;
            if (Case == "todos")
            {
                for (var i = 0; i < All.Length; i++)
                {
                    while (Time.time < 8f + 2 * i) yield return null;
                    while (board.Close()) { }
                    Seed();
                    Draw();
                    yield return null;
                    Moment(All[i]);
                    yield return null;
                    Debug.Log($"[cuadricula] {(All[i] == "" ? "inicio" : All[i])} lista a los {Time.time:0.0} s");
                }
                yield break;
            }
            var steps = System.Environment.GetEnvironmentVariable("BASTION_CUADRICULA_PASOS");
            if (!string.IsNullOrEmpty(steps))
                foreach (var step in steps.Split(';'))
                {
                    Drag(step, true);
                    yield return null;
                    yield return null;
                }
            Moment(Case);
        }

        void Moment(string moment)
        {
            switch (moment)
            {
                case "arrastra": Drag("g1>s1/pack@0,2", false); break;          // the rifle into Rojas's pack's free row
                case "arrastra-no": Drag("g1>s1/pack@0,0", false); break;       // onto the mags: taken
                case "pila": Drag("g11>stash@1,3", false); break;               // the medicines onto the other pile
                case "hueco": Drag("g2>s1/primary", false); break;              // the shotgun over the primary: the rifle there swaps
                case "hueco-no": Drag("g4>s1/primary", false); break;           // the pistol over the primary: no
                case "girar": Drag("g5>stash@9,1~", false); break;              // the bat, turned on end, into the stash's last column
                case "cambio": Drag("g2>s1/primary", true); break;              // done: the rifle comes to where the shotgun was
                case "ordenes": board.TestPick("g10"); break;                   // the medicines picked: Girar, Partir, Usar, Tirar
                case "partir": board.TestPick("g10", 2); break;
                case "tirar": board.TestThrow("g13"); break;                    // the scope, at the bin
                case "ana": Drag("g5>s3/pack@2,0", false); break;               // the bat into Ana's soldier's pack: not his
                case "montar": Drag("g13>stash@1,0", false); break;             // the scope onto the rifle
                case "combate": Drag("g22>s1/pack@0,2", false); break;          // the pistol in the pack, to the row below
            }
        }

        /// <summary>«id>where[@x,y][~]»: where is stash, «s1/pack», «s1/primary»…; the point is that cell's middle.</summary>
        void Drag(string step, bool release)
        {
            var parts = step.Split('>');
            var id = parts[0];
            var rest = parts[1];
            var turned = rest.EndsWith("~");
            rest = rest.TrimEnd('~');
            var at = rest.Split('@');
            var where = at[0];
            var holder = holders.TryGetValue(where, out var h) ? h : null;
            if (holder == null)
            {
                Debug.LogWarning("[cuadricula] no " + where);
                return;
            }
            var cells = holder.Q(className: "gear-cells") ?? holder.Q(className: "gear-slot");
            var r = cells.worldBound;
            var (thing, _) = Find(id);
            var point = r.center;
            if (at.Length > 1 && thing != null)
            {
                var xy = at[1].Split(',');
                var (w, hh) = Size(thing, turned);
                point = new Vector2(r.xMin + (int.Parse(xy[0]) + w / 2f) * board.Cell, r.yMin + (int.Parse(xy[1]) + hh / 2f) * board.Cell);
            }
            var was = Where(id).placed?.Turned ?? false;
            board.TestDrag(id, point, turned != was ? 1 : 0, release);
        }

        (Thing, bool) Find(string id)
        {
            var (_, placed, soldier, place, _) = Where(id);
            return (placed?.T ?? (soldier != null ? soldier.Kit[place] : null), placed?.Turned ?? false);
        }
    }
}
