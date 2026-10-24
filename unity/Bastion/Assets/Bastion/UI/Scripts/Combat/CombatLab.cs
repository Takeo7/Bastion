using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Client.Units;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The combat played without a network or a map (Gameplay 24/10): a state recorded by the TypeScript engine
    /// (engine-cs/golden/&lt;Step&gt;/&lt;Golden&gt;.json) laid out roughly (the floor grey, walls dark blocks, things in the
    /// way brown, the units capsules, registered as figures), the camera, the marks, the controller and the HUD. Its orders
    /// go to the engine's rules here, as the host's would (Rules.ExecuteCommand on the host's state), and the events back
    /// to this player's copy (Events.ApplyEvent), with no animation yet. For a capture, a soldier is picked, and either the
    /// pointer held on a tile at mid reach or an ability armed (Arm). Throwaway: Arte's map and figures replace the floor
    /// and the capsules.
    /// </summary>
    public sealed class CombatLab : MonoBehaviour
    {
        /// <summary>A folder of engine-cs/golden (step1, step2).</summary>
        public string Step = "step2";

        /// <summary>A file of that folder, without .json.</summary>
        public string Golden = "city-3-elimination";

        /// <summary>An ability to arm on the first soldier, for a capture (shoot, grenade…); empty, the pointer on a tile instead.</summary>
        public string Arm = "";

        /// <summary>The recorded orders played up to the first shot, and its soldier aiming it at its target (a capture of the aim).</summary>
        public bool ToShot;

        /// <summary>With ToShot, the first shot that kills its target (the shot over the shoulder).</summary>
        public bool Kills;

        /// <summary>With ToShot, the pointer on the target instead of the shot armed (what it sees, its flag's line).</summary>
        public bool HoverTarget;

        /// <summary>
        /// In the scene «Partida»: its map (Arte's Battlefield) and figures (UnitViews) instead of the rough floor and the
        /// capsules, and the events played by the event player into View, as in the game.
        /// </summary>
        public bool InScene;

        /// <summary>What is armed fired by itself a moment after the start (a strip of a shot played).</summary>
        public bool Fire;

        /// <summary>
        /// The recorded orders played up to the first end of turn after which several of the enemy move, and that turn's
        /// recorded events then played by the event player (a strip of the enemy's turn).
        /// </summary>
        public bool ToEnemyTurn;

        /// <summary>
        /// The game's menu open once it is set up, on a page (menu, ajustes, controles) or at a question (volver, abandonar,
        /// salir); with a question, the partner is in too.
        /// </summary>
        public string Menu = "";

        /// <summary>The pointer on a tile a soldier reaches up (or down) a flight of stairs: the path on the treads.</summary>
        public bool Stairs;

        /// <summary>Ana in too, with a plan for one of the soldiers, and a ping of hers on a tile ahead.</summary>
        public bool Plans;

        /// <summary>The HUD, if the lab has one (HudShots.Combat makes it).</summary>
        public HudRoot Hud;

        sealed class GoldenFile
        {
            public GameState State { get; set; }
            public int RngSeed { get; set; }
            public List<GoldenOp> Ops { get; set; } = new List<GoldenOp>();
        }

        /// <summary>A recorded order (record-step2.ts): what was asked, the events it gave, and the dice before it.</summary>
        sealed class GoldenOp
        {
            public string Op { get; set; }
            public Command Cmd { get; set; }
            public List<GameEvent> Events { get; set; } = new List<GameEvent>();
            public List<long> Rng { get; set; }
        }

        static readonly Color FloorColor = new Color(0.29f, 0.31f, 0.32f);
        static readonly Color WallColor = new Color(0.2f, 0.22f, 0.23f);
        static readonly Color ThingColor = new Color(0.42f, 0.35f, 0.27f);

        GameState host;
        Rng rng;
        CombatController control;
        EventPlayer player;
        readonly Dictionary<string, Transform> bodies = new Dictionary<string, Transform>();
        Material squad;
        Material enemy;

        void Start()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../engine-cs/golden", Step, Golden + ".json"));
            if (!File.Exists(path))
            {
                Debug.LogError("Combat lab: no " + path);
                return;
            }
            var file = Json.Read<GoldenFile>(File.ReadAllText(path));
            host = file.State;
            rng = new Rng(file.RngSeed);
            AbilityCommand shot = null;
            GoldenOp turn = null;
            if (ToEnemyTurn)
                foreach (var op in file.Ops)
                {
                    if (op.Cmd is EndTurnCommand && op.Events.Count(x => x is MovedEvent mv && mv.Unit.StartsWith("a")) >= 4)
                    {
                        turn = op;
                        break;
                    }
                    foreach (var e in op.Events) Events.ApplyEvent(host, e);
                }
            if (ToShot)
            {
                // Each recorded order's events, on the host's state, up to the first shot.
                foreach (var op in file.Ops)
                {
                    if (op.Cmd is AbilityCommand a && (a.Ability == AbilityIds.Shoot || a.Ability == AbilityIds.Pistol) && (!Kills || op.Events.Any(x => x is DiedEvent d && d.Unit == a.Target)))
                    {
                        shot = a;
                        if (op.Rng != null) rng = new Rng(op.Rng);
                        break;
                    }
                    foreach (var e in op.Events) Events.ApplyEvent(host, e);
                }
            }
            var mine = Events.CloneState(host);
            InScene &= Bastion.Client.Battlefield.Current != null;
            if (InScene) Bastion.Client.Battlefield.Current.Show(mine);
            else
            {
                LayOut(mine);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                squad = new Material(lit) { color = new Color(0.35f, 0.55f, 0.95f) };
                enemy = new Material(lit) { color = new Color(0.85f, 0.25f, 0.2f) };
                Bodies(mine);
            }

            var cam = Camera.main;
            var rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            rig.Bounds = new Rect(0, -mine.Height * Board.Tile, mine.Width * Board.Tile, mine.Height * Board.Tile);
            var marks = new GameObject("Marcas");
            control = gameObject.AddComponent<CombatController>();
            control.State = mine;
            control.MySlot = 0;
            control.Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Name = "Arturo", Color = Content.PlayerColors[0], Connected = true } };
            control.Rig = rig;
            control.Overlay = marks.AddComponent<MoveOverlay>();
            control.AimMarks = marks.AddComponent<AimOverlay>();
            control.Marks = marks.AddComponent<MissionMarks>();
            control.Hud = Hud;
            control.Order += Play;
            if (Hud != null)
            {
                Hud.MenuChose += control.MenuChose;
                Hud.SettingChanged += control.SettingChanged;
            }
            if (Plans) PlansSoon();
            if (!string.IsNullOrEmpty(Menu))
            {
                if (Menu != "menu" && Menu != "ajustes" && Menu != "controles") control.Players.Add(new PlayerInfo { Slot = 1, Name = "Ana", Color = Content.PlayerColors[1], Connected = true });
                MenuSoon();
            }
            if (InScene)
            {
                player = new EventPlayer(control, destroyCancellationToken);
                player.Load(mine);
                control.Busy = player.IsBusy;
                control.CloseFocus = () => player.CloseFocus;
            }

            if (turn != null && player != null)
            {
                PlaySoon(turn.Events);
                return;
            }
            if (Stairs && UpStairs(mine, rig)) return;
            var u = shot != null ? mine.Units.First(x => x.Id == shot.Unit) : mine.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0);
            if (u == null) return;
            control.Pick(u.Id, false);
            rig.Focus(Board.TileToWorld(mine, u.Pos), true);
            if (shot != null && HoverTarget)
            {
                control.Hover(mine.Units.First(x => x.Id == shot.Target).Pos);
                return;
            }
            if (shot != null) control.ArmAt(shot.Ability, shot.Target);
            else if (!string.IsNullOrEmpty(Arm)) control.OnAbility(Arm);
            if (control.Armed)
            {
                if (Fire) FireSoon();
                return;
            }
            // The pointer on a tile it can end its move on, on its floor, at 70 % of its two-action reach (with a throw armed, its area there).
            var reach = Paths.ComputeReach(mine, u, Paths.MaxMoveCost(u));
            var ends = Paths.Destinations(mine, u, reach).Where(d => d.Pos.Z == u.Pos.Z).ToList();
            if (ends.Count == 0) return;
            var max = ends.Max(d => d.Cost);
            control.Hover(ends.OrderBy(d => Mathf.Abs(d.Cost - max * 0.7f)).First().Pos);
        }

        /// <summary>A soldier and a tile it reaches over a flight of stairs, picked and pointed at, the camera between; false if none.</summary>
        bool UpStairs(GameState s, CameraRig rig, bool moved = false)
        {
            foreach (var u in s.Units.Where(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0))
            {
                var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
                foreach (var d in Paths.Destinations(s, u, reach))
                {
                    var path = Paths.BuildPath(s, u, reach, d.Pos);
                    if (path == null || !path.Any(p => Flights.FlightAt(s, p) != null)) continue;
                    control.Pick(u.Id, false);
                    rig.Focus(Vector3.Lerp(Board.TileToWorld(s, u.Pos), Board.TileToWorld(s, d.Pos), 0.5f), true);
                    // From the soldier's floor, close: the flight under the storey above, which that floor's view takes away.
                    control.OnFloor(u.Pos.Z - control.Level);
                    rig.CinematicZoom(12 * Board.Tile);
                    control.Hover(d.Pos);
                    Debug.Log($"[lab] stairs: {u.Id} at {u.Pos.X},{u.Pos.Y},{u.Pos.Z} to {d.Pos.X},{d.Pos.Y},{d.Pos.Z}");
                    return true;
                }
            }
            if (moved)
            {
                Debug.Log("[lab] stairs: none in reach");
                return false;
            }
            // None in reach: the first soldier put at the foot of a flight of several tiles (the lab's own state only).
            var first = s.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive && x.Ap > 0);
            for (var z = 0; first != null && z < 4; z++)
                for (var y = 0; y < s.Height; y++)
                    for (var x = 0; x < s.Width; x++)
                    {
                        var on = Flights.LongFlightAt(s, new Pos { X = x, Y = y, Z = z });
                        if (on == null || on.Step != 0) continue;
                        var foot = Flights.FlightFoot(on.Flight);
                        if (!Grid.InBounds(s, foot.X, foot.Y) || !Flights.CanStopAt(s, foot) || Grid.UnitAt(s, foot) != null) continue;
                        first.Pos = foot;
                        player?.Load(s);
                        Debug.Log($"[lab] stairs: {first.Id} put at the foot {foot.X},{foot.Y},{foot.Z}");
                        return UpStairs(s, rig, true);
                    }
            Debug.Log("[lab] stairs: no flight on the map");
            return false;
        }

        /// <summary>Ana's plan for the second soldier, to a tile it reaches 70 % of the way, and her ping by the first.</summary>
        async void PlansSoon()
        {
            control.Players.Add(new PlayerInfo { Slot = 1, Name = "Ana", Color = Content.PlayerColors[1], Connected = true });
            await Awaitable.WaitForSecondsAsync(0.4f, destroyCancellationToken);
            Hud?.CloseIntro();
            var s = control.State;
            var squad = s.Units.Where(x => x.Team == Teams.Xcom && x.Alive).ToList();
            if (squad.Count < 2) return;
            var u = squad[1];
            var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
            var ends = Paths.Destinations(s, u, reach).Where(d => d.Pos.Z == u.Pos.Z).ToList();
            if (ends.Count == 0) return;
            var max = ends.Max(d => d.Cost);
            control.PartnerPlan(1, u.Id, ends.OrderBy(d => Mathf.Abs(d.Cost - max * 0.7f)).First().Pos);
            var first = squad[0];
            control.OnPing(1, new Pos { X = first.Pos.X + 2, Y = first.Pos.Y + 1, Z = first.Pos.Z }, "point");
        }

        async void MenuSoon()
        {
            await Awaitable.WaitForSecondsAsync(0.3f, destroyCancellationToken);
            Hud?.CloseIntro();
            control.OnMenu();
            if (Menu == "ajustes") Hud?.MenuPage("settings");
            else if (Menu == "controles") Hud?.MenuPage("controls");
            else if (Menu == "volver") control.MenuChose(Bastion.UI.Hud.GameMenu.Leave);
            else if (Menu == "abandonar") control.MenuChose(Bastion.UI.Hud.GameMenu.Abandon);
            else if (Menu == "salir") control.MenuChose(Bastion.UI.Hud.GameMenu.Quit);
        }

        /// <summary>Recorded events, a moment after the start: on the host's and this player's states, and to the event player.</summary>
        async void PlaySoon(List<GameEvent> events)
        {
            var first = control.State.Units.FirstOrDefault(x => x.Team == Teams.Xcom && x.Alive);
            if (first != null) control.Rig.Focus(Board.TileToWorld(control.State, first.Pos), true);
            await Awaitable.WaitForSecondsAsync(0.5f, destroyCancellationToken);
            foreach (var e in events)
            {
                Events.ApplyEvent(host, e);
                Events.ApplyEvent(control.State, e);
            }
            player.Push(events, null);
            control.Refresh();
        }

        async void FireSoon()
        {
            await Awaitable.WaitForSecondsAsync(0.6f, destroyCancellationToken);
            control.OnConfirmTarget();
        }

        /// <summary>
        /// An order to the rules, as the host would take it; its events to this player's copy, and then either to the event
        /// player (in the scene) or straight to the capsules.
        /// </summary>
        void Play(Command cmd)
        {
            var result = Rules.ExecuteCommand(host, cmd, 0, new[] { 0 }, rng);
            if (!result.Ok)
            {
                Hud?.Toast(result.Error);
                return;
            }
            foreach (var e in result.Events) Events.ApplyEvent(control.State, e);
            if (player != null) player.Push(result.Events, 0);
            else Bodies(control.State);
            control.Refresh();
        }

        /// <summary>
        /// The ground roughly: a grey quad on each tile one can stand on, a dark block waist high for a wall (city's solid
        /// blocks a storey high hid the marks; Arte's map cuts them away), a brown one for a thing. Solid and lit, so the
        /// marks on a tile behind a wall stay behind it.
        /// </summary>
        void LayOut(GameState s)
        {
            var floor = new MeshBuilder();
            var walls = new MeshBuilder();
            var things = new MeshBuilder();
            for (var y = 0; y < s.Height; y++)
            for (var x = 0; x < s.Width; x++)
            {
                var kind = Grid.TileAt(s, x, y);
                var h = Grid.ElevAt(s, x, y) * Board.Storey;
                var a = Board.Corner(x, y, h);
                if (Grid.IsWalkable(kind) || kind == "floor")
                {
                    var t = Board.Tile;
                    floor.Face(a, a + new Vector3(t, 0, 0), a + new Vector3(t, 0, -t), a + new Vector3(0, 0, -t), Vector3.up, Color.white);
                    continue;
                }
                var tall = kind == "wall" ? 1.2f : Mathf.Max(0.4f, (float)Grid.ObstacleHeight(kind) * Board.Storey);
                Box(kind == "wall" ? walls : things, a, tall);
            }
            Solid("Suelo (laboratorio)", floor, FloorColor);
            Solid("Muros (laboratorio)", walls, WallColor);
            Solid("Obstáculos (laboratorio)", things, ThingColor);
        }

        static void Solid(string name, MeshBuilder b, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            go.AddComponent<MeshFilter>().sharedMesh = b.Build(null, true);
        }

        /// <summary>A block on a tile, `nw` its north-west corner at its floor, `tall` high.</summary>
        static void Box(MeshBuilder b, Vector3 nw, float tall)
        {
            var t = Board.Tile;
            var up = Vector3.up * tall;
            var ne = nw + new Vector3(t, 0, 0);
            var se = nw + new Vector3(t, 0, -t);
            var sw = nw + new Vector3(0, 0, -t);
            var c = (Color32)Color.white;
            b.Face(nw + up, ne + up, se + up, sw + up, Vector3.up, c);
            b.Face(nw, ne, ne + up, nw + up, Vector3.forward, c);
            b.Face(sw, se, se + up, sw + up, Vector3.back, c);
            b.Face(nw, sw, sw + up, nw + up, Vector3.left, c);
            b.Face(ne, se, se + up, ne + up, Vector3.right, c);
        }

        /// <summary>A capsule for each unit alive, the squad blue and the rest red, on its tile and registered as its figure; the fallen gone.</summary>
        void Bodies(GameState s)
        {
            foreach (var u in s.Units)
            {
                bodies.TryGetValue(u.Id, out var body);
                if (!u.Alive)
                {
                    if (body == null) continue;
                    Figures.Unregister(u.Id);
                    Destroy(body.gameObject);
                    bodies.Remove(u.Id);
                    continue;
                }
                if (body == null)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    c.name = "Cápsula";
                    Destroy(c.GetComponent<Collider>());
                    var mesh = c.transform;
                    mesh.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    mesh.GetComponent<Renderer>().sharedMaterial = u.Team == Teams.Xcom ? squad : enemy;
                    // The root on the feet, as a figure's; the capsule over it.
                    body = new GameObject(u.Name + " (" + u.Id + ")").transform;
                    mesh.SetParent(body, false);
                    mesh.localPosition = Vector3.up * 0.9f;
                    var head = new GameObject("Cabeza").transform;
                    head.SetParent(body, false);
                    head.localPosition = Vector3.up * 1.65f;
                    bodies[u.Id] = body;
                    Figures.Register(u.Id, body, head);
                }
                body.position = Board.TileToWorld(s, u.Pos);
            }
        }
    }
}
